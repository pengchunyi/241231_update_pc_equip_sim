//using AmqpModbusIntegration;
//using Confluent.Kafka;
//using Newtonsoft.Json;
//using System.Threading.Tasks;
//using System;

//public class KafkaManager
//{
//	private readonly IProducer<string, string> kafkaProducer;
//	private readonly string kafkaTopic;
//	private readonly ModbusViewer modbusViewer;

//	public KafkaManager(IProducer<string, string> kafkaProducer, string kafkaTopic, ModbusViewer modbusViewer)
//	{
//		this.kafkaProducer = kafkaProducer;
//		this.kafkaTopic = kafkaTopic;
//		this.modbusViewer = modbusViewer;
//	}

//	public async Task StartPublishingAsync()
//	{
//		Console.WriteLine("開始通過 Kafka 傳輸數據...");
//		while (true)
//		{
//			try
//			{
//				var slaveData = modbusViewer.GetSlaveData();

//				foreach (var stationNumber in slaveData.Keys)
//				{
//					if (!slaveData.TryGetValue(stationNumber, out var data))
//						continue;

//					var kafkaMessage = new
//					{
//						StationNumber = stationNumber,
//						Timestamp = DateTime.Now,
//						Data = data
//					};

//					string jsonMessage = JsonConvert.SerializeObject(kafkaMessage, Formatting.Indented);

//					await kafkaProducer.ProduceAsync(kafkaTopic, new Message<string, string>
//					{
//						Key = stationNumber.ToString(),
//						Value = jsonMessage
//					});

//					Console.WriteLine($"已發送數據到 Kafka: {jsonMessage}");
//				}

//				await Task.Delay(1000); // 每秒發送一次
//			}
//			catch (Exception ex)
//			{
//				Console.WriteLine($"發送數據到 Kafka 時出現錯誤: {ex.Message}");
//			}
//		}
//	}
//}
