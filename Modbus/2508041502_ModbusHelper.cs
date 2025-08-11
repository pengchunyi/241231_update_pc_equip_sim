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
		/// <summary>全域唯一鎖。所有 SerialPort I/O（Open/Close/Write/Read/RtsEnable/Discard/BytesToRead）統一鎖住。</summary>
		public static readonly object SerialSync = new object();

		// ====== 公用：計算字元時間、等待幀間沉默、清空到沉默、吞 0x06 的 ACK ======
		private static int GetCharTimeMs(SerialPort port)
			=> Math.Max(1, (int)Math.Ceiling(10000.0 / Math.Max(300, port?.BaudRate ?? 9600))); // 約 10 bit/字元

		/// <summary>發送前先等到連續 idleMs 毫秒都沒有資料（清掉零星殘段）</summary>
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

		/// <summary>從目前起持續讀，直到連續 idleMs 毫秒無資料或超時 maxMs</summary>
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

		/// <summary>針對 0x06 寫命令，吞掉 8 bytes ACK（最多等 timeoutMs）並清場到沉默</summary>
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
				SafeRead(port, dump, 0, dump.Length); // 吞掉 ACK
			}

			DrainUntilSilence(port, GetCharTimeMs(port) * 4, 50);
		}

		// ===== 安全包裝 =====
		private static bool TryGetBytesToRead(SerialPort port, out int bytes)
		{
			lock (SerialSync)
			{
				if (port == null || !port.IsOpen) { bytes = 0; return false; }
				bytes = port.BytesToRead;
				return true;
			}
		}

		private static int SafeRead(SerialPort port, byte[] buffer, int offset, int count)
		{
			lock (SerialSync)
			{
				if (port == null || !port.IsOpen) return 0;
				return port.Read(buffer, offset, count);
			}
		}

		private static void SafeWrite(SerialPort port, byte[] buffer, int offset, int count, bool driveRts = true)
		{
			lock (SerialSync)
			{
				if (port == null || !port.IsOpen) return;

				// 不要先 DiscardInBuffer(); 以免清掉剛到的回應
				port.DiscardOutBuffer();

				if (driveRts) port.RtsEnable = true;
				port.Write(buffer, offset, count);

				// 等待全部推出
				var t0 = Environment.TickCount;
				while (port.BytesToWrite > 0 && Environment.TickCount - t0 < 200) Thread.Sleep(1);

				// 轉向保險：3.5 字符時間（~ 11 bits/char）
				int guardMs = Math.Max(2, (int)Math.Ceiling(3.5 * 11.0 / port.BaudRate * 1000.0));
				Thread.Sleep(guardMs);

				if (driveRts) port.RtsEnable = false;
			}
		}

		// ===== 開關命令（寫 0x06）：發送後吞掉 ACK =====
		public static void SwitchON(SerialPort port, byte stationNumber)
		{
			if (port == null) return;
			byte[] openCommand = new byte[] { stationNumber, 0x06, 0x00, 0x31, 0x55, 0x88 };
			byte[] full = AppendCRC(openCommand);
			SafeWrite(port, full, 0, full.Length);
			ConsumeAck06(port); // ★
		}

		public static void SwitchOFF(SerialPort port, byte stationNumber)
		{
			if (port == null) return;
			byte[] closeCommand = new byte[] { stationNumber, 0x06, 0x00, 0x31, 0x55, 0x66 };
			byte[] full = AppendCRC(closeCommand);
			SafeWrite(port, full, 0, full.Length);
			ConsumeAck06(port); // ★
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

				// 發送前等沉默，確保不黏幀
				var t35 = GetCharTimeMs(port) * 4;
				WaitForBusIdle(port, t35, 100);

				SafeWrite(port, full, 0, full.Length);

				// ★ 吞寫入 ACK，避免殘留
				ConsumeAck06(port);

				slaveData[stationNumber]["TemperatureSV"] = temperature;
			}
		}

		// ===== 讀取流程 =====
		public static async Task<bool> ReadAllParametersAsync(SerialPort port, ModbusViewer viewer)
		{
			if (port == null || !port.IsOpen) return false;

			List<byte> stations;
			lock (viewer.slaveData) stations = viewer.slaveData.Keys.ToList();

			bool anyOk = false;

			// 以 9600bps、100+ bytes 回應估算，站際間隔抓保守 150~200ms
			int tChar = GetCharTimeMs(port);
			int t35 = tChar * 4;
			int interDelay = Math.Max(150, t35 + 120);

			foreach (var station in stations)
			{
				try
				{
					bool ok = await ReadOneStationAsync(port, station, viewer);
					anyOk |= ok;
				}
				catch (Exception ex)
				{
					AppLogger.Error(ex, "[MODBUS] 讀取站號 " + station + " 發生例外");
					// 異常後：清到沉默，避免殘段影響下一站
					DrainUntilSilence(port, t35);
				}

				// 站與站之間保留幀間沉默（保守）
				await Task.Delay(interDelay);
			}
			return anyOk;
		}

		private static async Task<bool> ReadOneStationAsync(SerialPort port, byte station, ModbusViewer viewer)
		{
			const int regs = 0x30;                  // 48
			const int expectedByteCount = regs * 2; // 96
			const int readTimeout = 5000;           // 固定 5 秒
			const int pollStep = 8;

			byte[] req = new byte[] { station, 0x03, 0x00, 0x00, 0x00, (byte)regs };
			byte[] cmd = AppendCRC(req);

			// 發送前：若有殘留，就丟掉一次（只在「發送前」做）
			if (TryGetBytesToRead(port, out var junk) && junk > 0)
			{
				var dump0 = new byte[Math.Min(junk, 256)];
				SafeRead(port, dump0, 0, dump0.Length);
			}
			// 空總線保險（~ t3.5）
			await Task.Delay(Math.Max(2, (int)Math.Ceiling(3.5 * 11.0 / port.BaudRate * 1000.0)));

			// 發送
			SafeWrite(port, cmd, 0, cmd.Length);

			int t35 = GetCharTimeMs(port) * 4;
			var deadline = DateTime.UtcNow.AddMilliseconds(readTimeout);

			while (DateTime.UtcNow < deadline)
			{
				// 等至少 3 bytes header
				int available = 0;
				while (DateTime.UtcNow < deadline)
				{
					if (!TryGetBytesToRead(port, out available)) { DrainUntilSilence(port, t35); return false; }
					if (available >= 3) break;
					await Task.Delay(pollStep);
				}
				if (available < 3) break;

				// 讀 header
				byte[] header = new byte[3];
				int gotHeader = SafeRead(port, header, 0, 3);
				if (gotHeader != 3) continue;

				byte addr = header[0];
				byte func = header[1];
				byte bc = header[2];

				// 目標站的正確 header
				if (addr == station && func == 0x03 && bc == expectedByteCount)
				{
					// 等剩餘 data(bc) + crc(2)
					int remain = bc + 2;
					int afterHdrAvail = 0;
					var d2 = DateTime.UtcNow.AddMilliseconds(readTimeout);
					while (DateTime.UtcNow < d2)
					{
						if (!TryGetBytesToRead(port, out afterHdrAvail)) { DrainUntilSilence(port, t35); return false; }
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
						return false;
					}

					// 讀尾段
					byte[] tail = new byte[remain];
					int gotTail = SafeRead(port, tail, 0, remain);
					if (gotTail != remain)
					{
						LogThrottler.Every("tail_len_" + station, 5,
							delegate { AppLogger.Warn("[MODBUS] 站號 " + station + " 回應尾段長度不符：" + gotTail + "/" + remain); });
						DrainUntilSilence(port, t35);
						return false;
					}

					// 合併 + CRC
					byte[] buffer = new byte[3 + remain];
					Array.Copy(header, 0, buffer, 0, 3);
					Array.Copy(tail, 0, buffer, 3, remain);

					if (!ValidateCRC(buffer, buffer.Length))
					{
						string head = string.Join("-", buffer.Take(Math.Min(16, buffer.Length)).Select(b => b.ToString("X2")).ToArray());
						LogThrottler.Every("crc_" + station, 5,
							delegate { AppLogger.Warn("[MODBUS] 站號 " + station + " CRC 失敗（len=" + buffer.Length + "）head=" + head); });
						DrainUntilSilence(port, t35);
						return false;
					}

					ParseResponse(buffer, station, viewer);
					DrainUntilSilence(port, t35, 30);
					return true;
				}
				else
				{
					// 非本站或格式不符 → 嘗試丟掉整包（若看起來像是 0x03 的標準長度），否則小幅清場
					if (func == 0x03 && bc == expectedByteCount)
					{
						int skip = bc + 2;
						var tDump = DateTime.UtcNow.AddMilliseconds(300);
						int need = skip;
						while (need > 0 && DateTime.UtcNow < tDump)
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
						// 繼續外層 while 等正確站號
						continue;
					}
					else
					{
						// 頭完全不合理，避免卡住：小幅清場
						LogThrottler.Every("hdr_mis_" + station, 5,
							delegate { AppLogger.Warn("[MODBUS] 站號 " + station + " 回應頭不符：addr=" + addr + ", func=" + func + ", bc=" + bc); });
						DrainUntilSilence(port, t35, 30);
						continue;
					}
				}
			}

			// 超時未取得 3 bytes 正確 header
			LogThrottler.Every("hdr_short_" + station, 5,
				delegate { AppLogger.Warn("[MODBUS] 站號 " + station + " 回應 header 不足：0/3 bytes（逾時 5000ms）"); });
			DrainUntilSilence(port, t35);
			return false;
		}

		// ===== 解析與 CRC =====
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
			sData["PowerSwitch"] = modbusViewer.switchStatus; // 0=開, 1=合（保持你原本相容邏輯）
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
					if ((crc & 1) != 0)
					{
						crc >>= 1;
						crc ^= 0xA001;
					}
					else
					{
						crc >>= 1;
					}
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
