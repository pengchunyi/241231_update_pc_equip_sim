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
//    public class EnergyConsumptionResponse : CFXMessage
//    {
//        public int SlaveID { get; set; } // 用於區分站號
//        public CFX.Structures.RequestResult Result { get; set; }
//        public DateTime StartTime { get; set; }
//        public DateTime EndTime { get; set; }
//        public double EnergyUsed { get; set; }
//        public double PeakPower { get; set; }
//        public double PowerNow { get; set; }
//        public double PowerFactorNow { get; set; }
//        public double PeakCurrent { get; set; }
//        public double CurrentNow { get; set; }
//        public double PeakVoltage { get; set; }
//        public double VoltageNow { get; set; }
//        public double PeakFrequency { get; set; }
//        public double FrequencyNow { get; set; }
//    }

//    public class EnergyConsumptionRequest : CFXMessage
//    {
//        public int RequestData { get; set; } // 請求中包含站號
//    }

//    public class ModbusViewer : Form
//    {
//        private SerialPort serialPort;
//        private ComboBox portSelector;
//        private TextBox stationNumberTextBox;
//        private Button connectButton, readButton;
//        private DataGridView dataGridView; // 用於顯示數據
//        private System.Windows.Forms.Timer updateTimer;
//        private List<byte> stationNumbers = new List<byte>(); // 支持多站號
//        private Dictionary<byte, Dictionary<string, int>> slaveData = new Dictionary<byte, Dictionary<string, int>>(); // 存儲多站號數據

//        public ModbusViewer()
//        {
//            InitializeUI();
//            InitializeTimer();
//        }


//        public EnergyConsumptionResponse GenerateEnergyConsumptionResponse(byte stationNumber)
//        {
//            // 確保站號數據存在
//            if (!slaveData.ContainsKey(stationNumber))
//                return null;

//            var data = slaveData[stationNumber];

//            // 返回封裝好的回應
//            return new EnergyConsumptionResponse
//            {
//                SlaveID = stationNumber,
//                Result = new CFX.Structures.RequestResult
//                {
//                    Result = CFX.Structures.StatusResult.Success,
//                    ResultCode = 0,
//                    Message = "OK"
//                },
//                StartTime = DateTime.Now,
//                EndTime = DateTime.Now,
//                EnergyUsed = data.ContainsKey("電能") ? data["電能"] / 100.0 : 0.0,
//                PeakPower = data.ContainsKey("總有功功率") ? data["總有功功率"] / 100.0 : 0.0,
//                PowerNow = data.ContainsKey("總有功功率") ? data["總有功功率"] / 100.0 : 0.0,
//                PowerFactorNow = 1.0, // 假設為固定值，可根據需要改為動態數據
//                PeakCurrent = Math.Max(
//                    data.ContainsKey("A相電流") ? data["A相電流"] : 0,
//                    Math.Max(
//                        data.ContainsKey("B相電流") ? data["B相電流"] : 0,
//                        data.ContainsKey("C相電流") ? data["C相電流"] : 0
//                    )
//                ) / 100.0,
//                CurrentNow = data.ContainsKey("A相電流") ? data["A相電流"] / 100.0 : 0.0,
//                PeakVoltage = Math.Max(
//                    data.ContainsKey("A相電壓") ? data["A相電壓"] : 0,
//                    Math.Max(
//                        data.ContainsKey("B相電壓") ? data["B相電壓"] : 0,
//                        data.ContainsKey("C相電壓") ? data["C相電壓"] : 0
//                    )
//                ) / 10.0,
//                VoltageNow = data.ContainsKey("A相電壓") ? data["A相電壓"] / 10.0 : 0.0,
//                PeakFrequency = data.ContainsKey("線頻率") ? data["線頻率"] / 10.0 : 0.0,
//                FrequencyNow = data.ContainsKey("線頻率") ? data["線頻率"] / 10.0 : 0.0
//            };
//        }

//        private void InitializeUI()
//        {
//            Text = "Modbus Control";
//            Width = 800;
//            Height = 600;

//            // COM口選擇
//            var portLabel = new Label { Text = "COM口:", Location = new System.Drawing.Point(10, 10), AutoSize = true };
//            Controls.Add(portLabel);
//            portSelector = new ComboBox { Location = new System.Drawing.Point(70, 10), Width = 100 };
//            portSelector.Items.AddRange(SerialPort.GetPortNames());
//            Controls.Add(portSelector);

//            // 站號輸入框（支持多站號，以逗號分隔）
//            var stationNumberLabel = new Label { Text = "站號(逗號分隔):", Location = new System.Drawing.Point(10, 50), AutoSize = true };
//            Controls.Add(stationNumberLabel);
//            stationNumberTextBox = new TextBox { Location = new System.Drawing.Point(120, 50), Width = 200 };
//            Controls.Add(stationNumberTextBox);

//            // 連接按鈕
//            connectButton = new Button { Text = "連接", Location = new System.Drawing.Point(330, 10), Width = 100 };
//            connectButton.Click += (s, e) => InitializeSerialPort(portSelector.SelectedItem?.ToString());
//            Controls.Add(connectButton);

//            // 讀取數值按鈕
//            readButton = new Button { Text = "讀取數值", Location = new System.Drawing.Point(330, 50), Width = 100 };
//            readButton.Click += (s, e) => UpdateValues();
//            Controls.Add(readButton);

//            // 數據顯示表格
//            dataGridView = new DataGridView
//            {
//                Location = new System.Drawing.Point(10, 100),
//                Width = 760,
//                Height = 400,
//                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
//                ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.AutoSize,
//                AllowUserToAddRows = false
//            };

//            Controls.Add(dataGridView);
//        }


//        private void InitializeTimer()
//        {
//            updateTimer = new System.Windows.Forms.Timer();
//            updateTimer.Interval = 3000; // 每3秒更新一次
//            updateTimer.Tick += (s, e) => UpdateValues();
//            updateTimer.Start();
//        }

//        private void InitializeSerialPort(string portName)
//        {
//            if (serialPort != null && serialPort.IsOpen)
//            {
//                serialPort.Close();
//                serialPort.Dispose();
//            }

//            serialPort = new SerialPort(portName, 9600, Parity.None, 8, StopBits.One);

//            try
//            {
//                serialPort.Open();
//                MessageBox.Show($"串口 {portName} 連接成功");

//                stationNumbers.Clear();
//                foreach (var station in stationNumberTextBox.Text.Split(','))
//                {
//                    if (byte.TryParse(station.Trim(), out var stationNumber))
//                    {
//                        stationNumbers.Add(stationNumber);
//                        slaveData[stationNumber] = new Dictionary<string, int>();
//                    }
//                }
//            }
//            catch (Exception ex)
//            {
//                MessageBox.Show($"串口連接失敗: {ex.Message}");
//            }
//        }





//        private void UpdateValues()
//        {

//            //20241119
//            foreach (var stationNumber in stationNumbers)
//            {
//                for (int switchIndex = 1; switchIndex <= 3; switchIndex++) // 假設處理三個開關
//                {
//                    ReadParameters(stationNumber, switchIndex); // 正確傳遞兩個參數
//                }
//            }

//            UpdateDataGridView(); // 更新表格
//        }
//        // 定義三個字典，分別存放三個開關的數據
//        private Dictionary<byte, Dictionary<string, int>> slaveDataSwitch1 = new Dictionary<byte, Dictionary<string, int>>();
//        private Dictionary<byte, Dictionary<string, int>> slaveDataSwitch2 = new Dictionary<byte, Dictionary<string, int>>();
//        private Dictionary<byte, Dictionary<string, int>> slaveDataSwitch3 = new Dictionary<byte, Dictionary<string, int>>();

//        // 讀取所有參數的方法，針對不同的開關
//        public void ReadParameters(byte stationNumber, int switchIndex)
//        {
//            if (serialPort != null && serialPort.IsOpen) // 確保串口已打開
//            {
//                // 根據開關選擇不同的讀取命令
//                byte[] readCommand = switchIndex switch
//                {
//                    1 => new byte[] { stationNumber, 0x03, 0x00, 0x00, 0x00, 0x30 }, // 第一個開關的讀取命令
//                    2 => new byte[] { stationNumber, 0x03, 0x00, 0x40, 0x00, 0x30 }, // 第二個開關的讀取命令
//                    3 => new byte[] { stationNumber, 0x03, 0x00, 0x80, 0x00, 0x30 }, // 第三個開關的讀取命令
//                    _ => throw new ArgumentException("Invalid switch index") // 無效的開關索引
//                };

//                // 計算CRC校驗碼
//                ushort crc = CalculateCRC(readCommand);
//                byte[] crcBytes = BitConverter.GetBytes(crc);
//                byte[] fullCommand = new byte[readCommand.Length + 2];
//                Array.Copy(readCommand, fullCommand, readCommand.Length);
//                fullCommand[fullCommand.Length - 2] = crcBytes[0];
//                fullCommand[fullCommand.Length - 1] = crcBytes[1];

//                // 發送完整命令
//                serialPort.Write(fullCommand, 0, fullCommand.Length);

//                // 開始新任務，讀取設備回傳數據
//                Task.Run(() =>
//                {
//                    try
//                    {
//                        byte[] buffer = new byte[256];
//                        int bytesRead = serialPort.Read(buffer, 0, buffer.Length);

//                        if (bytesRead > 5) // 如果數據長度有效
//                        {
//                            var data = new Dictionary<string, int>
//                    {
//                        { "A相溫度", (buffer[3] << 8) | buffer[4] },
//                        { "B相溫度", (buffer[5] << 8) | buffer[6] },
//                        { "C相溫度", (buffer[7] << 8) | buffer[8] },
//                        { "A相電壓", (buffer[9] << 8) | buffer[10] },
//                        { "B相電壓", (buffer[11] << 8) | buffer[12] },
//                        { "C相電壓", (buffer[13] << 8) | buffer[14] },
//                        { "A相電流", (buffer[15] << 8) | buffer[16] },
//                        { "B相電流", (buffer[17] << 8) | buffer[18] },
//                        { "C相電流", (buffer[19] << 8) | buffer[20] },
//                        { "開關狀態", (buffer[21] << 8) | buffer[22] },
//                        { "線頻率", (buffer[23] << 8) | buffer[24] },
//                        { "總有功功率", (buffer[25] << 8) | buffer[26] },
//                        { "電能", (buffer[27] << 8) | buffer[28] }
//                    };

//                            // 根據開關索引存入對應的字典
//                            switch (switchIndex)
//                            {
//                                case 1:
//                                    slaveDataSwitch1[stationNumber] = data;
//                                    break;
//                                case 2:
//                                    slaveDataSwitch2[stationNumber] = data;
//                                    break;
//                                case 3:
//                                    slaveDataSwitch3[stationNumber] = data;
//                                    break;
//                            }
//                        }
//                    }
//                    catch (Exception ex)
//                    {
//                        Console.WriteLine($"讀取站號 {stationNumber} 開關 {switchIndex} 時發生錯誤: {ex.Message}");
//                    }
//                });
//            }
//        }



//        private void UpdateDataGridView()
//        {
//            dataGridView.Columns.Clear(); // 清空所有列
//            dataGridView.Rows.Clear();   // 清空所有行

//            // 添加參數名稱列
//            dataGridView.Columns.Add("Parameter", "參數名稱");

//            // 添加每個站號的列
//            foreach (var station in stationNumbers)
//            {
//                dataGridView.Columns.Add($"Slave_{station}", $"站號 {station}");
//            }

//            if (slaveData.Count == 0) return;

//            // 獲取所有參數名稱
//            var parameterNames = new HashSet<string>();
//            foreach (var stationData in slaveData.Values)
//            {
//                foreach (var parameter in stationData.Keys)
//                {
//                    parameterNames.Add(parameter);
//                }
//            }

//            // 填充行數據，每行是一個參數
//            foreach (var parameter in parameterNames)
//            {
//                var row = new List<object> { parameter }; // 第一列為參數名稱

//                // 填充該參數對應的每個站號的值
//                foreach (var station in stationNumbers)
//                {
//                    if (slaveData.TryGetValue(station, out var stationData) && stationData.ContainsKey(parameter))
//                    {
//                        row.Add(stationData[parameter]); // 添加數值
//                    }
//                    else
//                    {
//                        row.Add("N/A"); // 如果該站號沒有此參數，填充 "N/A"
//                    }
//                }

//                dataGridView.Rows.Add(row.ToArray()); // 添加行
//            }

//            dataGridView.RowHeadersVisible = false; // 移除行頭顯示
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
//            Task.Run(() => StartAmqpEndpoint());
//            Application.Run(modbusViewer);
//        }

//        static void StartAmqpEndpoint()
//        {
//            AmqpCFXEndpoint endpoint = new AmqpCFXEndpoint();
//            endpoint.Open("CHUNYI_PC", new Uri("amqp://127.0.0.1:8888"));
//            endpoint.OnRequestReceived += OnRequestReceived;
//        }

//        static CFXEnvelope OnRequestReceived(CFXEnvelope request)
//        {
//            if (request.MessageBody is EnergyConsumptionRequest energyRequest)
//            {
//                var stationNumber = energyRequest.RequestData; // 從請求中提取站號
//                var response = modbusViewer.GenerateEnergyConsumptionResponse((byte)stationNumber);
//                return CFXEnvelope.FromCFXMessage(response);
//            }
//            return null;
//        }
//    }
//}


////using System;
////using System.Collections.Generic;
////using System.IO.Ports;
////using System.Linq;
////using System.Threading.Tasks;
////using System.Windows.Forms;
////using CFX;
////using CFX.Transport;

////namespace AmqpModbusIntegration
////{
////    public class EnergyConsumptionResponse : CFXMessage
////    {
////        public int SlaveID { get; set; }
////        public CFX.Structures.RequestResult Result { get; set; }
////        public DateTime StartTime { get; set; }
////        public DateTime EndTime { get; set; }
////        public double EnergyUsed { get; set; }
////        public double PeakPower { get; set; }
////        public double PowerNow { get; set; }
////        public double PowerFactorNow { get; set; }
////        public double PeakCurrent { get; set; }
////        public double CurrentNow { get; set; }
////        public double PeakVoltage { get; set; }
////        public double VoltageNow { get; set; }
////        public double PeakFrequency { get; set; }
////        public double FrequencyNow { get; set; }
////    }

////    public class EnergyConsumptionRequest : CFXMessage
////    {
////        public int RequestData { get; set; }
////    }

////    public class ModbusViewer : Form
////    {
////        private SerialPort serialPort;
////        private ComboBox portSelector;
////        private TextBox stationNumberTextBox;
////        private Button connectButton, readButton;
////        private DataGridView dataGridView;
////        private System.Windows.Forms.Timer updateTimer;
////        private List<byte> stationNumbers = new List<byte>();

////        // 三個開關的數據字典
////        private Dictionary<byte, Dictionary<string, int>> slaveDataSwitch1 = new();
////        private Dictionary<byte, Dictionary<string, int>> slaveDataSwitch2 = new();
////        private Dictionary<byte, Dictionary<string, int>> slaveDataSwitch3 = new();

////        public ModbusViewer()
////        {
////            InitializeUI();
////            InitializeTimer();
////        }

////        private void InitializeUI()
////        {
////            Text = "Modbus Control";
////            Width = 800;
////            Height = 600;

////            var portLabel = new Label { Text = "COM口:", Location = new System.Drawing.Point(10, 10), AutoSize = true };
////            Controls.Add(portLabel);
////            portSelector = new ComboBox { Location = new System.Drawing.Point(70, 10), Width = 100 };
////            portSelector.Items.AddRange(SerialPort.GetPortNames());
////            Controls.Add(portSelector);

////            var stationNumberLabel = new Label { Text = "站號(逗號分隔):", Location = new System.Drawing.Point(10, 50), AutoSize = true };
////            Controls.Add(stationNumberLabel);
////            stationNumberTextBox = new TextBox { Location = new System.Drawing.Point(120, 50), Width = 200 };
////            Controls.Add(stationNumberTextBox);

////            connectButton = new Button { Text = "連接", Location = new System.Drawing.Point(330, 10), Width = 100 };
////            connectButton.Click += (s, e) => InitializeSerialPort(portSelector.SelectedItem?.ToString());
////            Controls.Add(connectButton);

////            readButton = new Button { Text = "讀取數值", Location = new System.Drawing.Point(330, 50), Width = 100 };
////            readButton.Click += (s, e) => UpdateValues();
////            Controls.Add(readButton);

////            dataGridView = new DataGridView
////            {
////                Location = new System.Drawing.Point(10, 100),
////                Width = 760,
////                Height = 400,
////                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
////                ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.AutoSize,
////                AllowUserToAddRows = false
////            };
////            Controls.Add(dataGridView);
////        }

////        private void InitializeTimer()
////        {
////            updateTimer = new System.Windows.Forms.Timer
////            {
////                Interval = 3000
////            };
////            updateTimer.Tick += (s, e) => UpdateValues();
////            updateTimer.Start();
////        }

////        private void InitializeSerialPort(string portName)
////        {
////            if (serialPort != null && serialPort.IsOpen)
////            {
////                serialPort.Close();
////                serialPort.Dispose();
////            }

////            serialPort = new SerialPort(portName, 9600, Parity.None, 8, StopBits.One);

////            try
////            {
////                serialPort.Open();
////                MessageBox.Show($"串口 {portName} 連接成功");

////                stationNumbers.Clear();
////                foreach (var station in stationNumberTextBox.Text.Split(','))
////                {
////                    if (byte.TryParse(station.Trim(), out var stationNumber))
////                    {
////                        stationNumbers.Add(stationNumber);
////                    }
////                }
////            }
////            catch (Exception ex)
////            {
////                MessageBox.Show($"串口連接失敗: {ex.Message}");
////            }
////        }

////        private void UpdateValues()
////        {
////            foreach (var stationNumber in stationNumbers)
////            {
////                for (int i = 1; i <= 3; i++)
////                {
////                    ReadParameters(stationNumber, i);
////                }
////            }
////            UpdateDataGridView();
////        }

////        private void ReadParameters(byte stationNumber, int switchIndex)
////        {
////            if (serialPort == null || !serialPort.IsOpen) return;

////            byte[] readCommand = switchIndex switch
////            {
////                1 => new byte[] { stationNumber, 0x03, 0x00, 0x00, 0x00, 0x30 },
////                2 => new byte[] { stationNumber, 0x03, 0x00, 0x40, 0x00, 0x30 },
////                3 => new byte[] { stationNumber, 0x03, 0x00, 0x80, 0x00, 0x30 },
////                _ => throw new ArgumentException("Invalid switch index")
////            };

////            ushort crc = CalculateCRC(readCommand);
////            byte[] crcBytes = BitConverter.GetBytes(crc);
////            byte[] fullCommand = new byte[readCommand.Length + 2];
////            Array.Copy(readCommand, fullCommand, readCommand.Length);
////            fullCommand[fullCommand.Length - 2] = crcBytes[0];
////            fullCommand[fullCommand.Length - 1] = crcBytes[1];

////            serialPort.Write(fullCommand, 0, fullCommand.Length);

////            Task.Run(() =>
////            {
////                try
////                {
////                    byte[] buffer = new byte[256];
////                    int bytesRead = serialPort.Read(buffer, 0, buffer.Length);

////                    if (bytesRead > 5)
////                    {
////                        var data = new Dictionary<string, int>
////                        {
////                            { "A相溫度", (buffer[3] << 8) | buffer[4] },
////                            { "B相溫度", (buffer[5] << 8) | buffer[6] },
////                            { "C相溫度", (buffer[7] << 8) | buffer[8] },
////                            { "A相電壓", (buffer[9] << 8) | buffer[10] },
////                            { "B相電壓", (buffer[11] << 8) | buffer[12] },
////                            { "C相電壓", (buffer[13] << 8) | buffer[14] },
////                            { "A相電流", (buffer[15] << 8) | buffer[16] },
////                            { "B相電流", (buffer[17] << 8) | buffer[18] },
////                            { "C相電流", (buffer[19] << 8) | buffer[20] },
////                            { "總有功功率", (buffer[21] << 8) | buffer[22] },
////                            { "電能", (buffer[23] << 8) | buffer[24] }
////                        };

////                        switch (switchIndex)
////                        {
////                            case 1:
////                                slaveDataSwitch1[stationNumber] = data;
////                                break;
////                            case 2:
////                                slaveDataSwitch2[stationNumber] = data;
////                                break;
////                            case 3:
////                                slaveDataSwitch3[stationNumber] = data;
////                                break;
////                        }
////                    }
////                }
////                catch (Exception ex)
////                {
////                    Console.WriteLine($"讀取站號 {stationNumber} 開關 {switchIndex} 時發生錯誤: {ex.Message}");
////                }
////            });
////        }

////        private void UpdateDataGridView()
////        {
////            dataGridView.Columns.Clear();
////            dataGridView.Rows.Clear();

////            dataGridView.Columns.Add("Parameter", "參數名稱");

////            foreach (var station in stationNumbers)
////            {
////                dataGridView.Columns.Add($"Slave_{station}_Switch1", $"站號 {station} 開關 1");
////                dataGridView.Columns.Add($"Slave_{station}_Switch2", $"站號 {station} 開關 2");
////                dataGridView.Columns.Add($"Slave_{station}_Switch3", $"站號 {station} 開關 3");
////            }

////            var parameters = slaveDataSwitch1.Values.FirstOrDefault()?.Keys ?? Enumerable.Empty<string>();

////            foreach (var parameter in parameters)
////            {
////                var row = new List<object> { parameter };

////                foreach (var station in stationNumbers)
////                {
////                    row.Add(slaveDataSwitch1.ContainsKey(station) && slaveDataSwitch1[station].ContainsKey(parameter)
////                        ? slaveDataSwitch1[station][parameter]
////                        : "N/A");
////                    row.Add(slaveDataSwitch2.ContainsKey(station) && slaveDataSwitch2[station].ContainsKey(parameter)
////                        ? slaveDataSwitch2[station][parameter]
////                        : "N/A");
////                    row.Add(slaveDataSwitch3.ContainsKey(station) && slaveDataSwitch3[station].ContainsKey(parameter)
////                        ? slaveDataSwitch3[station][parameter]
////                        : "N/A");
////                }

////                dataGridView.Rows.Add(row.ToArray());
////            }
////        }

////        private ushort CalculateCRC(byte[] data)
////        {
////            ushort crc = 0xFFFF;
////            foreach (var b in data)
////            {
////                crc ^= b;
////                for (int i = 0; i < 8; i++)
////                {
////                    crc = (crc & 0x0001) != 0 ? (ushort)((crc >> 1) ^ 0xA001) : (ushort)(crc >> 1);
////                }
////            }
////            return crc;
////        }
////    }

////    internal class Program
////    {
////        static ModbusViewer modbusViewer = new();

////        [STAThread]
////        static void Main()
////        {
////            Application.Run(modbusViewer);
////        }
////    }
////}
