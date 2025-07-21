//using System;
//using System.Collections.Generic;
//using System.Linq;
//using System.Threading;
//using System.Threading.Tasks;
//using System.Net.Sockets;
//using System.Windows.Forms;
//using System.Net;

//namespace AmqpModbusIntegration
//{
//	public static class ModbusHelper
//	{
//		private static TcpClient tcpClient;
//		private static NetworkStream networkStream;
//		public static readonly SemaphoreSlim tcpLock = new SemaphoreSlim(1, 1);

//		public static byte[] BuildRequestWithMbap(byte unitId, byte[] pdu)
//		{
//			return BuildMbapHeader(unitId, (ushort)pdu.Length).Concat(pdu).ToArray();
//		}



//		/*============  MBAP 建構 & 交易序號  ============*/
//		private static ushort transactionId = 0;
//		public static byte[] BuildMbapHeader(byte unitId, ushort pduLen)
//		{
//			unchecked { ++transactionId; }
//			return new byte[]
//			{
//				(byte)(transactionId >> 8), (byte)transactionId, // Transaction ID
//                0x00, 0x00,                                      // Protocol ID = 0
//                (byte)((pduLen + 1) >> 8), (byte)(pduLen + 1),   // Length = UnitId + PDU
//                unitId                                           // Unit Identifier
//            };
//		}
//		/*================================================*/

//		public static async Task ConnectAsync(string ip, int port)
//		{
//			await tcpLock.WaitAsync();
//			try
//			{
//				if (tcpClient == null || !tcpClient.Connected)
//				{
//					tcpClient = new TcpClient();
//					await tcpClient.ConnectAsync(ip, port);
//					networkStream = tcpClient.GetStream();
//					Console.WriteLine($"已連接 {ip}:{port}");
//				}
//			}
//			finally { tcpLock.Release(); }
//		}

//		/*----------------  高階封裝  ----------------*/
//		public static async Task SwitchON(string ip, int port, byte stn)
//			=> await SendWriteSingleRegister(ip, port, stn, 0x0031, 0x5588); // 21896

//		public static async Task SwitchOFF(string ip, int port, byte stn)
//			=> await SendWriteSingleRegister(ip, port, stn, 0x0031, 0x5566); // 21862

//		public static void SetTemperature(string ip, int port, byte stn, ushort temp,
//										  Dictionary<byte, Dictionary<string, object>> dict)
//		{
//			SendWriteSingleRegister(ip, port, stn, 0x002B, temp).Wait();
//			dict[stn]["TemperatureSV"] = temp;
//		}

//		/*-----------  核心：寫單一暫存器  ------------*/
//		private static async Task SendWriteSingleRegister(string ip, int port, byte unit,
//														  ushort addr, ushort val)
//		{
//			await tcpLock.WaitAsync();
//			try
//			{
//				if (tcpClient == null || !tcpClient.Connected)
//					await ConnectAsync(ip, port);

//				byte[] pdu = {
//					0x06,
//					(byte)(addr>>8), (byte)addr,
//					(byte)(val >>8), (byte)val
//				};
//				byte[] frame = BuildMbapHeader(unit, (ushort)pdu.Length).Concat(pdu).ToArray();

//				await networkStream.WriteAsync(frame, 0, frame.Length);
//				await networkStream.FlushAsync();

//				// 讀回 Echo (12 bytes)。如需驗證可加檢查，這裡略過。
//				var buf = new byte[12];
//				await networkStream.ReadAsync(buf, 0, buf.Length);
//			}
//			finally { tcpLock.Release(); }
//		}
//		/*---------------------------------------------*/

//		public static async Task ReadAllParametersAsync(string ip, int port, ModbusViewer viewer)
//		{
//			await tcpLock.WaitAsync();
//			try
//			{
//				if (tcpClient == null || !tcpClient.Connected)
//					await ConnectAsync(ip, port);

//				foreach (var stn in viewer.slaveData.Keys.ToList())
//				{
//					byte[] pdu = { 0x03, 0x00, 0x00, 0x00, 0x30 };
//					byte[] req = BuildMbapHeader(stn, (ushort)pdu.Length).Concat(pdu).ToArray();

//					await networkStream.WriteAsync(req, 0, req.Length);

//					var buf = new byte[256];
//					int len = await networkStream.ReadAsync(buf, 0, buf.Length);
//					if (len >= 9 && buf[7] == stn && buf[8] == 0x03)
//						ParseModbusData(buf, 7, stn, viewer);  // offset = MBAP(7bytes)
//				}
//			}
//			finally { tcpLock.Release(); }
//		}





//		public static void ParseModbusData(byte[] data, int offset, byte stationNumber, ModbusViewer modbusViewer)
//		{
//			if (data.Length < 5)
//			{
//				Console.WriteLine("回應長度過短");
//				return;
//			}

//			byte recvStation = data[offset];
//			byte funcCode = data[offset + 1];

//			if (recvStation != stationNumber)
//			{
//				Console.WriteLine($"站號不符: 預期 {stationNumber}，收到 {recvStation}");
//				return;
//			}

//			if ((funcCode & 0x80) != 0)
//			{
//				Console.WriteLine($"Modbus 異常響應: function = {funcCode}, code = {data[offset + 2]}");
//				return;
//			}

//			if (funcCode == 0x03)
//			{
//				byte byteCount = data[offset + 2];
//				int dataStart = offset + 3;

//				if (data.Length < dataStart + byteCount)
//				{
//					Console.WriteLine($"資料長度不足: 預期 {byteCount}，實際 {data.Length - dataStart}");
//					return;
//				}

//				// ====== 寄存器數據映射 ======
//				// 狀態字解析 (原buffer[3]-[6] 對應TCP data[dataStart + 0]-[3])
//				modbusViewer.currentStatus1 = (data[dataStart] << 8) | data[dataStart + 1];
//				modbusViewer.currentStatus2 = (data[dataStart + 2] << 8) | data[dataStart + 3];
//				modbusViewer.currentStatus = (modbusViewer.currentStatus1 << 16) | modbusViewer.currentStatus2;

//				// 溫度寄存器 (原buffer[9]-[14] 對應TCP data[dataStart + 6]-[11])
//				modbusViewer.tempA = (data[dataStart + 6] << 8) | data[dataStart + 7];
//				modbusViewer.tempB = (data[dataStart + 8] << 8) | data[dataStart + 9];
//				modbusViewer.tempC = (data[dataStart + 10] << 8) | data[dataStart + 11];

//				// 電流值 (原buffer[23]-[28] 對應TCP data[dataStart + 20]-[25])
//				modbusViewer.currentA = ((data[dataStart + 20] << 8) | data[dataStart + 21]) / 100.0;
//				modbusViewer.currentB = ((data[dataStart + 22] << 8) | data[dataStart + 23]) / 100.0;
//				modbusViewer.currentC = ((data[dataStart + 24] << 8) | data[dataStart + 25]) / 100.0;

//				// 功率值 (原buffer[35]-[40] 對應TCP data[dataStart + 32]-[37])
//				modbusViewer.activePowerA = (data[dataStart + 32] << 8) | data[dataStart + 33];
//				modbusViewer.activePowerB = (data[dataStart + 34] << 8) | data[dataStart + 35];
//				modbusViewer.activePowerC = (data[dataStart + 36] << 8) | data[dataStart + 37];

//				// 能源值組合 (原buffer[49]-[52] 對應TCP data[dataStart + 46]-[49])
//				int energyHighByte = (data[dataStart + 46] << 8) | data[dataStart + 47];
//				int energyLowByte = (data[dataStart + 48] << 8) | data[dataStart + 49];
//				modbusViewer.energy = ((energyHighByte << 16) | energyLowByte) / 100.0;

//				// 開關狀態 (原buffer[53]-[54] 對應TCP data[dataStart + 50]-[51])
//				modbusViewer.switchStatus = (data[dataStart + 50] << 8) | data[dataStart + 51];

//				// 保護閾值 (原buffer[89]-[90] 對應TCP data[dataStart + 86]-[87])
//				modbusViewer.ProtectionThreshold = (data[dataStart + 86] << 8) | data[dataStart + 87];

//				// ====== 數據存儲到字典 ======
//				var sData = modbusViewer.slaveData[stationNumber];
//				sData["Fault_WarningCode"] = modbusViewer.currentStatus;
//				sData["PowerTemperatureA"] = modbusViewer.tempA;
//				sData["PowerTemperatureB"] = modbusViewer.tempB;
//				sData["PowerTemperatureC"] = modbusViewer.tempC;
//				sData["CurrentNowRYB_A"] = modbusViewer.currentA;
//				sData["CurrentNowRYB_B"] = modbusViewer.currentB;
//				sData["CurrentNowRYB_C"] = modbusViewer.currentC;
//				sData["PowerNowRYB_A"] = modbusViewer.activePowerA;
//				sData["PowerNowRYB_B"] = modbusViewer.activePowerB;
//				sData["PowerNowRYB_C"] = modbusViewer.activePowerC;
//				sData["EnergyUsed"] = modbusViewer.energy;
//				sData["PowerSwitch"] = modbusViewer.switchStatus;
//				sData["TemperatureSV"] = modbusViewer.ProtectionThreshold;
//			}
//			else
//			{
//				//Console.WriteLine($"Unsupported function code: 0x{functionCode:X2}");
//			}
//		}



//		public static void SimulateFaultTest(ModbusViewer modbusViewer, Dictionary<byte, Dictionary<string, object>> slaveData)
//		{
//			int faultCode = 0xA87;
//			modbusViewer.currentStatus = faultCode;
//			modbusViewer.currentStatus1 = (faultCode >> 16) & 0xFFFF;
//			modbusViewer.currentStatus2 = faultCode & 0xFFFF;

//			foreach (var station in slaveData.Keys)
//			{
//				// 若該站已有 key，則更新
//				if (slaveData[station].ContainsKey("Fault_WarningCode"))
//				{
//					slaveData[station]["Fault_WarningCode"] = faultCode;
//				}
//			}

//			modbusViewer.UpdateDataGridView();
//			MessageBox.Show($"已將Fault_WarningCode設置為故障碼 {faultCode:X}");
//		}

//	}
//}
