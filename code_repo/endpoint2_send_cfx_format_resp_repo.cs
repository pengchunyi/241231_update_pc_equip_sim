//using System;
//using System.Collections.Generic;
//using System.IO.Ports;
//using System.Threading.Tasks;
//using System.Windows.Forms;
//using CFX;
//using CFX.Transport;
//using CFX.ResourcePerformance;

//namespace AmqpModbusIntegration
//{
//    public class ModbusViewer : Form
//    {
//        private SerialPort serialPort;
//        private ComboBox portSelector;
//        private TextBox stationNumberTextBox;
//        private Button connectButton, readButton;
//        private byte stationNumber = 0xFF;

//        // 各參數變數
//        public int currentStatus1, currentStatus2, leakageCurrent, tempA, tempB, tempC, tempN;
//        public int voltageA, voltageB, voltageC, currentA, currentB, currentC;
//        public int powerFactorA, powerFactorB, powerFactorC, activePowerA, activePowerB, activePowerC;
//        public int reactivePowerA, reactivePowerB, reactivePowerC, breakerTimes, energyHighByte, energyLowByte;
//        public int switchStatus, apparentPowerA, apparentPowerB, apparentPowerC, totalApparentPower, totalActivePower, totalReactivePower;
//        public int combinedPowerFactor, lineFrequency, deviceType, historicalLeakage, historicalCurrentA, historicalCurrentB, historicalCurrentC, lineColorMark;

//        public ModbusViewer()
//        {
//            InitializeUI();
//        }

//        private void InitializeUI()
//        {
//            Text = "Modbus Control";
//            Width = 400;
//            Height = 300;

//            // COM Port Label and Selector
//            var portLabel = new Label { Text = "COM口:", Location = new System.Drawing.Point(10, 10), AutoSize = true };
//            Controls.Add(portLabel);

//            portSelector = new ComboBox { Location = new System.Drawing.Point(70, 10), Width = 100 };
//            portSelector.Items.AddRange(SerialPort.GetPortNames());
//            Controls.Add(portSelector);

//            // Station Number Label and TextBox
//            var stationNumberLabel = new Label { Text = "站號:", Location = new System.Drawing.Point(10, 50), AutoSize = true };
//            Controls.Add(stationNumberLabel);

//            stationNumberTextBox = new TextBox { Location = new System.Drawing.Point(70, 50), Width = 100 };
//            Controls.Add(stationNumberTextBox);

//            // Connect Button
//            connectButton = new Button { Text = "連接", Location = new System.Drawing.Point(200, 10), Width = 80 };
//            connectButton.Click += (s, e) => InitializeSerialPort(portSelector.SelectedItem?.ToString());
//            Controls.Add(connectButton);

//            // Read Button
//            readButton = new Button { Text = "讀取數值", Location = new System.Drawing.Point(200, 50), Width = 80 };
//            readButton.Click += (s, e) => ReadAllParameters();
//            Controls.Add(readButton);
//        }

//        private void InitializeSerialPort(string portName)
//        {
//            if (serialPort != null && serialPort.IsOpen)
//            {
//                serialPort.Close();
//                serialPort.Dispose();
//            }

//            if (!byte.TryParse(stationNumberTextBox.Text, out stationNumber))
//            {
//                MessageBox.Show("請輸入有效的站號 (0-255)");
//                return;
//            }

//            serialPort = new SerialPort(portName, 9600, Parity.None, 8, StopBits.One);
//            try
//            {
//                serialPort.Open();
//                MessageBox.Show($"串口 {portName} 連接成功");
//            }
//            catch (Exception ex)
//            {
//                MessageBox.Show($"串口連接失敗: {ex.Message}");
//            }
//        }

//        public void ReadAllParameters()
//        {
//            if (serialPort != null && serialPort.IsOpen)
//            {
//                byte[] readCommand = { stationNumber, 0x03, 0x00, 0x00, 0x00, 0x30 };
//                ushort crc = CalculateCRC(readCommand);
//                byte[] crcBytes = BitConverter.GetBytes(crc);
//                byte[] fullCommand = new byte[readCommand.Length + 2];
//                Array.Copy(readCommand, fullCommand, readCommand.Length);
//                fullCommand[fullCommand.Length - 2] = crcBytes[0];
//                fullCommand[fullCommand.Length - 1] = crcBytes[1];

//                serialPort.Write(fullCommand, 0, fullCommand.Length);

//                Task.Run(() =>
//                {
//                    try
//                    {
//                        byte[] buffer = new byte[256];
//                        int bytesRead = serialPort.Read(buffer, 0, buffer.Length);

//                        if (bytesRead > 5)
//                        {
//                            // 按照讀取順序依次填入各個變數
//                            currentStatus1 = (buffer[3] << 8) | buffer[4];
//                            currentStatus2 = (buffer[5] << 8) | buffer[6];
//                            leakageCurrent = (buffer[7] << 8) | buffer[8];
//                            tempA = (buffer[9] << 8) | buffer[10];
//                            tempB = (buffer[11] << 8) | buffer[12];
//                            tempC = (buffer[13] << 8) | buffer[14];
//                            tempN = (buffer[15] << 8) | buffer[16];
//                            voltageA = (buffer[17] << 8) | buffer[18];
//                            voltageB = (buffer[19] << 8) | buffer[20];
//                            voltageC = (buffer[21] << 8) | buffer[22];
//                            currentA = (buffer[23] << 8) | buffer[24];
//                            currentB = (buffer[25] << 8) | buffer[26];
//                            currentC = (buffer[27] << 8) | buffer[28];
//                            powerFactorA = (buffer[29] << 8) | buffer[30];
//                            powerFactorB = (buffer[31] << 8) | buffer[32];
//                            powerFactorC = (buffer[33] << 8) | buffer[34];
//                            activePowerA = (buffer[35] << 8) | buffer[36];
//                            activePowerB = (buffer[37] << 8) | buffer[38];
//                            activePowerC = (buffer[39] << 8) | buffer[40];
//                            reactivePowerA = (buffer[41] << 8) | buffer[42];
//                            reactivePowerB = (buffer[43] << 8) | buffer[44];
//                            reactivePowerC = (buffer[45] << 8) | buffer[46];
//                            breakerTimes = (buffer[47] << 8) | buffer[48];
//                            energyHighByte = (buffer[49] << 8) | buffer[50];
//                            energyLowByte = (buffer[51] << 8) | buffer[52];
//                            switchStatus = (buffer[53] << 8) | buffer[54];
//                            apparentPowerA = (buffer[55] << 8) | buffer[56];
//                            apparentPowerB = (buffer[57] << 8) | buffer[58];
//                            apparentPowerC = (buffer[59] << 8) | buffer[60];
//                            totalApparentPower = (buffer[61] << 8) | buffer[62];
//                            totalActivePower = (buffer[63] << 8) | buffer[64];
//                            totalReactivePower = (buffer[65] << 8) | buffer[66];
//                            combinedPowerFactor = (buffer[67] << 8) | buffer[68];
//                            lineFrequency = (buffer[69] << 8) | buffer[70];
//                            deviceType = (buffer[71] << 8) | buffer[72];
//                            historicalLeakage = (buffer[73] << 8) | buffer[74];
//                            historicalCurrentA = (buffer[75] << 8) | buffer[76];
//                            historicalCurrentB = (buffer[77] << 8) | buffer[78];
//                            historicalCurrentC = (buffer[79] << 8) | buffer[80];
//                            lineColorMark = (buffer[81] << 8) | buffer[82];
//                        }
//                    }
//                    catch (Exception ex)
//                    {
//                        Console.WriteLine($"讀取參數時發生錯誤: {ex.Message}");
//                    }
//                });
//            }
//        }

//        private ushort CalculateCRC(byte[] data)
//        {
//            ushort crc = 0xFFFF;
//            for (int pos = 0; pos < data.Length; pos++)
//            {
//                crc ^= (ushort)data[pos];
//                for (int i = 8; i != 0; i--)
//                {
//                    if ((crc & 0x0001) != 0)
//                    {
//                        crc >>= 1;
//                        crc ^= 0xA001;
//                    }
//                    else
//                    {
//                        crc >>= 1;
//                    }
//                }
//            }
//            return crc;
//        }
//    }

//    internal class Program
//    {
//        static ModbusViewer modbusViewer = new ModbusViewer();

//        [STAThread]
//        static void Main()
//        {
//            Application.Run(modbusViewer);
//            AmqpCFXEndpoint endpoint = new AmqpCFXEndpoint();
//            string handle = "endpoint2";
//            endpoint.Open(handle, new Uri("amqp://127.0.0.1:8888"));
//            endpoint.AddPublishChannel(new Uri("amqp://127.0.0.1:6666"), "event");
//            endpoint.Publish(new EndpointConnected());
//            Console.WriteLine("endpoint2 publish EndpointConnected\n");

//            endpoint.AddSubscribeChannel(new AmqpChannelAddress()
//            {
//                Address = "MessageSource",
//                Uri = new Uri("amqp://127.0.0.1:6666")
//            });

//            endpoint.OnRequestReceived += OnRequestReceived;
//            Console.ReadKey();
//        }

//        static CFXEnvelope OnRequestReceived(CFXEnvelope request)
//        {
//            Console.WriteLine("endpoint2 OnRequestReceived\n");
//            Console.WriteLine(request.ToJson());
//            Console.WriteLine("\n");

//            if (request.MessageBody is EnergyConsumptionRequest)
//            {
//                Console.WriteLine("Received EnergyConsumptionRequest, sending EnergyConsumptionResponse...\n");

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

//                CFXEnvelope result = CFXEnvelope.FromCFXMessage(response);
//                return result;
//            }

//            return null;
//        }
//    }
//}







//using System;
//using System.Collections.Generic;
//using System.IO.Ports;
//using System.Threading.Tasks;
//using System.Windows.Forms;
//using CFX;
//using CFX.Transport;
//using CFX.ResourcePerformance;

//namespace AmqpModbusIntegration
//{
//    public class ModbusViewer : Form
//    {
//        private SerialPort serialPort;
//        private ComboBox portSelector;
//        private TextBox stationNumberTextBox;
//        private Button connectButton, readButton;
//        private byte stationNumber = 0xFF;

//        // 各參數變數
//        public int currentStatus1, currentStatus2, leakageCurrent, tempA, tempB, tempC, tempN;
//        public int voltageA, voltageB, voltageC, currentA, currentB, currentC;

//        private void ModbusViewer_Load(object sender, EventArgs e)
//        {

//        }

//        private void InitializeComponent()
//        {
//            this.SuspendLayout();
//            // 
//            // ModbusViewer
//            // 
//            this.ClientSize = new System.Drawing.Size(910, 530);
//            this.Name = "ModbusViewer";
//            this.Load += new System.EventHandler(this.ModbusViewer_Load);
//            this.ResumeLayout(false);

//        }

//        public int powerFactorA, powerFactorB, powerFactorC, activePowerA, activePowerB, activePowerC;
//        public int reactivePowerA, reactivePowerB, reactivePowerC, breakerTimes, energyHighByte, energyLowByte;
//        public int switchStatus, apparentPowerA, apparentPowerB, apparentPowerC, totalApparentPower, totalActivePower, totalReactivePower;
//        public int combinedPowerFactor, lineFrequency, deviceType, historicalLeakage, historicalCurrentA, historicalCurrentB, historicalCurrentC;

//        public ModbusViewer()
//        {
//            InitializeUI();
//        }

//        private void InitializeUI()
//        {
//            Text = "Modbus Control";
//            Width = 400;
//            Height = 300;

//            var portLabel = new Label { Text = "COM口:", Location = new System.Drawing.Point(10, 10), AutoSize = true };
//            Controls.Add(portLabel);

//            portSelector = new ComboBox { Location = new System.Drawing.Point(70, 10), Width = 100 };
//            portSelector.Items.AddRange(SerialPort.GetPortNames());
//            Controls.Add(portSelector);

//            var stationNumberLabel = new Label { Text = "站號:", Location = new System.Drawing.Point(10, 50), AutoSize = true };
//            Controls.Add(stationNumberLabel);

//            stationNumberTextBox = new TextBox { Location = new System.Drawing.Point(70, 50), Width = 100 };
//            Controls.Add(stationNumberTextBox);

//            connectButton = new Button { Text = "連接", Location = new System.Drawing.Point(200, 10), Width = 80 };
//            connectButton.Click += (s, e) => InitializeSerialPort(portSelector.SelectedItem?.ToString());
//            Controls.Add(connectButton);

//            readButton = new Button { Text = "讀取數值", Location = new System.Drawing.Point(200, 50), Width = 80 };
//            readButton.Click += (s, e) => ReadAllParameters();
//            Controls.Add(readButton);
//        }

//        private void InitializeSerialPort(string portName)
//        {
//            if (serialPort != null && serialPort.IsOpen)
//            {
//                serialPort.Close();
//                serialPort.Dispose();
//            }

//            if (!byte.TryParse(stationNumberTextBox.Text, out stationNumber))
//            {
//                MessageBox.Show("請輸入有效的站號 (0-255)");
//                return;
//            }

//            serialPort = new SerialPort(portName, 9600, Parity.None, 8, StopBits.One);
//            try
//            {
//                serialPort.Open();
//                MessageBox.Show($"串口 {portName} 連接成功");
//            }
//            catch (Exception ex)
//            {
//                MessageBox.Show($"串口連接失敗: {ex.Message}");
//            }
//        }

//        public void ReadAllParameters()
//        {
//            if (serialPort != null && serialPort.IsOpen)
//            {
//                byte[] readCommand = { stationNumber, 0x03, 0x00, 0x00, 0x00, 0x30 };
//                ushort crc = CalculateCRC(readCommand);
//                byte[] crcBytes = BitConverter.GetBytes(crc);
//                byte[] fullCommand = new byte[readCommand.Length + 2];
//                Array.Copy(readCommand, fullCommand, readCommand.Length);
//                fullCommand[fullCommand.Length - 2] = crcBytes[0];
//                fullCommand[fullCommand.Length - 1] = crcBytes[1];

//                serialPort.Write(fullCommand, 0, fullCommand.Length);

//                Task.Run(() =>
//                {
//                    try
//                    {
//                        byte[] buffer = new byte[256];
//                        int bytesRead = serialPort.Read(buffer, 0, buffer.Length);

//                        if (bytesRead > 5)
//                        {
//                            currentStatus1 = (buffer[3] << 8) | buffer[4];          // 當前狀態1
//                            currentStatus2 = (buffer[5] << 8) | buffer[6];          // 當前狀態2
//                            leakageCurrent = (buffer[7] << 8) | buffer[8];          // 當前漏電值
//                            tempA = (buffer[9] << 8) | buffer[10];                  // 當前A相溫度
//                            tempB = (buffer[11] << 8) | buffer[12];                 // 當前B相溫度
//                            tempC = (buffer[13] << 8) | buffer[14];                 // 當前C相溫度
//                            tempN = (buffer[15] << 8) | buffer[16];                 // 當前N線溫度
//                            voltageA = (buffer[17] << 8) | buffer[18];              // 當前A相電壓
//                            voltageB = (buffer[19] << 8) | buffer[20];              // 當前B相電壓
//                            voltageC = (buffer[21] << 8) | buffer[22];              // 當前C相電壓
//                            currentA = (buffer[23] << 8) | buffer[24];              // 當前A相電流
//                            currentB = (buffer[25] << 8) | buffer[26];              // 當前B相電流
//                            currentC = (buffer[27] << 8) | buffer[28];              // 當前C相電流
//                            powerFactorA = (buffer[29] << 8) | buffer[30];          // 當前A相功率因數
//                            powerFactorB = (buffer[31] << 8) | buffer[32];          // 當前B相功率因數
//                            powerFactorC = (buffer[33] << 8) | buffer[34];          // 當前C相功率因數
//                            activePowerA = (buffer[35] << 8) | buffer[36];          // 當前A相有功功率
//                            activePowerB = (buffer[37] << 8) | buffer[38];          // 當前B相有功功率
//                            activePowerC = (buffer[39] << 8) | buffer[40];          // 當前C相有功功率
//                            reactivePowerA = (buffer[41] << 8) | buffer[42];        // 當前A相無功功率
//                            reactivePowerB = (buffer[43] << 8) | buffer[44];        // 當前B相無功功率
//                            reactivePowerC = (buffer[45] << 8) | buffer[46];        // 當前C相無功功率
//                            breakerTimes = (buffer[47] << 8) | buffer[48];          // 合閘次數
//                            energyHighByte = (buffer[49] << 8) | buffer[50];        // 電能高位
//                            energyLowByte = (buffer[51] << 8) | buffer[52];         // 電能低位
//                            switchStatus = (buffer[53] << 8) | buffer[54];          // 開關狀態
//                            apparentPowerA = (buffer[55] << 8) | buffer[56];        // 當前A相視在功率
//                            apparentPowerB = (buffer[57] << 8) | buffer[58];        // 當前B相視在功率
//                            apparentPowerC = (buffer[59] << 8) | buffer[60];        // 當前C相視在功率
//                            totalApparentPower = (buffer[61] << 8) | buffer[62];    // 當前總視在功率
//                            totalActivePower = (buffer[63] << 8) | buffer[64];      // 當前總有功功率
//                            totalReactivePower = (buffer[65] << 8) | buffer[66];    // 當前總無功功率
//                            combinedPowerFactor = (buffer[67] << 8) | buffer[68];   // 當前合相功率因數
//                            lineFrequency = (buffer[69] << 8) | buffer[70];         // 當前線頻率
//                            deviceType = (buffer[71] << 8) | buffer[72];            // 設備類型
//                            historicalLeakage = (buffer[73] << 8) | buffer[74];     // 歷史報警時漏電值
//                            historicalCurrentA = (buffer[75] << 8) | buffer[76];    // 歷史報警時A相電流
//                            historicalCurrentB = (buffer[77] << 8) | buffer[78];    // 歷史報警時B相電流
//                            historicalCurrentC = (buffer[79] << 8) | buffer[80];    // 歷史報警時C相電流
//                        }

//                    }
//                    catch (Exception ex)
//                    {
//                        Console.WriteLine($"讀取參數時發生錯誤: {ex.Message}");
//                    }
//                });
//            }
//        }

//        private ushort CalculateCRC(byte[] data)
//        {
//            ushort crc = 0xFFFF;
//            for (int pos = 0; pos < data.Length; pos++)
//            {
//                crc ^= (ushort)data[pos];
//                for (int i = 8; i != 0; i--)
//                {
//                    if ((crc & 0x0001) != 0)
//                    {
//                        crc >>= 1;
//                        crc ^= 0xA001;
//                    }
//                    else
//                    {
//                        crc >>= 1;
//                    }
//                }
//            }
//            return crc;
//        }
//    }

//    internal class Program
//    {
//        static ModbusViewer modbusViewer = new ModbusViewer();

//        [STAThread]
//        static void Main()
//        {
//            Task.Run(() => StartAmqpEndpoint());  // 將AMQP Endpoint 啟動在非同步任務中
//            Application.Run(modbusViewer);        // 啟動UI主線程
//        }

//        static void StartAmqpEndpoint()
//        {
//            AmqpCFXEndpoint endpoint = new AmqpCFXEndpoint();
//            string handle = "endpoint2";
//            endpoint.Open(handle, new Uri("amqp://127.0.0.1:8888"));
//            endpoint.AddPublishChannel(new Uri("amqp://127.0.0.1:6666"), "event");
//            endpoint.Publish(new EndpointConnected());
//            Console.WriteLine("endpoint2 publish EndpointConnected\n");

//            endpoint.AddSubscribeChannel(new AmqpChannelAddress()
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

//                CFXEnvelope result = CFXEnvelope.FromCFXMessage(response);
//                return result;
//            }

//            return null;
//        }
//    }
//}