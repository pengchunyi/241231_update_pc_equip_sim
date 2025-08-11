// ModbusHelper.cs
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Ports;
using System.Linq;
using System.Threading.Tasks;

namespace AmqpModbusIntegration
{
	public static class ModbusHelper
	{
		/// <summary>全域唯一鎖。所有 SerialPort I/O（Open/Close/Write/Read/RtsEnable/Discard/BytesToRead）統一鎖住。</summary>
		public static readonly object SerialSync = new object();

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

				port.DiscardInBuffer();
				port.DiscardOutBuffer();

				if (driveRts) port.RtsEnable = true;
				port.Write(buffer, offset, count);
				Task.Delay(10).Wait();
				if (driveRts) port.RtsEnable = false;
			}
		}

		// ===== 開關命令 =====
		public static void SwitchON(SerialPort port, byte stationNumber)
		{
			if (port == null) return;
			byte[] openCommand = new byte[] { stationNumber, 0x06, 0x00, 0x31, 0x55, 0x88 };
			byte[] full = AppendCRC(openCommand);
			SafeWrite(port, full, 0, full.Length);
		}

		public static void SwitchOFF(SerialPort port, byte stationNumber)
		{
			if (port == null) return;
			byte[] closeCommand = new byte[] { stationNumber, 0x06, 0x00, 0x31, 0x55, 0x66 };
			byte[] full = AppendCRC(closeCommand);
			SafeWrite(port, full, 0, full.Length);
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

				port.DiscardInBuffer();
				port.DiscardOutBuffer();
				port.RtsEnable = true;
				port.Write(full, 0, full.Length);
				Task.Delay(10).Wait();
				port.RtsEnable = false;

				slaveData[stationNumber]["TemperatureSV"] = temperature;
			}
		}

		// ===== 讀取流程 =====
		public static async Task<bool> ReadAllParametersAsync(SerialPort port, ModbusViewer viewer)
		{
			if (port == null || !port.IsOpen) return false;

			bool anyOk = false;
			var stations = viewer.slaveData.Keys.ToList();
			foreach (var station in stations)
			{
				try
				{
					bool ok = await ReadOneStationAsync(port, station, viewer);
					if (!anyOk && ok) anyOk = true;
				}
				catch (Exception ex)
				{
					AppLogger.Error(ex, "[MODBUS] 讀取站號 " + station + " 發生例外");
				}
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

			// 發送
			SafeWrite(port, cmd, 0, cmd.Length);

			// 等至少 3 bytes header
			var start = DateTime.UtcNow;
			int available = 0;
			while ((DateTime.UtcNow - start).TotalMilliseconds < readTimeout)
			{
				if (!TryGetBytesToRead(port, out available)) return false;
				if (available >= 3) break;
				await Task.Delay(pollStep);
			}
			if (available < 3)
			{
				if (available > 0)
				{
					var dump = new byte[available];
					SafeRead(port, dump, 0, available);
				}
				LogThrottler.Every("hdr_short_" + station, 5,
					delegate { AppLogger.Warn("[MODBUS] 站號 " + station + " 回應 header 不足：" + available + "/3 bytes（逾時 5000ms）"); });
				return false;
			}

			// 讀 header
			byte[] header = new byte[3];
			int gotHeader = SafeRead(port, header, 0, 3);
			if (gotHeader != 3)
			{
				LogThrottler.Every("hdr_len_" + station, 5,
					delegate { AppLogger.Warn("[MODBUS] 站號 " + station + " Header 讀取長度不符：" + gotHeader + "/3"); });
				return false;
			}

			byte addr = header[0];
			byte func = header[1];
			byte bc = header[2];

			// 基本格式檢查
			if (addr != station || func != 0x03 || bc != expectedByteCount)
			{
				int rest;
				if (!TryGetBytesToRead(port, out rest)) rest = 0;
				if (rest > 0)
				{
					var dump = new byte[rest];
					SafeRead(port, dump, 0, rest);
				}
				LogThrottler.Every("hdr_mis_" + station, 5,
					delegate { AppLogger.Warn("[MODBUS] 站號 " + station + " 回應頭不符：addr=" + addr + ", func=" + func + ", bc=" + bc); });
				return false;
			}

			// 等剩餘 data(bc) + crc(2)
			int remain = bc + 2;
			start = DateTime.UtcNow;
			int afterHdrAvail = 0;
			while ((DateTime.UtcNow - start).TotalMilliseconds < readTimeout)
			{
				if (!TryGetBytesToRead(port, out afterHdrAvail)) return false;
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
				return false;
			}

			// 讀尾段
			byte[] tail = new byte[remain];
			int gotTail = SafeRead(port, tail, 0, remain);
			if (gotTail != remain)
			{
				LogThrottler.Every("tail_len_" + station, 5,
					delegate { AppLogger.Warn("[MODBUS] 站號 " + station + " 回應尾段長度不符：" + gotTail + "/" + remain); });
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
				return false;
			}

			ParseResponse(buffer, station, viewer);
			return true;
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
