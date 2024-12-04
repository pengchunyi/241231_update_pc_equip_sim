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
//			var endpoint = new AmqpCFXEndpoint();
//			endpoint.Open(endpointName, new Uri(endpointUri));
//			endpoint.AddPublishChannel(new Uri(publishChannelUri), "event");
//			endpoint.Publish(new EndpointConnected());
//			Console.WriteLine($"AMQP endpoint \"{endpointName}\" connected.");

//			Task.Run(async () =>
//			{
//				while (true)
//				{
//					await PublishEnergyConsumptionResponses(endpoint);
//					await Task.Delay(8000);
//				}
//			});
//		}

//		private async Task PublishEnergyConsumptionResponses(AmqpCFXEndpoint endpoint)
//		{
//			foreach (var stationNumber in slaveData.Keys)
//			{
//				PublishFaultOccurredMessages(stationNumber, endpoint);
//				PublishReadingsRecordedMessages(stationNumber, endpoint);
//				PublishStationParametersModifiedMessages(stationNumber, endpoint);
//				PublishEnergyConsumedMessages(stationNumber, endpoint);
//			}
//		}

//		private void PublishFaultOccurredMessages(byte stationNumber, AmqpCFXEndpoint endpoint)
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
//							{
//								{ "zh-TW", description } // 如果需要，添加多語言對應描述
//							},
//							OccurredAt = DateTime.UtcNow,
//							Severity = FaultSeverity.Error
//						}
//					};
//					endpoint.Publish(faultOccurred);
//				}
//			}

//		}


//		private void PublishReadingsRecordedMessages(byte stationNumber, AmqpCFXEndpoint endpoint)
//		{
//			if (!slaveData.TryGetValue(stationNumber, out var data))
//				return;

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
//			if (!slaveData.TryGetValue(stationNumber, out var data))
//				return;

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





//		//private void PublishEnergyConsumedMessages(byte stationNumber, AmqpCFXEndpoint endpoint)
//		//{
//		//	if (!slaveData.TryGetValue(stationNumber, out var data))
//		//		return;

//		//	var energyConsumed = new EnergyConsumed
//		//	{
//		//		EnergyUsed = data.ContainsKey("電能 (kWh)") ? data["電能 (kWh)"] : 0,
//		//		StartTime = DateTime.UtcNow.AddMinutes(-1),
//		//		EndTime = DateTime.UtcNow
//		//	};

//		//	endpoint.Publish(energyConsumed);
//		//}


//		//private void PublishEnergyConsumedMessages(byte stationNumber, AmqpCFXEndpoint endpoint)
//		//{
//		//	if (!slaveData.TryGetValue(stationNumber, out var data) || data == null || !data.Any())
//		//	{
//		//		Console.WriteLine($"No data available for station {stationNumber}.");
//		//		return;
//		//	}

//		//	// 確保至少有一個非零數據值
//		//	if (!data.Values.Any(value => value != 0))
//		//	{
//		//		Console.WriteLine($"All data for station {stationNumber} is default (zero), skipping.");
//		//		return;
//		//	}

//		//	// 提取 RYB 電壓值
//		//			var voltageRYB = new List<double>
//		//	{
//		//		data.TryGetValue("A相電壓 (V)", out var voltageA) ? voltageA : 0.0,
//		//		data.TryGetValue("B相電壓 (V)", out var voltageB) ? voltageB : 0.0,
//		//		data.TryGetValue("C相電壓 (V)", out var voltageC) ? voltageC : 0.0
//		//	};

//		//			// 提取 RYB 電流
//		//			var currentRYB = new List<double>
//		//	{
//		//		data.TryGetValue("A相電流 (A)", out var currentA) ? currentA : 0.0,
//		//		data.TryGetValue("B相電流 (A)", out var currentB) ? currentB : 0.0,
//		//		data.TryGetValue("C相電流 (A)", out var currentC) ? currentC : 0.0
//		//	};

//		//			// 提取 RYB 功率
//		//			var powerRYB = new List<double>
//		//	{
//		//		data.TryGetValue("A相有功功率 (W)", out var powerA) ? powerA : 0.0,
//		//		data.TryGetValue("B相有功功率 (W)", out var powerB) ? powerB : 0.0,
//		//		data.TryGetValue("C相有功功率 (W)", out var powerC) ? powerC : 0.0
//		//	};

//		//			// 提取 RYB 功率因數
//		//			var powerFactorRYB = new List<double>
//		//	{
//		//		data.TryGetValue("A相功率因數", out var pfA) ? pfA : 0.0,
//		//		data.TryGetValue("B相功率因數", out var pfB) ? pfB : 0.0,
//		//		data.TryGetValue("C相功率因數", out var pfC) ? pfC : 0.0
//		//	};


//		//	// 單項數值提取
//		//	var voltageNow = voltageRYB.FirstOrDefault();
//		//	var currentNow = currentRYB.FirstOrDefault();
//		//	var powerNow = powerRYB.FirstOrDefault();
//		//	var frequencyNow = data.TryGetValue("當前線頻率 (Hz)", out var frequency) ? frequency : 0.0;

//		//	// 創建並發佈 EnergyConsumed 訊息
//		//	var energyConsumed = new EnergyConsumed
//		//	{
//		//		EnergyUsed = data.TryGetValue("電能 (kWh)", out var energy) ? energy : 0.0,
//		//		StartTime = DateTime.UtcNow.AddMinutes(-1),
//		//		EndTime = DateTime.UtcNow,
//		//		PeakPower = powerNow,
//		//		MinimumPower = powerNow,
//		//		MeanPower = powerNow,
//		//		PowerNow = powerNow,
//		//		PowerFactorNow = powerFactorRYB.FirstOrDefault(),
//		//		PeakCurrent = currentNow,
//		//		MinimumCurrent = currentNow,
//		//		MeanCurrent = currentNow,
//		//		CurrentNow = currentNow,
//		//		PeakVoltage = voltageNow,
//		//		MinimumVoltage = voltageNow,
//		//		MeanVoltage = voltageNow,
//		//		VoltageNow = voltageNow,
//		//		PeakFrequency = frequencyNow,
//		//		MinimumFrequency = frequencyNow,
//		//		MeanFrequency = frequencyNow,
//		//		FrequencyNow = frequencyNow,
//		//		PowerNowRYB = powerRYB,
//		//		PeakPowerRYB = powerRYB,
//		//		MinimumPowerRYB = powerRYB,
//		//		MeanPowerRYB = powerRYB,
//		//		CurrentNowRYB = currentRYB,
//		//		PeakCurrentRYB = currentRYB,
//		//		MinimumCurrentRYB = currentRYB,
//		//		MeanCurrentRYB = currentRYB,
//		//		VoltageNowRYB = voltageRYB,
//		//		PeakVoltageRYB = voltageRYB,
//		//		MinimumVoltageRYB = voltageRYB,
//		//		MeanVoltageRYB = voltageRYB,
//		//		PowerFactorNowRYB = powerFactorRYB
//		//	};

//		//	endpoint.Publish(energyConsumed);
//		//	Console.WriteLine($"Published EnergyConsumed message for station {stationNumber}.");
//		//}
//		//private void PublishEnergyConsumedMessages(byte stationNumber, AmqpCFXEndpoint endpoint)
//		//{
//		//	if (!slaveData.TryGetValue(stationNumber, out var data) || data == null || !data.Any())
//		//	{
//		//		Console.WriteLine($"No data available for station {stationNumber}.");
//		//		return;
//		//	}

//		//	// 確保至少有一個非零數據值
//		//	if (!data.Values.Any(value => value != 0))
//		//	{
//		//		Console.WriteLine($"All data for station {stationNumber} is default (zero), skipping.");
//		//		return;
//		//	}

//		//	// 提取 RYB 電流
//		//			var currentRYB = new List<double>
//		//	{
//		//		data.TryGetValue("A相電流 (A)", out var currentA) ? currentA : 0.0,
//		//		data.TryGetValue("B相電流 (A)", out var currentB) ? currentB : 0.0,
//		//		data.TryGetValue("C相電流 (A)", out var currentC) ? currentC : 0.0
//		//	};

//		//			// 提取 RYB 功率
//		//			var powerRYB = new List<double>
//		//	{
//		//		data.TryGetValue("A相有功功率 (W)", out var powerA) ? powerA : 0.0,
//		//		data.TryGetValue("B相有功功率 (W)", out var powerB) ? powerB : 0.0,
//		//		data.TryGetValue("C相有功功率 (W)", out var powerC) ? powerC : 0.0
//		//	};

//		//	// 提取總電能
//		//	var energyUsed = data.TryGetValue("電能 (kWh)", out var energy) ? energy : 0.0;

//		//	// 創建 EnergyConsumed 訊息
//		//	var energyConsumed = new EnergyConsumed
//		//	{
//		//		EnergyUsed = energyUsed,
//		//		StartTime = DateTime.UtcNow.AddMinutes(-1),
//		//		EndTime = DateTime.UtcNow,
//		//		CurrentNowRYB = currentRYB,
//		//		PowerNowRYB = powerRYB
//		//	};

//		//	// 發佈訊息
//		//	endpoint.Publish(energyConsumed);
//		//	Console.WriteLine($"Published EnergyConsumed message for station {stationNumber}.");
//		//}

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
