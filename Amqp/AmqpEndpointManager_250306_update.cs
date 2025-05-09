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

//		public static readonly object serialPortLock = new object(); // 用於串口操作的執行緒安全鎖

//		private static readonly Dictionary<int, (string Code, string ErrorDescription)> faultDictionary = new Dictionary<int, (string Code, string ErrorDescription)>
//		{
//			{ 0, ("EGY_1_WARN1", "A相過壓") },
//			{ 1, ("EGY_1_WARN2", "B相過壓") },
//			{ 2, ("EGY_1_WARN3", "C相過壓") },
//			{ 3, ("EGY_1_WARN4", "A相欠壓") },
//			{ 4, ("EGY_1_WARN5", "B相欠壓") },
//			{ 5, ("EGY_1_WARN6", "C相欠壓") },
//			{ 6, ("EGY_1_WARN7", "A相過流") },
//			{ 7, ("EGY_1_WARN8", "B相過流") },
//			{ 8, ("EGY_1_WARN9", "C相過流") },
//			{ 9, ("EGY_1_WARN10", "漏電異常") },
//			{ 10, ("EGY_1_WARN11", "A相出線溫度異常") },
//			{ 11, ("EGY_1_WARN12", "B相出線溫度異常") },
//			{ 12, ("EGY_1_WARN13", "C相出線溫度異常") },
//			{ 13, ("EGY_1_WARN14", "N相出線溫度異常") },
//			{ 14, ("EGY_1_WARN15", "電弧") },
//			{ 15, ("EGY_1_WARN16", "缺相") },
//			{ 16, ("EGY_1_WARN17", "斷零") },
//			{ 17, ("EGY_1_WARN18", "三相電壓不平衡") },
//			{ 18, ("EGY_1_WARN19", "鎖定") },
//			{ 19, ("EGY_1_WARN20", "進入維修或手動模式") },
//			{ 20, ("EGY_1_WARN21", "開關狀態異常，提示客戶換設備") },
//			{ 21, ("EGY_1_WARN22", "漏電功能壞，提示客戶換設備") },
//			{ 22, ("EGY_1_WARN23", "設備離線") },
//			{ 23, ("EGY_1_WARN24", "過壓預警") },
//			{ 24, ("EGY_1_WARN25", "欠壓預警") },
//			{ 25, ("EGY_1_WARN26", "過流預警") },
//			{ 26, ("EGY_1_WARN27", "過溫預警") }
//		};


//		public AmqpEndpointManager(
//			string endpointUri,
//			string publishChannelUri,
//			string subscribeChannelUri,
//			ModbusViewer modbusViewer,// 傳遞 ModbusViewer 實例
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
//			if (string.IsNullOrEmpty(endpointName))
//				throw new ArgumentException("Endpoint name cannot be null or empty", nameof(endpointName));

//			var endpoint = new AmqpCFXEndpoint();
//			endpoint.Open(endpointName, new Uri(endpointUri));
//			endpoint.AddPublishChannel(new Uri(publishChannelUri), "event");

//			// 發布連接消息
//			endpoint.Publish(new EndpointConnected());
//			Console.WriteLine($"AMQP endpoint \"{endpointName}\" connected.");

//			// Request/Response 處理邏輯
//			endpoint.OnRequestReceived += (request) =>
//			{
//				return OnRequestReceivedHandler(request);
//			};

//			// Publish 模式處理非請求消息
//			endpoint.OnCFXMessageReceived += async (sender, e) =>
//			{
//				try
//				{
//					// 提取消息類型
//					if (e.GetMessage<FaultOccurred>() is FaultOccurred faultOccurredMessage)
//					{
//						Console.WriteLine($"Received FaultOccurred message: {JsonConvert.SerializeObject(faultOccurredMessage, Formatting.Indented)}");
//						// 根據需要處理 FaultOccurred 消息
//					}
//					else if (e.GetMessage<EnergyConsumed>() is EnergyConsumed energyConsumedMessage)
//					{
//						Console.WriteLine($"Received EnergyConsumed message: {JsonConvert.SerializeObject(energyConsumedMessage, Formatting.Indented)}");
//						// 根據需要處理 EnergyConsumed 消息
//					}
//					else
//					{
//						Console.WriteLine("Received an unsupported message type.");
//					}
//				}
//				catch (Exception ex)
//				{
//					Console.WriteLine($"Error processing received message: {ex.Message}");
//				}
//			};

//			// 狀態檢查過程（用於定期發佈消息）
//			Task.Run(async () =>
//			{
//				Dictionary<byte, int> lastStatus = new Dictionary<byte, int>();
//				DateTime lastEnergyPublishTime = DateTime.MinValue; // 記錄上次能源消耗發佈的時間

//				while (true)
//				{
//					try
//					{
//						List<Task> tasks = new List<Task>();

//						// 發佈故障狀態
//						foreach (var stationNumber in slaveData.Keys)
//						{
//							if (slaveData.TryGetValue(stationNumber, out var data) && data.ContainsKey("Fault_WarningCode"))
//							{
//								var currentStatus = data["Fault_WarningCode"];

//								if (lastStatus.ContainsKey(stationNumber) && lastStatus[stationNumber] != currentStatus)
//								{
//									tasks.Add(PublishFaultOccurredMessages(stationNumber, endpoint));
//								}

//								lastStatus[stationNumber] = currentStatus;
//							}
//						}

//						// 每 15 秒發佈一次能源消耗訊息
//						if ((DateTime.Now - lastEnergyPublishTime).TotalSeconds >= 15)
//						{
//							foreach (var stationNumber in slaveData.Keys)
//							{
//								//PublishReadingsRecordedMessages(stationNumber, endpoint);
//								PublishStationParametersModifiedMessages(stationNumber, endpoint);
//								PublishEnergyConsumedMessages(stationNumber, endpoint);
//							}
//							lastEnergyPublishTime = DateTime.Now;
//						}

//						// 等待所有發佈任務完成
//						await Task.WhenAll(tasks);

//						// 延遲 1 秒後再進行下一次檢查
//						await Task.Delay(1000);
//					}
//					catch (Exception ex)
//					{
//						Console.WriteLine($"Error in publish task: {ex.Message}");
//					}
//				}
//			});
//		}


//		//將實際空開狀態轉換成LM上監控的總體空開狀態
//		public static int ConvertPowerSwitchToState(int powerSwitchValue)
//		{
//			// 判斷 powerSwitchValue 並轉換為 1 或 2
//			if (powerSwitchValue == 1) // 假設讀取到的數值為 1 表示合閘（開機）
//			{
//				return 1; // 設備開機
//			}
//			else if (powerSwitchValue == 0) // 假設讀取到的數值為 0 表示分閘（關機）
//			{
//				return 2; // 設備關機
//			}
//			else
//			{
//				Console.WriteLine($"無效的 PowerSwitch 值: {powerSwitchValue}");
//				return -1; // 錯誤處理
//			}
//		}






//		//重要，這邊的是專門處理接收到請求消息後做相應動作的地方
//		private CFXEnvelope OnRequestReceivedHandler(CFXEnvelope request)
//		{
//			try
//			{
//				if (request.MessageBody is ModifyStationParametersRequest modifyRequest)
//				{
//					Console.WriteLine($"收到 ModifyStationParametersRequest: {JsonConvert.SerializeObject(modifyRequest, Formatting.Indented)}");

//					// 解析站號列表
//					var stationNumbers = modifyRequest.NewParameters
//						.Where(p => p is GenericParameter genericParam && genericParam.Name == "站號")
//						.Select(p =>
//						{
//							var genericParam = (GenericParameter)p;
//							if (byte.TryParse(genericParam.Value.ToString(), out var station))
//							{
//								Console.WriteLine($"成功轉換站號為: {station}");
//								return (byte?)station;
//							}
//							else
//							{
//								Console.WriteLine($"站號轉換失敗: {genericParam.Value}");
//								return null;
//							}
//						})
//						.Where(p => p.HasValue)
//						.Select(p => p.Value)
//						.ToList();

//					if (!stationNumbers.Any())
//					{
//						Console.WriteLine("無有效的站號列表，請求無法執行。");
//						return CreateErrorResponse(request.RequestID, "No valid station numbers provided.");
//					}

//					bool hasProcessed = false; // 用於標記是否成功處理任何參數
//					bool hasError = false;     // 用於標記是否出現錯誤

//					foreach (var parameter in modifyRequest.NewParameters)
//					{
//						if (parameter is GenericParameter genericParam)
//						{

//							// 處理保護溫度操作=================================
//							if (genericParam.Name == "TemperatureSV" && ushort.TryParse(genericParam.Value.ToString(), out var temperature))
//							{
//								Console.WriteLine($"正在設定保護溫度為 {temperature}°C 對站號: {string.Join(",", stationNumbers)}");

//								foreach (var station in stationNumbers)
//								{
//									if (!slaveData.ContainsKey(station))
//									{
//										Console.WriteLine($"站號 {station} 不存在，跳過操作。");
//										hasError = true;
//										continue;
//									}

//									try
//									{
//										modbusViewer.ExecuteSetTemperature(station, temperature);
//										Console.WriteLine($"溫度設定成功: 站號 {station}, 溫度 {temperature}");
//										hasProcessed = true;
//									}
//									catch (Exception ex)
//									{
//										Console.WriteLine($"設置溫度操作失敗，站號: {station}，錯誤: {ex.Message}");
//										hasError = true;
//									}
//								}
//							}


//							//250306施工中======================================================
//							else if (genericParam.Name == "EnergyMode" &&
//									 (genericParam.Value.ToString() == "0" ||
//									  genericParam.Value.ToString() == "1" ||
//									  genericParam.Value.ToString() == "2"))
//							{
//								int mode = int.Parse(genericParam.Value.ToString());
//								Console.WriteLine($"正在設定節能模式為 {mode} 對站號: {string.Join(",", stationNumbers)}");

//								foreach (var station in stationNumbers)
//								{
//									if (!slaveData.ContainsKey(station))
//									{
//										Console.WriteLine($"站號 {station} 不存在，跳過操作。");
//										hasError = true;
//										continue;
//									}

//									try
//									{
//										// TODO: 在這裡把對應的 "EnergyMode" 寫入到 slaveData[station] 或做實際的 Modbus 設定
//										slaveData[station]["EnergyMode"] = mode;

//										// 你也可以在此處呼叫對應 Modbus 方法，如：
//										// modbusViewer.ExecuteSetEnergyMode(station, mode);

//										Console.WriteLine($"節能模式設定成功: 站號 {station}, 模式 {mode}");
//										hasProcessed = true;
//									}
//									catch (Exception ex)
//									{
//										Console.WriteLine($"設置節能模式失敗，站號: {station}，錯誤: {ex.Message}");
//										hasError = true;
//									}
//								}
//							}






//							//開關操作=================================
//							else if (genericParam.Name == "PowerSwitch" && (genericParam.Value.ToString() == "1" || genericParam.Value.ToString() == "2"))
//							{
//								bool isSwitchOn = genericParam.Value.ToString() == "2";
//								string action = isSwitchOn ? "合閘" : "分閘";

//								Console.WriteLine($"正在執行開關操作: {action} 對站號: {string.Join(",", stationNumbers)}");

//								foreach (var station in stationNumbers)
//								{
//									if (!slaveData.ContainsKey(station))
//									{
//										Console.WriteLine($"站號 {station} 不存在，跳過操作。");
//										hasError = true;
//										continue;
//									}

//									try
//									{
//										if (isSwitchOn)
//										{
//											modbusViewer.ExecuteSwitchCommand(ModbusHelper.SwitchON, new List<byte> { station });
//										}
//										else
//										{
//											modbusViewer.ExecuteSwitchCommand(ModbusHelper.SwitchOFF, new List<byte> { station });
//										}
//										Console.WriteLine($"開關操作成功: {action}, 站號: {station}");
//										hasProcessed = true;
//									}
//									catch (Exception ex)
//									{
//										Console.WriteLine($"開關操作失敗，站號: {station}，錯誤: {ex.Message}");
//										hasError = true;
//									}
//								}
//							}

							
//							else
//							{
//								//Console.WriteLine($"未知或無效的參數: {genericParam.Name}");
//								//hasError = true;
//							}
//							//250306施工中======================================================
//						}
//					}

//					if (hasError && !hasProcessed)
//					{
//						return CreateErrorResponse(request.RequestID, "No parameters were processed successfully.");
//					}

//					if (!hasProcessed)
//					{
//						return CreateErrorResponse(request.RequestID, "No valid actions were performed.");
//					}

//					// 返回成功響應
//					return CreateSuccessResponse(request.RequestID, "Parameters updated successfully.");
//				}
//				else
//				{
//					Console.WriteLine("Received an unsupported request type.");
//					return CreateErrorResponse(request.RequestID, "Unsupported request type.");
//				}
//			}
//			catch (Exception ex)
//			{
//				Console.WriteLine($"Error processing request: {ex.Message}");
//				return CreateErrorResponse(request.RequestID, ex.Message);
//			}
//		}


//		// 創建成功響應
//		private CFXEnvelope CreateSuccessResponse(string requestId, string message)
//		{
//			var response = new ModifyStationParametersResponse
//			{
//				Result = new RequestResult
//				{
//					Result = StatusResult.Success,
//					ResultCode = 1,
//					Message = message
//				}
//			};

//			var responseEnvelope = CFXEnvelope.FromCFXMessage(response);
//			responseEnvelope.RequestID = requestId;




//			return responseEnvelope;
//		}



//		// 創建錯誤響應
//		private CFXEnvelope CreateErrorResponse(string requestId, string errorMessage)
//		{
//			var response = new NotSupportedResponse
//			{
//				RequestResult = new RequestResult
//				{
//					Result = StatusResult.Failed,
//					ResultCode = 2,
//					Message = errorMessage
//				}
//			};

//			var responseEnvelope = CFXEnvelope.FromCFXMessage(response);
//			responseEnvelope.RequestID = requestId;

//			return responseEnvelope;
//		}











//		//報故障狀態
//		private async Task PublishFaultOccurredMessages(byte stationNumber, AmqpCFXEndpoint endpoint)
//		{
//			if (!slaveData.TryGetValue(stationNumber, out var data) || !data.ContainsKey("Fault_WarningCode"))
//				return;

//			var statusRegister = data["Fault_WarningCode"];

//			//這邊就是去遍歷所有的err code bit pos
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
//							Description = description,
//							//DescriptionTranslations = new Dictionary<string, string>
//							//{
//							//	//{ "zh-TW", description } // 如果需要，添加多語言對應描述
//		     //               },
//							OccurredAt = DateTime.Now,
//							Severity = FaultSeverity.Error
//						}
//					};

//					// 使用 Task.Run 將同步操作包裝成異步執行
//					await Task.Run(() => endpoint.Publish(faultOccurred));
//				}
//			}
//		}


//		//報開關狀態及三相溫度最大值
//		private void PublishStationParametersModifiedMessages(byte stationNumber, AmqpCFXEndpoint endpoint)
//		{
//			// 檢查是否有可用數據
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


//			// 構建報文
//			var parameters = new List<Parameter>{
//				// 開關狀態
//				new GenericParameter
//				{
//					Name = "PowerSwitch",
//					Value = ConvertPowerSwitchToState(data.ContainsKey("PowerSwitch") ? data["PowerSwitch"] : 0).ToString()
//				},

//				//250306施工中=============================================================
//				//保護溫度(TemperatureSV)
//				new GenericParameter
//				{
//					Name = "TemperatureSV",
//					Value = data.ContainsKey("TemperatureSV") ? data["TemperatureSV"].ToString() : "0"
//				},


//				// 節能模式 (EnergyMode)
//				new GenericParameter
//				{
//					Name = "EnergyMode",
//					Value = data.ContainsKey("EnergyMode") ? data["EnergyMode"].ToString() : "0"
//				},
//				//250306施工中=============================================================



//				// 三相溫度
//				new GenericParameter
//				{
//					Name = "PowerTemperatureA",
//					Value = data.ContainsKey("PowerTemperatureA") ? data["PowerTemperatureA"].ToString() : "0"
//				},

//				new GenericParameter
//				{
//					Name = "PowerTemperatureB",
//					Value = data.ContainsKey("PowerTemperatureB") ? data["PowerTemperatureB"].ToString() : "0"
//				},

//				new GenericParameter
//				{
//					Name = "PowerTemperatureC",
//					Value = data.ContainsKey("PowerTemperatureC") ? data["PowerTemperatureC"].ToString() : "0"
//				}
//	};

//			// 發布報文
//			endpoint.Publish(new StationParametersModified { ModifiedParameters = parameters });
//			Console.WriteLine($"Published StationParametersModified message for station {stationNumber}.");
//		}




//		//報使用的電流、功率等等
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

//			// 提取 RYB 電流
//			var currentRYB = new List<double>
//					{
//						data.TryGetValue("CurrentNowRYB_A", out var currentA) ? currentA : 0.0,
//						data.TryGetValue("CurrentNowRYB_B", out var currentB) ? currentB : 0.0,
//						data.TryGetValue("CurrentNowRYB_C", out var currentC) ? currentC : 0.0
//					};

//			// 提取 RYB 功率
//			var powerRYB = new List<double>
//					{
//						data.TryGetValue("PowerNowRYB_A", out var powerA) ? powerA : 0.0,
//						data.TryGetValue("PowerNowRYB_B", out var powerB) ? powerB : 0.0,
//						data.TryGetValue("PowerNowRYB_C", out var powerC) ? powerC : 0.0
//					};

//			// 提取 RYB 電壓
//			var voltageRYB = new List<double>
//					{
//						data.TryGetValue("VoltageNowRYB_A", out var voltageA) ? voltageA : 0.0,
//						data.TryGetValue("VoltageNowRYB_B", out var voltageB) ? voltageB : 0.0,
//						data.TryGetValue("VoltageNowRYB_C", out var voltageC) ? voltageC : 0.0
//					};

//			// 提取總電能
//			var energyUsed = data.TryGetValue("EnergyUsed", out var energy) ? energy : 0.0;

//			// 創建 EnergyConsumed 訊息
//			var energyConsumed = new EnergyConsumed
//			{
//				EnergyUsed = energyUsed,
//				StartTime = DateTime.Now,
//				EndTime = DateTime.Now,
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
