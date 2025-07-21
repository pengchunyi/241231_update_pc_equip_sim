//using System;
//using System.Collections.Generic;
//using System.Linq;
//using System.Threading.Tasks;
//using System.Net.Sockets;
//using CFX;
//using CFX.Production;
//using CFX.ResourcePerformance;
//using CFX.Structures;
//using CFX.Transport;
//using Newtonsoft.Json;



//namespace AmqpModbusIntegration
//{
//	public class AmqpEndpointManager
//	{
//		private readonly string endpointUri;
//		private readonly string publishChannelUri;
//		private readonly string subscribeChannelUri;
//		private readonly ModbusViewer modbusViewer;
//		private readonly Dictionary<byte, Dictionary<string, object>> slaveData;

//		private AmqpCFXEndpoint endpoint;





//		private static readonly Dictionary<int, (string Code, string ErrorDescription)> faultDictionary = new Dictionary<int, (string, string)>
//		{
//			{ 0,  ("EGY_1_WARN1",  "A相過壓") },
//			{ 1,  ("EGY_1_WARN2",  "B相過壓") },
//            // ... 其餘故障碼定義略 ...
//            { 26, ("EGY_1_WARN27", "過溫預警") }
//		};

//		public AmqpEndpointManager(
//			string endpointUri,
//			string publishChannelUri,
//			string subscribeChannelUri,
//			ModbusViewer modbusViewer,
//			Dictionary<byte, Dictionary<string, object>> slaveData)
//		{
//			this.endpointUri = endpointUri;
//			this.publishChannelUri = publishChannelUri;
//			this.subscribeChannelUri = subscribeChannelUri;
//			this.modbusViewer = modbusViewer;
//			this.slaveData = slaveData;
//		}

//		public void StartAmqpEndpoint(string endpointName)
//		{
//			if (string.IsNullOrEmpty(endpointName))
//				throw new ArgumentException("Endpoint name cannot be null or empty", nameof(endpointName));

//			endpoint = new AmqpCFXEndpoint();
//			endpoint.Open(endpointName, new Uri(endpointUri));
//			endpoint.AddPublishChannel(new Uri(publishChannelUri), "event");
//			endpoint.Publish(new EndpointConnected());
//			Console.WriteLine($"AMQP endpoint '{endpointName}' connected.");

//			endpoint.OnRequestReceived += req => OnRequestReceivedHandler(req);

//			// 定期狀態檢查與發布
//			Task.Run(async () =>
//			{
//				var lastStatus = new Dictionary<byte, int>();
//				DateTime lastEnergyTime = DateTime.MinValue;
//				TcpClient modbusClient = new TcpClient();

//				while (true)
//				{
//					try
//					{
//						var tasks = new List<Task>();

//						// 故障狀態檢查
//						foreach (var station in slaveData.Keys)
//						{
//							if (slaveData[station].TryGetValue("Fault_WarningCode", out var obj))
//							{
//								int code = Convert.ToInt32(obj);
//								if (lastStatus.TryGetValue(station, out var prev) && prev != code)
//									tasks.Add(PublishFaultOccurredMessages(station));
//								lastStatus[station] = code;
//							}
//						}

//						// 每60秒發佈能源與參數
//						if ((DateTime.Now - lastEnergyTime).TotalSeconds >= 60)
//						{
//							foreach (var station in slaveData.Keys)
//							{
//								try
//								{
//									if (!modbusClient.Connected)
//									{
//										await modbusClient.ConnectAsync(SystemConfig.IpAddress, SystemConfig.Port);
//										await Task.Delay(100);
//									}

//									var req = BuildModbusRequest(station, 0x03, 0x0000, 0x0030);
//									var stream = modbusClient.GetStream();
//									await stream.WriteAsync(req, 0, req.Length);

//									byte[] resp = new byte[256];
//									int len = await stream.ReadAsync(resp, 0, resp.Length);
//									if (len > 7 && ValidateResponse(resp, len))
//									{
//										ModbusHelper.ParseModbusData(resp, 7, station, modbusViewer);
//										PublishStationParametersModifiedMessages(station);
//										PublishEnergyConsumedMessages(station);
//									}
//								}
//								catch (Exception ex)
//								{
//									Console.WriteLine($"TCP error: {ex.Message}");
//									modbusClient.Close();
//									modbusClient = new TcpClient();
//								}
//							}
//							lastEnergyTime = DateTime.Now;
//						}

//						await Task.WhenAll(tasks);
//						await Task.Delay(1000);
//					}
//					catch (Exception ex)
//					{
//						Console.WriteLine($"Loop error: {ex.Message}");
//						await Task.Delay(1000);
//					}
//				}
//			});
//		}

//		/* ① 建立請求時使用 BuildMbapHeader */
//		private byte[] BuildModbusRequest(byte unitId, byte func, ushort start, ushort qty)
//		{
//			byte[] pdu = {
//			func,
//			(byte)(start>>8), (byte)start,
//			(byte)(qty  >>8), (byte)qty
//			};

//			return ModbusHelper.BuildRequestWithMbap(unitId, pdu);   // => 下面新增的小工具
//		}

//		private bool ValidateResponse(byte[] resp, int len)
//			=> resp.Length >= 9 && resp[7] == 0x03 && (resp[8] & 0x80) == 0;

//		private CFXEnvelope OnRequestReceivedHandler(CFXEnvelope request)
//		{
//			// 與原邏輯相同，省略重寫細節
//			return CreateErrorResponse(request.RequestID, "Unsupported request type.");
//		}

//		private async Task PublishFaultOccurredMessages(byte station)
//		{
//			await Task.CompletedTask;

//			if (!slaveData.TryGetValue(station, out var data)) return;
//			int reg = Convert.ToInt32(data["Fault_WarningCode"]);
//			for (int bit = 0; bit < 27; bit++)
//			{
//				if ((reg & (1 << bit)) != 0 && faultDictionary.TryGetValue(bit, out var info))
//				{
//					var msg = new FaultOccurred
//					{
//						Fault = new Fault
//						{
//							FaultCode = info.Code,
//							FaultOccurrenceId = Guid.NewGuid(),
//							Description = info.ErrorDescription,
//							OccurredAt = DateTime.Now,
//							Severity = FaultSeverity.Error
//						}
//					};
//					endpoint.Publish(msg);
//				}
//			}
//		}

//		private void PublishStationParametersModifiedMessages(byte station)
//		{
//			// 同前，只利用 endpoint.Publish(...)
//		}

//		private void PublishEnergyConsumedMessages(byte station)
//		{
//			// 同前
//		}

//		private CFXEnvelope CreateErrorResponse(string requestId, string msg)
//		{
//			var resp = new NotSupportedResponse
//			{
//				RequestResult = new RequestResult
//				{
//					Result = StatusResult.Failed,
//					ResultCode = 2,
//					Message = msg
//				}
//			};
//			var env = CFXEnvelope.FromCFXMessage(resp);
//			env.RequestID = requestId;
//			return env;
//		}
//	}
//}
