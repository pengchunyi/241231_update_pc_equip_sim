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
//        }


//        //20241119
//        private DataGridView dataGridView; // DataGridView 用於顯示數據
//        private Dictionary<byte, Dictionary<string, int>> slaveData = new Dictionary<byte, Dictionary<string, int>>(); // 存放站號及其參數數據


//        // 初始化UI界面，配置窗口中的控件
//        private void InitializeUI()
//        {
//            Text = "Modbus Control"; // 設定窗口標題
//            Width = 800; // 增加寬度以便顯示多列參數
//            Height = 500;

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
//            readButton.Click += (s, e) => UpdateValues();
//            Controls.Add(readButton);

//            // 開關控制按鈕
//            switchButton = new Button { Text = "開啟開關", Location = new System.Drawing.Point(300, 50), Width = 80 };
//            switchButton.Click += (s, e) => SwitchON();
//            Controls.Add(switchButton);

//            // 開關控制按鈕
//            switchButton = new Button { Text = "關閉開關", Location = new System.Drawing.Point(400, 50), Width = 80 };
//            switchButton.Click += (s, e) => SwitchOFF();
//            Controls.Add(switchButton);

//            // 創建參數名稱和數值標籤並分成多列顯示
//            //string[] parameterNames =
//            //{
//            //    "當前狀態", "漏電電流", "A相溫度", "B相溫度", "C相溫度", "N相溫度",
//            //    "A相電壓", "B相電壓", "C相電壓", "A相電流", "B相電流", "C相電流", "A相功率因數",
//            //    "B相功率因數", "C相功率因數", "A相有功功率", "B相有功功率", "C相有功功率",
//            //    "A相無功功率", "B相無功功率", "C相無功功率", "合閉次數", "電能",
//            //    "開關狀態", "A相視在功率", "B相視在功率", "C相視在功率", "總視在功率", "總有功功率",
//            //    "總無功功率", "合相功率因數", "線頻率", "設備類型", "歷史漏電值", "歷史A相電流",
//            //    "歷史B相電流", "歷史C相電流"
//            //};

//            string[] parameterNames =
//                {
//                    "當前狀態",        // valueLabels[0]
//                    "A相溫度",        // valueLabels[2]
//                    "B相溫度",        // valueLabels[3]
//                    "C相溫度",        // valueLabels[4]
//                    "A相電壓",        // valueLabels[6]
//                    "B相電壓",        // valueLabels[7]
//                    "C相電壓",        // valueLabels[8]
//                    "A相電流",        // valueLabels[9]
//                    "B相電流",        // valueLabels[10]
//                    "C相電流",        // valueLabels[11]
//                    "開關狀態",        // valueLabels[23]
//                    "總有功功率",      // valueLabels[28]
//                    "線頻率",          // valueLabels[31]
//                    "N相溫度"
//                };




//            int yPosition = 100;
//            for (int i = 0; i < parameterNames.Length; i++)
//            {
//                var parameterLabel = new Label
//                {
//                    Text = parameterNames[i],
//                    Location = new System.Drawing.Point(10 + (i / 20) * 250, yPosition + (i % 20) * 30),
//                    AutoSize = true
//                };
//                parameterLabels.Add(parameterLabel);
//                Controls.Add(parameterLabel);

//                var valueLabel = new Label
//                {
//                    Text = "0",
//                    Location = new System.Drawing.Point(120 + (i / 20) * 250, yPosition + (i % 20) * 30),
//                    AutoSize = true
//                };
//                valueLabels.Add(valueLabel);
//                Controls.Add(valueLabel);
//            }
//        }

//        // 更新值顯示
//        private void UpdateValues()
//        {
//            ReadAllParameters();
//            // 更新值顯示
//            //valueLabels[0].Text = currentStatus.ToString();
//            //valueLabels[1].Text = leakageCurrent.ToString();
//            //valueLabels[2].Text = tempA.ToString();
//            //valueLabels[3].Text = tempB.ToString();
//            //valueLabels[4].Text = tempC.ToString();
//            //valueLabels[5].Text = tempN.ToString();
//            //valueLabels[6].Text = voltageA.ToString();
//            //valueLabels[7].Text = voltageB.ToString();
//            //valueLabels[8].Text = voltageC.ToString();
//            //valueLabels[9].Text = currentA.ToString();
//            //valueLabels[10].Text = currentB.ToString();
//            //valueLabels[11].Text = currentC.ToString();
//            //valueLabels[12].Text = powerFactorA.ToString();
//            //valueLabels[13].Text = powerFactorB.ToString();
//            //valueLabels[14].Text = powerFactorC.ToString();
//            //valueLabels[15].Text = activePowerA.ToString();
//            //valueLabels[16].Text = activePowerB.ToString();
//            //valueLabels[17].Text = activePowerC.ToString();
//            //valueLabels[18].Text = reactivePowerA.ToString();
//            //valueLabels[19].Text = reactivePowerB.ToString();
//            //valueLabels[20].Text = reactivePowerC.ToString();
//            //valueLabels[21].Text = breakerTimes.ToString();
//            //valueLabels[22].Text = energy.ToString(); // 電能值（高低位合併後除以100）
//            //valueLabels[23].Text = switchStatus.ToString();
//            //valueLabels[24].Text = apparentPowerA.ToString();
//            //valueLabels[25].Text = apparentPowerB.ToString();
//            //valueLabels[26].Text = apparentPowerC.ToString();
//            //valueLabels[27].Text = totalApparentPower.ToString();
//            //valueLabels[28].Text = totalActivePower.ToString();
//            //valueLabels[29].Text = totalReactivePower.ToString();
//            //valueLabels[30].Text = combinedPowerFactor.ToString();
//            //valueLabels[31].Text = lineFrequency.ToString();
//            //valueLabels[32].Text = deviceType.ToString();
//            //valueLabels[33].Text = historicalLeakage.ToString();
//            //valueLabels[34].Text = historicalCurrentA.ToString();
//            //valueLabels[35].Text = historicalCurrentB.ToString();
//            //valueLabels[36].Text = historicalCurrentC.ToString();
//            ///


//            // UpdateValues 中的對應關係應與 parameterNames 長度一致
//            valueLabels[0].Text = currentStatus.ToString();
//            valueLabels[1].Text = tempA.ToString();
//            valueLabels[2].Text = tempB.ToString();
//            valueLabels[3].Text = tempC.ToString();
//            valueLabels[4].Text = voltageA.ToString();
//            valueLabels[5].Text = voltageB.ToString();
//            valueLabels[6].Text = voltageC.ToString();
//            valueLabels[7].Text = currentA.ToString();
//            valueLabels[8].Text = currentB.ToString();
//            valueLabels[9].Text = currentC.ToString();
//            valueLabels[10].Text = switchStatus.ToString();
//            valueLabels[11].Text = totalActivePower.ToString();
//            valueLabels[12].Text = lineFrequency.ToString();
//            valueLabels[13].Text = tempN.ToString();

//        }
//        // 切換開關狀態的控制方法
//        private void SwitchON()
//        {
//            if (serialPort != null && serialPort.IsOpen)
//            {
//                byte[] openCommand = { stationNumber, 0x06, 0x00, 0x31, 0x55, 0x88 };
//                ushort crc = CalculateCRC(openCommand);
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
//                ushort crc = CalculateCRC(closeCommand);
//                byte[] crcBytes = BitConverter.GetBytes(crc);
//                byte[] fullCommand = new byte[closeCommand.Length + 2];
//                Array.Copy(closeCommand, fullCommand, closeCommand.Length);
//                fullCommand[fullCommand.Length - 2] = crcBytes[0];
//                fullCommand[fullCommand.Length - 1] = crcBytes[1];

//                serialPort.Write(fullCommand, 0, fullCommand.Length);
//            }
//        }

//        // 初始化串口，並設定為指定的端口名稱
//        private void InitializeSerialPort(string portName)
//        {
//            if (serialPort != null && serialPort.IsOpen) // 如果已有串口且已開啟
//            {
//                serialPort.Close(); // 關閉串口
//                serialPort.Dispose(); // 釋放資源
//            }

//            // 將站號轉換為byte格式，如果無效則顯示錯誤消息
//            if (!byte.TryParse(stationNumberTextBox.Text, out stationNumber))
//            {
//                MessageBox.Show("請輸入有效的站號 (0-255)");
//                return;
//            }

//            // 創建新串口物件，設置波特率、校驗位、數據位和停止位
//            serialPort = new SerialPort(portName, 9600, Parity.None, 8, StopBits.One);
//            try
//            {
//                serialPort.Open(); // 開啟串口
//                MessageBox.Show($"串口 {portName} 連接成功"); // 成功提示
//            }
//            catch (Exception ex)
//            {
//                MessageBox.Show($"串口連接失敗: {ex.Message}"); // 失敗提示
//            }
//        }

//        // 讀取所有參數的方法
//        public void ReadAllParameters()
//        {
//            if (serialPort != null && serialPort.IsOpen) // 確保串口已打開
//            {
//                byte[] readCommand = { stationNumber, 0x03, 0x00, 0x00, 0x00, 0x30 }; // 定義Modbus讀取命令
//                ushort crc = CalculateCRC(readCommand); // 計算CRC校驗碼
//                byte[] crcBytes = BitConverter.GetBytes(crc); // 將CRC碼轉換為byte數組
//                byte[] fullCommand = new byte[readCommand.Length + 2]; // 定義完整命令數組
//                Array.Copy(readCommand, fullCommand, readCommand.Length); // 將讀取命令複製到完整命令數組中
//                fullCommand[fullCommand.Length - 2] = crcBytes[0]; // 將CRC碼的低位加入命令
//                fullCommand[fullCommand.Length - 1] = crcBytes[1]; // 將CRC碼的高位加入命令

//                serialPort.Write(fullCommand, 0, fullCommand.Length); // 通過串口發送完整命令

//                // 開始新任務，讀取設備回傳數據
//                Task.Run(() =>
//                {
//                    try
//                    {
//                        byte[] buffer = new byte[256]; // 定義數據緩衝區
//                        int bytesRead = serialPort.Read(buffer, 0, buffer.Length); // 從串口讀取數據

//                        // 如果讀取的字節數大於5，則解析數據並存儲在對應變數中
//                        if (bytesRead > 5)
//                        {
//                            currentStatus1 = (buffer[3] << 8) | buffer[4];          // 當前狀態1
//                            currentStatus2 = (buffer[5] << 8) | buffer[6];          // 當前狀態2
//                            //20241115新曾
//                            currentStatus = (currentStatus1 << 16) | currentStatus2;      // 合併成一個完整的 32 位數值


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
//                            energy = ((energyHighByte << 16) | energyLowByte) / 100.0;// 電能高低位合併並除以100

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
//                        Console.WriteLine($"讀取參數時發生錯誤: {ex.Message}"); // 顯示錯誤信息
//                    }
//                });
//            }
//        }

//        // 計算CRC校驗碼的方法
//        private ushort CalculateCRC(byte[] data)
//        {
//            ushort crc = 0xFFFF; // 設定初始值
//            for (int pos = 0; pos < data.Length; pos++) // 遍歷數據每一位元組
//            {
//                crc ^= (ushort)data[pos]; // 與當前字節異或
//                for (int i = 8; i != 0; i--) // 進行8次迴圈
//                {
//                    if ((crc & 0x0001) != 0) // 如果最低位為1
//                    {
//                        crc >>= 1; // 右移一位
//                        crc ^= 0xA001; // 異或0xA001
//                    }
//                    else
//                    {
//                        crc >>= 1; // 否則直接右移
//                    }
//                }
//            }
//            return crc; // 返回計算結果
//        }



//    }

//    // 主程序類，負責啟動AMQP端點和顯示窗口
//    internal class Program
//    {
//        static ModbusViewer modbusViewer = new ModbusViewer(); // 創建ModbusViewer窗口物件


//        [System.Runtime.InteropServices.DllImport("kernel32.dll")]
//        private static extern bool AllocConsole();

//        [STAThread]
//        static void Main()
//        {
//            AllocConsole(); // 分配一個控制台窗口
//            Task.Run(() => StartAmqpEndpoint()); // 非同步啟動 AMQP 端點
//            Application.Run(modbusViewer);       // 啟動窗口
//        }



//        // 初始化並啟動AMQP端點的方法
//        static void StartAmqpEndpoint()
//        {
//            AmqpCFXEndpoint endpoint = new AmqpCFXEndpoint(); // 創建CFX端點
//            string handle = "CHUNYI_PC"; // 設定端點名稱
//            endpoint.Open(handle, new Uri("amqp://127.0.0.1:8888")); // 打開端點並連接URI

//            endpoint.AddPublishChannel(new Uri("amqp://127.0.0.1:6666"), "event"); // 添加發布頻道
//            //endpoint.AddPublishChannel(new Uri("amqp://10.181.56.175:30031"), "event"); // 添加發布頻道


//            endpoint.Publish(new EndpointConnected()); // 發布端點連接消息
//            Console.WriteLine("endpoint2 publish EndpointConnected\n"); // 在控制台顯示信息

//            // 添加訂閱頻道，用於接收消息
//            endpoint.AddSubscribeChannel(new AmqpChannelAddress()
//            {
//                Address = "MessageSource", // 設置源地址
//                //Address = "MessageSource", // 設置源地址
//                Uri = new Uri("amqp://127.0.0.1:6666") // 設置URI
//                //Uri = new Uri("amqp://10.181.56.175:30031")
//            });

//            endpoint.OnRequestReceived += OnRequestReceived; // 設置接收到請求時的回調
//        }

//        // 當接收到請求時處理的函數
//        static CFXEnvelope OnRequestReceived(CFXEnvelope request)
//        {
//            // 如果接收到的是EnergyConsumptionRequest
//            if (request.MessageBody is EnergyConsumptionRequest)
//            {
//                modbusViewer.ReadAllParameters(); // 調用ModbusViewer中的讀取數據方法

//                // 建立回應消息，包含讀取到的電力參數
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

//                CFXEnvelope result = CFXEnvelope.FromCFXMessage(response); // 將回應包裝成CFXEnvelope
//                return result; // 返回回應
//            }

//            return null; // 如果不是EnergyConsumptionRequest，返回null
//        }

//    }
//}
