using System;
using System.Collections.Generic;
using System.IO.Ports;
using System.Threading.Tasks;
using CFX;
using CFX.ResourcePerformance;
using CFX.Transport;

namespace AmqpModbusIntegration
{
    public class AmqpEndpointManager
    {
        private readonly string endpointUri;
        private readonly string publishChannelUri;
        private readonly string subscribeChannelUri;
        private readonly ModbusViewer modbusViewer;

        //20241120
        private readonly SerialPort serialPort;
        private readonly Dictionary<byte, Dictionary<string, int>> slaveData;


        public AmqpEndpointManager(
       string endpointUri,
       string publishChannelUri,
       string subscribeChannelUri,
       ModbusViewer modbusViewer,
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

            AmqpCFXEndpoint endpoint = new AmqpCFXEndpoint(); // 創建CFX端點
            endpoint.Open(endpointName, new Uri(endpointUri)); // 打開端點並連接URI

            // 添加發布頻道
            endpoint.AddPublishChannel(new Uri(publishChannelUri), "event");

            // 發布連接消息
            endpoint.Publish(new EndpointConnected());
            Console.WriteLine($"AMQP端點 \"{endpointName}\" 已成功連接並發佈消息");

            // 添加訂閱頻道，用於接收消息
            endpoint.AddSubscribeChannel(new AmqpChannelAddress
            {
                Address = "MessageSource",
                Uri = new Uri(subscribeChannelUri)
            });

            // 註冊接收到請求時的回調
            endpoint.OnRequestReceived += (request) =>
            {
                return OnRequestReceivedAsync(request).GetAwaiter().GetResult();
            };
        }

        private async Task<CFXEnvelope> OnRequestReceivedAsync(CFXEnvelope request)
        {
            if (request.MessageBody is EnergyConsumptionRequest)
            {
                //await modbusViewer.ReadAllParametersAsync(); // 調用 ModbusViewer 的方法來讀取參數
                //await ModbusHelper.ReadAllParametersAsync(serialPort, slaveData);
                await ModbusHelper.ReadAllParametersAsync(serialPort, modbusViewer);




                // 生成回應消息的參數
                DateTime startTime = DateTime.Now;
                DateTime endTime = DateTime.Now;

                double energyUsed = modbusViewer.energyHighByte + modbusViewer.energyLowByte;
                double peakPower = modbusViewer.totalApparentPower;
                double powerNow = modbusViewer.totalActivePower;
                double powerFactorNow = modbusViewer.combinedPowerFactor ;
                double peakCurrent = Math.Max(modbusViewer.currentA, Math.Max(modbusViewer.currentB, modbusViewer.currentC)) ;
                double currentNow = modbusViewer.currentA ;
                double peakVoltage = Math.Max(modbusViewer.voltageA, Math.Max(modbusViewer.voltageB, modbusViewer.voltageC)) ;
                double voltageNow = modbusViewer.voltageA ;
                double peakFrequency = modbusViewer.lineFrequency ;
                double frequencyNow = modbusViewer.lineFrequency ;

                return GenerateEnergyResponse(
                    startTime,
                    endTime,
                    energyUsed,
                    peakPower,
                    powerNow,
                    powerFactorNow,
                    peakCurrent,
                    currentNow,
                    peakVoltage,
                    voltageNow,
                    peakFrequency,
                    frequencyNow
                );
            }

            return null; // 如果不是 EnergyConsumptionRequest，返回 null
        }





        private CFXEnvelope GenerateEnergyResponse(
            DateTime startTime,
            DateTime endTime,
            double energyUsed,
            double peakPower,
            double powerNow,
            double powerFactorNow,
            double peakCurrent,
            double currentNow,
            double peakVoltage,
            double voltageNow,
            double peakFrequency,
            double frequencyNow
        )
        {
            var response = new EnergyConsumptionResponse
            {
                Result = new CFX.Structures.RequestResult
                {
                    Result = CFX.Structures.StatusResult.Success,
                    ResultCode = 0,
                    Message = "OK"
                },
                StartTime = startTime,
                EndTime = endTime,
                EnergyUsed = energyUsed,
                PeakPower = peakPower,
                PowerNow = powerNow,
                PowerFactorNow = powerFactorNow,
                PeakCurrent = peakCurrent,
                CurrentNow = currentNow,
                PeakVoltage = peakVoltage,
                VoltageNow = voltageNow,
                PeakFrequency = peakFrequency,
                FrequencyNow = frequencyNow
            };

            return CFXEnvelope.FromCFXMessage(response);
        }
    }
}
