//using System;
//using System.Collections.Generic;
//using System.IO.Ports;
//using System.Threading.Tasks;
//using CFX;
//using CFX.ResourcePerformance;
//using CFX.Transport;
//using System.Linq; // 確保引用了這個命名空間


//namespace AmqpModbusIntegration
//{
//    public class AmqpEndpointManager
//    {
//        private readonly string endpointUri;
//        private readonly string publishChannelUri;
//        private readonly string subscribeChannelUri;
//        private readonly ModbusViewer modbusViewer;

//        //20241120
//        private readonly SerialPort serialPort;
//        private readonly Dictionary<byte, Dictionary<string, int>> slaveData;

//		public AmqpEndpointManager(
//       string endpointUri,
//       string publishChannelUri,
//       string subscribeChannelUri,
//       ModbusViewer modbusViewer,
//       SerialPort serialPort,
//       Dictionary<byte, Dictionary<string, int>> slaveData)
//        {
//            this.endpointUri = endpointUri;
//            this.publishChannelUri = publishChannelUri;
//            this.subscribeChannelUri = subscribeChannelUri;
//            this.modbusViewer = modbusViewer;
//            this.serialPort = serialPort;
//            this.slaveData = slaveData;
//        }

//        public void StartAmqpEndpoint(string endpointName)
//        {
//            if (string.IsNullOrEmpty(endpointName))
//                throw new ArgumentException("Endpoint name cannot be null or empty", nameof(endpointName));

//            AmqpCFXEndpoint endpoint = new AmqpCFXEndpoint(); // 創建CFX端點
//            endpoint.Open(endpointName, new Uri(endpointUri)); // 打開端點並連接URI

//            // 添加發布頻道
//            endpoint.AddPublishChannel(new Uri(publishChannelUri), "event");

//            // 發布連接消息
//            endpoint.Publish(new EndpointConnected());
//            Console.WriteLine($"AMQP端點 \"{endpointName}\" 已成功連接並發佈消息");

//            // 添加訂閱頻道，用於接收消息
//            endpoint.AddSubscribeChannel(new AmqpChannelAddress
//            {
//                Address = "MessageSource",
//                Uri = new Uri(subscribeChannelUri)
//            });

//            // 註冊接收到請求時的回調
//            endpoint.OnRequestReceived += (request) =>
//            {
//                return OnRequestReceivedAsync(request).GetAwaiter().GetResult();
//            };
//        }

//		private async Task<CFXEnvelope> OnRequestReceivedAsync(CFXEnvelope request)
//		{
//			if (request.MessageBody is EnergyConsumptionRequest)
//			{
//				await ModbusHelper.ReadAllParametersAsync(serialPort, modbusViewer);

//				// 生成回應消息的參數
//				DateTime startTime = DateTime.Now;
//				DateTime endTime = DateTime.Now;

//				double energyUsed = modbusViewer.energyHighByte + modbusViewer.energyLowByte;
//				double peakPower = modbusViewer.totalApparentPower;
//				double powerNow = modbusViewer.totalActivePower;
//				double powerFactorNow = modbusViewer.combinedPowerFactor / 100.0;
//				double peakCurrent = Math.Max(modbusViewer.currentA, Math.Max(modbusViewer.currentB, modbusViewer.currentC)) / 100.0;
//				double currentNow = modbusViewer.currentA / 100.0;
//				double peakVoltage = Math.Max(modbusViewer.voltageA, Math.Max(modbusViewer.voltageB, modbusViewer.voltageC)) / 10.0;
//				double voltageNow = modbusViewer.voltageA / 10.0;
//				double peakFrequency = modbusViewer.lineFrequency / 10.0;
//				double frequencyNow = modbusViewer.lineFrequency / 10.0;

//				return GenerateEnergyResponse(
//					startTime,
//					endTime,
//					energyUsed,
//					peakPower,
//					powerNow,
//					powerFactorNow,
//					peakCurrent,
//					currentNow,
//					peakVoltage,
//					voltageNow,
//					peakFrequency,
//					frequencyNow
//				);
//			}

//			//    return null; // 如果不是 EnergyConsumptionRequest，返回 null
//			////}
//			//private async Task<CFXEnvelope> OnRequestReceivedAsync(CFXEnvelope request)
//			//{
//			//	if (request.MessageBody is EnergyConsumptionRequest)
//			//	{
//			//		// 讀取所有站號數據
//			//		await ModbusHelper.ReadAllParametersAsync(serialPort, modbusViewer);

//			//		// 遍歷所有站號數據，生成單獨的 CFX 消息並分別發送
//			//		foreach (var station in modbusViewer.GetSlaveData())
//			//		{
//			//			var stationId = station.Key; // 當前站號
//			//			var data = station.Value;    // 當前站號的數據

//			//			// 為當前站號生成 CFX 消息
//			//			var response = GenerateEnergyResponse(stationId, DateTime.UtcNow, data);

//			//			// 將消息封裝為 CFXEnvelope
//			//			var envelope = CFXEnvelope.FromCFXMessage(response);

//			//			// 發送當前站號的消息
//			//			PublishCFXMessage(envelope);
//			//		}

//			//		// 返回一個簡單的確認響應，表示已接收到請求
//			//		return CFXEnvelope.FromCFXMessage(new EnergyConsumptionResponse
//			//		{
//			//			Result = new CFX.Structures.RequestResult
//			//			{
//			//				Result = CFX.Structures.StatusResult.Success,
//			//				ResultCode = 0,
//			//				Message = "All station responses sent successfully."
//			//			},
//			//			StartTime = DateTime.UtcNow,
//			//			EndTime = DateTime.UtcNow
//			//		});
//			//	}

//			//	return null;
//			//}



//			private CFXEnvelope GenerateEnergyResponse(
//			DateTime startTime,
//			DateTime endTime,
//			double energyUsed,
//			double peakPower,
//			double powerNow,

//			double powerFactorNow,
//			double peakCurrent,
//			double currentNow,
//			double peakVoltage,
//			double voltageNow,

//			double peakFrequency,
//			double frequencyNow
//		)
//		{
//			var response = new EnergyConsumptionResponse
//			{
//				Result = new CFX.Structures.RequestResult
//				{
//					Result = CFX.Structures.StatusResult.Success,
//					ResultCode = 0,
//					Message = "OK"
//				},
//				StartTime = startTime,
//				EndTime = endTime,
//				EnergyUsed = energyUsed,
//				PeakPower = peakPower,
//				PowerNow = powerNow,

//				PowerFactorNow = powerFactorNow,
//				PeakCurrent = peakCurrent,
//				CurrentNow = currentNow,
//				PeakVoltage = peakVoltage,
//				VoltageNow = voltageNow,

//				PeakFrequency = peakFrequency,
//				FrequencyNow = frequencyNow
//			};

//			return CFXEnvelope.FromCFXMessage(response);
//		}





//			//private void PublishCFXMessage(CFXEnvelope envelope)
//			//{
//			//	try
//			//	{
//			//		AmqpCFXEndpoint endpoint = new AmqpCFXEndpoint(); // 創建 CFX 端點
//			//		endpoint.Open("StationEndpoint", new Uri(endpointUri)); // 打開端點
//			//		endpoint.AddPublishChannel(new Uri(publishChannelUri), "event"); // 添加發布頻道

//			//		// 發布 CFX 消息
//			//		endpoint.Publish(envelope); // 直接發送整個 CFXEnvelope 對象
//			//		Console.WriteLine($"Message for station published: {envelope.ToJson()}");

//			//		endpoint.Close(); // 關閉端點
//			//	}
//			//	catch (Exception ex)
//			//	{
//			//		Console.WriteLine($"Error publishing message: {ex.Message}");
//			//	}
//			//}


//		//	private EnergyConsumptionResponse GenerateEnergyResponse(
//		//byte stationId,
//		//DateTime timestamp,
//		//Dictionary<string, int> data)
//		//	{
//		//		return new EnergyConsumptionResponse
//		//		{
//		//			Result = new CFX.Structures.RequestResult
//		//			{
//		//				Result = CFX.Structures.StatusResult.Success,
//		//				ResultCode = 0,
//		//				Message = $"Station {stationId} Data Retrieved Successfully"
//		//			},
//		//			StartTime = timestamp,
//		//			EndTime = DateTime.UtcNow,
//		//			EnergyUsed = data.ContainsKey("電能 (kWh)") ? data["電能 (kWh)"] / 1000.0 : 0, // 電能
//		//			PeakPower = data.ContainsKey("當前總有功功率 (W)") ? data["當前總有功功率 (W)"] / 1000.0 : 0, // 最大功率
//		//			MinimumPower = 0, // 無對應數據
//		//			MeanPower = 0, // 無對應數據
//		//			PowerNow = data.ContainsKey("當前總有功功率 (W)") ? data["當前總有功功率 (W)"] / 1000.0 : 0, // 當前功率
//		//			PowerFactorNow = data.ContainsKey("A相功率因數") ? data["A相功率因數"] / 100.0 : 0, // 功率因數
//		//			PeakCurrent = 0, // 無對應數據
//		//			MinimumCurrent = 0, // 無對應數據
//		//			MeanCurrent = 0, // 無對應數據
//		//			CurrentNow = data.ContainsKey("A相電流 (A)") ? data["A相電流 (A)"] / 100.0 : 0, // 當前電流
//		//			PeakVoltage = 0, // 無對應數據
//		//			MinimumVoltage = 0, // 無對應數據
//		//			MeanVoltage = 0, // 無對應數據
//		//			VoltageNow = data.ContainsKey("A相電壓 (V)") ? data["A相電壓 (V)"] / 10.0 : 0, // 當前電壓
//		//			PeakFrequency = data.ContainsKey("當前線頻率 (Hz)") ? data["當前線頻率 (Hz)"] : 0, // 最大頻率
//		//			MinimumFrequency = 0, // 無對應數據
//		//			MeanFrequency = 0, // 無對應數據
//		//			FrequencyNow = data.ContainsKey("當前線頻率 (Hz)") ? data["當前線頻率 (Hz)"] : 0, // 當前頻率
//		//			PeakPowerRYB = new List<double>
//		//{
//		//	data.ContainsKey("A相有功功率 (W)") ? data["A相有功功率 (W)"] / 1000.0 : 0,
//		//	data.ContainsKey("B相有功功率 (W)") ? data["B相有功功率 (W)"] / 1000.0 : 0,
//		//	data.ContainsKey("C相有功功率 (W)") ? data["C相有功功率 (W)"] / 1000.0 : 0
//		//},
//		//			MinimumPowerRYB = new List<double> { 0.0, 0.0, 0.0 }, // 無對應數據
//		//			MeanPowerRYB = new List<double> { 0.0, 0.0, 0.0 }, // 無對應數據
//		//			PowerNowRYB = new List<double>
//		//{
//		//	data.ContainsKey("A相有功功率 (W)") ? data["A相有功功率 (W)"] / 1000.0 : 0,
//		//	data.ContainsKey("B相有功功率 (W)") ? data["B相有功功率 (W)"] / 1000.0 : 0,
//		//	data.ContainsKey("C相有功功率 (W)") ? data["C相有功功率 (W)"] / 1000.0 : 0
//		//},
//		//			PowerFactorNowRYB = new List<double>
//		//{
//		//	data.ContainsKey("A相功率因數") ? data["A相功率因數"] / 100.0 : 0,
//		//	data.ContainsKey("B相功率因數") ? data["B相功率因數"] / 100.0 : 0,
//		//	data.ContainsKey("C相功率因數") ? data["C相功率因數"] / 100.0 : 0
//		//},
//		//			PeakCurrentRYB = new List<double> { 0.0, 0.0, 0.0 }, // 無對應數據
//		//			MinimumCurrentRYB = new List<double> { 0.0, 0.0, 0.0 }, // 無對應數據
//		//			MeanCurrentRYB = new List<double> { 0.0, 0.0, 0.0 }, // 無對應數據
//		//			CurrentNowRYB = new List<double>
//		//{
//		//	data.ContainsKey("A相電流 (A)") ? data["A相電流 (A)"] / 100.0 : 0,
//		//	data.ContainsKey("B相電流 (A)") ? data["B相電流 (A)"] / 100.0 : 0,
//		//	data.ContainsKey("C相電流 (A)") ? data["C相電流 (A)"] / 100.0 : 0
//		//},
//		//			PeakVoltageRYB = new List<double> { 0.0, 0.0, 0.0 }, // 無對應數據
//		//			MinimumVoltageRYB = new List<double> { 0.0, 0.0, 0.0 }, // 無對應數據
//		//			MeanVoltageRYB = new List<double> { 0.0, 0.0, 0.0 }, // 無對應數據
//		//			VoltageNowRYB = new List<double>
//		//{
//		//	data.ContainsKey("A相電壓 (V)") ? data["A相電壓 (V)"] / 10.0 : 0,
//		//	data.ContainsKey("B相電壓 (V)") ? data["B相電壓 (V)"] / 10.0 : 0,
//		//	data.ContainsKey("C相電壓 (V)") ? data["C相電壓 (V)"] / 10.0 : 0

//		//},
//		//			ThreePhaseNeutralCurrentNow = 0 // 無對應數據
//		//		};
//		//	}



//		}
//}
