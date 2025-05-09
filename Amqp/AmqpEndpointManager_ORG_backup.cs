////using System;
////using System.Collections.Generic;
////using System.IO.Ports;
////using System.Threading.Tasks;
////using CFX;
////using CFX.ResourcePerformance;
////using CFX.Transport;

////namespace AmqpModbusIntegration
////{
////    public class AmqpEndpointManager
////    {
////        private readonly string endpointUri;
////        private readonly string publishChannelUri;
////        private readonly string subscribeChannelUri;
////        private readonly ModbusViewer modbusViewer;

////        //20241120
////        private readonly SerialPort serialPort;
////        private readonly Dictionary<byte, Dictionary<string, int>> slaveData;


////        public AmqpEndpointManager(
////       string endpointUri,
////       string publishChannelUri,
////       string subscribeChannelUri,
////       ModbusViewer modbusViewer,
////       SerialPort serialPort,
////       Dictionary<byte, Dictionary<string, int>> slaveData)
////        {
////            this.endpointUri = endpointUri;
////            this.publishChannelUri = publishChannelUri;
////            this.subscribeChannelUri = subscribeChannelUri;
////            this.modbusViewer = modbusViewer;
////            this.serialPort = serialPort;
////            this.slaveData = slaveData;
////        }

////        public void StartAmqpEndpoint(string endpointName)
////        {
////            if (string.IsNullOrEmpty(endpointName))
////                throw new ArgumentException("Endpoint name cannot be null or empty", nameof(endpointName));

////            AmqpCFXEndpoint endpoint = new AmqpCFXEndpoint(); // 創建CFX端點
////            endpoint.Open(endpointName, new Uri(endpointUri)); // 打開端點並連接URI

////            // 添加發布頻道
////            endpoint.AddPublishChannel(new Uri(publishChannelUri), "event");

////            // 發布連接消息
////            endpoint.Publish(new EndpointConnected());
////            Console.WriteLine($"AMQP端點 \"{endpointName}\" 已成功連接並發佈消息");

////            // 添加訂閱頻道，用於接收消息
////            endpoint.AddSubscribeChannel(new AmqpChannelAddress
////            {
////                Address = "MessageSource",
////                Uri = new Uri(subscribeChannelUri)
////            });

////            // 註冊接收到請求時的回調
////            endpoint.OnRequestReceived += (request) =>
////            {
////                return OnRequestReceivedAsync(request).GetAwaiter().GetResult();
////            };
////        }

////        private async Task<CFXEnvelope> OnRequestReceivedAsync(CFXEnvelope request)
////        {
////            if (request.MessageBody is EnergyConsumptionRequest)
////            {
////                //await modbusViewer.ReadAllParametersAsync(); // 調用 ModbusViewer 的方法來讀取參數
////                //await ModbusHelper.ReadAllParametersAsync(serialPort, slaveData);
////                await ModbusHelper.ReadAllParametersAsync(serialPort, modbusViewer);




////                // 生成回應消息的參數
////                DateTime startTime = DateTime.Now;
////                DateTime endTime = DateTime.Now;

////                double energyUsed = modbusViewer.energyHighByte + modbusViewer.energyLowByte;
////                double peakPower = modbusViewer.totalApparentPower;
////                double powerNow = modbusViewer.totalActivePower;
////                double powerFactorNow = modbusViewer.combinedPowerFactor ;
////                double peakCurrent = Math.Max(modbusViewer.currentA, Math.Max(modbusViewer.currentB, modbusViewer.currentC)) ;
////                double currentNow = modbusViewer.currentA ;
////                double peakVoltage = Math.Max(modbusViewer.voltageA, Math.Max(modbusViewer.voltageB, modbusViewer.voltageC)) ;
////                double voltageNow = modbusViewer.voltageA ;
////                double peakFrequency = modbusViewer.lineFrequency ;
////                double frequencyNow = modbusViewer.lineFrequency ;

////                return GenerateEnergyResponse(
////                    startTime,
////                    endTime,
////                    energyUsed,
////                    peakPower,
////                    powerNow,
////                    powerFactorNow,
////                    peakCurrent,
////                    currentNow,
////                    peakVoltage,
////                    voltageNow,
////                    peakFrequency,
////                    frequencyNow
////                );
////            }

////            return null; // 如果不是 EnergyConsumptionRequest，返回 null
////        }





////        private CFXEnvelope GenerateEnergyResponse(
////            DateTime startTime,
////            DateTime endTime,
////            double energyUsed,
////            double peakPower,
////            double powerNow,
////            double powerFactorNow,
////            double peakCurrent,
////            double currentNow,
////            double peakVoltage,
////            double voltageNow,
////            double peakFrequency,
////            double frequencyNow
////        )
////        {
////            var response = new EnergyConsumptionResponse
////            {
////                Result = new CFX.Structures.RequestResult
////                {
////                    Result = CFX.Structures.StatusResult.Success,
////                    ResultCode = 0,
////                    Message = "OK"
////                },
////                StartTime = startTime,
////                EndTime = endTime,
////                EnergyUsed = energyUsed,
////                PeakPower = peakPower,
////                PowerNow = powerNow,
////                PowerFactorNow = powerFactorNow,
////                PeakCurrent = peakCurrent,
////                CurrentNow = currentNow,
////                PeakVoltage = peakVoltage,
////                VoltageNow = voltageNow,
////                PeakFrequency = peakFrequency,
////                FrequencyNow = frequencyNow
////            };

////            return CFXEnvelope.FromCFXMessage(response);
////        }
////    }
////}


////20241123_使用publish模式，已經可以發兩條了，但是兩條數值一模一樣=================================
//using System;
//using System.Collections.Generic;
//using System.IO.Ports;
//using System.Threading.Tasks;
//using CFX;
//using CFX.ResourcePerformance;
//using CFX.Transport;

//namespace AmqpModbusIntegration
//{
//	public class AmqpEndpointManager
//	{
//		private readonly string endpointUri;
//		private readonly string publishChannelUri;
//		private readonly string subscribeChannelUri;
//		private readonly ModbusViewer modbusViewer;

//		//20241120
//		private readonly SerialPort serialPort;
//		private readonly Dictionary<byte, Dictionary<string, int>> slaveData;

//		public AmqpEndpointManager(
//	   string endpointUri,
//	   string publishChannelUri,
//	   string subscribeChannelUri,
//	   ModbusViewer modbusViewer,
//	   SerialPort serialPort,
//	   Dictionary<byte, Dictionary<string, int>> slaveData)
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

//		//private async Task PublishEnergyConsumptionResponses(AmqpCFXEndpoint endpoint)
//		//{
//		//	try
//		//	{
//		//		// Read parameters from the Modbus device
//		//		await ModbusHelper.ReadAllParametersAsync(serialPort, modbusViewer);

//		//		// Create multiple EnergyConsumptionResponse messages
//		//		var response1 = CreateEnergyConsumptionResponse();
//		//		var response2 = CreateEnergyConsumptionResponse();

//		//		// Add responses to a list
//		//		var responses = new List<CFXMessage> { response1, response2 };

//		//		// Publish multiple messages using PublishMany
//		//		endpoint.PublishMany(responses);
//		//		Console.WriteLine("Published multiple EnergyConsumptionResponse messages.");
//		//	}
//		//	catch (Exception ex)
//		//	{
//		//		Console.WriteLine($"Error publishing EnergyConsumptionResponse messages: {ex.Message}");
//		//	}
//		//}
//		private async Task PublishEnergyConsumptionResponses(AmqpCFXEndpoint endpoint)
//		{
//			try
//			{
//				// 讀取所有站號的參數
//				await ModbusHelper.ReadAllParametersAsync(serialPort, modbusViewer);

//				// 創建要發佈的訊息列表
//				var responses = new List<CFXMessage>();

//				// 假設有兩個站號，分別為 1 和 2
//				byte[] stationNumbers = { 1, 2 };

//				foreach (var stationNumber in stationNumbers)
//				{
//					var response = CreateEnergyConsumptionResponse(stationNumber);
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






//		//private EnergyConsumptionResponse CreateEnergyConsumptionResponse()
//		//{
//		//	return new EnergyConsumptionResponse
//		//	{
//		//		Result = new CFX.Structures.RequestResult
//		//		{
//		//			Result = CFX.Structures.StatusResult.Success,
//		//			ResultCode = 0,
//		//			Message = "OK"
//		//		},
//		//		StartTime = DateTime.Now,
//		//		EndTime = DateTime.Now,
//		//		EnergyUsed = modbusViewer.energyHighByte + modbusViewer.energyLowByte,
//		//		PeakPower = modbusViewer.totalApparentPower,
//		//		PowerNow = modbusViewer.totalActivePower,
//		//		PowerFactorNow = modbusViewer.combinedPowerFactor,
//		//		PeakCurrent = Math.Max(modbusViewer.currentA, Math.Max(modbusViewer.currentB, modbusViewer.currentC)),
//		//		CurrentNow = modbusViewer.currentA,
//		//		PeakVoltage = Math.Max(modbusViewer.voltageA, Math.Max(modbusViewer.voltageB, modbusViewer.voltageC)),
//		//		VoltageNow = modbusViewer.voltageA,
//		//		PeakFrequency = modbusViewer.lineFrequency,
//		//		FrequencyNow = modbusViewer.lineFrequency
//		//	};
//		//}

//		private EnergyConsumptionResponse CreateEnergyConsumptionResponse(byte stationNumber)
//		{
//			// 從 modbusViewer 中取得對應站號的資料
//			if (!modbusViewer.GetSlaveData().TryGetValue(stationNumber, out var slaveData))
//			{
//				Console.WriteLine($"無法找到站號 {stationNumber} 的資料。");
//				return null;
//			}

//			// 創建並返回 EnergyConsumptionResponse
//			return new EnergyConsumptionResponse
//			{
//				Result = new CFX.Structures.RequestResult
//				{
//					Result = CFX.Structures.StatusResult.Success,
//					ResultCode = 0,
//					Message = "OK"
//				},
//				StartTime = DateTime.Now,
//				EndTime = DateTime.Now,
//				EnergyUsed = slaveData.TryGetValue("電能 (kWh)", out var energyUsed) ? energyUsed : 0,
//				PeakPower = slaveData.TryGetValue("當前總有功功率 (W)", out var peakPower) ? peakPower : 0,
//				PowerNow = slaveData.TryGetValue("當前總有功功率 (W)", out var powerNow) ? powerNow : 0,
//				PowerFactorNow = slaveData.TryGetValue("合併功率因數", out var powerFactorNow) ? powerFactorNow : 0,
//				PeakCurrent = Math.Max(
//					slaveData.TryGetValue("A相電流 (A)", out var currentA) ? currentA : 0,
//					Math.Max(
//						slaveData.TryGetValue("B相電流 (A)", out var currentB) ? currentB : 0,
//						slaveData.TryGetValue("C相電流 (A)", out var currentC) ? currentC : 0
//					)
//				),
//				CurrentNow = slaveData.TryGetValue("A相電流 (A)", out var currentNow) ? currentNow : 0,
//				PeakVoltage = Math.Max(
//					slaveData.TryGetValue("A相電壓 (V)", out var voltageA) ? voltageA : 0,
//					Math.Max(
//						slaveData.TryGetValue("B相電壓 (V)", out var voltageB) ? voltageB : 0,
//						slaveData.TryGetValue("C相電壓 (V)", out var voltageC) ? voltageC : 0
//					)
//				),
//				VoltageNow = slaveData.TryGetValue("B相電壓 (V)", out var voltageNow) ? voltageNow : 0,
//				PeakFrequency = slaveData.TryGetValue("當前線頻率 (Hz)", out var frequencyNow) ? frequencyNow : 0,
//				FrequencyNow = frequencyNow
//			};
//		}



//	}
//}
