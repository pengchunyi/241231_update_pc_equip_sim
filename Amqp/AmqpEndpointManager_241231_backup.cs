using System;
using System.Collections.Generic;
using System.IO.Ports;
using System.Linq;
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
		private readonly Dictionary<byte, Dictionary<string, int>> slaveData;

		public static readonly object serialPortLock = new object(); // 用於串口操作的執行緒安全鎖



		//對應的故障碼
		private static readonly Dictionary<int, (string FaultCode, string Description)> faultDictionary = new Dictionary<int, (string FaultCode, string Description)>
				{
					{ 0, ("A_OVERVOLTAGE", "A相過壓") },
					{ 1, ("B_OVERVOLTAGE", "B相過壓") },
					{ 2, ("C_OVERVOLTAGE", "C相過壓") },
					{ 3, ("A_UNDERVOLTAGE", "A相欠壓") },
					{ 4, ("B_UNDERVOLTAGE", "B相欠壓") },
					{ 5, ("C_UNDERVOLTAGE", "C相欠壓") },
					{ 6, ("A_OVERCURRENT", "A相過流") },
					{ 7, ("B_OVERCURRENT", "B相過流") },
					{ 8, ("C_OVERCURRENT", "C相過流") },
					{ 9, ("LEAKAGE_FAULT", "漏電異常") },
					{ 10, ("A_OVERHEAT", "A相出線溫度異常") },
					{ 11, ("B_OVERHEAT", "B相出線溫度異常") },
					{ 12, ("C_OVERHEAT", "C相出線溫度異常") },
					{ 13, ("N_OVERHEAT", "N相出線溫度異常") },
					{ 14, ("SHORT_CIRCUIT", "短路") },
					{ 15, ("ARC_FAULT", "電弧") },
					{ 16, ("PHASE_LOSS", "缺相") },
					{ 17, ("NEUTRAL_DISCONNECT", "斷零") },
					{ 18, ("VOLTAGE_IMBALANCE", "三相電壓不平衡") },
					{ 19, ("LOCKOUT", "鎖定") },
					{ 20, ("MAINTENANCE_MODE", "進入維修或手動模式") },
					{ 21, ("SWITCH_FAULT", "開關狀態異常，提示客戶換設備") },
					{ 22, ("LEAKAGE_FAILURE", "漏電功能壞，提示客戶換設備") },
					{ 23, ("DEVICE_OFFLINE", "設備離線") },
					{ 24, ("OVERVOLTAGE_WARNING", "過壓預警") },
					{ 25, ("UNDERVOLTAGE_WARNING", "欠壓預警") },
					{ 26, ("OVERCURRENT_WARNING", "過流預警") },
					{ 27, ("OVERHEAT_WARNING", "過溫預警") }
				};


		public AmqpEndpointManager(
			string endpointUri,
			string publishChannelUri,
			string subscribeChannelUri,
			ModbusViewer modbusViewer,// 傳遞 ModbusViewer 實例
			SerialPort serialPort,
			Dictionary<byte, Dictionary<string, int>> slaveData)
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

			var endpoint = new AmqpCFXEndpoint();
			endpoint.Open(endpointName, new Uri(endpointUri));
			endpoint.AddPublishChannel(new Uri(publishChannelUri), "event");

			// 發布連接消息
			endpoint.Publish(new EndpointConnected());
			Console.WriteLine($"AMQP endpoint \"{endpointName}\" connected.");

			// Request/Response 處理邏輯
			endpoint.OnRequestReceived += (request) =>
			{
				return OnRequestReceivedHandler(request);
			};

			// Publish 模式處理非請求消息
			endpoint.OnCFXMessageReceived += async (sender, e) =>
			{
				try
				{
					// 提取消息類型
					if (e.GetMessage<FaultOccurred>() is FaultOccurred faultOccurredMessage)
					{
						Console.WriteLine($"Received FaultOccurred message: {JsonConvert.SerializeObject(faultOccurredMessage, Formatting.Indented)}");
						// 根據需要處理 FaultOccurred 消息
					}
					else if (e.GetMessage<EnergyConsumed>() is EnergyConsumed energyConsumedMessage)
					{
						Console.WriteLine($"Received EnergyConsumed message: {JsonConvert.SerializeObject(energyConsumedMessage, Formatting.Indented)}");
						// 根據需要處理 EnergyConsumed 消息
					}
					else
					{
						Console.WriteLine("Received an unsupported message type.");
					}
				}
				catch (Exception ex)
				{
					Console.WriteLine($"Error processing received message: {ex.Message}");
				}
			};

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
							if (slaveData.TryGetValue(stationNumber, out var data) && data.ContainsKey("當前狀態"))
							{
								var currentStatus = data["當前狀態"];

								if (lastStatus.ContainsKey(stationNumber) && lastStatus[stationNumber] != currentStatus)
								{
									tasks.Add(PublishFaultOccurredMessages(stationNumber, endpoint));
								}

								lastStatus[stationNumber] = currentStatus;
							}
						}

						// 每 6 秒發佈一次能源消耗訊息
						if ((DateTime.Now - lastEnergyPublishTime).TotalSeconds >= 6)
						{
							foreach (var stationNumber in slaveData.Keys)
							{
								PublishReadingsRecordedMessages(stationNumber, endpoint);
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



		//20241219新增=====================================================
		//private CFXEnvelope OnRequestReceivedHandler(CFXEnvelope request)
		//{
		//	try
		//	{
		//		if (request.MessageBody is ModifyStationParametersRequest modifyRequest)
		//		{
		//			Console.WriteLine($"收到 ModifyStationParametersRequest: {JsonConvert.SerializeObject(modifyRequest, Formatting.Indented)}");

		//			if (modifyRequest.NewParameters != null && modifyRequest.NewParameters.Any())
		//			{
		//				foreach (var parameter in modifyRequest.NewParameters)
		//				{
		//					if (parameter is GenericParameter genericParam)
		//					{
		//						if (genericParam.Name == "保護溫度")
		//						{
		//							if (ushort.TryParse(genericParam.Value.ToString(), out var temperature))
		//							{
		//								Console.WriteLine($"正在設定保護溫度: {temperature}°C");

		//								// 使用 UI 執行緒執行溫度設置
		//								modbusViewer.Invoke(new Action(() =>
		//								{
		//									modbusViewer.SetTemperatureTextboxValue(temperature.ToString());
		//									modbusViewer.ExecuteSetTemperature();
		//								}));
		//							}
		//							else
		//							{
		//								Console.WriteLine($"無效的溫度值: {genericParam.Value}");
		//								return CreateErrorResponse(request.RequestID, $"Invalid temperature value: {genericParam.Value}");
		//							}
		//						}
		//						else if (genericParam.Name == "開關操作")
		//						{
		//							if (genericParam.Value.ToString() == "開" || genericParam.Value.ToString() == "關")
		//							{
		//								bool isSwitchOn = genericParam.Value.ToString() == "開";
		//								Console.WriteLine($"正在執行開關操作: {(isSwitchOn ? "合閘" : "分閘")}");

		//								// 呼叫開關操作
		//								byte stationNumber = 1; // 替換為實際站號
		//								Task.Run(() =>
		//								{
		//									if (serialPort != null && serialPort.IsOpen)
		//									{
		//										if (isSwitchOn)
		//										{
		//											ModbusHelper.SwitchON(serialPort, stationNumber);
		//										}
		//										else
		//										{
		//											ModbusHelper.SwitchOFF(serialPort, stationNumber);
		//										}
		//									}
		//									else
		//									{
		//										Console.WriteLine("串口未打開，無法執行開關操作");
		//									}
		//								});
		//							}
		//							else
		//							{
		//								Console.WriteLine($"無效的開關操作值: {genericParam.Value}");
		//								return CreateErrorResponse(request.RequestID, $"Invalid switch operation value: {genericParam.Value}");
		//							}
		//						}
		//						else
		//						{
		//							Console.WriteLine($"未知的參數名稱或類型: {parameter.GetType().Name}");
		//							return CreateErrorResponse(request.RequestID, $"Unknown parameter name or type: {parameter.GetType().Name}");
		//						}
		//					}
		//				}
		//			}
		//			else
		//			{
		//				return CreateErrorResponse(request.RequestID, "No parameters provided.");
		//			}

		//			var response = new ModifyStationParametersResponse
		//			{
		//				Result = new RequestResult
		//				{
		//					Result = StatusResult.Success,
		//					ResultCode = 0,
		//					Message = "Parameters updated successfully."
		//				}
		//			};

		//			var responseEnvelope = CFXEnvelope.FromCFXMessage(response);
		//			responseEnvelope.RequestID = request.RequestID;

		//			return responseEnvelope;
		//		}
		//		else
		//		{
		//			Console.WriteLine("Received an unsupported request type.");
		//			return CreateErrorResponse(request.RequestID, "Unsupported request type.");
		//		}
		//	}
		//	catch (Exception ex)
		//	{
		//		Console.WriteLine($"Error processing request: {ex.Message}");
		//		return CreateErrorResponse(request.RequestID, ex.Message);
		//	}
		//}


		//private CFXEnvelope OnRequestReceivedHandler(CFXEnvelope request)
		//{
		//	try
		//	{
		//		if (request.MessageBody is ModifyStationParametersRequest modifyRequest)
		//		{
		//			Console.WriteLine($"收到 ModifyStationParametersRequest: {JsonConvert.SerializeObject(modifyRequest, Formatting.Indented)}");

		//			if (modifyRequest.NewParameters != null && modifyRequest.NewParameters.Any())
		//			{
		//				foreach (var parameter in modifyRequest.NewParameters)
		//				{
		//					if (parameter is GenericParameter genericParam)
		//					{
		//						if (genericParam.Name == "保護溫度")
		//						{
		//							if (ushort.TryParse(genericParam.Value.ToString(), out var temperature))
		//							{
		//								Console.WriteLine($"正在設定保護溫度: {temperature}°C");

		//								// 使用 UI 執行緒執行溫度設置
		//								modbusViewer.Invoke(new Action(() =>
		//								{
		//									modbusViewer.SetTemperatureTextboxValue(temperature.ToString());
		//									modbusViewer.ExecuteSetTemperature();
		//								}));
		//							}
		//							else
		//							{
		//								Console.WriteLine($"無效的溫度值: {genericParam.Value}");
		//								return CreateErrorResponse(request.RequestID, $"Invalid temperature value: {genericParam.Value}");
		//							}
		//						}
		//						else if (genericParam.Name == "開關操作")
		//						{
		//							if (genericParam.Value.ToString() == "開" || genericParam.Value.ToString() == "關")
		//							{
		//								bool isSwitchOn = genericParam.Value.ToString() == "開";
		//								Console.WriteLine($"正在執行開關操作: {(isSwitchOn ? "合閘" : "分閘")}");

		//								//// 呼叫 ExecuteSwitchCommand
		//								modbusViewer.Invoke(new Action(() =>
		//								{
		//									if (isSwitchOn)
		//									{
		//										modbusViewer.ExecuteSwitchCommand(ModbusHelper.SwitchON);
		//									}
		//									else
		//									{
		//										modbusViewer.ExecuteSwitchCommand(ModbusHelper.SwitchOFF);
		//									}
		//								}));


		//							}
		//							else
		//							{
		//								Console.WriteLine($"無效的開關操作值: {genericParam.Value}");
		//								return CreateErrorResponse(request.RequestID, $"Invalid switch operation value: {genericParam.Value}");
		//							}
		//						}
		//						else
		//						{
		//							Console.WriteLine($"未知的參數名稱或類型: {parameter.GetType().Name}");
		//							return CreateErrorResponse(request.RequestID, $"Unknown parameter name or type: {parameter.GetType().Name}");
		//						}
		//					}
		//				}
		//			}
		//			else
		//			{
		//				return CreateErrorResponse(request.RequestID, "No parameters provided.");
		//			}

		//			var response = new ModifyStationParametersResponse
		//			{
		//				Result = new RequestResult
		//				{
		//					Result = StatusResult.Success,
		//					ResultCode = 0,
		//					Message = "Parameters updated successfully."
		//				}
		//			};

		//			var responseEnvelope = CFXEnvelope.FromCFXMessage(response);
		//			responseEnvelope.RequestID = request.RequestID;

		//			return responseEnvelope;
		//		}
		//		else
		//		{
		//			Console.WriteLine("Received an unsupported request type.");
		//			return CreateErrorResponse(request.RequestID, "Unsupported request type.");
		//		}
		//	}
		//	catch (Exception ex)
		//	{
		//		Console.WriteLine($"Error processing request: {ex.Message}");
		//		return CreateErrorResponse(request.RequestID, ex.Message);
		//	}
		//}

		//private CFXEnvelope OnRequestReceivedHandler(CFXEnvelope request)
		//{
		//	try
		//	{
		//		if (request.MessageBody is ModifyStationParametersRequest modifyRequest)
		//		{
		//			Console.WriteLine($"收到 ModifyStationParametersRequest: {JsonConvert.SerializeObject(modifyRequest, Formatting.Indented)}");

		//			// 解析站號列表
		//			var stationNumbers = modifyRequest.NewParameters
		//				.Where(p => p.Name == "站號")
		//				.Select(p => byte.TryParse(p.Value.ToString(), out var station) ? station : (byte?)null)
		//				.Where(p => p.HasValue)
		//				.Select(p => p.Value)
		//				.ToList();

		//			if (!stationNumbers.Any())
		//			{
		//				Console.WriteLine("無有效的站號列表，請求無法執行。");
		//				return CreateErrorResponse(request.RequestID, "No valid station numbers provided.");
		//			}

		//			foreach (var parameter in modifyRequest.NewParameters)
		//			{
		//				if (parameter is GenericParameter genericParam)
		//				{
		//					// 處理保護溫度
		//					if (genericParam.Name == "保護溫度" && ushort.TryParse(genericParam.Value.ToString(), out var temperature))
		//					{
		//						Console.WriteLine($"正在設定保護溫度為 {temperature}°C 對站號: {string.Join(",", stationNumbers)}");

		//						foreach (var station in stationNumbers)
		//						{
		//							modbusViewer.Invoke(new Action(() =>
		//							{
		//								modbusViewer.ExecuteSetTemperature(station, temperature);
		//							}));
		//						}
		//					}
		//					// 處理開關操作
		//					else if (genericParam.Name == "開關操作" && (genericParam.Value.ToString() == "開" || genericParam.Value.ToString() == "關"))
		//					{
		//						bool isSwitchOn = genericParam.Value.ToString() == "開";
		//						string action = isSwitchOn ? "合閘" : "分閘";

		//						Console.WriteLine($"正在執行開關操作: {action} 對站號: {string.Join(",", stationNumbers)}");

		//						modbusViewer.Invoke(new Action(() =>
		//						{
		//							modbusViewer.ExecuteSwitchCommand(isSwitchOn ? ModbusHelper.SwitchON : ModbusHelper.SwitchOFF, stationNumbers);
		//						}));
		//					}
		//					else
		//					{
		//						Console.WriteLine($"未知或無效的參數: {genericParam.Name}");
		//						return CreateErrorResponse(request.RequestID, $"Unknown or invalid parameter: {genericParam.Name}");
		//					}
		//				}
		//			}

		//			// 成功回應
		//			var response = new ModifyStationParametersResponse
		//			{
		//				Result = new RequestResult
		//				{
		//					Result = StatusResult.Success,
		//					ResultCode = 0,
		//					Message = "Parameters updated successfully."
		//				}
		//			};

		//			var responseEnvelope = CFXEnvelope.FromCFXMessage(response);
		//			responseEnvelope.RequestID = request.RequestID;

		//			return responseEnvelope;
		//		}
		//		else
		//		{
		//			Console.WriteLine("Received an unsupported request type.");
		//			return CreateErrorResponse(request.RequestID, "Unsupported request type.");
		//		}
		//	}
		//	catch (Exception ex)
		//	{
		//		Console.WriteLine($"Error processing request: {ex.Message}");
		//		return CreateErrorResponse(request.RequestID, ex.Message);
		//	}
		//}


		//重要，這邊的是專門處理接收到請求消息後做相應動作的地方
		private CFXEnvelope OnRequestReceivedHandler(CFXEnvelope request)
		{
			try
			{
				if (request.MessageBody is ModifyStationParametersRequest modifyRequest)
				{
					Console.WriteLine($"收到 ModifyStationParametersRequest: {JsonConvert.SerializeObject(modifyRequest, Formatting.Indented)}");

					// 解析站號列表
					var stationNumbers = modifyRequest.NewParameters
						.Where(p => p is GenericParameter genericParam && genericParam.Name == "站號")
						.Select(p => byte.TryParse(((GenericParameter)p).Value.ToString(), out var station) ? station : (byte?)null)
						.Where(p => p.HasValue)
						.Select(p => p.Value)
						.ToList();

					if (!stationNumbers.Any())
					{
						Console.WriteLine("無有效的站號列表，請求無法執行。");
						return CreateErrorResponse(request.RequestID, "No valid station numbers provided.");
					}

					foreach (var parameter in modifyRequest.NewParameters)
					{
						if (parameter is GenericParameter genericParam)
						{
							// 處理接收的保護溫度=========================================
							//if (genericParam.Name == "保護溫度" && ushort.TryParse(genericParam.Value.ToString(), out var temperature))
							//{
							//	Console.WriteLine($"正在設定保護溫度為 {temperature}°C 對站號: {string.Join(",", stationNumbers)}");

							//	foreach (var station in stationNumbers)
							//	{
							//		modbusViewer.Invoke(new Action(() =>
							//		{
							//			modbusViewer.ExecuteSetTemperature(station, temperature);
							//		}));
							//	}
							//}


							if (genericParam.Name == "保護溫度" && ushort.TryParse(genericParam.Value.ToString(), out var temperature))
							{
								Console.WriteLine($"正在設定保護溫度為 {temperature}°C 對站號: {string.Join(",", stationNumbers)}");

								foreach (var station in stationNumbers)
								{
									if (!slaveData.ContainsKey(station))
									{
										Console.WriteLine($"站號 {station} 不存在，跳過操作。");
										continue;
									}

									Task.Run(() =>
									{
										try
										{
											modbusViewer.ExecuteSetTemperature(station, temperature);
										}
										catch (Exception ex)
										{
											Console.WriteLine($"設置溫度操作失敗，站號: {station}，錯誤: {ex.Message}");
										}
									});

								}
							}

							// 處理接收的開關操作============================================
							else if (genericParam.Name == "開關操作" && (genericParam.Value.ToString() == "開" || genericParam.Value.ToString() == "關"))
							{
								bool isSwitchOn = genericParam.Value.ToString() == "開";
								string action = isSwitchOn ? "合閘" : "分閘";

								Console.WriteLine($"正在執行開關操作: {action} 對站號: {string.Join(",", stationNumbers)}");

								//								//// 呼叫 ExecuteSwitchCommand
								modbusViewer.Invoke(new Action(() =>
								{
									if (isSwitchOn)
									{
										modbusViewer.ExecuteSwitchCommand(ModbusHelper.SwitchON);
									}
									else
									{
										modbusViewer.ExecuteSwitchCommand(ModbusHelper.SwitchOFF);
									}
								}));
							}
							else
							{
								Console.WriteLine($"未知或無效的參數: {genericParam.Name}");
								return CreateErrorResponse(request.RequestID, $"Unknown or invalid parameter: {genericParam.Name}");
							}
						}
					}

					// 成功回應
					var response = new ModifyStationParametersResponse
					{
						Result = new RequestResult
						{
							Result = StatusResult.Success,
							ResultCode = 0,
							Message = "Parameters updated successfully."
						}
					};

					var responseEnvelope = CFXEnvelope.FromCFXMessage(response);
					responseEnvelope.RequestID = request.RequestID;

					return responseEnvelope;
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




		//20241219新增=====================================================



		// 創建錯誤回應
		private CFXEnvelope CreateErrorResponse(string requestId, string errorMessage)
		{
			var errorResponse = new NotSupportedResponse
			{
				RequestResult = new RequestResult
				{
					Result = StatusResult.Failed,
					ResultCode = -1,
					Message = errorMessage
				}
			};

			var responseEnvelope = CFXEnvelope.FromCFXMessage(errorResponse);
			responseEnvelope.RequestID = requestId;

			return responseEnvelope;
		}






		//報故障狀態
		private async Task PublishFaultOccurredMessages(byte stationNumber, AmqpCFXEndpoint endpoint)
		{
			if (!slaveData.TryGetValue(stationNumber, out var data) || !data.ContainsKey("當前狀態"))
				return;

			var statusRegister = data["當前狀態"];
			for (int bitPosition = 0; bitPosition <= 27; bitPosition++)
			{
				if ((statusRegister & (1 << bitPosition)) != 0 && faultDictionary.ContainsKey(bitPosition))
				{
					var (faultCode, description) = faultDictionary[bitPosition];
					var faultOccurred = new FaultOccurred
					{
						Fault = new Fault
						{
							FaultCode = faultCode,
							FaultOccurrenceId = Guid.NewGuid(),
							Description = faultCode,
							DescriptionTranslations = new Dictionary<string, string>
							{
								{ "zh-TW", description } // 如果需要，添加多語言對應描述
		                    },
							OccurredAt = DateTime.UtcNow,
							Severity = FaultSeverity.Error
						}
					};

					// 使用 Task.Run 將同步操作包裝成異步執行
					await Task.Run(() => endpoint.Publish(faultOccurred));
				}
			}
		}

		//報三項溫度
		private void PublishReadingsRecordedMessages(byte stationNumber, AmqpCFXEndpoint endpoint)
		{
			//if (!slaveData.TryGetValue(stationNumber, out var data))
			//	return;
			//20241204新增====================================================
			if (!slaveData.TryGetValue(stationNumber, out var data) || data == null || !data.Any())
			{
				Console.WriteLine($"No data available for station {stationNumber}.");
				return;
			}

			// 確保至少有一個非零數據值
			if (!data.Values.Any(value => value != 0))
			{
				Console.WriteLine($"All data for station {stationNumber} is default (zero), skipping.");
				return;
			}

			var readings = new List<Reading>
					{
						new Reading
						{
							Value = data.ContainsKey("A相溫度 (℃)") ? data["A相溫度 (℃)"].ToString() : "0",
							ReadingIdentifier = "A相溫度",
							ValueUnits = "℃",
							TimeRecorded = DateTime.UtcNow
						},
						new Reading
						{
							Value = data.ContainsKey("B相溫度 (℃)") ? data["B相溫度 (℃)"].ToString() : "0",
							ReadingIdentifier = "B相溫度",
							ValueUnits = "℃",
							TimeRecorded = DateTime.UtcNow
						},
						new Reading
						{
							Value = data.ContainsKey("C相溫度 (℃)") ? data["C相溫度 (℃)"].ToString() : "0",
							ReadingIdentifier = "C相溫度",
							ValueUnits = "℃",
							TimeRecorded = DateTime.UtcNow
						}
					};


			var readingsRecorded = new ReadingsRecorded
			{
				Readings = readings,
			};

			endpoint.Publish(readingsRecorded);
			Console.WriteLine($"Published ReadingsRecorded message for station {stationNumber}.");
		}



		//報開關狀態
		private void PublishStationParametersModifiedMessages(byte stationNumber, AmqpCFXEndpoint endpoint)
		{
			//20241204新增====================================================
			if (!slaveData.TryGetValue(stationNumber, out var data) || data == null || !data.Any())
			{
				Console.WriteLine($"No data available for station {stationNumber}.");
				return;
			}

			// 確保至少有一個非零數據值
			if (!data.Values.Any(value => value != 0))
			{
				Console.WriteLine($"All data for station {stationNumber} is default (zero), skipping.");
				return;
			}

			var parameters = new List<Parameter>
					{
						new GenericParameter
						{
							Name = "開關狀態",
							Value = data.ContainsKey("開關狀態") ? data["開關狀態"].ToString() : "0"
						}
					};

			endpoint.Publish(new StationParametersModified { ModifiedParameters = parameters });
		}


		//報使用的電流、功率等等
		private void PublishEnergyConsumedMessages(byte stationNumber, AmqpCFXEndpoint endpoint)
		{
			if (!slaveData.TryGetValue(stationNumber, out var data) || data == null || !data.Any())
			{
				Console.WriteLine($"No data available for station {stationNumber}.");
				return;
			}

			// 確保至少有一個非零數據值
			if (!data.Values.Any(value => value != 0))
			{
				Console.WriteLine($"All data for station {stationNumber} is default (zero), skipping.");
				return;
			}

			// 提取 RYB 電流
			var currentRYB = new List<double>
					{
						data.TryGetValue("A相電流 (A)", out var currentA) ? currentA : 0.0,
						data.TryGetValue("B相電流 (A)", out var currentB) ? currentB : 0.0,
						data.TryGetValue("C相電流 (A)", out var currentC) ? currentC : 0.0
					};

			// 提取 RYB 功率
			var powerRYB = new List<double>
					{
						data.TryGetValue("A相有功功率 (W)", out var powerA) ? powerA : 0.0,
						data.TryGetValue("B相有功功率 (W)", out var powerB) ? powerB : 0.0,
						data.TryGetValue("C相有功功率 (W)", out var powerC) ? powerC : 0.0
					};

			// 提取 RYB 電壓
			var voltageRYB = new List<double>
					{
						data.TryGetValue("A相電壓 (V)", out var voltageA) ? voltageA : 0.0,
						data.TryGetValue("B相電壓 (V)", out var voltageB) ? voltageB : 0.0,
						data.TryGetValue("C相電壓 (V)", out var voltageC) ? voltageC : 0.0
					};

			// 提取總電能
			var energyUsed = data.TryGetValue("電能 (kWh)", out var energy) ? energy : 0.0;

			// 創建 EnergyConsumed 訊息
			var energyConsumed = new EnergyConsumed
			{
				EnergyUsed = energyUsed,
				StartTime = DateTime.UtcNow,
				EndTime = DateTime.UtcNow,
				CurrentNowRYB = currentRYB,
				PowerNowRYB = powerRYB,

				//20241129新增測試==================================================
				VoltageNowRYB = voltageRYB // 添加電壓數據
				//20241129新增測試==================================================
			};

			// 發佈訊息
			endpoint.Publish(energyConsumed);
			Console.WriteLine($"Published EnergyConsumed message for station {stationNumber}.");
		}

	}



}
