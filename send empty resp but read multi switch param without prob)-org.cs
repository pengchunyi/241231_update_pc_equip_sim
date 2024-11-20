//using System;  // 引用系統核心命名空間，提供基礎功能
//using System.Collections.Generic;  // 引用泛型集合命名空間
//using System.IO.Ports;  // 引用串口通訊命名空間，提供串口通訊支持
//using System.Threading.Tasks;  // 引用多執行緒與非同步處理任務命名空間
//using System.Windows.Forms;  // 引用Windows窗體應用程序命名空間
//using CFX;  // 引用CFX（連接工廠交換）命名空間
//using CFX.Transport;  // 引用CFX傳輸層命名空間
//using CFX.ResourcePerformance;  // 引用CFX資源性能命名空間
//using System.Timers; // 引入計時器的命名空間
//using Newtonsoft.Json;
//using System.Linq;


//namespace AmqpModbusIntegration  // 命名空間，用於AMQP（高級消息隊列協議）和Modbus整合的程序
//{
//    // 定義一個類，ModbusViewer，繼承自Form，負責處理Modbus通訊和顯示界面
//    public class ModbusViewer : Form
//    {
//        private SerialPort serialPort; // 定義一個串口物件，用來與Modbus設備通訊
//        private ComboBox portSelector; // 下拉選單，用於選擇可用的串口COM口
//        private TextBox stationNumberTextBox; // 輸入框，用於輸入Modbus站號
//        private Button connectButton, readButton, switchButton; // 分別用於連接和讀取數據、新增開關控制按鈕 
//        private byte stationNumber = 0xFF; // 儲存Modbus站號，預設值為0xFF（255）

//        // 顯示參數和數值的 Label 列表
//        private List<Label> parameterLabels = new List<Label>();
//        private List<Label> valueLabels = new List<Label>();

//        //private Timer updateTimer; // 定義計時器
//        private System.Windows.Forms.Timer updateTimer; // 明確指定為 Windows Forms Timer


//        // 當計時器到期時執行的操作
//        private void OnTimerElapsed()
//        {
//            // 必須在 UI 線程上執行 UpdateValues()，否則會拋出跨執行緒操作例外
//            this.Invoke(new Action(() => UpdateValues()));
//        }

//        // 初始化計時器
//        private void InitializeTimer()
//        {
//            updateTimer = new System.Windows.Forms.Timer(); // 創建 Windows Forms Timer
//            updateTimer.Interval = 3000; // 設定時間間隔為 3000 毫秒（3 秒）
//            updateTimer.Tick += (sender, e) => UpdateValues(); // 使用 Tick 事件直接調用 UpdateValues
//            updateTimer.Start(); // 啟動計時器
//        }

//        // 當窗口載入時觸發的事件
//        private void ModbusViewer_Load(object sender, EventArgs e)
//        {
//            // 在窗口載入時可以初始化一些設置
//        }

//        // 定義窗口界面的初始化方法
//        private void InitializeComponent()
//        {
//            this.SuspendLayout(); // 暫停佈局更新
//            this.ClientSize = new System.Drawing.Size(910, 530); // 設定窗口大小
//            this.Name = "ModbusViewer"; // 設定窗口名稱
//            this.Load += new System.EventHandler(this.ModbusViewer_Load); // 設定窗口載入事件
//            this.ResumeLayout(false); // 恢復佈局更新
//        }


//        // 定義各種變數，用來儲存從設備讀取的各種參數
//        public int currentStatus1, currentStatus2, currentStatus, leakageCurrent, tempA, tempB, tempC, tempN;
//        public int voltageA, voltageB, voltageC, currentA, currentB, currentC;

//        // 定義更多參數變數，用於儲存設備提供的其他數據
//        public int powerFactorA, powerFactorB, powerFactorC, activePowerA, activePowerB, activePowerC;
//        public int reactivePowerA, reactivePowerB, reactivePowerC, breakerTimes, energyHighByte, energyLowByte;
//        public double energy;
//        public int switchStatus, apparentPowerA, apparentPowerB, apparentPowerC, totalApparentPower, totalActivePower, totalReactivePower;
//        public int combinedPowerFactor, lineFrequency, deviceType, historicalLeakage, historicalCurrentA, historicalCurrentB, historicalCurrentC;

//        // 構造方法，創建窗口並初始化界面
//        public ModbusViewer()
//        {
//            InitializeUI(); // 呼叫UI初始化方法
//            InitializeTimer(); // 初始化計時器
//            InitializeParameters();   // 初始化所有參數為 0
//        }


//        //20241119
//        private DataGridView dataGridView; // DataGridView 用於顯示數據
//        private Dictionary<byte, Dictionary<string, int>> slaveData = new Dictionary<byte, Dictionary<string, int>>(); // 存放站號及其參數數據


//        private void InitializeUI()
//        {
//            Text = "Modbus Control";
//            Width = 900;
//            Height = 600;

//            // COM口選擇
//            var portLabel = new Label { Text = "COM口:", Location = new System.Drawing.Point(10, 10), AutoSize = true };
//            Controls.Add(portLabel);
//            portSelector = new ComboBox { Location = new System.Drawing.Point(70, 10), Width = 100 };
//            portSelector.Items.AddRange(SerialPort.GetPortNames());
//            Controls.Add(portSelector);

//            // 站號選擇
//            var stationNumberLabel = new Label { Text = "站號:", Location = new System.Drawing.Point(10, 50), AutoSize = true };
//            Controls.Add(stationNumberLabel);
//            stationNumberTextBox = new TextBox { Location = new System.Drawing.Point(70, 50), Width = 100 };
//            Controls.Add(stationNumberTextBox);

//            // 連接按鈕
//            connectButton = new Button { Text = "連接", Location = new System.Drawing.Point(200, 10), Width = 80 };
//            connectButton.Click += (s, e) => InitializeSerialPort(portSelector.SelectedItem?.ToString());
//            Controls.Add(connectButton);

//            // 讀取數值按鈕
//            readButton = new Button { Text = "讀取數值", Location = new System.Drawing.Point(200, 50), Width = 80 };
//            //readButton.Click += (s, e) => UpdateValues();
//            readButton.Click += async (s, e) => await ReadAllParametersAsync();
//            Controls.Add(readButton);

//            // 開關控制按鈕
//            switchButton = new Button { Text = "開啟開關", Location = new System.Drawing.Point(300, 50), Width = 80 };
//            switchButton.Click += (s, e) => SwitchON();
//            Controls.Add(switchButton);

//            // 開關控制按鈕3
//            switchButton = new Button { Text = "關閉開關", Location = new System.Drawing.Point(400, 50), Width = 80 };
//            switchButton.Click += (s, e) => SwitchOFF();
//            Controls.Add(switchButton);

//            // 初始化 DataGridView
//            dataGridView = new DataGridView
//            {
//                Location = new System.Drawing.Point(10, 100),
//                Width = 850,
//                Height = 400,
//                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
//                ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.AutoSize,
//                AllowUserToAddRows = false, // 禁止用戶新增行
//                ReadOnly = true             // 只讀模式
//            };

//            Controls.Add(dataGridView); // 將 DataGridView 添加到視窗控件
//        }

//        private void UpdateDataGridView()
//        {
//            if (dataGridView.InvokeRequired)
//            {
//                // 如果需要跨執行緒操作，使用 Invoke 調用
//                dataGridView.Invoke(new Action(UpdateDataGridView));
//                return;
//            }

//            // 確保在 UI 執行緒上執行以下代碼
//            dataGridView.Columns.Clear();
//            dataGridView.Rows.Clear();

//            // 添加第一列：參數名稱
//            dataGridView.Columns.Add("Parameter", "參數名稱");

//            // 動態添加站號列
//            foreach (var station in slaveData.Keys)
//            {
//                dataGridView.Columns.Add($"Slave_{station}", $"站號 {station}");
//            }

//            // 動態添加行：每個參數及其對應的值
//            foreach (var parameter in slaveData.Values.SelectMany(d => d.Keys).Distinct())
//            {
//                var row = new List<object> { parameter }; // 第一列是參數名稱
//                foreach (var station in slaveData.Keys)
//                {
//                    // 如果站號有這個參數，填入數值；否則顯示 "N/A"
//                    row.Add(slaveData[station].ContainsKey(parameter) ? slaveData[station][parameter].ToString() : "N/A");
//                }
//                dataGridView.Rows.Add(row.ToArray());
//            }
//        }



//        //updateTimer 每隔 3 秒觸發一次，並執行 UpdateValues()。如果上一次的操作尚未完成，下一次操作可能會重疊，導致執行緒競爭和性能問題。
//        //添加一個 執行鎖（lock） 或標記來避免同時執行多個更新操作
//        private bool isUpdating = false;
//        private async void UpdateValues()
//        {
//            if (isUpdating) return; // 如果已經在更新，直接返回
//            isUpdating = true;
//            try
//            {
//                await ReadAllParametersAsync(); // 使用異步方法
//                this.Invoke(new Action(() => UpdateDataGridView()));

//            }
//            finally
//            {
//                isUpdating = false; // 更新完成，釋放鎖
//            }
//        }



//        // 切換開關狀態的控制方法
//        private void SwitchON()
//        {
//            if (serialPort != null && serialPort.IsOpen)
//            {
//                byte[] openCommand = { stationNumber, 0x06, 0x00, 0x31, 0x55, 0x88 };
//                ushort crc = CrcHelper.CalculateCRC(openCommand);
//                byte[] crcBytes = BitConverter.GetBytes(crc);
//                byte[] fullCommand = new byte[openCommand.Length + 2];
//                Array.Copy(openCommand, fullCommand, openCommand.Length);
//                fullCommand[fullCommand.Length - 2] = crcBytes[0];
//                fullCommand[fullCommand.Length - 1] = crcBytes[1];

//                serialPort.Write(fullCommand, 0, fullCommand.Length);
//            }
//        }

//        private void SwitchOFF()
//        {
//            if (serialPort != null && serialPort.IsOpen)
//            {
//                byte[] closeCommand = { stationNumber, 0x06, 0x00, 0x31, 0x55, 0x66 };
//                ushort crc = CrcHelper.CalculateCRC(closeCommand);
//                byte[] crcBytes = BitConverter.GetBytes(crc);
//                byte[] fullCommand = new byte[closeCommand.Length + 2];
//                Array.Copy(closeCommand, fullCommand, closeCommand.Length);
//                fullCommand[fullCommand.Length - 2] = crcBytes[0];
//                fullCommand[fullCommand.Length - 1] = crcBytes[1];

//                serialPort.Write(fullCommand, 0, fullCommand.Length);
//            }
//        }


//        private void InitializeSerialPort(string portName)
//        {
//            if (serialPort != null && serialPort.IsOpen)
//            {
//                serialPort.Close();
//                serialPort.Dispose();
//            }

//            // 將站號拆分，支持多站號
//            slaveData.Clear();
//            foreach (var station in stationNumberTextBox.Text.Split(','))
//            {
//                if (byte.TryParse(station.Trim(), out var stationNumber))
//                {
//                    slaveData[stationNumber] = new Dictionary<string, int>(); // 初始化該站號的數據
//                }
//            }

//            // 創建串口並連接
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

//        public async Task ReadAllParametersAsync()
//        {
//            if (serialPort != null && serialPort.IsOpen)
//            {
//                foreach (var station in slaveData.Keys.ToList()) // 複製 Keys 集合
//                {
//                    try
//                    {
//                        // 構建 Modbus 讀取指令
//                        byte[] readCommand = { station, 0x03, 0x00, 0x00, 0x00, 0x30 };
//                        ushort crc = CrcHelper.CalculateCRC(readCommand);
//                        byte[] crcBytes = BitConverter.GetBytes(crc);
//                        byte[] fullCommand = new byte[readCommand.Length + 2];
//                        Array.Copy(readCommand, fullCommand, readCommand.Length);
//                        fullCommand[fullCommand.Length - 2] = crcBytes[0];
//                        fullCommand[fullCommand.Length - 1] = crcBytes[1];

//                        // 發送指令
//                        serialPort.Write(fullCommand, 0, fullCommand.Length);

//                        // 延遲等待設備回應
//                        await Task.Delay(1000); // 延遲 100 毫秒（可根據需要調整）

//                        // 讀取回應資料
//                        byte[] buffer = new byte[256];
//                        int bytesRead = await serialPort.BaseStream.ReadAsync(buffer, 0, buffer.Length);


//                        if (bytesRead > 5)
//                        {
//                            var data = slaveData[station]; // 獲取該站號的數據字典
//                            data["當前狀態"] = (buffer[3] << 8) | buffer[4];
//                            data["A相溫度"] = (buffer[9] << 8) | buffer[10];
//                            data["B相溫度"] = (buffer[11] << 8) | buffer[12];
//                            data["C相溫度"] = (buffer[13] << 8) | buffer[14];
//                            data["A相電壓"] = (buffer[17] << 8) | buffer[18];
//                            data["A相電流"] = (buffer[23] << 8) | buffer[24];
//                            data["開關狀態"] = (buffer[53] << 8) | buffer[54];
//                            data["總有功功率"] = (buffer[63] << 8) | buffer[64];
//                            data["線頻率"] = (buffer[69] << 8) | buffer[70];
//                        }
//                    }
//                    catch (Exception ex)
//                    {
//                        Console.WriteLine($"讀取站號 {station} 時發生錯誤: {ex.Message}");
//                    }
//                }

//                // 更新 UI
//                UpdateDataGridView();
//            }
//        }


//        // 初始化所有參數為 0
//        private void InitializeParameters()
//        {
//            currentStatus1 = 0;
//            currentStatus2 = 0;
//            currentStatus = 0;
//            leakageCurrent = 0;
//            tempA = 0;
//            tempB = 0;
//            tempC = 0;
//            tempN = 0;
//            voltageA = 0;
//            voltageB = 0;
//            voltageC = 0;
//            currentA = 0;
//            currentB = 0;
//            currentC = 0;
//            powerFactorA = 0;
//            powerFactorB = 0;
//            powerFactorC = 0;
//            activePowerA = 0;
//            activePowerB = 0;
//            activePowerC = 0;
//            reactivePowerA = 0;
//            reactivePowerB = 0;
//            reactivePowerC = 0;
//            breakerTimes = 0;
//            energyHighByte = 0;
//            energyLowByte = 0;
//            energy = 0.0;
//            switchStatus = 0;
//            apparentPowerA = 0;
//            apparentPowerB = 0;
//            apparentPowerC = 0;
//            totalApparentPower = 0;
//            totalActivePower = 0;
//            totalReactivePower = 0;
//            combinedPowerFactor = 0;
//            lineFrequency = 0;
//            deviceType = 0;
//            historicalLeakage = 0;
//            historicalCurrentA = 0;
//            historicalCurrentB = 0;
//            historicalCurrentC = 0;
//        }
//    }

//    internal class Program
//    {
//        static ModbusViewer modbusViewer = new ModbusViewer(); // 創建 ModbusViewer 窗口物件

//        [System.Runtime.InteropServices.DllImport("kernel32.dll")]
//        private static extern bool AllocConsole();

//        [STAThread]
//        static void Main()
//        {
//            AllocConsole(); // 分配一個控制台窗口

//            // 使用 AmqpEndpointManager 初始化 AMQP 端點
//            var amqpManager = new AmqpEndpointManager(
//                "amqp://127.0.0.1:8888", // 端點 URI
//                "amqp://127.0.0.1:6666", // 發布頻道 URI
//                "amqp://127.0.0.1:6666", // 訂閱頻道 URI
//                modbusViewer             // 傳入 ModbusViewer 實例
//            );

//            // 啟動 AMQP 端點並指定端點名稱
//            amqpManager.StartAmqpEndpoint("CHUNYI_PC");

//            // 啟動 Windows 應用程序
//            Application.Run(modbusViewer);
//        }
//    }

//}
