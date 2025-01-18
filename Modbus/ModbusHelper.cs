using System;
using System.Collections.Generic;
using System.IO.Ports;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
//MessageBox 是屬於 Windows Forms 命名空間的一部分
using System.Windows.Forms;


namespace AmqpModbusIntegration
{
	public static class ModbusHelper
	{

		//private static readonly object serialPortLock = new object(); // 用於串口操作的執行緒安全鎖
		private static readonly SemaphoreSlim serialPortLock = new SemaphoreSlim(1, 1);


		public static void SwitchON(SerialPort serialPort, byte stationNumber)
		{
			if (serialPort != null && serialPort.IsOpen)
			{
				byte[] openCommand = { stationNumber, 0x06, 0x00, 0x31, 0x55, 0x88 };
				byte[] fullCommand = AppendCRC(openCommand);
				serialPort.DiscardInBuffer(); // 清空輸入緩衝區
				serialPort.DiscardOutBuffer(); // 清空輸出緩衝區
				serialPort.Write(fullCommand, 0, fullCommand.Length);
				Console.WriteLine($"站號 {stationNumber} 的開關已打開");
			}
		}

		public static void SwitchOFF(SerialPort serialPort, byte stationNumber)
		{
			if (serialPort != null && serialPort.IsOpen)
			{
				byte[] closeCommand = { stationNumber, 0x06, 0x00, 0x31, 0x55, 0x66 };
				byte[] fullCommand = AppendCRC(closeCommand);
				serialPort.DiscardInBuffer(); // 清空輸入緩衝區
				serialPort.DiscardOutBuffer(); // 清空輸出緩衝區
				serialPort.Write(fullCommand, 0, fullCommand.Length);
				Console.WriteLine($"站號 {stationNumber} 的開關已關閉");
			}
		}


		//20241202新增=======================================
		// 在 ModbusViewer 類中新增方法
		public static void SimulateFaultTest(ModbusViewer modbusViewer, Dictionary<byte, Dictionary<string, int>> slaveData)
		{
			// 模擬故障碼 0xA87
			int faultCode = 0xA87;

			// 更新 currentStatus 和 currentStatus1, currentStatus2
			modbusViewer.currentStatus = faultCode;
			modbusViewer.currentStatus1 = (faultCode >> 16) & 0xFFFF; // 高16位
			modbusViewer.currentStatus2 = faultCode & 0xFFFF;        // 低16位

			// 更新 slaveData 中的 "當前狀態"
			foreach (var station in slaveData.Keys)
			{
				if (slaveData[station].ContainsKey("當前狀態"))
				{
					slaveData[station]["當前狀態"] = modbusViewer.currentStatus;
				}
			}

			// 更新 DataGridView 顯示
			modbusViewer.UpdateDataGridView();

			// 提示用戶操作成功
			MessageBox.Show($"已將當前狀態設置為故障碼 {faultCode:X}");
		}


		//241231新增================================
		public static void SetTemperature(SerialPort serialPort, byte stationNumber, ushort temperature, Dictionary<byte, Dictionary<string, int>> slaveData)
		{
			// 確保線程安全
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

				// 構建 Modbus 命令
				byte[] command = {
			stationNumber,
			0x06,
			0x00, 0x2B,    // 假設溫度寄存器地址
            (byte)(temperature >> 8),
			(byte)(temperature & 0xFF)
		};

				byte[] fullCommand = AppendCRC(command);

				try
				{
					// 清空緩衝區
					serialPort.DiscardInBuffer();
					serialPort.DiscardOutBuffer();

					// 發送命令
					serialPort.Write(fullCommand, 0, fullCommand.Length);
					Console.WriteLine($"已發送溫度設定命令到站號 {stationNumber}，設定溫度：{temperature}°C");

					// 設置超時並嘗試讀取回應
					serialPort.ReadTimeout = 2000; // 2秒超時
					byte[] responseBuffer = new byte[256];
					int bytesRead = serialPort.Read(responseBuffer, 0, responseBuffer.Length);

					if (bytesRead > 5 && ValidateCRC(responseBuffer, bytesRead))
					{
						Console.WriteLine($"收到設備回應: {BitConverter.ToString(responseBuffer, 0, bytesRead)}");
						slaveData[stationNumber]["溫度保護(℃)"] = temperature;
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


		//20241206_修改==============================================

		public static async Task ReadAllParametersAsync(SerialPort serialPort, ModbusViewer modbusViewer)
		{
			if (serialPort != null && serialPort.IsOpen)
			{
				foreach (var station in modbusViewer.GetSlaveData().Keys.ToList())
				{
					try
					{
						// 清空緩衝區
						serialPort.DiscardInBuffer();
						serialPort.DiscardOutBuffer();

						// 發送讀取命令
						byte[] readCommand = { station, 0x03, 0x00, 0x00, 0x00, 0x30 };
						byte[] fullCommand = AppendCRC(readCommand);
						serialPort.Write(fullCommand, 0, fullCommand.Length);

						// **延長延遲，確保設備有時間準備回應**
						await Task.Delay(500); // 延遲從200ms增加到500ms

						// 讀取回應數據
						byte[] buffer = new byte[256];
						int bytesRead = 0;

						// **優化緩衝邏輯，確保數據完整**
						while (bytesRead < buffer.Length && serialPort.BytesToRead > 0)
						{
							bytesRead += await serialPort.BaseStream.ReadAsync(buffer, bytesRead, buffer.Length - bytesRead);
							await Task.Delay(50); // 適當延遲，避免數據未完全準備好
						}

						// 校驗數據長度和CRC
						if (bytesRead > 5 && ValidateCRC(buffer, bytesRead))
						{
							ParseResponse(buffer, station, modbusViewer);
						}
						else
						{
							Console.WriteLine($"站號 {station} 收到無效數據或 CRC 校驗失敗");
						}

						// **增加站號之間的間隔，避免總線衝突**
						await Task.Delay(300); // 每個站號間增加延遲
					}
					catch (Exception ex)
					{
						Console.WriteLine($"讀取站號 {station} 時發生錯誤: {ex.Message}");
					}
				}
			}
		}




		// 校驗 CRC 方法
		public static bool ValidateCRC(byte[] data, int length)
		{
			if (length < 2) return false;
			ushort receivedCRC = (ushort)(data[length - 2] | (data[length - 1] << 8));
			ushort calculatedCRC = CalculateCRC(data.Take(length - 2).ToArray());
			return receivedCRC == calculatedCRC;
		}


		private static void ParseResponse(byte[] buffer, byte station, ModbusViewer modbusViewer)
		{


			if (!modbusViewer.GetSlaveData().ContainsKey(station))
			{
				Console.WriteLine($"站号 {station} 不存在于设备列表中，跳过更新。");
				return;
			}

			//20241125===============================================================
			// 檢查數據包中的站號
			if (buffer[0] != station)
			{
				Console.WriteLine($"收到錯誤站號的數據包：{buffer[0]}，預期：{station}");
				return; // 跳過數據
			}


			// 解析數據並存入 ModbusViewer 變數，這邊不想要的數值可以直接註解調，buffer不會需要重新排序
			modbusViewer.currentStatus1 = (buffer[3] << 8) | buffer[4];
			modbusViewer.currentStatus2 = (buffer[5] << 8) | buffer[6];
			modbusViewer.currentStatus = (modbusViewer.currentStatus1 << 16) | modbusViewer.currentStatus2;

			modbusViewer.tempA = (buffer[9] << 8) | buffer[10]; // 單位: ℃
			modbusViewer.tempB = (buffer[11] << 8) | buffer[12]; // 單位: ℃
			modbusViewer.tempC = (buffer[13] << 8) | buffer[14]; // 單位: ℃


			modbusViewer.currentA = ((buffer[23] << 8) | buffer[24]) / 100; // 單位: A
			modbusViewer.currentB = ((buffer[25] << 8) | buffer[26]) / 100; // 單位: A
			modbusViewer.currentC = ((buffer[27] << 8) | buffer[28]) / 100; // 單位: A



			modbusViewer.activePowerA = (buffer[35] << 8) | buffer[36]; // 單位: W
			modbusViewer.activePowerB = (buffer[37] << 8) | buffer[38]; // 單位: W
			modbusViewer.activePowerC = (buffer[39] << 8) | buffer[40]; // 單位: W


			int energyHighByte = (buffer[49] << 8) | buffer[50];
			int energyLowByte = (buffer[51] << 8) | buffer[52];
			modbusViewer.energy = ((energyHighByte << 16) | energyLowByte) / 100; // 單位: kWh

			modbusViewer.switchStatus = (buffer[53] << 8) | buffer[54]; // 單位: 開關狀態


			//20241204_新增================================
			modbusViewer.ProtectionThreshold = ((buffer[89] << 8) | buffer[90]);//單位: ℃
																				//20241204_新增================================



			//20241125===============================================================
			if (!modbusViewer.GetSlaveData().ContainsKey(station))
			{
				Console.WriteLine($"站號 {station} 不存在於設備列表中，跳過更新。");
				return;
			}
			//20241125===============================================================

			var slaveData = modbusViewer.GetSlaveData()[station];
			// 更新 slaveData 中的數據結構
			// 更新 slaveData 中的數據結構，並添加對應單位的註解
			slaveData["當前狀態"] = modbusViewer.currentStatus; // 無單位

			slaveData["A相溫度 (℃)"] = modbusViewer.tempA; // 單位: ℃
			slaveData["B相溫度 (℃)"] = modbusViewer.tempB; // 單位: ℃
			slaveData["C相溫度 (℃)"] = modbusViewer.tempC; // 單位: ℃

			slaveData["A相電流 (A)"] = modbusViewer.currentA; // 單位: A
			slaveData["B相電流 (A)"] = modbusViewer.currentB; // 單位: A
			slaveData["C相電流 (A)"] = modbusViewer.currentC; // 單位: A

			slaveData["A相有功功率 (W)"] = modbusViewer.activePowerA; // 單位: W
			slaveData["B相有功功率 (W)"] = modbusViewer.activePowerB; // 單位: W
			slaveData["C相有功功率 (W)"] = modbusViewer.activePowerC; // 單位: W

			slaveData["電能 (kWh)"] = modbusViewer.energy; // 單位: kWh
			slaveData["開關狀態"] = modbusViewer.switchStatus;
			slaveData["溫度保護(℃)"] = modbusViewer.ProtectionThreshold;

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
			ushort crc = 0xFFFF; // 設定初始值
			for (int pos = 0; pos < data.Length; pos++) // 遍歷數據每一位元組
			{
				crc ^= (ushort)data[pos]; // 與當前字節異或
				for (int i = 8; i != 0; i--) // 進行8次迴圈
				{
					if ((crc & 0x0001) != 0) // 如果最低位為1
					{
						crc >>= 1; // 右移一位
						crc ^= 0xA001; // 異或0xA001
					}
					else
					{
						crc >>= 1; // 否則直接右移
					}
				}
			}
			return crc; // 返回計算結果
		}
	}
}
