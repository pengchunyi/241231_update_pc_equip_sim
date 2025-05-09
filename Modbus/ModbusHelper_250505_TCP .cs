using System;
using System.Collections.Generic;
using System.IO.Ports;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

//250505為了TCP新增
using System.Net.Sockets;

namespace AmqpModbusIntegration
{
	public static class ModbusHelper
	{

		// 替换 SerialPort 为 TcpClient
		private static TcpClient tcpClient;
		private static NetworkStream networkStream;
		//鎖頭，必免陷入爭搶資源
		//private static readonly object tcpLock = new object(); // 用于网络操作的锁
		public static readonly SemaphoreSlim tcpLock = new SemaphoreSlim(1, 1); // 替換原有的 object lock

		// 修正後 (使用 SemaphoreSlim 同步鎖)
		public static void SwitchON(string ipAddress, int port, byte stationNumber)
		{
			tcpLock.Wait();
			try
			{
				if (tcpClient == null || !tcpClient.Connected)
				{
					tcpClient = new TcpClient();
					tcpClient.Connect(ipAddress, port);
					networkStream = tcpClient.GetStream();
				}

				byte[] openCommand = { stationNumber, 0x06, 0x00, 0x31, 0x55, 0x88 };
				//byte[] fullCommand = AppendCRC(openCommand);
				networkStream.Write(openCommand, 0, openCommand.Length);
			}
			finally
			{
				tcpLock.Release();
			}
		}

		public static void SwitchOFF(string ipAddress, int port, byte stationNumber)
		{
			lock (tcpLock)
			{
				try
				{
					if (tcpClient == null || !tcpClient.Connected)
					{
						tcpClient = new TcpClient();
						tcpClient.Connect(ipAddress, port);
						networkStream = tcpClient.GetStream();
					}

					// 250423新增==========
					// 对于TCP，不需要设置RtsEnable
					// 250423新增==========

					byte[] closeCommand = { stationNumber, 0x06, 0x00, 0x31, 0x55, 0x66 };
					//byte[] fullCommand = AppendCRC(closeCommand);
					networkStream.Write(closeCommand, 0, closeCommand.Length);

					// 250423新增==========
					// 对于TCP，不需要Sleep和设置RtsEnable
					// Thread.Sleep(10);
					// serialPort.RtsEnable = false;
					// 250423新增==========

					Console.WriteLine($"站號 {stationNumber} 的開關已關閉 (TCP)");
				}
				catch (Exception ex)
				{
					Console.WriteLine($"執行 SwitchOFF 時發生錯誤: {ex.Message}");
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

		public static void SetTemperature(string ipAddress, int port, byte stationNumber, ushort temperature, Dictionary<byte, Dictionary<string, object>> slaveData)
		{
			lock (tcpLock)
			{
				try
				{
					if (tcpClient == null || !tcpClient.Connected)
					{
						tcpClient = new TcpClient();
						tcpClient.Connect(ipAddress, port);
						networkStream = tcpClient.GetStream();
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

					networkStream.Write(command, 0, command.Length);

					// 读取响应
					byte[] buffer = new byte[256];
					int bytesRead = networkStream.Read(buffer, 0, buffer.Length);

					if (bytesRead > 5 )
					{
						Console.WriteLine($"已發送溫度設定命令到站號 {stationNumber}，設定溫度：{temperature}°C (TCP)");

						// 解析响应并更新 slaveData
						// 这里需要根据实际的响应格式进行解析
						// 示例中假设响应包含状态码
						slaveData[stationNumber]["TemperatureSV"] = temperature;
					}
					else
					{
						Console.WriteLine("收到無效回應或 CRC 校驗失敗 (TCP)");
					}
				}
				catch (Exception ex)
				{
					Console.WriteLine($"設置溫度時發生錯誤 (TCP): {ex.Message}");
				}
			}
		}



		// 修正後 (使用 SemaphoreSlim 異步鎖)
		public static async Task ReadAllParametersAsync(string ipAddress, int port, ModbusViewer modbusViewer)
		{
			await tcpLock.WaitAsync();
			try
			{
				if (tcpClient == null || !tcpClient.Connected)
				{
					tcpClient = new TcpClient();
					await tcpClient.ConnectAsync(ipAddress, port);
					networkStream = tcpClient.GetStream();
				}

				foreach (var station in modbusViewer.slaveData.Keys.ToList())
				{
					byte[] readCommand = { station, 0x03, 0x00, 0x00, 0x00, 0x30 };
					await networkStream.WriteAsync(readCommand, 0, readCommand.Length);

					byte[] buffer = new byte[256];
					int bytesRead = await networkStream.ReadAsync(buffer, 0, buffer.Length);

					if (bytesRead >= 7)
					{
						ParseModbusData(buffer, 0, station, modbusViewer);
					}
				}
			}
			finally
			{
				tcpLock.Release();
			}
		}


		public static void ParseModbusData(byte[] data, int offset, byte stationNumber, ModbusViewer modbusViewer)
		{
			// ====== Modbus TCP 協議頭驗證 ======
			// 檢查數據長度是否足夠解析MBAP頭 (7字節)
			if (data.Length < offset + 7)
			{
				Console.WriteLine($"Invalid Modbus TCP frame length: {data.Length - offset} bytes");
				return;
			}

			// 提取MBAP頭字段 (網頁7規範)
			ushort transactionId = (ushort)((data[offset] << 8) | data[offset + 1]);
			ushort protocolId = (ushort)((data[offset + 2] << 8) | data[offset + 3]);
			ushort length = (ushort)((data[offset + 4] << 8) | data[offset + 5]);
			byte unitId = data[offset + 6];

			// 驗證協議標識符和單元ID (網頁8)
			if (protocolId != 0)
			{
				Console.WriteLine($"Invalid protocol ID: 0x{protocolId:X4}");
				return;
			}

			if (unitId != stationNumber)
			{
				Console.WriteLine($"Unit ID mismatch: Received {unitId}, Expected {stationNumber}");
				return;
			}

			// ====== PDU(協議數據單元)解析 ======
			int pduStart = offset + 7;  // PDU起始位置
			byte functionCode = data[pduStart];

			// 異常響應處理 (網頁6)
			if ((functionCode & 0x80) != 0)
			{
				byte errorCode = data[pduStart + 1];
				Console.WriteLine($"Modbus Exception (Function 0x{functionCode & 0x7F:X2}, Code 0x{errorCode:X2})");
				return;
			}

			// 功能碼03(讀保持寄存器)處理
			if (functionCode == 0x03)
			{
				byte byteCount = data[pduStart + 1];
				int dataStart = pduStart + 2;  // 實際數據起始位置

				// 驗證數據完整性
				if (data.Length < dataStart + byteCount)
				{
					Console.WriteLine($"Incomplete data: Expected {byteCount} bytes, got {data.Length - dataStart}");
					return;
				}

				// ====== 寄存器數據映射 ======
				// 狀態字解析 (原buffer[3]-[6] 對應TCP data[dataStart + 0]-[3])
				modbusViewer.currentStatus1 = (data[dataStart] << 8) | data[dataStart + 1];
				modbusViewer.currentStatus2 = (data[dataStart + 2] << 8) | data[dataStart + 3];
				modbusViewer.currentStatus = (modbusViewer.currentStatus1 << 16) | modbusViewer.currentStatus2;

				// 溫度寄存器 (原buffer[9]-[14] 對應TCP data[dataStart + 6]-[11])
				modbusViewer.tempA = (data[dataStart + 6] << 8) | data[dataStart + 7];
				modbusViewer.tempB = (data[dataStart + 8] << 8) | data[dataStart + 9];
				modbusViewer.tempC = (data[dataStart + 10] << 8) | data[dataStart + 11];

				// 電流值 (原buffer[23]-[28] 對應TCP data[dataStart + 20]-[25])
				modbusViewer.currentA = ((data[dataStart + 20] << 8) | data[dataStart + 21]) / 100.0;
				modbusViewer.currentB = ((data[dataStart + 22] << 8) | data[dataStart + 23]) / 100.0;
				modbusViewer.currentC = ((data[dataStart + 24] << 8) | data[dataStart + 25]) / 100.0;

				// 功率值 (原buffer[35]-[40] 對應TCP data[dataStart + 32]-[37])
				modbusViewer.activePowerA = (data[dataStart + 32] << 8) | data[dataStart + 33];
				modbusViewer.activePowerB = (data[dataStart + 34] << 8) | data[dataStart + 35];
				modbusViewer.activePowerC = (data[dataStart + 36] << 8) | data[dataStart + 37];

				// 能源值組合 (原buffer[49]-[52] 對應TCP data[dataStart + 46]-[49])
				int energyHighByte = (data[dataStart + 46] << 8) | data[dataStart + 47];
				int energyLowByte = (data[dataStart + 48] << 8) | data[dataStart + 49];
				modbusViewer.energy = ((energyHighByte << 16) | energyLowByte) / 100.0;

				// 開關狀態 (原buffer[53]-[54] 對應TCP data[dataStart + 50]-[51])
				modbusViewer.switchStatus = (data[dataStart + 50] << 8) | data[dataStart + 51];

				// 保護閾值 (原buffer[89]-[90] 對應TCP data[dataStart + 86]-[87])
				modbusViewer.ProtectionThreshold = (data[dataStart + 86] << 8) | data[dataStart + 87];

				// ====== 數據存儲到字典 ======
				var sData = modbusViewer.slaveData[stationNumber];
				sData["Fault_WarningCode"] = modbusViewer.currentStatus;
				sData["PowerTemperatureA"] = modbusViewer.tempA;
				sData["PowerTemperatureB"] = modbusViewer.tempB;
				sData["PowerTemperatureC"] = modbusViewer.tempC;
				sData["CurrentNowRYB_A"] = modbusViewer.currentA;
				sData["CurrentNowRYB_B"] = modbusViewer.currentB;
				sData["CurrentNowRYB_C"] = modbusViewer.currentC;
				sData["PowerNowRYB_A"] = modbusViewer.activePowerA;
				sData["PowerNowRYB_B"] = modbusViewer.activePowerB;
				sData["PowerNowRYB_C"] = modbusViewer.activePowerC;
				sData["EnergyUsed"] = modbusViewer.energy;
				sData["PowerSwitch"] = modbusViewer.switchStatus;
				sData["TemperatureSV"] = modbusViewer.ProtectionThreshold;
			}
			else
			{
				Console.WriteLine($"Unsupported function code: 0x{functionCode:X2}");
			}
		}





		//添加连接和断开函数​
		public static void Connect(string ipAddress, int port)
		{
			lock (tcpLock)
			{
				try
				{
					if (tcpClient == null || !tcpClient.Connected)
					{
						tcpClient = new TcpClient();
						tcpClient.Connect(ipAddress, port);
						networkStream = tcpClient.GetStream();
						Console.WriteLine($"已連接到 {ipAddress}:{port}");
					}
				}
				catch (Exception ex)
				{
					Console.WriteLine($"連接失敗: {ex.Message}");
				}
			}
		}

		public static void Disconnect()
		{
			lock (tcpLock)
			{
				try
				{
					if (networkStream != null)
					{
						networkStream.Close();
						networkStream = null;
					}
					if (tcpClient != null)
					{
						tcpClient.Close();
						tcpClient = null;
					}
					Console.WriteLine("已斷開連接");
				}
				catch (Exception ex)
				{
					Console.WriteLine($"斷開連接時發生錯誤: {ex.Message}");
				}
			}
		}



	}
}

