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
		/// <summary>全域唯一鎖。所有 SerialPort I/O（Write/Read/RtsEnable/Discard）統一鎖住。</summary>
		public static readonly object SerialSync = new object();

		// ========== 開關命令 ==========

		public static void SwitchON(SerialPort serialPort, byte stationNumber)
		{
			if (serialPort == null) return;
			lock (SerialSync)
			{
				if (!serialPort.IsOpen) return;

				serialPort.DiscardInBuffer();
				serialPort.DiscardOutBuffer();

				serialPort.RtsEnable = true;

				byte[] openCommand = { stationNumber, 0x06, 0x00, 0x31, 0x55, 0x88 };
				byte[] fullCommand = AppendCRC(openCommand);
				serialPort.Write(fullCommand, 0, fullCommand.Length);

				Task.Delay(10).Wait();
				serialPort.RtsEnable = false;
			}
		}

		public static void SwitchOFF(SerialPort serialPort, byte stationNumber)
		{
			if (serialPort == null) return;
			lock (SerialSync)
			{
				if (!serialPort.IsOpen) return;

				serialPort.DiscardInBuffer();
				serialPort.DiscardOutBuffer();

				serialPort.RtsEnable = true;

				byte[] closeCommand = { stationNumber, 0x06, 0x00, 0x31, 0x55, 0x66 };
				byte[] fullCommand = AppendCRC(closeCommand);
				serialPort.Write(fullCommand, 0, fullCommand.Length);

				Task.Delay(10).Wait();
				serialPort.RtsEnable = false;
			}
		}

		public static void SetTemperature(SerialPort serialPort, byte stationNumber, ushort temperature, Dictionary<byte, Dictionary<string, object>> slaveData)
		{
			if (serialPort == null) return;

			lock (SerialSync)
			{
				if (!serialPort.IsOpen) return;
				if (!slaveData.ContainsKey(stationNumber)) return;

				byte[] command = {
					stationNumber,
					0x06,
					0x00, 0x2B,
					(byte)(temperature >> 8),
					(byte)(temperature & 0xFF)
				};
				byte[] fullCommand = AppendCRC(command);

				serialPort.DiscardInBuffer();
				serialPort.DiscardOutBuffer();

				serialPort.RtsEnable = true;
				serialPort.Write(fullCommand, 0, fullCommand.Length);
				Task.Delay(10).Wait();
				serialPort.RtsEnable = false;

				// 寫回快取（若設備有 echo 可在此驗證）
				slaveData[stationNumber]["TemperatureSV"] = temperature;
			}
		}

		// ========== 讀取（每站） ==========

		/// <summary>
		/// 讀取 viewer.slaveData 中每站 0x0000 起 48 regs（0x30）。
		/// 兩段式讀取：先讀 3 bytes header，再依 ByteCount 讀完剩餘資料。
		/// 任一站成功→true。
		/// </summary>
		public static async Task<bool> ReadAllParametersAsync(SerialPort serialPort, ModbusViewer modbusViewer)
		{
			if (serialPort == null || !serialPort.IsOpen) return false;

			bool anyOk = false;
			var stations = modbusViewer.slaveData.Keys.ToList();
			foreach (var station in stations)
			{
				try
				{
					bool ok = await ReadOneStationAsync(serialPort, station, modbusViewer);
					if (!anyOk && ok) anyOk = true;
				}
				catch (Exception ex)
				{
					AppLogger.Error(ex, $"[MODBUS] 讀取站號 {station} 發生例外");
					modbusViewer.AppendLog($"[MODBUS] 讀取站號 {station} 發生例外：{ex.Message}");
				}
			}
			return anyOk;
		}

		private static async Task<bool> ReadOneStationAsync(SerialPort port, byte station, ModbusViewer viewer)
		{
			const int regs = 0x30; // 48
			const int expectedByteCount = regs * 2; // 96

			byte[] req = { station, 0x03, 0x00, 0x00, 0x00, (byte)regs };
			byte[] cmd = AppendCRC(req);

			int readTimeout = Math.Max(800, port.ReadTimeout > 0 ? port.ReadTimeout : 2000);
			const int pollStep = 8;

			// 送出
			lock (SerialSync)
			{
				if (!port.IsOpen) throw new IOException("Serial port is closed.");

				port.DiscardInBuffer();
				port.DiscardOutBuffer();

				port.RtsEnable = true;
				port.Write(cmd, 0, cmd.Length);
				Task.Delay(10).Wait();
				port.RtsEnable = false;
			}

			// 等到至少有 3 bytes header
			var start = DateTime.UtcNow;
			while ((DateTime.UtcNow - start).TotalMilliseconds < readTimeout)
			{
				if (port.BytesToRead >= 3) break;
				await Task.Delay(pollStep);
			}
			if (port.BytesToRead < 3)
			{
				int available = port.BytesToRead;
				if (available > 0)
				{
					var dump = new byte[available];
					lock (SerialSync) { try { port.Read(dump, 0, available); } catch { } }
				}
				string msg = $"[MODBUS] 站號 {station} 回應 header 不足：{available}/3 bytes（逾時 {readTimeout}ms）";
				//AppLogger.Warn(msg); 
				LogThrottler.Every($"mb_hdr_short:{station}", 5, () => AppLogger.Warn(msg));
				return false;
			}

			// 讀 header (addr, func, bc)
			byte[] header = new byte[3];
			int gotHeader = 0;
			lock (SerialSync)
			{
				while (gotHeader < 3)
				{
					int n = port.Read(header, gotHeader, 3 - gotHeader);
					if (n <= 0) break;
					gotHeader += n;
				}
			}
			if (gotHeader != 3)
			{
				string msg = $"[MODBUS] 站號 {station} Header 讀取長度不符：{gotHeader}/3";
				//AppLogger.Warn(msg); 
				LogThrottler.Every($"mb_hdr_short:{station}", 5, () => AppLogger.Warn(msg));
				return false;
			}

			byte addr = header[0];
			byte func = header[1];
			byte bc = header[2];

			// 檢查基本格式
			if (addr != station || func != 0x03 || bc != expectedByteCount)
			{
				int rest = port.BytesToRead;
				if (rest > 0)
				{
					var dump = new byte[rest];
					lock (SerialSync) { try { port.Read(dump, 0, rest); } catch { } }
				}
				string msg = $"[MODBUS] 站號 {station} 回應頭不符：addr={addr}, func={func}, bc={bc}";
				//AppLogger.Warn(msg); 
				LogThrottler.Every($"mb_hdr_short:{station}", 5, () => AppLogger.Warn(msg));
				return false;
			}

			// 計算剩餘需讀取的長度：data(bc) + crc(2)
			int remain = bc + 2;

			// 等待剩餘資料到齊
			start = DateTime.UtcNow;
			while ((DateTime.UtcNow - start).TotalMilliseconds < readTimeout)
			{
				if (port.BytesToRead >= remain) break;
				await Task.Delay(pollStep);
			}
			int availableRemain = port.BytesToRead;
			if (availableRemain < remain)
			{
				if (availableRemain > 0)
				{
					var dump = new byte[availableRemain];
					lock (SerialSync) { try { port.Read(dump, 0, availableRemain); } catch { } }
				}
				string msg = $"[MODBUS] 站號 {station} 回應長度不足：{availableRemain}/{remain} bytes（逾時 {readTimeout}ms）";
				//AppLogger.Warn(msg); 
				LogThrottler.Every($"mb_hdr_short:{station}", 5, () => AppLogger.Warn(msg));
				return false;
			}

			// 讀取剩餘資料
			byte[] tail = new byte[remain];
			int gotTail = 0;
			lock (SerialSync)
			{
				while (gotTail < remain)
				{
					int n = port.Read(tail, gotTail, remain - gotTail);
					if (n <= 0) break;
					gotTail += n;
				}
			}
			if (gotTail != remain)
			{
				string msg = $"[MODBUS] 站號 {station} 回應尾段長度不符：{gotTail}/{remain}";
				//AppLogger.Warn(msg); 
				LogThrottler.Every($"mb_hdr_short:{station}", 5, () => AppLogger.Warn(msg));
				return false;
			}

			// 組合完整封包：header(3) + tail(remain)
			byte[] buffer = new byte[3 + remain];
			Array.Copy(header, 0, buffer, 0, 3);
			Array.Copy(tail, 0, buffer, 3, remain);

			// CRC 驗證
			if (!ValidateCRC(buffer, buffer.Length))
			{
				var head = string.Join("-", buffer.Take(Math.Min(16, buffer.Length)).Select(b => b.ToString("X2")));
				string msg = $"[MODBUS] 站號 {station} CRC 校驗失敗（len={buffer.Length}）head={head}";
				//AppLogger.Warn(msg); 
				LogThrottler.Every($"mb_hdr_short:{station}", 5, () => AppLogger.Warn(msg));
				return false;
			}

			// 解析
			ParseResponse(buffer, station, viewer);
			return true;
		}

		// ========== 解析 ==========
		// buffer: [0]=addr [1]=func [2]=bc [3..2+bc]=data [2+bc+1..]=CRC(lo,hi)
		private static void ParseResponse(byte[] buffer, byte station, ModbusViewer modbusViewer)
		{
			if (!modbusViewer.slaveData.ContainsKey(station)) return;

			// 資料從 buffer[3] 開始
			int dataOffset = 3;
			byte bc = buffer[2];

			// 這裡依舊假設連續 48 regs（bc=96）
			if (bc < 96) return; // 防守式

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
			sData["PowerSwitch"] = modbusViewer.switchStatus; // 0=開,1=合
			sData["TemperatureSV"] = modbusViewer.ProtectionThreshold;
		}

		// ========== CRC ==========

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
