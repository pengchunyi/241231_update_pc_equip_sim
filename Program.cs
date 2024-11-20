//using System;
//using System.Threading.Tasks;
//using System.Windows.Forms;
//using CFX;
//using CFX.ResourcePerformance;
//using CFX.Transport;

//namespace AmqpModbusIntegration
//{
//    internal class Program
//    {
//        static ModbusViewer modbusViewer = new ModbusViewer();

//        [System.Runtime.InteropServices.DllImport("kernel32.dll")]
//        private static extern bool AllocConsole();

//        [STAThread]
//        static void Main()
//        {
//            AllocConsole(); // 分配控制台窗口
//            Task.Run(() => StartAmqpEndpoint()); // 啟動 AMQP 端點
//            Application.Run(modbusViewer); // 啟動 Modbus Viewer 窗口
//        }

//        static void StartAmqpEndpoint()
//        {
//            AmqpCFXEndpoint endpoint = new AmqpCFXEndpoint();
//            string handle = "CHUNYI_PC";
//            endpoint.Open(handle, new Uri("amqp://127.0.0.1:8888"));
//            endpoint.AddPublishChannel(new Uri("amqp://127.0.0.1:6666"), "event");

//            endpoint.Publish(new EndpointConnected());
//            Console.WriteLine("endpoint2 publish EndpointConnected\n");

//            endpoint.AddSubscribeChannel(new AmqpChannelAddress
//            {
//                Address = "MessageSource",
//                Uri = new Uri("amqp://127.0.0.1:6666")
//            });

//            endpoint.OnRequestReceived += OnRequestReceived;
//        }

//        static CFXEnvelope OnRequestReceived(CFXEnvelope request)
//        {
//            if (request.MessageBody is EnergyConsumptionRequest)
//            {
//                modbusViewer.ReadAllParameters();

//                var response = new EnergyConsumptionResponse
//                {
//                    Result = new CFX.Structures.RequestResult
//                    {
//                        Result = CFX.Structures.StatusResult.Success,
//                        ResultCode = 0,
//                        Message = "OK"
//                    },
//                    StartTime = DateTime.Now,
//                    EndTime = DateTime.Now,
//                    EnergyUsed = modbusViewer.energyHighByte + modbusViewer.energyLowByte,
//                    PeakPower = modbusViewer.totalApparentPower,
//                    PowerNow = modbusViewer.totalActivePower,
//                    PowerFactorNow = modbusViewer.combinedPowerFactor / 100.0,
//                    PeakCurrent = Math.Max(modbusViewer.currentA, Math.Max(modbusViewer.currentB, modbusViewer.currentC)) / 100.0,
//                    CurrentNow = modbusViewer.currentA / 100.0,
//                    PeakVoltage = Math.Max(modbusViewer.voltageA, Math.Max(modbusViewer.voltageB, modbusViewer.voltageC)) / 10.0,
//                    VoltageNow = modbusViewer.voltageA / 10.0,
//                    PeakFrequency = modbusViewer.lineFrequency / 10.0,
//                    FrequencyNow = modbusViewer.lineFrequency / 10.0
//                };

//                return CFXEnvelope.FromCFXMessage(response);
//            }

//            return null;
//        }
//    }
//}
