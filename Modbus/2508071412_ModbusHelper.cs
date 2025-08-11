using System;
using System.Collections.Generic;
using System.IO.Ports;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace AmqpModbusIntegration
{
	public static class ModbusHelper
	{



		public static readonly object SerialSync = new object();

		// === 退避機制（不存在或長期失敗的站號降頻輪詢） ===
		private static readonly Dictionary<byte, int> _failCount = new Dictionary<byte, int>();
		private static readonly Dictionary<byte, DateTime> _nextPoll = new Dictionary<byte, DateTime>();

		private static bool IsPollDue(byte station)
		{
			lock (SerialSync)
				return !_nextPoll.TryGetValue(station, out var due) || DateTime.UtcNow >= due;
		}
		private static void RecordBackoff(byte station)
		{
			lock (SerialSync)
			{
				int f = _failCount.TryGetValue(station, out var c) ? c + 1 : 1;
				_failCount[station] = f;
				var backoff = TimeSpan.FromSeconds(Math.Min(60, 2 * Math.Pow(2, Math.Max(0, f - 1))));
				_nextPoll[station] = DateTime.UtcNow + backoff;
			}
		}
		private static void ClearBackoff(byte station)
		{
			lock (SerialSync)
			{
				_failCount[station] = 0;
				_nextPoll[station] = DateTime.UtcNow;
			}
		}





		private static int GetCharTimeMs(SerialPort port)
			=> Math.Max(1, (int)Math.Ceiling(10000.0 / Math.Max(300, port?.BaudRate ?? 9600)));

		private static void WaitForBusIdle(SerialPort port, int idleMs, int maxWaitMs = 100)
		{
			lock (SerialSync)
			{
				if (port == null || !port.IsOpen) return;

				var start = DateTime.UtcNow;
				var last = DateTime.UtcNow;

				try { port.DiscardInBuffer(); } catch { }

				while ((DateTime.UtcNow - start).TotalMilliseconds < maxWaitMs)
				{
					int n = 0; try { n = port.BytesToRead; } catch { }
					if (n > 0)
					{
						var tmp = new byte[Math.Min(n, 256)];
						try { port.Read(tmp, 0, tmp.Length); } catch { }
						last = DateTime.UtcNow;
					}
					if ((DateTime.UtcNow - last).TotalMilliseconds >= idleMs) break;
					Thread.Sleep(1);
				}
			}
		}

		private static void DrainUntilSilence(SerialPort port, int idleMs, int maxMs = 100)
		{
			lock (SerialSync)
			{
				if (port == null || !port.IsOpen) return;

				var start = DateTime.UtcNow;
				var last = DateTime.UtcNow;

				while ((DateTime.UtcNow - start).TotalMilliseconds < maxMs)
				{
					int n = 0; try { n = port.BytesToRead; } catch { }
					if (n > 0)
					{
						var tmp = new byte[Math.Min(n, 256)];
						try { port.Read(tmp, 0, tmp.Length); } catch { }
						last = DateTime.UtcNow;
					}
					if ((DateTime.UtcNow - last).TotalMilliseconds >= idleMs) break;
					Thread.Sleep(1);
				}
			}
		}

		private static void ConsumeAck06(SerialPort port, int timeoutMs = 150)
		{
			var start = DateTime.UtcNow;
			while ((DateTime.UtcNow - start).TotalMilliseconds < timeoutMs)
			{
				if (!TryGetBytesToRead(port, out var n)) return;
				if (n >= 8) break;
				Thread.Sleep(1);
			}

			int toRead = 0; TryGetBytesToRead(port, out toRead);
			if (toRead > 0)
			{
				var dump = new byte[Math.Min(toRead, 64)];
				SafeRead(port, dump, 0, dump.Length);
			}

			DrainUntilSilence(port, GetCharTimeMs(port) * 4, 50);
		}

		private static bool TryGetBytesToRead(SerialPort port, out int bytes)
		{
			lock (SerialSync)
			{
				bytes = 0;
				if (port == null || !port.IsOpen) return false;
				try { bytes = port.BytesToRead; return true; }
				catch { return false; }
			}
		}

		private static int SafeRead(SerialPort port, byte[] buffer, int offset, int count)
		{
			lock (SerialSync)
			{
				if (port == null || !port.IsOpen) return 0;
				try { return port.Read(buffer, offset, count); }
				catch { return 0; }
			}
		}

		// ModbusHelper.cs 內
		private static void SafeWrite(SerialPort port, byte[] buffer, int offset, int count, bool driveRts = false)
		{
			lock (SerialSync)
			{
				if (port == null || !port.IsOpen) return;

				try { port.DiscardOutBuffer(); } catch { }

				if (driveRts) port.RtsEnable = true;
				try { port.Write(buffer, offset, count); } catch { }

				try
				{
					var t0 = Environment.TickCount;
					while (port.BytesToWrite > 0 && Environment.TickCount - t0 < 200) Thread.Sleep(1);
				}
				catch { /* 個別驅動偶爾丟 UnauthorizedAccess，忽略 */ }

				int guardMs = Math.Max(2, (int)Math.Ceiling(3.5 * 11.0 / Math.Max(300, port.BaudRate) * 1000.0));
				Thread.Sleep(guardMs);

				if (driveRts) port.RtsEnable = false;
			}
		}






		public static void SwitchON(SerialPort port, byte stationNumber)
		{
			if (port == null) return;
			byte[] openCommand = new byte[] { stationNumber, 0x06, 0x00, 0x31, 0x55, 0x88 };
			byte[] full = AppendCRC(openCommand);
			SafeWrite(port, full, 0, full.Length);
			ConsumeAck06(port);
		}

		public static void SwitchOFF(SerialPort port, byte stationNumber)
		{
			if (port == null) return;
			byte[] closeCommand = new byte[] { stationNumber, 0x06, 0x00, 0x31, 0x55, 0x66 };
			byte[] full = AppendCRC(closeCommand);
			SafeWrite(port, full, 0, full.Length);
			ConsumeAck06(port);
		}

		public static void SetTemperature(SerialPort port, byte stationNumber, ushort temperature, Dictionary<byte, Dictionary<string, object>> slaveData)
		{
			if (port == null) return;

			lock (SerialSync)
			{
				if (!(port.IsOpen && slaveData.ContainsKey(stationNumber))) return;

				byte[] command = new byte[]
				{
					stationNumber, 0x06, 0x00, 0x2B,
					(byte)(temperature >> 8), (byte)(temperature & 0xFF)
				};
				byte[] full = AppendCRC(command);

				var t35 = GetCharTimeMs(port) * 4;
				WaitForBusIdle(port, t35, 100);

				SafeWrite(port, full, 0, full.Length);
				ConsumeAck06(port);

				slaveData[stationNumber]["TemperatureSV"] = temperature;
			}
		}

		public static async Task<bool> ReadAllParametersAsync(SerialPort port, ModbusViewer viewer)
		{
			if (port == null || !port.IsOpen) return false;

			List<byte> stations;
			lock (viewer.slaveData) stations = viewer.slaveData.Keys.ToList();

			bool anyOk = false;

			int tChar = GetCharTimeMs(port);
			int t35 = tChar * 4;
			int interDelay = Math.Max(150, t35 + 120);
			var now = DateTime.UtcNow;



			foreach (var station in stations)
			{
				// 退避：若該站尚未到下一次允許時間，略過
				if (!IsPollDue(station)) continue;

				try
				{
					bool ok = await ReadOneStationAsync(port, station, viewer);
					anyOk |= ok;
				}
				catch (Exception ex)
				{
					AppLogger.Error(ex, "[MODBUS] 讀取站號 " + station + " 發生例外");
					DrainUntilSilence(port, t35);
					RecordBackoff(station); // ★ 發生例外也退避
				}

				await Task.Delay(interDelay);
			}

			return anyOk;
		}

		private static async Task<bool> ReadOneStationAsync(SerialPort port, byte station, ModbusViewer viewer)
		{
			const int regs = 0x30;
			const int expectedByteCount = regs * 2;
			const int readTimeout = 5000;
			const int pollStep = 8;

			byte[] req = new byte[] { station, 0x03, 0x00, 0x00, 0x00, (byte)regs };
			byte[] cmd = AppendCRC(req);

			if (TryGetBytesToRead(port, out var junk) && junk > 0)
			{
				var dump0 = new byte[Math.Min(junk, 256)];
				SafeRead(port, dump0, 0, dump0.Length);
			}
			await Task.Delay(Math.Max(2, (int)Math.Ceiling(3.5 * 11.0 / Math.Max(300, port.BaudRate) * 1000.0)));

			SafeWrite(port, cmd, 0, cmd.Length);

			int t35 = GetCharTimeMs(port) * 4;
			var deadline = DateTime.UtcNow.AddMilliseconds(readTimeout);

			while (DateTime.UtcNow < deadline)
			{
				int available = 0;
				while (DateTime.UtcNow < deadline)
				{
					if (!TryGetBytesToRead(port, out available)) { DrainUntilSilence(port, t35); RecordBackoff(station); return false; }
					if (available >= 3) break;
					await Task.Delay(pollStep);
				}
				if (available < 3) break;

				byte[] header = new byte[3];
				int gotHeader = SafeRead(port, header, 0, 3);
				if (gotHeader != 3) continue;

				byte addr = header[0];
				byte func = header[1];
				byte bc = header[2];

				if (addr == station && func == 0x03 && bc == expectedByteCount)
				{
					int remain = bc + 2;
					int afterHdrAvail = 0;
					var d2 = DateTime.UtcNow.AddMilliseconds(readTimeout);
					while (DateTime.UtcNow < d2)
					{
						if (!TryGetBytesToRead(port, out afterHdrAvail)) { DrainUntilSilence(port, t35); RecordBackoff(station); return false; }
						if (afterHdrAvail >= remain) break;
						await Task.Delay(pollStep);
					}
					if (afterHdrAvail < remain)
					{
						if (afterHdrAvail > 0)
						{
							var dump = new byte[afterHdrAvail];
							SafeRead(port, dump, 0, afterHdrAvail);
						}
						LogThrottler.Every("tail_short_" + station, 5,
							delegate { AppLogger.Warn("[MODBUS] 站號 " + station + " 回應長度不足：" + afterHdrAvail + "/" + remain + " bytes（逾時 5000ms）"); });
						DrainUntilSilence(port, t35);
						RecordBackoff(station); // ★
						return false;
					}

					byte[] tail = new byte[remain];
					int gotTail = SafeRead(port, tail, 0, remain);
					if (gotTail != remain)
					{
						LogThrottler.Every("tail_len_" + station, 5,
							delegate { AppLogger.Warn("[MODBUS] 站號 " + station + " 回應尾段長度不符：" + gotTail + "/" + remain); });
						DrainUntilSilence(port, t35);
						RecordBackoff(station); // ★
						return false;
					}

					byte[] buffer = new byte[3 + remain];
					Array.Copy(header, 0, buffer, 0, 3);
					Array.Copy(tail, 0, buffer, 3, remain);

					if (!ValidateCRC(buffer, buffer.Length))
					{
						string head = string.Join("-", buffer.Take(Math.Min(16, buffer.Length)).Select(b => b.ToString("X2")).ToArray());
						LogThrottler.Every("crc_" + station, 5,
							delegate { AppLogger.Warn("[MODBUS] 站號 " + station + " CRC 失敗（len=" + buffer.Length + "）head=" + head); });
						DrainUntilSilence(port, t35);
						RecordBackoff(station); // ★
						return false;
					}

					ParseResponse(buffer, station, viewer);
					ClearBackoff(station); // ★ 成功清退避
					DrainUntilSilence(port, t35, 30);
					return true;
				}
				else
				{
					// 丟掉看似完整 0x03 回應的其餘部分，避免下次黏幀
					if (func == 0x03 && bc == expectedByteCount)
					{
						int need = bc + 2;
						var until = DateTime.UtcNow.AddMilliseconds(300);
						while (need > 0 && DateTime.UtcNow < until)
						{
							if (!TryGetBytesToRead(port, out var n2)) break;
							if (n2 > 0)
							{
								var dump = new byte[Math.Min(n2, need)];
								int g = SafeRead(port, dump, 0, dump.Length);
								need -= g;
							}
							else
							{
								await Task.Delay(pollStep);
							}
						}
						LogThrottler.Every("hdr_mis_" + station, 5,
							delegate { AppLogger.Warn("[MODBUS] 站號 " + station + " 回應頭不符：addr=" + addr + ", func=" + func + ", bc=" + bc); });
						continue;
					}
					else
					{
						LogThrottler.Every("hdr_mis_" + station, 5,
							delegate { AppLogger.Warn("[MODBUS] 站號 " + station + " 回應頭不符：addr=" + addr + ", func=" + func + ", bc=" + bc); });
						DrainUntilSilence(port, t35, 30);
						continue;
					}
				}
			}

			LogThrottler.Every("hdr_short_" + station, 5,
				delegate { AppLogger.Warn("[MODBUS] 站號 " + station + " 回應 header 不足：0/3 bytes（逾時 5000ms）"); });
			DrainUntilSilence(port, t35);
			RecordBackoff(station); // ★
			return false;
		}




		private static void ParseResponse(byte[] buffer, byte station, ModbusViewer modbusViewer)
		{
			if (!modbusViewer.slaveData.ContainsKey(station)) return;

			int dataOffset = 3;
			byte bc = buffer[2];
			if (bc < 96) return;

			modbusViewer.currentStatus1 = (buffer[dataOffset + 0] << 8) | buffer[dataOffset + 1];
			modbusViewer.currentStatus2 = (buffer[dataOffset + 2] << 8) | buffer[dataOffset + 3];
			modbusViewer.currentStatus = (modbusViewer.currentStatus1 << 16) | modbusViewer.currentStatus2;

			modbusViewer.tempA = (buffer[dataOffset + 6] << 8) | buffer[dataOffset + 7];
			modbusViewer.tempB = (buffer[dataOffset + 8] << 8) | buffer[dataOffset + 9];
			modbusViewer.tempC = (buffer[dataOffset + 10] << 8) | buffer[dataOffset + 11];

			modbusViewer.currentA = ((buffer[dataOffset + 20] << 8) | buffer[dataOffset + 21]) / 100.0;
			modbusViewer.currentB = ((buffer[dataOffset + 22] << 8) | buffer[dataOffset + 23]) / 100.0;
			modbusViewer.currentC = ((buffer[dataOffset + 24] << 8) | buffer[dataOffset + 25]) / 100.0;

			modbusViewer.activePowerA = (buffer[dataOffset + 32] << 8) | buffer[dataOffset + 33];
			modbusViewer.activePowerB = (buffer[dataOffset + 34] << 8) | buffer[dataOffset + 35];
			modbusViewer.activePowerC = (buffer[dataOffset + 36] << 8) | buffer[dataOffset + 37];

			int energyHighByte = (buffer[dataOffset + 46] << 8) | buffer[dataOffset + 47];
			int energyLowByte = (buffer[dataOffset + 48] << 8) | buffer[dataOffset + 49];
			modbusViewer.energy = ((energyHighByte << 16) | energyLowByte) / 100.0;

			modbusViewer.switchStatus = (buffer[dataOffset + 50] << 8) | buffer[dataOffset + 51];
			modbusViewer.ProtectionThreshold = ((buffer[dataOffset + 86] << 8) | buffer[dataOffset + 87]);

			var sData = modbusViewer.slaveData[station];
			sData["Fault_WarningCode"] = modbusViewer.currentStatus;
			sData["PowerTemperature_A"] = modbusViewer.tempA;
			sData["PowerTemperature_B"] = modbusViewer.tempB;
			sData["PowerTemperature_C"] = modbusViewer.tempC;
			sData["CurrentRYB_A"] = modbusViewer.currentA;
			sData["CurrentRYB_B"] = modbusViewer.currentB;
			sData["CurrentRYB_C"] = modbusViewer.currentC;
			sData["PowerRYB_A"] = modbusViewer.activePowerA;
			sData["PowerRYB_B"] = modbusViewer.activePowerB;
			sData["PowerRYB_C"] = modbusViewer.activePowerC;
			sData["EnergyUsed"] = modbusViewer.energy;
			sData["PowerSwitch"] = modbusViewer.switchStatus; // 0=開, 1=合
			sData["TemperatureSV"] = modbusViewer.ProtectionThreshold;
		}

		public static byte[] AppendCRC(byte[] command)
		{
			ushort crc = CalculateCRC(command);
			byte[] full = new byte[command.Length + 2];
			Array.Copy(command, full, command.Length);
			full[full.Length - 2] = (byte)(crc & 0xFF);
			full[full.Length - 1] = (byte)((crc >> 8) & 0xFF);
			return full;
		}

		public static ushort CalculateCRC(byte[] data)
		{
			ushort crc = 0xFFFF;
			for (int i = 0; i < data.Length; i++)
			{
				crc ^= data[i];
				for (int j = 0; j < 8; j++)
				{
					if ((crc & 1) != 0) { crc >>= 1; crc ^= 0xA001; }
					else { crc >>= 1; }
				}
			}
			return crc;
		}

		public static bool ValidateCRC(byte[] data, int length)
		{
			if (length < 2) return false;
			ushort recv = (ushort)(data[length - 2] | (data[length - 1] << 8));
			ushort calc = CalculateCRC(data.Take(length - 2).ToArray());
			return recv == calc;
		}
	}
}
