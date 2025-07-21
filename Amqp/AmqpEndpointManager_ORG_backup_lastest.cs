////////20241123_使用publish模式，已經可以發兩條了，但是兩條數值一模一樣=================================
//////using System;
//////using System.Collections.Generic;
//////using System.IO.Ports;
//////using System.Threading.Tasks;
//////using CFX;
//////using CFX.ResourcePerformance;
//////using CFX.Transport;

//////namespace AmqpModbusIntegration
//////{
//////	public class AmqpEndpointManager
//////	{
//////		private readonly string endpointUri;
//////		private readonly string publishChannelUri;
//////		private readonly string subscribeChannelUri;
//////		private readonly ModbusViewer modbusViewer;

//////		//20241120
//////		private readonly SerialPort serialPort;
//////		private readonly Dictionary<byte, Dictionary<string, int>> slaveData;

//////		public AmqpEndpointManager(
//////	   string endpointUri,
//////	   string publishChannelUri,
//////	   string subscribeChannelUri,
//////	   ModbusViewer modbusViewer,
//////	   SerialPort serialPort,
//////	   Dictionary<byte, Dictionary<string, int>> slaveData)
//////		{
//////			this.endpointUri = endpointUri;
//////			this.publishChannelUri = publishChannelUri;
//////			this.subscribeChannelUri = subscribeChannelUri;
//////			this.modbusViewer = modbusViewer;
//////			this.serialPort = serialPort;
//////			this.slaveData = slaveData;
//////		}


//////		public void StartAmqpEndpoint(string endpointName)
//////		{
//////			if (string.IsNullOrEmpty(endpointName))
//////				throw new ArgumentException("Endpoint name cannot be null or empty", nameof(endpointName));

//////			// Create and open the AMQP CFX endpoint
//////			AmqpCFXEndpoint endpoint = new AmqpCFXEndpoint();
//////			endpoint.Open(endpointName, new Uri(endpointUri));

//////			// Add a publish channel
//////			endpoint.AddPublishChannel(new Uri(publishChannelUri), "event");

//////			// Publish an EndpointConnected message
//////			endpoint.Publish(new EndpointConnected());
//////			Console.WriteLine($"AMQP endpoint \"{endpointName}\" connected and published EndpointConnected message.");

//////			// Start a task to periodically publish energy consumption responses
//////			Task.Run(async () =>
//////			{
//////				while (true)
//////				{
//////					await PublishEnergyConsumptionResponses(endpoint);
//////					await Task.Delay(8000); // Wait for 8 seconds before publishing again
//////				}
//////			});
//////		}

//////		private async Task PublishEnergyConsumptionResponses(AmqpCFXEndpoint endpoint)
//////		{
//////			try
//////			{
//////				// 讀取所有站號的參數
//////				await ModbusHelper.ReadAllParametersAsync(serialPort, modbusViewer);

//////				// 創建要發佈的訊息列表
//////				var responses = new List<CFXMessage>();

//////				// 假設有兩個站號，分別為 1 和 2
//////				byte[] stationNumbers = { 1, 2 };

//////				foreach (var stationNumber in stationNumbers)
//////				{
//////					var response = CreateEnergyConsumptionResponse(stationNumber);
//////					if (response != null)
//////					{
//////						responses.Add(response);
//////					}
//////				}

//////				// 發佈多個訊息
//////				endpoint.PublishMany(responses);
//////				Console.WriteLine("已發佈多個 EnergyConsumptionResponse 訊息。");
//////			}
//////			catch (Exception ex)
//////			{
//////				Console.WriteLine($"發佈 EnergyConsumptionResponse 訊息時發生錯誤：{ex.Message}");
//////			}
//////		}


//////		private EnergyConsumptionResponse CreateEnergyConsumptionResponse(byte stationNumber)
//////		{
//////			// 從 modbusViewer 中取得對應站號的資料
//////			if (!modbusViewer.GetSlaveData().TryGetValue(stationNumber, out var slaveData))
//////			{
//////				Console.WriteLine($"無法找到站號 {stationNumber} 的資料。");
//////				return null;
//////			}

//////			// 創建並返回 EnergyConsumptionResponse
//////			return new EnergyConsumptionResponse
//////			{
//////				Result = new CFX.Structures.RequestResult
//////				{
//////					Result = CFX.Structures.StatusResult.Success,
//////					ResultCode = 0,
//////					Message = "OK"
//////				},
//////				StartTime = DateTime.Now,
//////				EndTime = DateTime.Now,
//////				EnergyUsed = slaveData.TryGetValue("電能 (kWh)", out var energyUsed) ? energyUsed : 0,
//////				PeakPower = slaveData.TryGetValue("當前總有功功率 (W)", out var peakPower) ? peakPower : 0,
//////				PowerNow = slaveData.TryGetValue("當前總有功功率 (W)", out var powerNow) ? powerNow : 0,
//////				PowerFactorNow = slaveData.TryGetValue("合併功率因數", out var powerFactorNow) ? powerFactorNow : 0,
//////				PeakCurrent = Math.Max(
//////					slaveData.TryGetValue("A相電流 (A)", out var currentA) ? currentA : 0,
//////					Math.Max(
//////						slaveData.TryGetValue("B相電流 (A)", out var currentB) ? currentB : 0,
//////						slaveData.TryGetValue("C相電流 (A)", out var currentC) ? currentC : 0
//////					)
//////				),
//////				CurrentNow = slaveData.TryGetValue("A相電流 (A)", out var currentNow) ? currentNow : 0,
//////				PeakVoltage = Math.Max(
//////					slaveData.TryGetValue("A相電壓 (V)", out var voltageA) ? voltageA : 0,
//////					Math.Max(
//////						slaveData.TryGetValue("B相電壓 (V)", out var voltageB) ? voltageB : 0,
//////						slaveData.TryGetValue("C相電壓 (V)", out var voltageC) ? voltageC : 0
//////					)
//////				),
//////				VoltageNow = slaveData.TryGetValue("B相電壓 (V)", out var voltageNow) ? voltageNow : 0,
//////				PeakFrequency = slaveData.TryGetValue("當前線頻率 (Hz)", out var frequencyNow) ? frequencyNow : 0,
//////				FrequencyNow = frequencyNow
//////			};
//////		}

//////	}
//////}



////using System;
////using System.Collections.Generic;
////using System.IO.Ports;
////using System.Linq;
////using System.Threading.Tasks;
////using CFX;
////using CFX.ResourcePerformance;
////using CFX.Transport;

////namespace AmqpModbusIntegration
////{
////	public class AmqpEndpointManager
////	{
////		private readonly string endpointUri;
////		private readonly string publishChannelUri;
////		private readonly string subscribeChannelUri;
////		private readonly ModbusViewer modbusViewer;

////		private readonly SerialPort serialPort;
////		private readonly Dictionary<byte, Dictionary<string, int>> slaveData;

////		public AmqpEndpointManager(
////			string endpointUri,
////			string publishChannelUri,
////			string subscribeChannelUri,
////			ModbusViewer modbusViewer,
////			SerialPort serialPort,
////			Dictionary<byte, Dictionary<string, int>> slaveData)
////		{
////			this.endpointUri = endpointUri;
////			this.publishChannelUri = publishChannelUri;
////			this.subscribeChannelUri = subscribeChannelUri;
////			this.modbusViewer = modbusViewer;
////			this.serialPort = serialPort;
////			this.slaveData = slaveData;
////		}

////		public void StartAmqpEndpoint(string endpointName)
////		{
////			if (string.IsNullOrEmpty(endpointName))
////				throw new ArgumentException("Endpoint name cannot be null or empty", nameof(endpointName));

////			// Create and open the AMQP CFX endpoint
////			AmqpCFXEndpoint endpoint = new AmqpCFXEndpoint();
////			endpoint.Open(endpointName, new Uri(endpointUri));

////			// Add a publish channel
////			endpoint.AddPublishChannel(new Uri(publishChannelUri), "event");

////			// Publish an EndpointConnected message
////			endpoint.Publish(new EndpointConnected());
////			Console.WriteLine($"AMQP endpoint \"{endpointName}\" connected and published EndpointConnected message.");

////			// Start a task to periodically publish energy consumption responses
////			Task.Run(async () =>
////			{
////				while (true)
////				{
////					await PublishEnergyConsumptionResponses(endpoint);
////					await Task.Delay(8000); // Wait for 8 seconds before publishing again
////				}
////			});
////		}

//private async Task PublishEnergyConsumptionResponses(AmqpCFXEndpoint endpoint)
//{
//	try
//	{
//		// 讀取所有站號的參數
//		await ModbusHelper.ReadAllParametersAsync(serialPort, modbusViewer);

//		// 創建要發佈的訊息列表
//		var responses = new List<CFXMessage>();

//		// 遍歷 slaveData 的站號
//		foreach (var stationNumber in slaveData.Keys)
//		{
//			var response = CreateEnergyConsumptionResponse(stationNumber);
//			if (response != null)
//			{
//				responses.Add(response);
//			}
//		}

//		// 發佈多個訊息
//		endpoint.PublishMany(responses);
//		Console.WriteLine("已發佈多個 EnergyConsumptionResponse 訊息。");
//	}
//	catch (Exception ex)
//	{
//		Console.WriteLine($"發佈 EnergyConsumptionResponse 訊息時發生錯誤：{ex.Message}");
//	}
//}

////		//private EnergyConsumptionResponse CreateEnergyConsumptionResponse(byte stationNumber)
////		//{
////		//	// 從 slaveData 中取得對應站號的資料
////		//	if (!slaveData.TryGetValue(stationNumber, out var data))
////		//	{
////		//		Console.WriteLine($"無法找到站號 {stationNumber} 的資料。");
////		//		return null;
////		//	}

////		//	// 提取 RYB 電流
////		//	var currentRYB = new List<double>
////		//	{
////		//		data.TryGetValue("A相電流 (A)", out var currentA) ? currentA  : 0.0,
////		//		data.TryGetValue("B相電流 (A)", out var currentB) ? currentB  : 0.0,
////		//		data.TryGetValue("C相電流 (A)", out var currentC) ? currentC  : 0.0
////		//	};

////		//	// 提取 RYB 功率
////		//	var powerRYB = new List<double>
////		//	{
////		//		data.TryGetValue("A相有功功率 (W)", out var powerA) ? powerA : 0.0,
////		//		data.TryGetValue("B相有功功率 (W)", out var powerB) ? powerB : 0.0,
////		//		data.TryGetValue("C相有功功率 (W)", out var powerC) ? powerC : 0.0
////		//	};


////		//	var voltageRYB = new List<double>
////		//	{
////		//		data.TryGetValue("A相電壓 (V)", out var tempVoltageA) ? tempVoltageA : 0.0,
////		//		data.TryGetValue("B相電壓 (V)", out var tempVoltageB) ? tempVoltageB : 0.0,
////		//		data.TryGetValue("C相電壓 (V)", out var tempVoltageC) ? tempVoltageC  : 0.0
////		//	};


////		//	// 創建並返回 EnergyConsumptionResponse
////		//	return new EnergyConsumptionResponse
////		//	{
////		//		Result = new CFX.Structures.RequestResult
////		//		{
////		//			Result = CFX.Structures.StatusResult.Success,
////		//			ResultCode = 0,
////		//			Message = $"Station {stationNumber} Data Retrieved Successfully"
////		//		},
////		//		StartTime = DateTime.UtcNow,
////		//		EndTime = DateTime.UtcNow,
////		//		EnergyUsed = data.TryGetValue("電能 (kWh)", out var energyUsed) ? energyUsed  : 0.0,
////		//		PeakPower = powerRYB.Max(),
////		//		PowerNow = data.TryGetValue("當前總有功功率 (W)", out var powerNow) ? powerNow  : 0.0,
////		//		PowerFactorNow = data.TryGetValue("合併功率因數", out var powerFactorNow) ? powerFactorNow  : 0.0,
////		//		CurrentNowRYB = currentRYB,
////		//		PowerNowRYB = powerRYB,
////		//		PeakVoltage = Math.Max(
////		//			data.TryGetValue("A相電壓 (V)", out var voltageA) ? voltageA  : 0.0,
////		//			Math.Max(
////		//				data.TryGetValue("B相電壓 (V)", out var voltageB) ? voltageB  : 0.0,
////		//				data.TryGetValue("C相電壓 (V)", out var voltageC) ? voltageC  : 0.0
////		//			)
////		//		),
////		//		VoltageNow = data.TryGetValue("A相電壓 (V)", out var voltageNow) ? voltageNow  : 0.0,
////		//		//FrequencyNow = data.TryGetValue("當前線頻率 (Hz)", out var frequencyNow) ? frequencyNow / 10.0 : 0.0,
////		//		//看一下frequencynow數值有沒有變成跟peakfrequency一樣
////		//		FrequencyNow = data.TryGetValue("當前線頻率 (Hz)", out var frequencyNow) ? frequencyNow  : 0.0,
////		//		//新增
////		//		VoltageNowRYB= voltageRYB
////		//	};
////		//}

//private EnergyConsumptionResponse CreateEnergyConsumptionResponse(byte stationNumber)
//{
//	// 從 slaveData 中取得對應站號的資料
//	if (!slaveData.TryGetValue(stationNumber, out var data))
//	{
//		Console.WriteLine($"無法找到站號 {stationNumber} 的資料。");
//		return null;
//	}

//	// 提取 RYB 電壓值
//	var voltageRYB = new List<double>
//	{
//		data.TryGetValue("A相電壓 (V)", out var voltageA) ? voltageA : 0.0,
//		data.TryGetValue("B相電壓 (V)", out var voltageB) ? voltageB : 0.0,
//		data.TryGetValue("C相電壓 (V)", out var voltageC) ? voltageC : 0.0
//	};

//	// 提取 RYB 電流
//	var currentRYB = new List<double>
//	{
//		data.TryGetValue("A相電流 (A)", out var currentA) ? currentA : 0.0,
//		data.TryGetValue("B相電流 (A)", out var currentB) ? currentB : 0.0,
//		data.TryGetValue("C相電流 (A)", out var currentC) ? currentC : 0.0
//	};

//	// 提取 RYB 功率
//	var powerRYB = new List<double>
//	{
//		data.TryGetValue("A相有功功率 (W)", out var powerA) ? powerA : 0.0,
//		data.TryGetValue("B相有功功率 (W)", out var powerB) ? powerB : 0.0,
//		data.TryGetValue("C相有功功率 (W)", out var powerC) ? powerC : 0.0
//	};

//	// 提取 RYB 功率因數
//	var powerFactorRYB = new List<double>
//	{
//		data.TryGetValue("A相功率因數", out var pfA) ? pfA : 0.0,
//		data.TryGetValue("B相功率因數", out var pfB) ? pfB : 0.0,
//		data.TryGetValue("C相功率因數", out var pfC) ? pfC : 0.0
//	};

//	// 單項數值提取
//	var voltageNow = voltageRYB.FirstOrDefault();
//	var currentNow = currentRYB.FirstOrDefault();
//	var powerNow = powerRYB.FirstOrDefault();
//	var frequencyNow = data.TryGetValue("當前線頻率 (Hz)", out var frequency) ? frequency : 0.0;

//	// 創建並返回 EnergyConsumptionResponse
//	return new EnergyConsumptionResponse
//	{
//		Result = new CFX.Structures.RequestResult
//		{
//			Result = CFX.Structures.StatusResult.Success,
//			ResultCode = 0,
//			Message = $"Station {stationNumber} Data Retrieved Successfully"
//		},
//		StartTime = DateTime.UtcNow,
//		EndTime = DateTime.UtcNow,

//		EnergyUsed = data.TryGetValue("電能 (kWh)", out var energy) ? energy : 0.0,

//		PeakPower = powerNow,
//		MinimumPower = powerNow,
//		MeanPower = powerNow,
//		PowerNow = powerNow,
//		PowerFactorNow = powerFactorRYB.FirstOrDefault(),

//		PeakCurrent = currentNow,
//		MinimumCurrent = currentNow,
//		MeanCurrent = currentNow,
//		CurrentNow = currentNow,

//		PeakVoltage = voltageNow,
//		MinimumVoltage = voltageNow,
//		MeanVoltage = voltageNow,
//		VoltageNow = voltageNow,

//		PeakFrequency = frequencyNow,
//		MinimumFrequency = frequencyNow,
//		MeanFrequency = frequencyNow,
//		FrequencyNow = frequencyNow,

//		PeakPowerRYB = powerRYB,
//		MinimumPowerRYB = powerRYB,
//		MeanPowerRYB = powerRYB,
//		PowerNowRYB = powerRYB,

//		PowerFactorNowRYB = powerFactorRYB,

//		PeakCurrentRYB = currentRYB,
//		MinimumCurrentRYB = currentRYB,
//		MeanCurrentRYB = currentRYB,
//		CurrentNowRYB = currentRYB,

//		PeakVoltageRYB = voltageRYB,
//		MinimumVoltageRYB = voltageRYB,
//		MeanVoltageRYB = voltageRYB,
//		VoltageNowRYB = voltageRYB,

//		ThreePhaseNeutralCurrentNow = 0.0
//	};
//}

////	}
////}
