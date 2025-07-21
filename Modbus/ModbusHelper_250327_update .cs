using System;
using System.Collections.Generic;
using System.IO.Ports;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace AmqpModbusIntegration
{
	public static class ModbusHelper
	{

		//鎖頭，必免陷入爭搶資源
		private static readonly SemaphoreSlim serialPortLock = new SemaphoreSlim(1, 1);

		public static void SwitchON(SerialPort serialPort, byte stationNumber)
		{
			lock (serialPortLock)  // 使用鎖來防止同時執行
			{

				if (serialPort != null && serialPort.IsOpen)
				{
					
					serialPort.RtsEnable = true;
					
					byte[] openCommand = { stationNumber, 0x06, 0x00, 0x31, 0x55, 0x88 };
					byte[] fullCommand = AppendCRC(openCommand);
					serialPort.DiscardInBuffer();
					serialPort.DiscardOutBuffer();
					serialPort.Write(fullCommand, 0, fullCommand.Length);

					
					Thread.Sleep(10);
					serialPort.RtsEnable = false;
					

					Console.WriteLine($"站號 {stationNumber} 的開關已打開");
				}

			}
		}

		public static void SwitchOFF(SerialPort serialPort, byte stationNumber)
		{
			lock (serialPortLock)  // 使用鎖來防止同時執行
			{
				if (serialPort != null && serialPort.IsOpen)
				{
					
					serialPort.RtsEnable = true;
					
					byte[] closeCommand = { stationNumber, 0x06, 0x00, 0x31, 0x55, 0x66 };
					byte[] fullCommand = AppendCRC(closeCommand);
					serialPort.DiscardInBuffer();
					serialPort.DiscardOutBuffer();
					serialPort.Write(fullCommand, 0, fullCommand.Length);

					
					Thread.Sleep(10);
					serialPort.RtsEnable = false;
					

					Console.WriteLine($"站號 {stationNumber} 的開關已關閉");
				}
			}

		}

		public static void SimulateFaultTest(ModbusViewer modbusViewer, Dictionary<byte, Dictionary<string, object>> slaveData)
		{
			int faultCode = 0xA87;
			modbusViewer.currentStatus = faultCode;
			modbusViewer.currentStatus1 = (faultCode >> 16) & 0xFFFF;
			modbusViewer.currentStatus2 = faultCode & 0xFFFF;

			foreach (var station in slaveData.Keys)
			{
				// 若該站已有 key，則更新
				if (slaveData[station].ContainsKey("Fault_WarningCode"))
				{
					slaveData[station]["Fault_WarningCode"] = faultCode;
				}
			}

			modbusViewer.UpdateDataGridView();
			MessageBox.Show($"已將Fault_WarningCode設置為故障碼 {faultCode:X}");
		}

		public static void SetTemperature(SerialPort serialPort, byte stationNumber, ushort temperature, Dictionary<byte, Dictionary<string, object>> slaveData)
		{
			lock (serialPortLock)
			{
				if (serialPort == null || !serialPort.IsOpen)
				{
					Console.WriteLine("串口未開啟或無效，無法設定溫度");
					return;
				}

				if (!slaveData.ContainsKey(stationNumber))
				{
					Console.WriteLine($"站號 {stationNumber} 不存在，操作中止。");
					return;
				}

				byte[] command = {
					stationNumber,
					0x06,
					0x00, 0x2B,
					(byte)(temperature >> 8),
					(byte)(temperature & 0xFF)
				};
				byte[] fullCommand = AppendCRC(command);

				try
				{
					serialPort.DiscardInBuffer();
					serialPort.DiscardOutBuffer();
					serialPort.Write(fullCommand, 0, fullCommand.Length);
					Console.WriteLine($"已發送溫度設定命令到站號 {stationNumber}，設定溫度：{temperature}°C");

					serialPort.ReadTimeout = 2000;
					byte[] responseBuffer = new byte[256];
					int bytesRead = serialPort.Read(responseBuffer, 0, responseBuffer.Length);

					if (bytesRead > 5 && ValidateCRC(responseBuffer, bytesRead))
					{
						Console.WriteLine($"收到設備回應: {BitConverter.ToString(responseBuffer, 0, bytesRead)}");
						slaveData[stationNumber]["TemperatureSV"] = temperature;
					}
					else
					{
						Console.WriteLine("收到無效回應或 CRC 校驗失敗");
					}
				}
				catch (TimeoutException)
				{
					Console.WriteLine($"讀取站號 {stationNumber} 的設備回應超時，請檢查設備連線。");
				}
				catch (Exception ex)
				{
					Console.WriteLine($"設置溫度時發生錯誤: {ex.Message}");
				}
			}
		}



		public static async Task ReadAllParametersAsync(SerialPort serialPort, ModbusViewer modbusViewer)
		{
			if (serialPort == null || !serialPort.IsOpen) return;

			// 在發送第一個命令前添加100毫秒延遲，確保第一個空開穩定
			// 使用 modbusViewer.slaveData
			foreach (var station in modbusViewer.slaveData.Keys.ToList())
			{

				try
				{
					serialPort.DiscardInBuffer();
					serialPort.DiscardOutBuffer();

					byte[] readCommand = { station, 0x03, 0x00, 0x00, 0x00, 0x30 };
					byte[] fullCommand = AppendCRC(readCommand);
					//==
					serialPort.RtsEnable = true;
					//==

					serialPort.Write(fullCommand, 0, fullCommand.Length);

					await Task.Delay(300);
					serialPort.RtsEnable = false;
					//==

					byte[] buffer = new byte[256];
					int bytesRead = 0;

					while (bytesRead < buffer.Length && serialPort.BytesToRead > 0)
					{
						bytesRead += await serialPort.BaseStream.ReadAsync(buffer, bytesRead, buffer.Length - bytesRead);
						await Task.Delay(50);
					}


					if (bytesRead > 5 && ValidateCRC(buffer, bytesRead))
					{
						ParseResponse(buffer, station, modbusViewer);
					}
					else
					{
						Console.WriteLine($"站號 {station} 收到無效數據或 CRC 校驗失敗");
					}
					//await Task.Delay(200); // 非同步延遲
					await Task.Delay(500); // 非同步延遲
				}
				catch (Exception ex)
				{
					Console.WriteLine($"讀取站號 {station} 時發生錯誤: {ex.Message}");
				}

			}
		}



		private static void ParseResponse(byte[] buffer, byte station, ModbusViewer modbusViewer)
		{
			if (!modbusViewer.slaveData.ContainsKey(station))
			{
				Console.WriteLine($"站号 {station} 不存在于设备列表中，跳过更新。");
				return;
			}

			if (buffer[0] != station)
			{
				Console.WriteLine($"收到錯誤站號的數據包：{buffer[0]}，預期：{station}");
				return;
			}

			// 解析數據
			modbusViewer.currentStatus1 = (buffer[3] << 8) | buffer[4];
			modbusViewer.currentStatus2 = (buffer[5] << 8) | buffer[6];
			modbusViewer.currentStatus = (modbusViewer.currentStatus1 << 16) | modbusViewer.currentStatus2;

			modbusViewer.tempA = (buffer[9] << 8) | buffer[10];
			modbusViewer.tempB = (buffer[11] << 8) | buffer[12];
			modbusViewer.tempC = (buffer[13] << 8) | buffer[14];

			modbusViewer.currentA = ((buffer[23] << 8) | buffer[24]) / 100;
			modbusViewer.currentB = ((buffer[25] << 8) | buffer[26]) / 100;
			modbusViewer.currentC = ((buffer[27] << 8) | buffer[28]) / 100;

			modbusViewer.activePowerA = (buffer[35] << 8) | buffer[36];
			modbusViewer.activePowerB = (buffer[37] << 8) | buffer[38];
			modbusViewer.activePowerC = (buffer[39] << 8) | buffer[40];

			int energyHighByte = (buffer[49] << 8) | buffer[50];
			int energyLowByte = (buffer[51] << 8) | buffer[52];

			//電能這邊已經除以100了
			modbusViewer.energy = ((energyHighByte << 16) | energyLowByte) / 100.0; // kWh

			modbusViewer.switchStatus = (buffer[53] << 8) | buffer[54];

			modbusViewer.ProtectionThreshold = ((buffer[89] << 8) | buffer[90]);

			// 存入 modbusViewer.slaveData
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



		public static byte[] AppendCRC(byte[] command)
		{
			ushort crc = CalculateCRC(command);
			byte[] crcBytes = BitConverter.GetBytes(crc);
			byte[] fullCommand = new byte[command.Length + 2];
			Array.Copy(command, fullCommand, command.Length);
			fullCommand[fullCommand.Length - 2] = crcBytes[0];
			fullCommand[fullCommand.Length - 1] = crcBytes[1];
			return fullCommand;
		}



		public static ushort CalculateCRC(byte[] data)
		{
			ushort crc = 0xFFFF;
			for (int pos = 0; pos < data.Length; pos++)
			{
				crc ^= data[pos];
				for (int i = 0; i < 8; i++)
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


		// 校驗 CRC 方法
		public static bool ValidateCRC(byte[] data, int length)
		{
			if (length < 2) return false;
			ushort receivedCRC = (ushort)(data[length - 2] | (data[length - 1] << 8));
			ushort calculatedCRC = CalculateCRC(data.Take(length - 2).ToArray());
			return receivedCRC == calculatedCRC;
		}

	}
}

