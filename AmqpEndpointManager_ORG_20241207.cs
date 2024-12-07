//using System;
//using System.Collections.Generic;
//using System.IO.Ports;
//using System.Linq;
//using System.Threading.Tasks;
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

//		private readonly SerialPort serialPort;
//		private readonly Dictionary<byte, Dictionary<string, int>> slaveData;


//		private static readonly Dictionary<int, (string FaultCode, string Description)> faultDictionary = new Dictionary<int, (string FaultCode, string Description)>
//		{
//			{ 0, ("A_OVERVOLTAGE", "A相過壓") },
//			{ 1, ("B_OVERVOLTAGE", "B相過壓") },
//			{ 2, ("C_OVERVOLTAGE", "C相過壓") },
//			{ 3, ("A_UNDERVOLTAGE", "A相欠壓") },
//			{ 4, ("B_UNDERVOLTAGE", "B相欠壓") },
//			{ 5, ("C_UNDERVOLTAGE", "C相欠壓") },
//			{ 6, ("A_OVERCURRENT", "A相過流") },
//			{ 7, ("B_OVERCURRENT", "B相過流") },
//			{ 8, ("C_OVERCURRENT", "C相過流") },
//			{ 9, ("LEAKAGE_FAULT", "漏電異常") },
//			{ 10, ("A_OVERHEAT", "A相出線溫度異常") },
//			{ 11, ("B_OVERHEAT", "B相出線溫度異常") },
//			{ 12, ("C_OVERHEAT", "C相出線溫度異常") },
//			{ 13, ("N_OVERHEAT", "N相出線溫度異常") },
//			{ 14, ("SHORT_CIRCUIT", "短路") },
//			{ 15, ("ARC_FAULT", "電弧") },
//			{ 16, ("PHASE_LOSS", "缺相") },
//			{ 17, ("NEUTRAL_DISCONNECT", "斷零") },
//			{ 18, ("VOLTAGE_IMBALANCE", "三相電壓不平衡") },
//			{ 19, ("LOCKOUT", "鎖定") },
//			{ 20, ("MAINTENANCE_MODE", "進入維修或手動模式") },
//			{ 21, ("SWITCH_FAULT", "開關狀態異常，提示客戶換設備") },
//			{ 22, ("LEAKAGE_FAILURE", "漏電功能壞，提示客戶換設備") },
//			{ 23, ("DEVICE_OFFLINE", "設備離線") },
//			{ 24, ("OVERVOLTAGE_WARNING", "過壓預警") },
//			{ 25, ("UNDERVOLTAGE_WARNING", "欠壓預警") },
//			{ 26, ("OVERCURRENT_WARNING", "過流預警") },
//			{ 27, ("OVERHEAT_WARNING", "過溫預警") }
//		};


//		public AmqpEndpointManager(
//			string endpointUri,
//			string publishChannelUri,
//			string subscribeChannelUri,
//			ModbusViewer modbusViewer,
//			SerialPort serialPort,
//			Dictionary<byte, Dictionary<string, int>> slaveData)
//		{
//			this.endpointUri = endpointUri;
//			this.publishChannelUri = publishChannelUri;
//			this.subscribeChannelUri = subscribeChannelUri;
//			this.modbusViewer = modbusViewer;
//			this.serialPort = serialPort;
//			this.slaveData = slaveData;
//		}

//		public void StartAmqpEndpoint(string endpointName)
//		{
//			try
//			{
//				var endpoint = new AmqpCFXEndpoint();
//				endpoint.Open(endpointName, new Uri(endpointUri));
//				endpoint.AddPublishChannel(new Uri(publishChannelUri), "event");

//				//20241206_新增_處理遠端溫度設定_處理遠端溫度設定=========================================================
//				endpoint.OnCFXMessageReceived += async (sender, e) =>
//				{
//					try
//					{
//						// 提取 RequestID
//						var requestId = e.RequestID;

//						// 提取 ModifyStationParametersRequest
//						if (e.GetMessage<ModifyStationParametersRequest>() is ModifyStationParametersRequest modifyRequest)
//						{
//							var responseEnvelope = await HandleModifyStationParametersRequest(modifyRequest, requestId);
//							endpoint.Publish(responseEnvelope); // 確保正確回應
//						}
//						else
//						{
//							Console.WriteLine("Received a message, but it was not a ModifyStationParametersRequest.");
//						}
//					}
//					catch (Exception ex)
//					{
//						Console.WriteLine($"Error processing received message: {ex.Message}");
//					}
//				};

//				//20241206_新增_處理遠端溫度設定=========================================================



//				endpoint.Publish(new EndpointConnected());
//				Console.WriteLine($"AMQP endpoint \"{endpointName}\" connected.");

//				// 狀態檢查過程（即時發佈故障消息）
//				Task.Run(async () =>
//				{
//					Dictionary<byte, int> lastStatus = new Dictionary<byte, int>();
//					DateTime lastEnergyPublishTime = DateTime.MinValue; // 用來記錄上次能源消耗發佈的時間

//					while (true)
//					{
//						List<Task> tasks = new List<Task>(); // 用來儲存所有發佈訊息的任務

//						// 檢查每個站點的狀態是否變動
//						foreach (var stationNumber in slaveData.Keys)
//						{
//							try
//							{
//								if (slaveData.TryGetValue(stationNumber, out var data) && data.ContainsKey("當前狀態"))
//								{
//									var currentStatus = data["當前狀態"];

//									// 比對當前狀態和上一個狀態
//									if (lastStatus.ContainsKey(stationNumber) && lastStatus[stationNumber] != currentStatus)
//									{
//										// 發現狀態變動，立即發佈故障信息
//										tasks.Add(PublishFaultOccurredMessages(stationNumber, endpoint));
//									}

//									// 更新上一個狀態
//									lastStatus[stationNumber] = currentStatus;
//								}
//							}
//							catch (Exception ex)
//							{
//								Console.WriteLine($"錯誤處理站點 {stationNumber}: {ex.Message}");
//							}
//						}

//						// 每 6 秒發佈一次能源消耗訊息
//						if ((DateTime.Now - lastEnergyPublishTime).TotalSeconds >= 12)
//						{
//							tasks.Add(PublishEnergyConsumptionResponses(endpoint));
//							lastEnergyPublishTime = DateTime.Now; // 更新上次發佈時間
//						}

//						// 等待所有發佈任務完成
//						await Task.WhenAll(tasks);

//						// 延遲1秒後再進行下一次檢查
//						await Task.Delay(1000);
//					}
//				});
//			}
//			catch (Exception ex)
//			{
//				Console.WriteLine($"啟動AMQP端點時發生錯誤: {ex.Message}");
//			}
//		}


//		//20241206_新增_處理遠端溫度設定=================================================
//		// 新增的處理請求邏輯
//		private async Task<CFXEnvelope> HandleModifyStationParametersRequest(ModifyStationParametersRequest request, string requestId)
//		{
//			Console.WriteLine($"收到 ModifyStationParametersRequest: {JsonConvert.SerializeObject(request, Formatting.Indented)}");

//			// 構造成功的 ModifyStationParametersResponse
//			var response = new ModifyStationParametersResponse
//			{
//				Result = new RequestResult
//				{
//					Result = StatusResult.Success,
//					ResultCode = 0,
//					Message = "Parameters updated successfully."
//				}
//			};

//			// 將響應封裝為 CFXEnvelope 並附加 RequestID
//			var responseEnvelope = CFXEnvelope.FromCFXMessage(response);
//			responseEnvelope.RequestID = requestId; // 使用從 CFXEnvelope 中提取的 RequestID
//			return responseEnvelope;
//		}

//		//20241206_新增_處理遠端溫度設定=================================================



//		private async Task PublishEnergyConsumptionResponses(AmqpCFXEndpoint endpoint)
//		{
//			foreach (var stationNumber in slaveData.Keys)
//			{
//				//PublishFaultOccurredMessages(stationNumber, endpoint);
//				PublishReadingsRecordedMessages(stationNumber, endpoint);
//				PublishStationParametersModifiedMessages(stationNumber, endpoint);
//				PublishEnergyConsumedMessages(stationNumber, endpoint);
//			}
//		}


//		private async Task PublishFaultOccurredMessages(byte stationNumber, AmqpCFXEndpoint endpoint)
//		{
//			if (!slaveData.TryGetValue(stationNumber, out var data) || !data.ContainsKey("當前狀態"))
//				return;

//			var statusRegister = data["當前狀態"];
//			for (int bitPosition = 0; bitPosition <= 27; bitPosition++)
//			{
//				if ((statusRegister & (1 << bitPosition)) != 0 && faultDictionary.ContainsKey(bitPosition))
//				{
//					var (faultCode, description) = faultDictionary[bitPosition];
//					var faultOccurred = new FaultOccurred
//					{
//						Fault = new Fault
//						{
//							FaultCode = faultCode,
//							FaultOccurrenceId = Guid.NewGuid(),
//							Description = faultCode,
//							DescriptionTranslations = new Dictionary<string, string>
//					{
//						{ "zh-TW", description } // 如果需要，添加多語言對應描述
//                    },
//							OccurredAt = DateTime.UtcNow,
//							Severity = FaultSeverity.Error
//						}
//					};

//					// 使用 Task.Run 將同步操作包裝成異步執行
//					await Task.Run(() => endpoint.Publish(faultOccurred));
//				}
//			}
//		}


//		private void PublishReadingsRecordedMessages(byte stationNumber, AmqpCFXEndpoint endpoint)
//		{
//			//if (!slaveData.TryGetValue(stationNumber, out var data))
//			//	return;
//			//20241204新增====================================================
//			if (!slaveData.TryGetValue(stationNumber, out var data) || data == null || !data.Any())
//			{
//				Console.WriteLine($"No data available for station {stationNumber}.");
//				return;
//			}

//			// 確保至少有一個非零數據值
//			if (!data.Values.Any(value => value != 0))
//			{
//				Console.WriteLine($"All data for station {stationNumber} is default (zero), skipping.");
//				return;
//			}

//			var readings = new List<Reading>
//			{
//				new Reading
//				{
//					Value = data.ContainsKey("A相溫度 (℃)") ? data["A相溫度 (℃)"].ToString() : "0",
//					ReadingIdentifier = "A相溫度",
//					ValueUnits = "℃",
//					TimeRecorded = DateTime.UtcNow
//				},
//				new Reading
//				{
//					Value = data.ContainsKey("B相溫度 (℃)") ? data["B相溫度 (℃)"].ToString() : "0",
//					ReadingIdentifier = "B相溫度",
//					ValueUnits = "℃",
//					TimeRecorded = DateTime.UtcNow
//				},
//				new Reading
//				{
//					Value = data.ContainsKey("C相溫度 (℃)") ? data["C相溫度 (℃)"].ToString() : "0",
//					ReadingIdentifier = "C相溫度",
//					ValueUnits = "℃",
//					TimeRecorded = DateTime.UtcNow
//				}
//			};


//			var readingsRecorded = new ReadingsRecorded
//			{
//				Readings = readings,
//			};

//			endpoint.Publish(readingsRecorded);
//			Console.WriteLine($"Published ReadingsRecorded message for station {stationNumber}.");
//		}





//		private void PublishStationParametersModifiedMessages(byte stationNumber, AmqpCFXEndpoint endpoint)
//		{
//			//20241204新增====================================================
//			if (!slaveData.TryGetValue(stationNumber, out var data) || data == null || !data.Any())
//			{
//				Console.WriteLine($"No data available for station {stationNumber}.");
//				return;
//			}

//			// 確保至少有一個非零數據值
//			if (!data.Values.Any(value => value != 0))
//			{
//				Console.WriteLine($"All data for station {stationNumber} is default (zero), skipping.");
//				return;
//			}

//			var parameters = new List<Parameter>
//			{
//				new GenericParameter
//				{
//					Name = "開關狀態",
//					Value = data.ContainsKey("開關狀態") ? data["開關狀態"].ToString() : "0"
//				}
//			};

//			endpoint.Publish(new StationParametersModified { ModifiedParameters = parameters });
//		}


//		private void PublishEnergyConsumedMessages(byte stationNumber, AmqpCFXEndpoint endpoint)
//		{
//			if (!slaveData.TryGetValue(stationNumber, out var data) || data == null || !data.Any())
//			{
//				Console.WriteLine($"No data available for station {stationNumber}.");
//				return;
//			}

//			// 確保至少有一個非零數據值
//			if (!data.Values.Any(value => value != 0))
//			{
//				Console.WriteLine($"All data for station {stationNumber} is default (zero), skipping.");
//				return;
//			}

//					// 提取 RYB 電流
//					var currentRYB = new List<double>
//			{
//				data.TryGetValue("A相電流 (A)", out var currentA) ? currentA : 0.0,
//				data.TryGetValue("B相電流 (A)", out var currentB) ? currentB : 0.0,
//				data.TryGetValue("C相電流 (A)", out var currentC) ? currentC : 0.0
//			};

//					// 提取 RYB 功率
//					var powerRYB = new List<double>
//			{
//				data.TryGetValue("A相有功功率 (W)", out var powerA) ? powerA : 0.0,
//				data.TryGetValue("B相有功功率 (W)", out var powerB) ? powerB : 0.0,
//				data.TryGetValue("C相有功功率 (W)", out var powerC) ? powerC : 0.0
//			};

//					// 提取 RYB 電壓
//					var voltageRYB = new List<double>
//			{
//				data.TryGetValue("A相電壓 (V)", out var voltageA) ? voltageA : 0.0,
//				data.TryGetValue("B相電壓 (V)", out var voltageB) ? voltageB : 0.0,
//				data.TryGetValue("C相電壓 (V)", out var voltageC) ? voltageC : 0.0
//			};

//			// 提取總電能
//			var energyUsed = data.TryGetValue("電能 (kWh)", out var energy) ? energy : 0.0;

//			// 創建 EnergyConsumed 訊息
//			var energyConsumed = new EnergyConsumed
//			{
//				EnergyUsed = energyUsed,
//				StartTime = DateTime.UtcNow,
//				EndTime = DateTime.UtcNow,
//				CurrentNowRYB = currentRYB,
//				PowerNowRYB = powerRYB,

//				//20241129新增測試==================================================
//				VoltageNowRYB = voltageRYB // 添加電壓數據
//				//20241129新增測試==================================================
//			};

//			// 發佈訊息
//			endpoint.Publish(energyConsumed);
//			Console.WriteLine($"Published EnergyConsumed message for station {stationNumber}.");
//		}





//	}
//}
