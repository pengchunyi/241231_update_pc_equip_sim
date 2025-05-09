//////20241125_使用publish模式，已經可以發多條了，而且數值都有對應到=================================
//using System;
//using System.Collections.Generic;
//using System.IO.Ports;
//using System.Linq;
//using System.Threading.Tasks;
//using CFX;
//using CFX.ResourcePerformance;
//using CFX.Transport;
////20241127新增==================================
//using CFX.Structures;

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







//		//20241127新增===========================================================================
//		private static readonly Dictionary<int, (string FaultCode, string Description)> faultDictionary = new Dictionary<int, (string FaultCode, string Description)>
//	{
//		{ 0, ("A_OVERVOLTAGE", "A相過壓") },
//		{ 1, ("B_OVERVOLTAGE", "B相過壓") },
//		{ 2, ("C_OVERVOLTAGE", "C相過壓") },
//		{ 3, ("A_UNDERVOLTAGE", "A相欠壓") },
//		{ 4, ("B_UNDERVOLTAGE", "B相欠壓") },
//		{ 5, ("C_UNDERVOLTAGE", "C相欠壓") },
//		{ 6, ("A_OVERCURRENT", "A相過流") },
//		{ 7, ("B_OVERCURRENT", "B相過流") },
//		{ 8, ("C_OVERCURRENT", "C相過流") },
//		{ 9, ("LEAKAGE_FAULT", "漏電異常") },
//		{ 10, ("A_OVERHEAT", "A相出線溫度異常") },
//		{ 11, ("B_OVERHEAT", "B相出線溫度異常") },
//		{ 12, ("C_OVERHEAT", "C相出線溫度異常") },
//		{ 13, ("N_OVERHEAT", "N相出線溫度異常") },
//		{ 14, ("SHORT_CIRCUIT", "短路") },
//		{ 15, ("ARC_FAULT", "電弧") },
//		{ 16, ("PHASE_LOSS", "缺相") },
//		{ 17, ("NEUTRAL_DISCONNECT", "斷零") },
//		{ 18, ("VOLTAGE_IMBALANCE", "三相電壓不平衡") },
//		{ 19, ("LOCKOUT", "鎖定") },
//		{ 20, ("MAINTENANCE_MODE", "進入維修或手動模式") },
//		{ 21, ("SWITCH_FAULT", "開關狀態異常，提示客戶換設備") },
//		{ 22, ("LEAKAGE_FAILURE", "漏電功能壞，提示客戶換設備") },
//		{ 23, ("DEVICE_OFFLINE", "設備離線") },
//		{ 24, ("OVERVOLTAGE_WARNING", "過壓預警") },
//		{ 25, ("UNDERVOLTAGE_WARNING", "欠壓預警") },
//		{ 26, ("OVERCURRENT_WARNING", "過流預警") },
//		{ 27, ("OVERHEAT_WARNING", "過溫預警") }
//	};

//		//20241127新增===========================================================================



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
//			if (string.IsNullOrEmpty(endpointName))
//				throw new ArgumentException("Endpoint name cannot be null or empty", nameof(endpointName));

//			// Create and open the AMQP CFX endpoint
//			AmqpCFXEndpoint endpoint = new AmqpCFXEndpoint();
//			endpoint.Open(endpointName, new Uri(endpointUri));

//			// Add a publish channel
//			endpoint.AddPublishChannel(new Uri(publishChannelUri), "event");

//			// Publish an EndpointConnected message
//			endpoint.Publish(new EndpointConnected());
//			Console.WriteLine($"AMQP endpoint \"{endpointName}\" connected and published EndpointConnected message.");

//			// Start a task to periodically publish energy consumption responses
//			Task.Run(async () =>
//			{
//				while (true)
//				{
//					await PublishEnergyConsumptionResponses(endpoint);
//					await Task.Delay(8000); // Wait for 8 seconds before publishing again
//				}
//			});
//		}

//		private async Task PublishEnergyConsumptionResponses(AmqpCFXEndpoint endpoint)
//		{
//			try
//			{
//				// 讀取所有站號的參數
//				await ModbusHelper.ReadAllParametersAsync(serialPort, modbusViewer);

//				// 創建要發佈的訊息列表
//				var responses = new List<CFXMessage>();

//				// 遍歷 slaveData 的站號
//				foreach (var stationNumber in slaveData.Keys)
//				{
//					// 發佈 FaultOccurred 消息
//					PublishFaultOccurredMessages(stationNumber, endpoint);
//					//var response = CreateEnergyConsumptionResponse(stationNumber);
//					var response = CreateStationParametersModified(stationNumber);

//					if (response != null)
//					{
//						responses.Add(response);
//					}
//				}

//				// 發佈多個訊息
//				endpoint.PublishMany(responses);
//				Console.WriteLine("已發佈多個 EnergyConsumptionResponse 訊息。");
//			}
//			catch (Exception ex)
//			{
//				Console.WriteLine($"發佈 EnergyConsumptionResponse 訊息時發生錯誤：{ex.Message}");
//			}
//		}

//		//20241127_新增=======================================================================
//		//public void SimulateFaultForTesting(Dictionary<byte, Dictionary<string, int>> simulatedData)
//		//{
//		//	foreach (var stationData in simulatedData)
//		//	{
//		//		byte stationNumber = stationData.Key;
//		//		if (stationData.Value.TryGetValue("當前狀態", out var statusRegister))
//		//		{
//		//			for (int bitPosition = 0; bitPosition <= 27; bitPosition++)
//		//			{
//		//				if ((statusRegister & (1 << bitPosition)) != 0 && faultDictionary.ContainsKey(bitPosition))
//		//				{
//		//					var (faultCode, description) = faultDictionary[bitPosition];
//		//					var faultOccurred = new FaultOccurred
//		//					{
//		//						Fault = new Fault
//		//						{
//		//							Cause = FaultCause.MechanicalFailure,
//		//							Severity = FaultSeverity.Error,
//		//							FaultCode = faultCode,
//		//							FaultOccurrenceId = Guid.NewGuid(),
//		//							Description = description,
//		//							OccurredAt = DateTime.UtcNow
//		//						}
//		//					};
//		//					Console.WriteLine($"Simulated FaultOccurred: {faultCode} - {description}");
//		//				}
//		//			}
//		//		}
//		//	}
//		//}
//		//20241127_新增=======================================================================
//		//private void PublishFaultOccurredMessages(byte stationNumber, AmqpCFXEndpoint endpoint)
//		//{
//		//	if (!slaveData.TryGetValue(stationNumber, out var data) || data == null || !data.Any())
//		//	{
//		//		Console.WriteLine($"No valid data found for station number {stationNumber}.");
//		//		return;
//		//	}

//		//	if (data.TryGetValue("當前狀態", out var statusRegister))
//		//	{
//		//		for (int bitPosition = 0; bitPosition <= 27; bitPosition++)
//		//		{
//		//			if ((statusRegister & (1 << bitPosition)) != 0 && faultDictionary.ContainsKey(bitPosition))
//		//			{
//		//				var (faultCode, description) = faultDictionary[bitPosition];
//		//				var faultOccurred = new FaultOccurred
//		//				{
//		//					Fault = new Fault
//		//					{
//		//						FaultCode = faultCode, // 必帶
//		//						FaultOccurrenceId = Guid.NewGuid(), // 必帶，唯一識別每個錯誤
//		//						Description = description, // 必帶，錯誤描述
//		//						OccurredAt = DateTime.UtcNow, // 必帶，發生錯誤的時間
//		//						Severity = FaultSeverity.Error, // 必帶，錯誤的嚴重程度，固定為 Error
//		//						Lane = 0, // 非必填，固定為 0
//		//						SideLocation = SideLocation.Unknown, // 非必填，默認為 Unknown
//		//						TransactionID = Guid.NewGuid(), // 必帶，工位事件的唯一 ID
//		//						Stage = new Stage // 可選
//		//						{
//		//							StageName = $"Station_{stationNumber}", // 工位名稱
//		//							StageSequence = 1, // 假設為第 1 工位
//		//							StageType = StageType.Processing // 默認為加工站
//		//						}
//		//					}
//		//				};

//		//				// 發佈 FaultOccurred 消息
//		//				endpoint.Publish(faultOccurred);
//		//				Console.WriteLine($"Published FaultOccurred message for fault: {faultCode} - {description}");
//		//			}
//		//		}
//		//	}
//		//}



//		//20241127_新增=======================================================================
//		private void PublishFaultOccurredMessages(byte stationNumber, AmqpCFXEndpoint endpoint)
//		{
//			if (!slaveData.TryGetValue(stationNumber, out var data) || data == null || !data.Any())
//			{
//				Console.WriteLine($"No valid data found for station number {stationNumber}.");
//				return;
//			}

//			if (data.TryGetValue("當前狀態", out var statusRegister))
//			{
//				for (int bitPosition = 0; bitPosition <= 27; bitPosition++)
//				{
//					if ((statusRegister & (1 << bitPosition)) != 0 && faultDictionary.ContainsKey(bitPosition))
//					{
//						var (faultCode, description) = faultDictionary[bitPosition];
//						var faultOccurred = new FaultOccurred
//						{
//							Fault = new Fault
//							{
//								Cause = FaultCause.MechanicalFailure,
//								Severity = FaultSeverity.Error,
//								FaultCode = faultCode,
//								//FaultOccurrenceId = Guid.NewGuid().ToString(),
//								FaultOccurrenceId = Guid.NewGuid(),
//								Description = description,
//								OccurredAt = DateTime.UtcNow
//							}
//						};
//						endpoint.Publish(faultOccurred);
//						Console.WriteLine($"Published FaultOccurred message for fault: {faultCode} - {description}");
//					}
//				}
//			}

//		}



//		private StationParametersModified CreateStationParametersModified(byte stationNumber)
//		{
//			// 從 slaveData 中取得對應站號的資料
//			if (!slaveData.TryGetValue(stationNumber, out var data) || data == null || !data.Any())
//			{
//				Console.WriteLine($"無法找到站號 {stationNumber} 的有效資料。");
//				return null; // 無數據返回 null
//			}

//			//// 確保至少有一個非零的數據值
//			bool hasValidData = data.Values.Any(value => value != 0);
//			if (!hasValidData)
//			{
//				Console.WriteLine($"站號 {stationNumber} 的數據全部為默認值（零），跳過處理。");
//				return null;
//			}

//			// 創建並返回 StationParametersModified
//			var parameters = new List<Parameter>
//	{
//		new GenericParameter
//		{
//			Name = "當前狀態",
//			Value = data.TryGetValue("當前狀態", out var currentStatus) ? currentStatus.ToString() : "0"
//		},
//		new GenericParameter
//		{
//			Name = "A相溫度 (℃)",
//			Value = data.TryGetValue("A相溫度 (℃)", out var tempA) ? tempA.ToString() : "0"
//		},
//		new GenericParameter
//		{
//			Name = "B相溫度 (℃)",
//			Value = data.TryGetValue("B相溫度 (℃)", out var tempB) ? tempB.ToString() : "0"
//		},
//		new GenericParameter
//		{
//			Name = "C相溫度 (℃)",
//			Value = data.TryGetValue("C相溫度 (℃)", out var tempC) ? tempC.ToString() : "0"
//		},
//		new GenericParameter
//		{
//			Name = "A相電流 (A)",
//			Value = data.TryGetValue("A相電流 (A)", out var currentA) ? currentA.ToString() : "0"
//		},
//		new GenericParameter
//		{
//			Name = "B相電流 (A)",
//			Value = data.TryGetValue("B相電流 (A)", out var currentB) ? currentB.ToString() : "0"
//		},
//		new GenericParameter
//		{
//			Name = "C相電流 (A)",
//			Value = data.TryGetValue("C相電流 (A)", out var currentC) ? currentC.ToString() : "0"
//		},
//		new GenericParameter
//		{
//			Name = "A相有功功率 (W)",
//			Value = data.TryGetValue("A相有功功率 (W)", out var powerA) ? powerA.ToString() : "0"
//		},
//		new GenericParameter
//		{
//			Name = "B相有功功率 (W)",
//			Value = data.TryGetValue("B相有功功率 (W)", out var powerB) ? powerB.ToString() : "0"
//		},
//		new GenericParameter
//		{
//			Name = "C相有功功率 (W)",
//			Value = data.TryGetValue("C相有功功率 (W)", out var powerC) ? powerC .ToString() : "0"
//		},
//		new GenericParameter
//		{
//			Name = "電能 (kWh)",
//			Value = data.TryGetValue("電能 (kWh)", out var energy) ? energy.ToString() : "0"
//		},
//		new GenericParameter
//		{
//			Name = "當前總有功功率 (W)",
//			Value = data.TryGetValue("當前總有功功率 (W)", out var totalPower) ? totalPower.ToString() : "0"
//		},
//		new GenericParameter
//		{
//			Name = "開關狀態",
//			Value = data.TryGetValue("開關狀態", out var switchStatus) ? switchStatus.ToString() : "0"
//		}
//	};

//			return new StationParametersModified
//			{
//				ModifiedParameters = parameters
//			};
//		}


//	}
//}


////		private async Task PublishEnergyConsumptionResponses(AmqpCFXEndpoint endpoint)
////		{
////			try
////			{
////				// 讀取所有站號的參數
////				await ModbusHelper.ReadAllParametersAsync(serialPort, modbusViewer);

////				// 創建要發佈的訊息列表
////				var responses = new List<CFXMessage>();

////				// 遍歷 slaveData 的站號
////				foreach (var stationNumber in slaveData.Keys)
////				{
////					//var response = CreateEnergyConsumptionResponse(stationNumber);
////					var response = CreateStationParametersModified(stationNumber);

////					if (response != null)
////					{
////						responses.Add(response);
////					}
////				}

////				// 發佈多個訊息
////				endpoint.PublishMany(responses);
////				Console.WriteLine("已發佈多個 EnergyConsumptionResponse 訊息。");
////			}
////			catch (Exception ex)
////			{
////				Console.WriteLine($"發佈 EnergyConsumptionResponse 訊息時發生錯誤：{ex.Message}");
////			}
////		}


////		//private EnergyConsumptionResponse CreateEnergyConsumptionResponse(byte stationNumber)
////		//{

////		//	// 從 slaveData 中取得對應站號的資料
////		//	if (!slaveData.TryGetValue(stationNumber, out var data) || data == null || !data.Any())
////		//	{
////		//		Console.WriteLine($"無法找到站號 {stationNumber} 的有效資料。");
////		//		return null; // 無數據返回 null
////		//	}

////		//	// 確保至少有一個非零的數據值
////		//	bool hasValidData = data.Values.Any(value => value != 0);
////		//	if (!hasValidData)
////		//	{
////		//		Console.WriteLine($"站號 {stationNumber} 的數據全部為默認值（零），跳過處理。");
////		//		return null;
////		//	}





////		//	// 提取 RYB 電壓值
////		//	var voltageRYB = new List<double>
////		//	{
////		//		data.TryGetValue("A相電壓 (V)", out var voltageA) ? voltageA : 0.0,
////		//		data.TryGetValue("B相電壓 (V)", out var voltageB) ? voltageB : 0.0,
////		//		data.TryGetValue("C相電壓 (V)", out var voltageC) ? voltageC : 0.0
////		//	};

////		//	// 提取 RYB 電流
////		//	var currentRYB = new List<double>
////		//	{
////		//		data.TryGetValue("A相電流 (A)", out var currentA) ? currentA : 0.0,
////		//		data.TryGetValue("B相電流 (A)", out var currentB) ? currentB : 0.0,
////		//		data.TryGetValue("C相電流 (A)", out var currentC) ? currentC : 0.0
////		//	};

////		//	// 提取 RYB 功率
////		//	var powerRYB = new List<double>
////		//	{
////		//		data.TryGetValue("A相有功功率 (W)", out var powerA) ? powerA : 0.0,
////		//		data.TryGetValue("B相有功功率 (W)", out var powerB) ? powerB : 0.0,
////		//		data.TryGetValue("C相有功功率 (W)", out var powerC) ? powerC : 0.0
////		//	};

////		//	// 提取 RYB 功率因數
////		//	var powerFactorRYB = new List<double>
////		//	{
////		//		data.TryGetValue("A相功率因數", out var pfA) ? pfA : 0.0,
////		//		data.TryGetValue("B相功率因數", out var pfB) ? pfB : 0.0,
////		//		data.TryGetValue("C相功率因數", out var pfC) ? pfC : 0.0
////		//	};

////		//	// 單項數值提取
////		//	var voltageNow = voltageRYB.FirstOrDefault();
////		//	var currentNow = currentRYB.FirstOrDefault();
////		//	var powerNow = powerRYB.FirstOrDefault();
////		//	var frequencyNow = data.TryGetValue("當前線頻率 (Hz)", out var frequency) ? frequency : 0.0;

////		//	// 創建並返回 EnergyConsumptionResponse
////		//	return new EnergyConsumptionResponse
////		//	{
////		//		Result = new CFX.Structures.RequestResult
////		//		{
////		//			Result = CFX.Structures.StatusResult.Success,
////		//			ResultCode = 0,
////		//			Message = $"Station {stationNumber} Data Retrieved Successfully"
////		//		},
////		//		StartTime = DateTimeOffset.Now.DateTime, // 符合格式要求
////		//		EndTime = DateTimeOffset.Now.DateTime,




////		//		EnergyUsed = data.TryGetValue("電能 (kWh)", out var energy) ? energy : 0.0,

////		//		PeakPower = powerNow,
////		//		MinimumPower = powerNow,
////		//		MeanPower = powerNow,
////		//		PowerNow = powerNow,
////		//		PowerFactorNow = powerFactorRYB.FirstOrDefault(),

////		//		PeakCurrent = currentNow,
////		//		MinimumCurrent = currentNow,
////		//		MeanCurrent = currentNow,
////		//		CurrentNow = currentNow,

////		//		PeakVoltage = voltageNow,
////		//		MinimumVoltage = voltageNow,
////		//		MeanVoltage = voltageNow,
////		//		VoltageNow = voltageNow,

////		//		PeakFrequency = frequencyNow,
////		//		MinimumFrequency = frequencyNow,
////		//		MeanFrequency = frequencyNow,
////		//		FrequencyNow = frequencyNow,

////		//		PeakPowerRYB = powerRYB,
////		//		MinimumPowerRYB = powerRYB,
////		//		MeanPowerRYB = powerRYB,
////		//		PowerNowRYB = powerRYB,

////		//		PowerFactorNowRYB = powerFactorRYB,

////		//		PeakCurrentRYB = currentRYB,
////		//		MinimumCurrentRYB = currentRYB,
////		//		MeanCurrentRYB = currentRYB,
////		//		CurrentNowRYB = currentRYB,

////		//		PeakVoltageRYB = voltageRYB,
////		//		MinimumVoltageRYB = voltageRYB,
////		//		MeanVoltageRYB = voltageRYB,
////		//		VoltageNowRYB = voltageRYB,

////		//		ThreePhaseNeutralCurrentNow = 0.0
////		//	};
////		//}