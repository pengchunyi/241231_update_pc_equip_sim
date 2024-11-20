//using System;
//using System.Text;
//using System.Collections.Generic;
//using System.Threading.Tasks;
//using System.Net.Http;
//using System.Windows.Forms;
//using Newtonsoft.Json.Linq;
//using Timer = System.Windows.Forms.Timer;
//using System.IO.Ports;

//public struct Schedule
//{
//    public int Year;
//    public int Month;
//    public int Day;
//    public int Hour;
//    public int Minute;
//    public int Second;
//    public int Action; // 1: 開啟, 0: 關閉
//    public string PlanStartDate; // 開始日期時間
//    public string PlanCloseDate; // 結束日期時間
//}

//public class ModbusViewer : Form
//{
//    private SerialPort serialPort;
//    private List<Schedule> scheduleList = new List<Schedule>();
//    private Label[] parameterLabels = new Label[40];
//    private Button openButton, closeButton, scheduleButton, autoScheduleButton, manualModeButton, connectButton, readAllButton;
//    private Label timeDisplay;
//    private ComboBox portSelector;
//    private ListBox scheduleListBox;
//    private Form scheduleWindow;
//    private bool isScheduleRunning = false;
//    private string[] daysOfWeek = { "SUN", "MON", "TUES", "WED", "THU", "FRI", "SAT" };
//    private string previousTimeString = "";

//    private TextBox stationNumberTextBox; // 站號輸入框
//    private byte stationNumber = 0xFF; // 默認站號

//    public ModbusViewer()
//    {
//        InitializeUI();
//    }

//    private void InitializeSerialPort(string portName)
//    {
//        try
//        {
//            if (serialPort != null && serialPort.IsOpen)
//            {
//                serialPort.Close();
//                serialPort.Dispose();
//            }

//            serialPort = new SerialPort(portName, 9600, Parity.None, 8, StopBits.One);
//            serialPort.Open();
//            MessageBox.Show($"串口 {portName} 初始化成功");
//        }
//        catch (Exception ex)
//        {
//            MessageBox.Show($"串口初始化失敗: {ex.Message}");
//        }
//    }

//    private void RefreshScheduleList()
//    {
//        scheduleListBox.Items.Clear();
//        foreach (var schedule in scheduleList)
//        {
//            scheduleListBox.Items.Add($"{schedule.PlanStartDate} START --- {schedule.PlanCloseDate} CLOSE");
//        }
//    }

//    private void ShowScheduleWindow()
//    {
//        if (scheduleWindow == null || scheduleWindow.IsDisposed)
//        {
//            scheduleWindow = new Form { Text = "生產排程", Width = 800, Height = 600 };
//            scheduleListBox = new ListBox { Dock = DockStyle.Fill };
//            scheduleWindow.Controls.Add(scheduleListBox);
//            RefreshScheduleList();
//        }

//        scheduleWindow.Show();
//        scheduleWindow.BringToFront();
//    }

//    private void InitializeUI()
//    {
//        Text = "Modbus Control";
//        Width = 800;
//        Height = 600;

//        var portLabel = new Label
//        {
//            Text = "串口:",
//            Location = new System.Drawing.Point(10, 10),
//            AutoSize = true
//        };
//        Controls.Add(portLabel);

//        portSelector = new ComboBox
//        {
//            Location = new System.Drawing.Point(80, 10),
//            Width = 100
//        };
//        portSelector.Items.AddRange(SerialPort.GetPortNames()); // 加入可用的串口
//        Controls.Add(portSelector);

//        connectButton = new Button
//        {
//            Text = "連接",
//            Location = new System.Drawing.Point(190, 10),
//            Width = 80
//        };
//        connectButton.Click += (s, e) =>
//        {
//            if (portSelector.SelectedItem != null)
//            {
//                InitializeSerialPort(portSelector.SelectedItem.ToString());
//            }
//            else
//            {
//                MessageBox.Show("請選擇一個串口");
//            }
//        };
//        Controls.Add(connectButton);

//        var stationNumberLabel = new Label
//        {
//            Text = "站號:",
//            Location = new System.Drawing.Point(10, 50),
//            AutoSize = true
//        };
//        Controls.Add(stationNumberLabel);

//        stationNumberTextBox = new TextBox
//        {
//            Location = new System.Drawing.Point(60, 50),
//            Width = 50
//        };
//        stationNumberTextBox.TextChanged += (s, e) =>
//        {
//            if (byte.TryParse(stationNumberTextBox.Text, out byte result))
//            {
//                stationNumber = result;
//            }
//            else
//            {
//                MessageBox.Show("請輸入有效的站號 (0-255)");
//            }
//        };
//        Controls.Add(stationNumberTextBox);

//        for (int i = 0; i < 40; i++)
//        {
//            int col = i / 20;
//            int row = i % 20;
//            int xOffset = col * 400;
//            int yOffset = 90 + row * 30;

//            parameterLabels[i] = new Label()
//            {
//                Text = "0",
//                Location = new System.Drawing.Point(10 + xOffset, yOffset),
//                AutoSize = true
//            };
//            Controls.Add(parameterLabels[i]);
//        }

//        openButton = new Button { Text = "開啟空開", Location = new System.Drawing.Point(10, 70) };
//        openButton.Click += (s, e) => OpenBreaker();
//        Controls.Add(openButton);

//        closeButton = new Button { Text = "關閉空開", Location = new System.Drawing.Point(170, 70) };
//        closeButton.Click += (s, e) => CloseBreaker();
//        Controls.Add(closeButton);

//        scheduleButton = new Button { Text = "顯示生產排程", Location = new System.Drawing.Point(330, 70) };
//        scheduleButton.Click += (s, e) => ShowScheduleWindow();
//        Controls.Add(scheduleButton);

//        autoScheduleButton = new Button { Text = "依照生產排程", Location = new System.Drawing.Point(500, 70) };
//        autoScheduleButton.Click += (s, e) => isScheduleRunning = !isScheduleRunning;
//        Controls.Add(autoScheduleButton);

//        manualModeButton = new Button { Text = "手動模式", Location = new System.Drawing.Point(670, 70) };
//        manualModeButton.Click += (s, e) => isScheduleRunning = false;
//        Controls.Add(manualModeButton);

//        // 新增讀取全部參數的按鈕
//        readAllButton = new Button { Text = "讀取全部參數", Location = new System.Drawing.Point(10, 110) };
//        readAllButton.Click += (s, e) => ReadAllParameters();
//        Controls.Add(readAllButton);

//        timeDisplay = new Label { Text = "00:00:00", Location = new System.Drawing.Point(1000, 10) };
//        Controls.Add(timeDisplay);

//        var timer = new Timer();
//        timer.Interval = 1000;
//        timer.Tick += (s, e) => UpdateTimeDisplay();
//        timer.Start();
//    }

//    private ushort CalculateCRC(byte[] data)
//    {
//        ushort crc = 0xFFFF;
//        for (int pos = 0; pos < data.Length; pos++)
//        {
//            crc ^= (ushort)data[pos];
//            for (int i = 8; i != 0; i--)
//            {
//                if ((crc & 0x0001) != 0)
//                {
//                    crc >>= 1;
//                    crc ^= 0xA001;
//                }
//                else
//                {
//                    crc >>= 1;
//                }
//            }
//        }
//        return crc;
//    }

//    private void ReadAllParameters()
//    {
//        if (serialPort != null && serialPort.IsOpen)
//        {
//            byte[] readCommand = { stationNumber, 0x03, 0x00, 0x00, 0x00, 0x30 };
//            ushort crc = CalculateCRC(readCommand);
//            byte[] crcBytes = BitConverter.GetBytes(crc);
//            byte[] fullCommand = new byte[readCommand.Length + 2];
//            Array.Copy(readCommand, fullCommand, readCommand.Length);
//            fullCommand[fullCommand.Length - 2] = crcBytes[0];
//            fullCommand[fullCommand.Length - 1] = crcBytes[1];

//            serialPort.Write(fullCommand, 0, fullCommand.Length);

//            // 等待回應並處理數據
//            Task.Run(() =>
//            {
//                try
//                {
//                    byte[] buffer = new byte[256];
//                    int bytesRead = serialPort.Read(buffer, 0, buffer.Length);

//                    if (bytesRead > 5) // 確保至少有數據
//                    {
//                        for (int i = 0; i < 40; i++)
//                        {
//                            int value = (buffer[3 + (i * 2)] << 8) | buffer[4 + (i * 2)];
//                            Invoke((MethodInvoker)(() => parameterLabels[i].Text = value.ToString()));
//                        }
//                    }
//                }
//                catch (Exception ex)
//                {
//                    MessageBox.Show($"讀取參數時發生錯誤: {ex.Message}");
//                }
//            });
//        }
//    }

//    private void OpenBreaker()
//    {
//        if (serialPort != null && serialPort.IsOpen)
//        {
//            byte[] openCommand = { stationNumber, 0x06, 0x00, 0x31, 0x55, 0x88 };
//            ushort crc = CalculateCRC(openCommand);
//            byte[] crcBytes = BitConverter.GetBytes(crc);
//            byte[] fullCommand = new byte[openCommand.Length + 2];
//            Array.Copy(openCommand, fullCommand, openCommand.Length);
//            fullCommand[fullCommand.Length - 2] = crcBytes[0];
//            fullCommand[fullCommand.Length - 1] = crcBytes[1];

//            serialPort.Write(fullCommand, 0, fullCommand.Length);
//        }
//    }

//    private void CloseBreaker()
//    {
//        if (serialPort != null && serialPort.IsOpen)
//        {
//            byte[] closeCommand = { stationNumber, 0x06, 0x00, 0x31, 0x55, 0x66 };
//            ushort crc = CalculateCRC(closeCommand);
//            byte[] crcBytes = BitConverter.GetBytes(crc);
//            byte[] fullCommand = new byte[closeCommand.Length + 2];
//            Array.Copy(closeCommand, fullCommand, closeCommand.Length);
//            fullCommand[fullCommand.Length - 2] = crcBytes[0];
//            fullCommand[fullCommand.Length - 1] = crcBytes[1];

//            serialPort.Write(fullCommand, 0, fullCommand.Length);
//        }
//    }

//    private void UpdateTimeDisplay()
//    {
//        var now = DateTime.Now;
//        string currentTime = $"{now:yyyy-MM-dd} ({daysOfWeek[(int)now.DayOfWeek]}) {now:HH:mm:ss}";
//        if (currentTime != previousTimeString)
//        {
//            timeDisplay.Text = currentTime;
//            previousTimeString = currentTime;
//        }
//    }

//    [STAThread]
//    public static void Main()
//    {
//        Application.Run(new ModbusViewer());
//    }
//}



//20241113目前可以讀取到數值的部分，現在要研究怎麼傳出成為json格式還要顯示出來參數名稱

//using System;
//using System.Text;
//using System.Collections.Generic;
//using System.Threading.Tasks;
//using System.Windows.Forms;
//using System.IO.Ports;

//public struct Schedule
//{
//    public int Year;
//    public int Month;
//    public int Day;
//    public int Hour;
//    public int Minute;
//    public int Second;
//    public int Action; // 1: 開啟, 0: 關閉
//    public string PlanStartDate; // 開始日期時間
//    public string PlanCloseDate; // 結束日期時間
//}

//public class ModbusViewer : Form
//{
//    private SerialPort serialPort;
//    private List<Schedule> scheduleList = new List<Schedule>();
//    private Label[] parameterLabels = new Label[40];
//    private Button openButton, closeButton, scheduleButton, autoScheduleButton, manualModeButton, connectButton;
//    private Label timeDisplay;
//    private ComboBox portSelector;
//    private ListBox scheduleListBox;
//    private Form scheduleWindow;
//    private bool isScheduleRunning = false;
//    private string[] daysOfWeek = { "SUN", "MON", "TUES", "WED", "THU", "FRI", "SAT" };
//    private string previousTimeString = "";

//    private TextBox stationNumberTextBox; // 站號輸入框
//    private byte stationNumber = 0xFF; // 默認站號

//    private Timer refreshTimer; // 每3秒刷新一次

//    public ModbusViewer()
//    {
//        InitializeUI();
//        InitializeRefreshTimer();
//    }

//    private void InitializeSerialPort(string portName)
//    {
//        try
//        {
//            if (serialPort != null && serialPort.IsOpen)
//            {
//                serialPort.Close();
//                serialPort.Dispose();
//            }

//            serialPort = new SerialPort(portName, 9600, Parity.None, 8, StopBits.One);
//            serialPort.Open();
//            MessageBox.Show($"串口 {portName} 初始化成功");
//        }
//        catch (Exception ex)
//        {
//            MessageBox.Show($"串口初始化失敗: {ex.Message}");
//        }
//    }

//        private void RefreshScheduleList()
//        {
//            scheduleListBox.Items.Clear();
//            foreach (var schedule in scheduleList)
//            {
//                scheduleListBox.Items.Add($"{schedule.PlanStartDate} START --- {schedule.PlanCloseDate} CLOSE");
//            }
//        }

//        private void ShowScheduleWindow()
//        {
//            if (scheduleWindow == null || scheduleWindow.IsDisposed)
//            {
//                scheduleWindow = new Form { Text = "生產排程", Width = 800, Height = 600 };
//                scheduleListBox = new ListBox { Dock = DockStyle.Fill };
//                scheduleWindow.Controls.Add(scheduleListBox);
//                RefreshScheduleList();
//            }

//            scheduleWindow.Show();
//            scheduleWindow.BringToFront();
//        }

//    private void InitializeUI()
//    {
//        Text = "Modbus Control";
//        Width = 800;
//        Height = 600;

//        var portLabel = new Label
//        {
//            Text = "串口:",
//            Location = new System.Drawing.Point(10, 10),
//            AutoSize = true
//        };
//        Controls.Add(portLabel);

//        portSelector = new ComboBox
//        {
//            Location = new System.Drawing.Point(80, 10),
//            Width = 100
//        };
//        portSelector.Items.AddRange(SerialPort.GetPortNames()); // 加入可用的串口
//        Controls.Add(portSelector);

//        connectButton = new Button
//        {
//            Text = "連接",
//            Location = new System.Drawing.Point(190, 10),
//            Width = 80
//        };
//        connectButton.Click += (s, e) =>
//        {
//            if (portSelector.SelectedItem != null)
//            {
//                InitializeSerialPort(portSelector.SelectedItem.ToString());
//            }
//            else
//            {
//                MessageBox.Show("請選擇一個串口");
//            }
//        };
//        Controls.Add(connectButton);

//        var stationNumberLabel = new Label
//        {
//            Text = "站號:",
//            Location = new System.Drawing.Point(10, 50),
//            AutoSize = true
//        };
//        Controls.Add(stationNumberLabel);

//        stationNumberTextBox = new TextBox
//        {
//            Location = new System.Drawing.Point(60, 50),
//            Width = 50
//        };
//        stationNumberTextBox.TextChanged += (s, e) =>
//        {
//            if (byte.TryParse(stationNumberTextBox.Text, out byte result))
//            {
//                stationNumber = result;
//            }
//            else
//            {
//                MessageBox.Show("請輸入有效的站號 (0-255)");
//            }
//        };
//        Controls.Add(stationNumberTextBox);

//        for (int i = 0; i < 40; i++)
//        {
//            int col = i / 20;
//            int row = i % 20;
//            int xOffset = col * 400;
//            int yOffset = 90 + row * 30;

//            parameterLabels[i] = new Label()
//            {
//                Text = "0",
//                Location = new System.Drawing.Point(10 + xOffset, yOffset),
//                AutoSize = true
//            };
//            Controls.Add(parameterLabels[i]);
//        }

//        openButton = new Button { Text = "開啟空開", Location = new System.Drawing.Point(10, 70) };
//        openButton.Click += (s, e) => OpenBreaker();
//        Controls.Add(openButton);

//        closeButton = new Button { Text = "關閉空開", Location = new System.Drawing.Point(170, 70) };
//        closeButton.Click += (s, e) => CloseBreaker();
//        Controls.Add(closeButton);

//        scheduleButton = new Button { Text = "顯示生產排程", Location = new System.Drawing.Point(330, 70) };
//        scheduleButton.Click += (s, e) => ShowScheduleWindow();
//        Controls.Add(scheduleButton);

//        autoScheduleButton = new Button { Text = "依照生產排程", Location = new System.Drawing.Point(500, 70) };
//        autoScheduleButton.Click += (s, e) => isScheduleRunning = !isScheduleRunning;
//        Controls.Add(autoScheduleButton);

//        manualModeButton = new Button { Text = "手動模式", Location = new System.Drawing.Point(670, 70) };
//        manualModeButton.Click += (s, e) => isScheduleRunning = false;
//        Controls.Add(manualModeButton);

//        timeDisplay = new Label { Text = "00:00:00", Location = new System.Drawing.Point(1000, 10) };
//        Controls.Add(timeDisplay);

//        var timer = new Timer();
//        timer.Interval = 1000;
//        timer.Tick += (s, e) => UpdateTimeDisplay();
//        timer.Start();
//    }

//    private void InitializeRefreshTimer()
//    {
//        // 設定每3秒刷新一次的 Timer
//        refreshTimer = new Timer();
//        refreshTimer.Interval = 3000; // 3秒
//        refreshTimer.Tick += (s, e) => ReadAllParameters();
//        refreshTimer.Start();
//    }

//    private ushort CalculateCRC(byte[] data)
//    {
//        ushort crc = 0xFFFF;
//        for (int pos = 0; pos < data.Length; pos++)
//        {
//            crc ^= (ushort)data[pos];
//            for (int i = 8; i != 0; i--)
//            {
//                if ((crc & 0x0001) != 0)
//                {
//                    crc >>= 1;
//                    crc ^= 0xA001;
//                }
//                else
//                {
//                    crc >>= 1;
//                }
//            }
//        }
//        return crc;
//    }

//    private void ReadAllParameters()
//    {
//        if (serialPort != null && serialPort.IsOpen)
//        {
//            byte[] readCommand = { stationNumber, 0x03, 0x00, 0x00, 0x00, 0x30 };
//            ushort crc = CalculateCRC(readCommand);
//            byte[] crcBytes = BitConverter.GetBytes(crc);
//            byte[] fullCommand = new byte[readCommand.Length + 2];
//            Array.Copy(readCommand, fullCommand, readCommand.Length);
//            fullCommand[fullCommand.Length - 2] = crcBytes[0];
//            fullCommand[fullCommand.Length - 1] = crcBytes[1];

//            serialPort.Write(fullCommand, 0, fullCommand.Length);

//            // 等待回應並處理數據
//            Task.Run(() =>
//            {
//                try
//                {
//                    byte[] buffer = new byte[256];
//                    int bytesRead = serialPort.Read(buffer, 0, buffer.Length);

//                    if (bytesRead > 5) // 確保至少有數據
//                    {
//                        for (int i = 0; i < 40; i++)
//                        {
//                            int value = (buffer[3 + (i * 2)] << 8) | buffer[4 + (i * 2)];
//                            Invoke((MethodInvoker)(() => parameterLabels[i].Text = value.ToString()));
//                        }
//                    }
//                }
//                catch (Exception ex)
//                {
//                    MessageBox.Show($"讀取參數時發生錯誤: {ex.Message}");
//                }
//            });
//        }
//    }

//    private void OpenBreaker()
//    {
//        if (serialPort != null && serialPort.IsOpen)
//        {
//            byte[] openCommand = { stationNumber, 0x06, 0x00, 0x31, 0x55, 0x88 };
//            ushort crc = CalculateCRC(openCommand);
//            byte[] crcBytes = BitConverter.GetBytes(crc);
//            byte[] fullCommand = new byte[openCommand.Length + 2];
//            Array.Copy(openCommand, fullCommand, openCommand.Length);
//            fullCommand[fullCommand.Length - 2] = crcBytes[0];
//            fullCommand[fullCommand.Length - 1] = crcBytes[1];

//            serialPort.Write(fullCommand, 0, fullCommand.Length);
//        }
//    }

//    private void CloseBreaker()
//    {
//        if (serialPort != null && serialPort.IsOpen)
//        {
//            byte[] closeCommand = { stationNumber, 0x06, 0x00, 0x31, 0x55, 0x66 };
//            ushort crc = CalculateCRC(closeCommand);
//            byte[] crcBytes = BitConverter.GetBytes(crc);
//            byte[] fullCommand = new byte[closeCommand.Length + 2];
//            Array.Copy(closeCommand, fullCommand, closeCommand.Length);
//            fullCommand[fullCommand.Length - 2] = crcBytes[0];
//            fullCommand[fullCommand.Length - 1] = crcBytes[1];

//            serialPort.Write(fullCommand, 0, fullCommand.Length);
//        }
//    }

//    private void UpdateTimeDisplay()
//    {
//        var now = DateTime.Now;
//        string currentTime = $"{now:yyyy-MM-dd} ({daysOfWeek[(int)now.DayOfWeek]}) {now:HH:mm:ss}";
//        if (currentTime != previousTimeString)
//        {
//            timeDisplay.Text = currentTime;
//            previousTimeString = currentTime;
//        }
//    }

//    [STAThread]
//    public static void Main()
//    {
//        Application.Run(new ModbusViewer());
//    }
//}







//這個只有參數忠文名稱，讀取不到數值=============================================================================

//using System;
//using System.Text;
//using System.Collections.Generic;
//using System.Threading.Tasks;
//using System.Net.Http;
//using System.Windows.Forms;
//using Newtonsoft.Json.Linq;
//using Timer = System.Windows.Forms.Timer;
//using System.IO.Ports;

//public struct Schedule
//{
//    public int Year;
//    public int Month;
//    public int Day;
//    public int Hour;
//    public int Minute;
//    public int Second;
//    public int Action; // 1: 開啟, 0: 關閉
//    public string PlanStartDate; // 開始日期時間
//    public string PlanCloseDate; // 結束日期時間
//}

//public class ModbusViewer : Form
//{
//    private SerialPort serialPort;
//    private List<Schedule> scheduleList = new List<Schedule>();
//    private Button openButton, closeButton, scheduleButton, autoScheduleButton, manualModeButton, connectButton, readAllButton;
//    private Label timeDisplay;
//    private ComboBox portSelector;
//    private ListBox scheduleListBox;
//    private Form scheduleWindow;
//    private bool isScheduleRunning = false;
//    private string[] daysOfWeek = { "SUN", "MON", "TUES", "WED", "THU", "FRI", "SAT" };
//    private string previousTimeString = "";

//    private TextBox stationNumberTextBox;
//    private byte stationNumber = 0xFF;

//    // 定義每個參數的變量
//    private int currentStatus1, currentStatus2, leakageCurrent;
//    private int tempA, tempB, tempC, tempN;
//    private int voltageA, voltageB, voltageC;
//    private int currentA, currentB, currentC;
//    private int powerFactorA, powerFactorB, powerFactorC;
//    private int activePowerA, activePowerB, activePowerC;
//    private int reactivePowerA, reactivePowerB, reactivePowerC;
//    private int breakerTimes, energyHighByte, energyLowByte;
//    private int switchStatus, apparentPowerA, apparentPowerB, apparentPowerC;
//    private int totalApparentPower, totalActivePower, totalReactivePower;
//    private int combinedPowerFactor, lineFrequency, deviceType;
//    private int historicalLeakage, historicalCurrentA, historicalCurrentB, historicalCurrentC, lineColorMark;

//    public ModbusViewer()
//    {
//        InitializeUI();
//    }

//    private void InitializeSerialPort(string portName)
//    {
//        try
//        {
//            if (serialPort != null && serialPort.IsOpen)
//            {
//                serialPort.Close();
//                serialPort.Dispose();
//            }

//            serialPort = new SerialPort(portName, 9600, Parity.None, 8, StopBits.One);
//            serialPort.Open();
//            MessageBox.Show($"串口 {portName} 初始化成功");
//        }
//        catch (Exception ex)
//        {
//            MessageBox.Show($"串口初始化失敗: {ex.Message}");
//        }
//    }

//    private void InitializeUI()
//    {
//        Text = "Modbus Control";
//        Width = 800;
//        Height = 600;

//        var portLabel = new Label
//        {
//            Text = "串口:",
//            Location = new System.Drawing.Point(10, 10),
//            AutoSize = true
//        };
//        Controls.Add(portLabel);

//        portSelector = new ComboBox
//        {
//            Location = new System.Drawing.Point(80, 10),
//            Width = 100
//        };
//        portSelector.Items.AddRange(SerialPort.GetPortNames());
//        Controls.Add(portSelector);

//        connectButton = new Button
//        {
//            Text = "連接",
//            Location = new System.Drawing.Point(190, 10),
//            Width = 80
//        };
//        connectButton.Click += (s, e) =>
//        {
//            if (portSelector.SelectedItem != null)
//            {
//                InitializeSerialPort(portSelector.SelectedItem.ToString());
//            }
//            else
//            {
//                MessageBox.Show("請選擇一個串口");
//            }
//        };
//        Controls.Add(connectButton);

//        var stationNumberLabel = new Label
//        {
//            Text = "站號:",
//            Location = new System.Drawing.Point(10, 50),
//            AutoSize = true
//        };
//        Controls.Add(stationNumberLabel);

//        stationNumberTextBox = new TextBox
//        {
//            Location = new System.Drawing.Point(60, 50),
//            Width = 50
//        };
//        stationNumberTextBox.TextChanged += (s, e) =>
//        {
//            if (byte.TryParse(stationNumberTextBox.Text, out byte result))
//            {
//                stationNumber = result;
//            }
//            else
//            {
//                MessageBox.Show("請輸入有效的站號 (0-255)");
//            }
//        };
//        Controls.Add(stationNumberTextBox);

//        // 初始化顯示 Label
//        var parameters = new (string Name, int Parameter)[]
//        {
//            ("當前狀態1", currentStatus1), ("當前狀態2", currentStatus2), ("漏電電流", leakageCurrent),
//            ("A相溫度", tempA), ("B相溫度", tempB), ("C相溫度", tempC), ("N相溫度", tempN),
//            ("A相電壓", voltageA), ("B相電壓", voltageB), ("C相電壓", voltageC),
//            ("A相電流", currentA), ("B相電流", currentB), ("C相電流", currentC),
//            ("A相功率因數", powerFactorA), ("B相功率因數", powerFactorB), ("C相功率因數", powerFactorC),
//            ("A相有功功率", activePowerA), ("B相有功功率", activePowerB), ("C相有功功率", activePowerC),
//            ("A相無功功率", reactivePowerA), ("B相無功功率", reactivePowerB), ("C相無功功率", reactivePowerC),
//            ("合閘次數", breakerTimes), ("電能高字節", energyHighByte), ("電能低字節", energyLowByte),
//            ("開關狀態", switchStatus), ("A相視在功率", apparentPowerA), ("B相視在功率", apparentPowerB),
//            ("C相視在功率", apparentPowerC), ("總視在功率", totalApparentPower),
//            ("總有功功率", totalActivePower), ("總無功功率", totalReactivePower),
//            ("合相功率因數", combinedPowerFactor), ("線頻率", lineFrequency), ("設備類型", deviceType),
//            ("歷史漏電值", historicalLeakage), ("歷史A相電流", historicalCurrentA),
//            ("歷史B相電流", historicalCurrentB), ("歷史C相電流", historicalCurrentC),
//            ("線路顏色標誌", lineColorMark)
//        };

//        int yOffset = 90;
//        foreach (var (name, _) in parameters)
//        {
//            var label = new Label
//            {
//                Text = $"{name}: 0",
//                Location = new System.Drawing.Point(10, yOffset),
//                AutoSize = true
//            };
//            Controls.Add(label);
//            yOffset += 30;
//        }

//        // Initialize other UI components such as buttons, etc.
//        // The rest of the code remains similar.
//    }

//    // The rest of the methods would go here, such as ReadAllParameters, OpenBreaker, CloseBreaker, etc.

//    [STAThread]
//    public static void Main()
//    {
//        Application.Run(new ModbusViewer());
//    }
//}


