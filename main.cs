using System;  // 引用系統核心命名空間，提供基礎功能
using System.Collections.Generic;  // 引用泛型集合命名空間
using System.IO.Ports;  // 引用串口通訊命名空間，提供串口通訊支持
using System.Threading.Tasks;  // 引用多執行緒與非同步處理任務命名空間
using System.Windows.Forms;  // 引用Windows窗體應用程序命名空間
using CFX;  // 引用CFX（連接工廠交換）命名空間
using CFX.Transport;  // 引用CFX傳輸層命名空間
using CFX.ResourcePerformance;  // 引用CFX資源性能命名空間
using System.Timers; // 引入計時器的命名空間
using Newtonsoft.Json;
using System.Linq;


namespace AmqpModbusIntegration  // 命名空間，用於AMQP（高級消息隊列協議）和Modbus整合的程序
{
    // 定義一個類，ModbusViewer，繼承自Form，負責處理Modbus通訊和顯示界面
    public class ModbusViewer : Form
    {

        public SerialPort GetSerialPort() => serialPort;
        public Dictionary<byte, Dictionary<string, int>> GetSlaveData() => slaveData;
		//public Dictionary<byte, Dictionary<string, double>> GetSlaveData() => slaveData;


		private SerialPort serialPort; // 定義一個串口物件，用來與Modbus設備通訊
        private ComboBox portSelector; // 下拉選單，用於選擇可用的串口COM口
        private TextBox stationNumberTextBox; // 輸入框，用於輸入Modbus站號
        private Button connectButton, readButton, switchButton; // 分別用於連接和讀取數據、新增開關控制按鈕 
        private byte stationNumber = 0xFF; // 儲存Modbus站號，預設值為0xFF（255）

        // 顯示參數和數值的 Label 列表
        private List<Label> parameterLabels = new List<Label>();
        private List<Label> valueLabels = new List<Label>();

        //private Timer updateTimer; // 定義計時器
        private System.Windows.Forms.Timer updateTimer; // 明確指定為 Windows Forms Timer


        // 當計時器到期時執行的操作
        private void OnTimerElapsed()
        {
            // 必須在 UI 線程上執行 UpdateValues()，否則會拋出跨執行緒操作例外
            this.Invoke(new Action(() => UpdateValues()));
        }

        // 初始化計時器
        private void InitializeTimer()
        {
            updateTimer = new System.Windows.Forms.Timer(); // 創建 Windows Forms Timer
            updateTimer.Interval = 3000; // 設定時間間隔為 3000 毫秒（3 秒）
            updateTimer.Tick += (sender, e) => UpdateValues(); // 使用 Tick 事件直接調用 UpdateValues
            updateTimer.Start(); // 啟動計時器
        }

        // 當窗口載入時觸發的事件
        private void ModbusViewer_Load(object sender, EventArgs e)
        {
            // 在窗口載入時可以初始化一些設置
        }

        // 定義窗口界面的初始化方法
        private void InitializeComponent()
        {
            this.SuspendLayout(); // 暫停佈局更新
            this.ClientSize = new System.Drawing.Size(910, 530); // 設定窗口大小
            this.Name = "ModbusViewer"; // 設定窗口名稱
            this.Load += new System.EventHandler(this.ModbusViewer_Load); // 設定窗口載入事件
            this.ResumeLayout(false); // 恢復佈局更新
        }


        // 定義各種變數，用來儲存從設備讀取的各種參數
        public int currentStatus1, currentStatus2, currentStatus, leakageCurrent, tempA, tempB, tempC, tempN;
        public int voltageA, voltageB, voltageC, currentA, currentB, currentC;

        // 定義更多參數變數，用於儲存設備提供的其他數據
        public int powerFactorA, powerFactorB, powerFactorC, activePowerA, activePowerB, activePowerC;
        public int reactivePowerA, reactivePowerB, reactivePowerC, breakerTimes, energyHighByte, energyLowByte;
        public int energy;
        public int switchStatus, apparentPowerA, apparentPowerB, apparentPowerC, totalApparentPower, totalActivePower, totalReactivePower;
        public int combinedPowerFactor, lineFrequency, deviceType, historicalLeakage, historicalCurrentA, historicalCurrentB, historicalCurrentC;


        //      // 定義各種變數，用來儲存從設備讀取的各種參數
        //public int currentStatus1, currentStatus2, currentStatus; // 狀態表: 狀態數值不會太大，int 是合適的
        //public double leakageCurrent; // 漏電電流: 單位是 mA，範圍是 0-100，需要高精度，建議 double
        //public int tempA, tempB, tempC, tempN; // 溫度: 單位是攝氏度，範圍 0-127，int 是合適的

        //// 定義更多參數變數，用於儲存電壓和電流
        //public double voltageA, voltageB, voltageC; // 電壓: 單位 V，數值已乘以 10，建議 double
        //public double currentA, currentB, currentC; // 電流: 單位 A，數值已乘以 100，建議 double

        //// 定義更多參數變數，用於儲存功率和功率因數
        //public double powerFactorA, powerFactorB, powerFactorC; // 功率因數: 範圍 0-1，建議 double
        //public int activePowerA, activePowerB, activePowerC; // 有功功率: 單位 W，範圍是 0-50000，int 是合適的
        //public int reactivePowerA, reactivePowerB, reactivePowerC; // 無功功率: 單位 W，範圍是 0-50000，int 是合適的

        //// 合計值
        //public int breakerTimes; // 合閘次數: 整數，範圍不超過 50000，int 是合適的
        //public double energy; // 電能: 單位 kWh，範圍是高低位組合後除以 100，建議 double
        //public int switchStatus; // 開關狀態: 0 或 1，int 是合適的
        //public int apparentPowerA, apparentPowerB, apparentPowerC; // 視在功率: 單位 W，範圍是 0-65536，int 是合適的
        //public int totalApparentPower, totalActivePower, totalReactivePower; // 總功率: 單位 W，範圍是 0-65536，int 是合適的
        //public double combinedPowerFactor; // 合相功率因數: 範圍 0-1，建議 double
        //public double lineFrequency; // 線頻率: 單位 Hz，範圍是 0-100，建議 double

        //// 設備類型和歷史數據
        //public int deviceType; // 設備類型: 整數代碼（0-6），int 是合適的
        //public double historicalLeakage; // 歷史漏電值: 單位 mA，範圍 0-100，建議 double
        //public double historicalCurrentA, historicalCurrentB, historicalCurrentC; // 歷史電流: 單位 A，範圍已乘 100，建議 double
        //public int energyHighByte, energyLowByte;


        //public ModbusViewer()
        //{
        //    // 使用 UIInitializer 初始化界面
        //    UIInitializer.InitializeUI(
        //        this,
        //        out portSelector,
        //        out stationNumberTextBox,
        //        out connectButton,
        //        out readButton,
        //        out var switchOnButton,
        //        out var switchOffButton,
        //        out dataGridView);

        //    // 綁定按鈕事件
        //    connectButton.Click += (s, e) => InitializeSerialPort(portSelector.SelectedItem?.ToString());
        //    readButton.Click += async (s, e) => await ModbusHelper.ReadAllParametersAsync(serialPort, this);

        //    switchOnButton.Click += (s, e) => ModbusHelper.SwitchON(serialPort, stationNumber);
        //    switchOffButton.Click += (s, e) => ModbusHelper.SwitchOFF(serialPort, stationNumber);



        //    InitializeTimer(); // 初始化定时器
        //    InitializeParameters(); // 初始化参数
        //}
        public ModbusViewer()
        {
            // 使用 UIInitializer 初始化界面
            UIInitializer.InitializeUI(
                this,
                out portSelector,
                out stationNumberTextBox,
                out connectButton,
                out readButton,
                out var switchOnButton,
                out var switchOffButton,
                out dataGridView);

            // 綁定按鈕事件
            connectButton.Click += (s, e) => InitializeSerialPort(portSelector.SelectedItem?.ToString());
            readButton.Click += async (s, e) => await ModbusHelper.ReadAllParametersAsync(serialPort, this);

            // 綁定開關按鈕的多站號處理邏輯
            switchOnButton.Click += (s, e) => ExecuteSwitchCommand(ModbusHelper.SwitchON);
            switchOffButton.Click += (s, e) => ExecuteSwitchCommand(ModbusHelper.SwitchOFF);

            InitializeTimer(); // 初始化定時器
            InitializeParameters(); // 初始化參數
        }

        // 添加方法：處理開關開啟/關閉的通用邏輯
        private void ExecuteSwitchCommand(Action<SerialPort, byte> switchCommand)
        {
            if (serialPort == null || !serialPort.IsOpen)
            {
                MessageBox.Show("請先連接串口！");
                return;
            }

            // 從輸入框中解析站號列表
            var stationNumbers = stationNumberTextBox.Text
                .Split(',')
                .Select(s => s.Trim())
                .Where(s => byte.TryParse(s, out _)) // 過濾有效站號
                .Select(byte.Parse)
                .ToList();

            if (!stationNumbers.Any())
            {
                MessageBox.Show("請輸入有效的站號！");
                return;
            }

            // 依次對每個站號執行開關操作
            foreach (var station in stationNumbers)
            {
                try
                {
                    switchCommand(serialPort, station);
                    Console.WriteLine($"站號 {station} 的開關操作執行成功");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"執行站號 {station} 的開關操作時出現錯誤: {ex.Message}");
                }
            }
        }




        //20241121
        private DataGridView dataGridView; // DataGridView 用於顯示數據
		private Dictionary<byte, Dictionary<string, int>> slaveData = new Dictionary<byte, Dictionary<string, int>>(); // 存放站號及其參數數據
																													   //private Dictionary<byte, Dictionary<string, double>> slaveData = new Dictionary<byte, Dictionary<string, double>>();


		//private void UpdateDataGridView()
		//{
		//    if (dataGridView.InvokeRequired)
		//    {
		//        // 如果需要跨執行緒操作，使用 Invoke 調用
		//        dataGridView.Invoke(new Action(UpdateDataGridView));
		//        return;
		//    }

		//    // 確保在 UI 執行緒上執行以下代碼
		//    dataGridView.Columns.Clear();
		//    dataGridView.Rows.Clear();

		//    // 添加第一列：參數名稱
		//    dataGridView.Columns.Add("Parameter", "參數名稱");

		//    // 動態添加站號列
		//    foreach (var station in slaveData.Keys)
		//    {
		//        dataGridView.Columns.Add($"Slave_{station}", $"站號 {station}");
		//    }

		//    // 動態添加行：每個參數及其對應的值
		//    foreach (var parameter in slaveData.Values.SelectMany(d => d.Keys).Distinct())
		//    {
		//        var row = new List<object> { parameter }; // 第一列是參數名稱
		//        foreach (var station in slaveData.Keys)
		//        {
		//            // 如果站號有這個參數，填入數值；否則顯示 "N/A"
		//            row.Add(slaveData[station].ContainsKey(parameter) ? slaveData[station][parameter].ToString() : "N/A");
		//        }
		//        dataGridView.Rows.Add(row.ToArray());
		//    }
		//}
		private void UpdateDataGridView()
		{
			if (dataGridView.InvokeRequired)
			{
				dataGridView.Invoke(new Action(UpdateDataGridView));
				return;
			}

			// 記錄當前選中行和滾動位置
			int currentSelectedRowIndex = dataGridView.CurrentRow?.Index ?? -1;
			int firstDisplayedRowIndex = dataGridView.FirstDisplayedScrollingRowIndex;

			dataGridView.Columns.Clear();
			dataGridView.Rows.Clear();

			// 移除行首箭頭
			dataGridView.RowHeadersVisible = false;

			// 添加參數名稱列
			dataGridView.Columns.Add("Parameter", "參數名稱");

			// 添加站號列
			foreach (var station in slaveData.Keys)
			{
				dataGridView.Columns.Add($"Slave_{station}", $"站號 {station}");
			}

			// 收集所有參數
			var allParameters = slaveData.Values
				.SelectMany(d => d.Keys)
				.Distinct()
				.ToList();

			// 添加行數據
			foreach (var parameter in allParameters)
			{
				var row = new List<object> { parameter }; // 第一列是參數名稱
				foreach (var station in slaveData.Keys)
				{
					row.Add(slaveData[station].TryGetValue(parameter, out var value) ? value.ToString() : "N/A");
				}
				dataGridView.Rows.Add(row.ToArray());
			}

			// 恢復選中行
			if (currentSelectedRowIndex >= 0 && currentSelectedRowIndex < dataGridView.RowCount)
			{
				dataGridView.Rows[currentSelectedRowIndex].Selected = true;
				dataGridView.CurrentCell = dataGridView.Rows[currentSelectedRowIndex].Cells[0]; // 確保焦點保持在同一行
			}

			// 恢復滾動位置
			if (firstDisplayedRowIndex >= 0 && firstDisplayedRowIndex < dataGridView.RowCount)
			{
				dataGridView.FirstDisplayedScrollingRowIndex = firstDisplayedRowIndex;
			}
		}





		//updateTimer 每隔 3 秒觸發一次，並執行 UpdateValues()。如果上一次的操作尚未完成，下一次操作可能會重疊，導致執行緒競爭和性能問題。
		//添加一個 執行鎖（lock） 或標記來避免同時執行多個更新操作
		private bool isUpdating = false;
        private async void UpdateValues()
        {
            if (isUpdating) return; // 如果已經在更新，直接返回
            isUpdating = true;
            try
            {
                //await ReadAllParametersAsync(); // 使用異步方法
                //await ModbusHelper.ReadAllParametersAsync(serialPort, slaveData);
                await ModbusHelper.ReadAllParametersAsync(serialPort, this);


                this.Invoke(new Action(() => UpdateDataGridView()));
            }
            finally
            {
                isUpdating = false; // 更新完成，釋放鎖
            }
        }

        private void InitializeSerialPort(string portName)
        {
            if (serialPort != null && serialPort.IsOpen)
            {
                serialPort.Close();
                serialPort.Dispose();
            }

            // 將站號拆分，支持多站號
            slaveData.Clear();
            foreach (var station in stationNumberTextBox.Text.Split(','))
            {
                if (byte.TryParse(station.Trim(), out var stationNumber))
                {
                    slaveData[stationNumber] = new Dictionary<string, int>(); // 初始化該站號的數據
                }
                //if (byte.TryParse(station.Trim(), out var stationNumber))
                //{
                //	slaveData[stationNumber] = new Dictionary<string, int>(); // 修正為 double
                //}

            }

			// 創建串口並連接
			serialPort = new SerialPort(portName, 9600, Parity.None, 8, StopBits.One);
            try
            {
                serialPort.Open();
                MessageBox.Show($"串口 {portName} 連接成功");
            }
            catch (Exception ex)
            {
                MessageBox.Show($"串口連接失敗: {ex.Message}");
            }
        }

        // 初始化所有參數為 0
        private void InitializeParameters()
        {
            currentStatus1 = 0;
            currentStatus2 = 0;
            currentStatus = 0;
            leakageCurrent = 0;
            tempA = 0;
            tempB = 0;
            tempC = 0;
            tempN = 0;
            voltageA = 0;
            voltageB = 0;
            voltageC = 0;
            currentA = 0;
            currentB = 0;
            currentC = 0;
            powerFactorA = 0;
            powerFactorB = 0;
            powerFactorC = 0;
            activePowerA = 0;
            activePowerB = 0;
            activePowerC = 0;
            reactivePowerA = 0;
            reactivePowerB = 0;
            reactivePowerC = 0;
            breakerTimes = 0;
            energyHighByte = 0;
            energyLowByte = 0;
            energy = 0;
            switchStatus = 0;
            apparentPowerA = 0;
            apparentPowerB = 0;
            apparentPowerC = 0;
            totalApparentPower = 0;
            totalActivePower = 0;
            totalReactivePower = 0;
            combinedPowerFactor = 0;
            lineFrequency = 0;
            deviceType = 0;
            historicalLeakage = 0;
            historicalCurrentA = 0;
            historicalCurrentB = 0;
            historicalCurrentC = 0;
        }
    }

    internal class Program
    {
        static ModbusViewer modbusViewer = new ModbusViewer(); // 創建 ModbusViewer 窗口物件

        //[System.Runtime.InteropServices.DllImport("kernel32.dll")]
        //private static extern bool AllocConsole();

        [STAThread]
        static void Main()
        {
            //AllocConsole(); // 分配一個控制台窗口

            // 從 modbusViewer 獲取 serialPort 和 slaveData
            var serialPort = modbusViewer.GetSerialPort();
            var slaveData = modbusViewer.GetSlaveData();

            var amqpManager = new AmqpEndpointManager(
                "amqp://127.0.0.1:8888",
                "amqp://127.0.0.1:6666",
                "amqp://127.0.0.1:6666",
                //"amqp://10.181.56.175:30031",
                //"amqp://10.181.56.175:30031",
                modbusViewer,
                serialPort,
                slaveData
            );

            // 啟動 AMQP 端點並指定端點名稱
            amqpManager.StartAmqpEndpoint("CHUNYI_PC");

            // 啟動 Windows 應用程序
            Application.Run(modbusViewer);
        }
    }

}
