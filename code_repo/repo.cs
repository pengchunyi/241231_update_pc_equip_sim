//using System;
//using System.Text.RegularExpressions;
//using System.Collections.Generic;
//using System.Threading;
//using System.Threading.Tasks;
//using System.Net.Http;
//using System.Windows.Forms;
//using Newtonsoft.Json.Linq;
//using Timer = System.Windows.Forms.Timer;
//using System.Xml.Linq;
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
//    private Button openButton, closeButton, scheduleButton, autoScheduleButton, manualModeButton;
//    private Label timeDisplay;
//    private ListBox scheduleListBox;
//    private Form scheduleWindow;
//    private bool isScheduleRunning = false;
//    private string[] daysOfWeek = { "SUN", "MON", "TUES", "WED", "THU", "FRI", "SAT" };
//    private string previousTimeString = "";

//    private TextBox stationNumberTextBox; // 站號輸入框
//    private byte stationNumber = 0xFF; // 默認站號

//    public ModbusViewer()
//    {
//        InitializeSerialPort();
//        InitializeUI();
//        //Task.Run(() => CallScheduleAPI()); // 啟動排程 API 更新
//        //Task.Run(() => RunSchedule());     // 啟動生產排程
//    }

//    private void InitializeSerialPort()
//    {
//        try
//        {
//            // 初始化串口
//            serialPort = new SerialPort("COM5", 9600, Parity.None, 8, StopBits.One);
//            serialPort.Open();
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

//        // 添加站號輸入框
//        var stationNumberLabel = new Label
//        {
//            Text = "站號:",
//            Location = new System.Drawing.Point(10, 10),
//            AutoSize = true
//        };
//        Controls.Add(stationNumberLabel);

//        stationNumberTextBox = new TextBox
//        {
//            Location = new System.Drawing.Point(60, 10),
//            Width = 50
//        };
//        stationNumberTextBox.TextChanged += (s, e) =>
//        {
//            if (byte.TryParse(stationNumberTextBox.Text, out byte result))
//            {
//                stationNumber = result; // 將站號保存為變數
//            }
//            else
//            {
//                MessageBox.Show("請輸入有效的站號 (0-255)"); // 輸入無效時顯示錯誤
//            }
//        };
//        Controls.Add(stationNumberTextBox);

//        for (int i = 0; i < 40; i++)
//        {
//            int col = i / 20;
//            int row = i % 20;
//            int xOffset = col * 400;
//            int yOffset = 50 + row * 30;

//            parameterLabels[i] = new Label()
//            {
//                Text = "0",  // 預設顯示為 "0"
//                Location = new System.Drawing.Point(10 + xOffset, yOffset),
//                AutoSize = true
//            };
//            Controls.Add(parameterLabels[i]);
//        }

//        openButton = new Button { Text = "開啟空開", Location = new System.Drawing.Point(10, 40) };
//        openButton.Click += (s, e) => OpenBreaker();
//        Controls.Add(openButton);

//        closeButton = new Button { Text = "關閉空開", Location = new System.Drawing.Point(170, 40) };
//        closeButton.Click += (s, e) => CloseBreaker();
//        Controls.Add(closeButton);

//        scheduleButton = new Button { Text = "顯示生產排程", Location = new System.Drawing.Point(330, 40) };
//        scheduleButton.Click += (s, e) => ShowScheduleWindow();
//        Controls.Add(scheduleButton);

//        autoScheduleButton = new Button { Text = "依照生產排程", Location = new System.Drawing.Point(500, 40) };
//        autoScheduleButton.Click += (s, e) => isScheduleRunning = !isScheduleRunning;
//        Controls.Add(autoScheduleButton);

//        manualModeButton = new Button { Text = "手動模式", Location = new System.Drawing.Point(670, 40) };
//        manualModeButton.Click += (s, e) => isScheduleRunning = false;
//        Controls.Add(manualModeButton);

//        timeDisplay = new Label { Text = "00:00:00", Location = new System.Drawing.Point(1000, 10) };
//        Controls.Add(timeDisplay);

//        var timer = new Timer();
//        timer.Interval = 1000;
//        timer.Tick += (s, e) => UpdateTimeDisplay();
//        timer.Start();
//    }

//    private async void CallScheduleAPI()
//    {
//        using (HttpClient client = new HttpClient())
//        {
//            var uri = "http://CNDGNMESIMQ001.delta.corp:10101/schedule/queryScheduleMo";
//            var requestBody = new
//            {
//                orgInfo = new { plant = "WJ3" },
//                clientInfo = new { user = "55319938", pcName = "CNWJxxxx", program = "test", programVer = "1.2" },
//                parameters = new { prodArea = "WJ3_SMT", lineName = "SMT-21", modelName = "", startDate = "2024-11-04" }
//            };
//            var jsonBody = JObject.FromObject(requestBody);
//            HttpContent content = new StringContent(jsonBody.ToString(), System.Text.Encoding.UTF8, "application/json");
//            content.Headers.Add("tokenID", "107574C7FAAA0EA5E0630ECA940AF1FE");

//            var response = await client.PostAsync(uri, content);
//            if (response.IsSuccessStatusCode)
//            {
//                var jsonResponse = await response.Content.ReadAsStringAsync();
//                var responseData = JObject.Parse(jsonResponse)["responseData"];
//                if (responseData != null)
//                {
//                    foreach (var item in responseData)
//                    {
//                        scheduleList.Add(new Schedule
//                        {
//                            PlanStartDate = item["planStartDate"].ToString(),
//                            PlanCloseDate = item["planCloseDate"].ToString()
//                        });
//                    }
//                }
//            }
//        }
//    }

//    private void OpenBreaker()
//    {
//        if (serialPort != null && serialPort.IsOpen)
//        {
//            byte[] openCommand = { stationNumber, 0x06, 0x00, 0x31, 0x55, 0x88, 0xF2, 0xED }; // 使用動態站號
//            serialPort.Write(openCommand, 0, openCommand.Length);
//        }
//    }

//    private void CloseBreaker()
//    {
//        if (serialPort != null && serialPort.IsOpen)
//        {
//            byte[] closeCommand = { stationNumber, 0x06, 0x00, 0x31, 0x55, 0x66, 0x72, 0xA1 }; // 使用動態站號
//            serialPort.Write(closeCommand, 0, closeCommand.Length);
//        }
//    }

//    private async void RunSchedule()
//    {
//        while (true)
//        {
//            if (isScheduleRunning)
//            {
//                DateTime now = DateTime.Now;
//                foreach (var schedule in scheduleList)
//                {
//                    if (now.Year == schedule.Year &&
//                        now.Month == schedule.Month &&
//                        now.Day == schedule.Day &&
//                        now.Hour == schedule.Hour &&
//                        now.Minute == schedule.Minute &&
//                        now.Second == schedule.Second)
//                    {
//                        if (schedule.Action == 1)
//                            OpenBreaker();
//                        else
//                            CloseBreaker();

//                        await Task.Delay(1000);
//                    }
//                }
//            }
//            await Task.Delay(1000);
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

//    private void RefreshScheduleList()
//    {
//        scheduleListBox.Items.Clear();
//        foreach (var schedule in scheduleList)
//        {
//            scheduleListBox.Items.Add($"{schedule.PlanStartDate} START --- {schedule.PlanCloseDate} CLOSE");
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