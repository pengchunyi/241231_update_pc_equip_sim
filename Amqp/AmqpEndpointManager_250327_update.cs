
using System;
using System.Collections.Generic;
using System.IO.Ports;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using CFX;
using CFX.Production;
using CFX.ResourcePerformance;
using CFX.Structures;
using CFX.Transport;
using Newtonsoft.Json;

namespace AmqpModbusIntegration
{
	public class AmqpEndpointManager
	{
		private readonly string endpointUri;
		private readonly string publishChannelUri;
		private readonly string subscribeChannelUri;
		private readonly ModbusViewer modbusViewer;
		private readonly SerialPort serialPort;
		private readonly Dictionary<byte, Dictionary<string, object>> slaveData;

		// 供其他方法共用
		private AmqpCFXEndpoint endpoint;

		public static readonly object serialPortLock = new object(); // 用於串口操作的執行緒安全鎖

		private static readonly Dictionary<int, (string Code, string ErrorDescription)> faultDictionary = new Dictionary<int, (string Code, string ErrorDescription)>
		{
			{ 0,  ("EGY_1_WARN1",  "A相過壓") },
			{ 1,  ("EGY_1_WARN2",  "B相過壓") },
			{ 2,  ("EGY_1_WARN3",  "C相過壓") },
			{ 3,  ("EGY_1_WARN4",  "A相欠壓") },
			{ 4,  ("EGY_1_WARN5",  "B相欠壓") },
			{ 5,  ("EGY_1_WARN6",  "C相欠壓") },
			{ 6,  ("EGY_1_WARN7",  "A相過流") },
			{ 7,  ("EGY_1_WARN8",  "B相過流") },
			{ 8,  ("EGY_1_WARN9",  "C相過流") },
			{ 9,  ("EGY_1_WARN10", "漏電異常") },
			{ 10, ("EGY_1_WARN11", "A相出線溫度異常") },
			{ 11, ("EGY_1_WARN12", "B相出線溫度異常") },
			{ 12, ("EGY_1_WARN13", "C相出線溫度異常") },
			{ 13, ("EGY_1_WARN14", "N相出線溫度異常") },
			{ 14, ("EGY_1_WARN15", "電弧") },
			{ 15, ("EGY_1_WARN16", "缺相") },
			{ 16, ("EGY_1_WARN17", "斷零") },
			{ 17, ("EGY_1_WARN18", "三相電壓不平衡") },
			{ 18, ("EGY_1_WARN19", "鎖定") },
			{ 19, ("EGY_1_WARN20", "進入維修或手動模式") },
			{ 20, ("EGY_1_WARN21", "開關狀態異常，提示客戶換設備") },
			{ 21, ("EGY_1_WARN22", "漏電功能壞，提示客戶換設備") },
			{ 22, ("EGY_1_WARN23", "設備離線") },
			{ 23, ("EGY_1_WARN24", "過壓預警") },
			{ 24, ("EGY_1_WARN25", "欠壓預警") },
			{ 25, ("EGY_1_WARN26", "過流預警") },
			{ 26, ("EGY_1_WARN27", "過溫預警") }
		};

		public AmqpEndpointManager(
			string endpointUri,
			string publishChannelUri,
			string subscribeChannelUri,
			ModbusViewer modbusViewer,
			SerialPort serialPort,
			Dictionary<byte, Dictionary<string, object>> slaveData)
		{
			this.endpointUri = endpointUri;
			this.publishChannelUri = publishChannelUri;
			this.subscribeChannelUri = subscribeChannelUri;
			this.modbusViewer = modbusViewer;
			this.serialPort = serialPort;
			this.slaveData = slaveData;
		}

		public void StartAmqpEndpoint(string endpointName)
		{
			if (string.IsNullOrEmpty(endpointName))
				throw new ArgumentException("Endpoint name cannot be null or empty", nameof(endpointName));

			endpoint = new AmqpCFXEndpoint();
			endpoint.Open(endpointName, new Uri(endpointUri));
			endpoint.AddPublishChannel(new Uri(publishChannelUri), "event");

			// 發布連接消息
			endpoint.Publish(new EndpointConnected());
			Console.WriteLine($"AMQP endpoint \"{endpointName}\" connected.");

			// Request/Response 處理邏輯
			endpoint.OnRequestReceived += (req) => OnRequestReceivedHandler(req);

			// 狀態檢查過程（用於定期發佈消息）
			Task.Run(async () =>
			{
				Dictionary<byte, int> lastStatus = new Dictionary<byte, int>();
				DateTime lastEnergyPublishTime = DateTime.MinValue; // 記錄上次能源消耗發佈的時間

				while (true)
				{
					try
					{
						List<Task> tasks = new List<Task>();

						// 發佈故障狀態
						foreach (var stationNumber in slaveData.Keys)
						{
							if (slaveData[stationNumber].TryGetValue("Fault_WarningCode", out var faultObj))
							{
								// 將 object 轉為 int
								int currentStatus = Convert.ToInt32(faultObj);

								// 若該站已有紀錄且狀態改變，就發布故障狀態
								if (lastStatus.ContainsKey(stationNumber) && lastStatus[stationNumber] != currentStatus)
								{
									tasks.Add(PublishFaultOccurredMessages(stationNumber, endpoint));
								}
								lastStatus[stationNumber] = currentStatus;
							}
						}

						// 每 60 秒(1mins)定期發佈一次空開相關訊息
						if ((DateTime.Now - lastEnergyPublishTime).TotalSeconds >= 60)
						{
							foreach (var stationNumber in slaveData.Keys)
							{
								PublishStationParametersModifiedMessages(stationNumber, endpoint);
								PublishEnergyConsumedMessages(stationNumber, endpoint);
							}
							lastEnergyPublishTime = DateTime.Now;
						}

						// 等待所有發佈任務完成
						await Task.WhenAll(tasks);

						// 延遲 1 秒後再進行下一次檢查
						await Task.Delay(1000);
					}
					catch (Exception ex)
					{
						Console.WriteLine($"Error in publish task: {ex.Message}");
					}
				}
			});
		}

		private CFXEnvelope OnRequestReceivedHandler(CFXEnvelope request)
		{
			try
			{
				if (request.MessageBody is ModifyStationParametersRequest modifyRequest)
				{
					Console.WriteLine($"收到 ModifyStationParametersRequest: {JsonConvert.SerializeObject(modifyRequest, Formatting.Indented)}");

					// 解析站號列表
					var stationNumbers = modifyRequest.NewParameters
						.OfType<GenericParameter>()
						.Where(g => g.Name == "站號")
						.Select(g =>
						{
							if (byte.TryParse(g.Value.ToString(), out var stn))
							{
								Console.WriteLine($"成功轉換站號為: {stn}");
								return (byte?)stn;
							}
							Console.WriteLine($"站號轉換失敗: {g.Value}");
							return (byte?)null;
						})
						.Where(p => p.HasValue)
						.Select(p => p.Value)
						.ToList();

					if (!stationNumbers.Any())
					{
						Console.WriteLine("無有效的站號列表，請求無法執行。");
						return CreateErrorResponse(request.RequestID, "No valid station numbers provided.");
					}

					bool hasProcessed = false;
					bool hasError = false;

					// 檢查是否需要發送 StationStateChanged
					bool shouldPublishStateChanged = false;

					// 逐一解析參數
					foreach (var param in modifyRequest.NewParameters.OfType<GenericParameter>())
					{
						switch (param.Name)
						{
							case "TemperatureSV":
								if (ushort.TryParse(param.Value.ToString(), out var tempVal))
								{
									foreach (var stn in stationNumbers)
									{
										if (!slaveData.ContainsKey(stn))
										{
											hasError = true;
											continue;
										}

										try
										{
											modbusViewer.ExecuteSetTemperature(stn, tempVal);
											Console.WriteLine($"溫度設定成功: 站號 {stn}, 溫度 {tempVal}");
											hasProcessed = true;
										}
										catch (Exception ex)
										{
											hasError = true;
											Console.WriteLine($"設置溫度操作失敗，站號: {stn}，錯誤: {ex.Message}");
										}
									}
								}
								break;

							case "EnergyMode":
								// 只要是 0/1/2 才是有效數值
								if (param.Value.ToString() == "0" ||
									param.Value.ToString() == "1" ||
									param.Value.ToString() == "2")
								{
									int mode = int.Parse(param.Value.ToString());
									foreach (var stn in stationNumbers)
									{
										if (!slaveData.ContainsKey(stn))
										{
											hasError = true;
											continue;
										}

										try
										{
											slaveData[stn]["EnergyMode"] = mode;
											Console.WriteLine($"節能模式設定成功: 站號 {stn}, 模式 {mode}");
											hasProcessed = true;

											// 如果 mode != 0 則需要發布 StationStateChanged
											if (mode != 0)
												shouldPublishStateChanged = true;
										}
										catch (Exception ex)
										{
											hasError = true;
											Console.WriteLine($"設置節能模式失敗，站號: {stn}，錯誤: {ex.Message}");
										}
									}
								}
								break;

							case "PowerSwitch":
								// 1=關機,2=開機
								if (param.Value.ToString() == "1" || param.Value.ToString() == "2")
								{
									bool isSwitchOn = param.Value.ToString() == "2";
									foreach (var stn in stationNumbers)
									{
										if (!slaveData.ContainsKey(stn))
										{
											hasError = true;
											continue;
										}

										try
										{
											if (isSwitchOn)
											{
												modbusViewer.ExecuteSwitchCommand(ModbusHelper.SwitchON, new List<byte> { stn });
												Console.WriteLine($"開關操作成功: 合閘, 站號: {stn}");
												// 開機 => 發布狀態改變
												shouldPublishStateChanged = true;
											}
											else
											{
												modbusViewer.ExecuteSwitchCommand(ModbusHelper.SwitchON, new List<byte> { stn });
												Console.WriteLine($"開關操作成功: 分閘, 站號: {stn}");
											}
											hasProcessed = true;
										}
										catch (Exception ex)
										{
											hasError = true;
											Console.WriteLine($"開關操作失敗，站號: {stn}，錯誤: {ex.Message}");
										}
									}
								}
								break;

							default:
								// 其他未知參數
								break;
						}
					}

					if (hasError && !hasProcessed)
					{
						return CreateErrorResponse(request.RequestID, "No parameters were processed successfully.");
					}

					if (!hasProcessed)
					{
						return CreateErrorResponse(request.RequestID, "No valid actions were performed.");
					}

					// 產生成功回應
					var successEnvelope = CreateSuccessResponse(request.RequestID, "Parameters updated successfully.");

					// 如果需要，發送 StationStateChanged
					if (shouldPublishStateChanged)
					{
						var stateChangedMessage = new StationStateChanged
						{
							// 假設無法確定舊狀態 => 預設 ResourceState.NST
							NewState = ResourceState.NST_ShutdownAndStartup, // 6500
						};

						endpoint.Publish(stateChangedMessage);
						Console.WriteLine("Published StationStateChanged with state 6500.");
					}

					return successEnvelope;
				}
				else
				{
					Console.WriteLine("Received an unsupported request type.");
					return CreateErrorResponse(request.RequestID, "Unsupported request type.");
				}
			}
			catch (Exception ex)
			{
				Console.WriteLine($"Error processing request: {ex.Message}");
				return CreateErrorResponse(request.RequestID, ex.Message);
			}
		}

		private CFXEnvelope CreateSuccessResponse(string requestId, string message)
		{
			var response = new ModifyStationParametersResponse
			{
				Result = new RequestResult
				{
					Result = StatusResult.Success,
					ResultCode = 1,
					Message = message
				}
			};

			var responseEnvelope = CFXEnvelope.FromCFXMessage(response);
			responseEnvelope.RequestID = requestId;

			return responseEnvelope;
		}

		private CFXEnvelope CreateErrorResponse(string requestId, string errorMessage)
		{
			var response = new NotSupportedResponse
			{
				RequestResult = new RequestResult
				{
					Result = StatusResult.Failed,
					ResultCode = 2,
					Message = errorMessage
				}
			};

			var responseEnvelope = CFXEnvelope.FromCFXMessage(response);
			responseEnvelope.RequestID = requestId;

			return responseEnvelope;
		}

		private async Task PublishFaultOccurredMessages(byte stationNumber, AmqpCFXEndpoint endpoint)
		{
			if (!slaveData.TryGetValue(stationNumber, out var data) || !data.ContainsKey("Fault_WarningCode"))
				return;

			// 轉為 int, 以便位運算
			int statusRegister = Convert.ToInt32(data["Fault_WarningCode"]);

			for (int bitPosition = 0; bitPosition <= 27; bitPosition++)
			{
				if (((statusRegister) & (1 << bitPosition)) != 0 && faultDictionary.ContainsKey(bitPosition))
				{
					var (faultCode, description) = faultDictionary[bitPosition];
					var faultOccurred = new FaultOccurred
					{
						Fault = new Fault
						{
							FaultCode = faultCode,
							FaultOccurrenceId = Guid.NewGuid(),
							Description = description,
							OccurredAt = DateTime.Now,
							Severity = FaultSeverity.Error
						}
					};
					await Task.Run(() => endpoint.Publish(faultOccurred));
				}
			}
		}

		private void PublishStationParametersModifiedMessages(byte stationNumber, AmqpCFXEndpoint endpoint)
		{
			if (!slaveData.TryGetValue(stationNumber, out var data) || data == null || !data.Any())
			{
				Console.WriteLine($"No data available for station {stationNumber}.");
				return;
			}

			// 檢查：僅在有非零數值時才發送
			// => 需嘗試將object轉為 double 再檢查
			bool anyNonZero = data.Values.Any(obj =>
			{
				double val;
				if (obj == null) return false;
				// 盡量轉成 double
				if (obj is int i) val = i;
				else if (obj is double d) val = d;
				else
				{
					// 其餘型別暫時視為0
					return false;
				}
				return Math.Abs(val) > 0.000001;
			});

			if (!anyNonZero)
			{
				Console.WriteLine($"All data for station {stationNumber} is default (zero), skipping.");
				return;
			}

			// 構建報文
			var parameters = new List<Parameter>
			{
				new GenericParameter
				{
					Name = "PowerSwitch",
					Value = ConvertPowerSwitchToState(data.ContainsKey("PowerSwitch") ? Convert.ToInt32(data["PowerSwitch"]) : 0).ToString()
				},
				new GenericParameter
				{
					Name = "TemperatureSV",
					Value = data.ContainsKey("TemperatureSV") ? data["TemperatureSV"].ToString() : "0"
				},
				new GenericParameter
				{
					Name = "EnergyMode",
					Value = data.ContainsKey("EnergyMode") ? data["EnergyMode"].ToString() : "0"
				},
				new GenericParameter
				{
					Name = "PowerTemperatureA",
					Value = data.ContainsKey("PowerTemperatureA") ? data["PowerTemperatureA"].ToString() : "0"
				},
				new GenericParameter
				{
					Name = "PowerTemperatureB",
					Value = data.ContainsKey("PowerTemperatureB") ? data["PowerTemperatureB"].ToString() : "0"
				},
				new GenericParameter
				{
					Name = "PowerTemperatureC",
					Value = data.ContainsKey("PowerTemperatureC") ? data["PowerTemperatureC"].ToString() : "0"
				}
			};

			endpoint.Publish(new StationParametersModified { ModifiedParameters = parameters });
			Console.WriteLine($"Published StationParametersModified message for station {stationNumber}.");
		}

		private void PublishEnergyConsumedMessages(byte stationNumber, AmqpCFXEndpoint endpoint)
		{
			if (!slaveData.TryGetValue(stationNumber, out var data) || data == null || !data.Any())
			{
				Console.WriteLine($"No data available for station {stationNumber}.");
				return;
			}

			// 同樣檢查非零
			bool anyNonZero = data.Values.Any(obj =>
			{
				if (obj == null) return false;
				double val = 0;
				if (obj is int i) val = i;
				else if (obj is double d) val = d;
				else return false;
				return Math.Abs(val) > 0.000001;
			});
			if (!anyNonZero)
			{
				Console.WriteLine($"All data for station {stationNumber} is default (zero), skipping.");
				return;
			}

			// 提取 RYB 電流
			double currentA = ConvertToDouble(data, "CurrentNowRYB_A");
			double currentB = ConvertToDouble(data, "CurrentNowRYB_B");
			double currentC = ConvertToDouble(data, "CurrentNowRYB_C");

			var currentRYB = new List<double> { currentA, currentB, currentC };

			// 提取 RYB 功率
			double powerA = ConvertToDouble(data, "PowerNowRYB_A");
			double powerB = ConvertToDouble(data, "PowerNowRYB_B");
			double powerC = ConvertToDouble(data, "PowerNowRYB_C");

			var powerRYB = new List<double> { powerA, powerB, powerC };

			// 提取 RYB 電壓
			double voltageA = ConvertToDouble(data, "VoltageNowRYB_A");
			double voltageB = ConvertToDouble(data, "VoltageNowRYB_B");
			double voltageC = ConvertToDouble(data, "VoltageNowRYB_C");

			var voltageRYB = new List<double> { voltageA, voltageB, voltageC };

			// 提取總電能
			double energyUsed = ConvertToDouble(data, "EnergyUsed");

			var energyConsumed = new EnergyConsumed
			{
				EnergyUsed = energyUsed,
				StartTime = DateTime.Now,
				EndTime = DateTime.Now,
				CurrentNowRYB = currentRYB,
				PowerNowRYB = powerRYB,
				VoltageNowRYB = voltageRYB
			};

			endpoint.Publish(energyConsumed);
			Console.WriteLine($"Published EnergyConsumed message for station {stationNumber}.");
		}

		// 轉換 object -> double (若無法轉換就回傳 0)
		private double ConvertToDouble(Dictionary<string, object> data, string key)
		{
			if (!data.ContainsKey(key)) return 0.0;

			var obj = data[key];
			if (obj is null) return 0.0;
			if (obj is double dd) return dd;
			if (obj is int ii) return ii;
			// 其他嘗試
			double result;
			return double.TryParse(obj.ToString(), out result) ? result : 0.0;
		}

		// 將實際空開狀態轉換成LM上監控的總體空開狀態
		public static int ConvertPowerSwitchToState(int powerSwitchValue)
		{
			if (powerSwitchValue == 1)
			{
				return 1; // 設備開機
			}
			else if (powerSwitchValue == 0)
			{
				return 2; // 設備關機
			}
			else
			{
				Console.WriteLine($"無效的 PowerSwitch 值: {powerSwitchValue}");
				return -1; // 錯誤處理
			}
		}
	}
}