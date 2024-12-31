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
		//20241129_新增=============================================
		private TextBox tempTextBox;
		private Button tempSetButton;
        //20241129_新增=============================================

        //20241202_新增=======================================
        private Button testFaultButton;



		private byte stationNumber = 0xFF; // 儲存Modbus站號，預設值為0xFF（255）

        // 顯示參數和數值的 Label 列表
        private List<Label> parameterLabels = new List<Label>();
        private List<Label> valueLabels = new List<Label>();

        //private Timer updateTimer; // 定義計時器
        private System.Windows.Forms.Timer updateTimer; // 明確指定為 Windows Forms Timer


		public void SetTemperatureTextboxValue(string value)
		{
			if (tempTextBox.InvokeRequired)
			{
				tempTextBox.Invoke(new Action(() => tempTextBox.Text = value));
			}
			else
			{
				tempTextBox.Text = value;
			}
		}




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
            updateTimer.Interval = 1000; // 設定時間間隔為 1000 毫秒（1 秒）
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
        public int ProtectionThreshold;


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
                out tempTextBox,//20241129_新增
				out tempSetButton, //20241129_新增
                out testFaultButton,

				out var refreshButton, // 20241204新增

				out dataGridView);

			// 綁定按鈕事件
			connectButton.Click += (s, e) => InitializeSerialPort(portSelector.SelectedItem?.ToString());
			readButton.Click += async (s, e) => await ModbusHelper.ReadAllParametersAsync(serialPort, this);
            // 綁定開關按鈕的多站號處理邏輯
            switchOnButton.Click += (s, e) => ExecuteSwitchCommand(ModbusHelper.SwitchON);
            switchOffButton.Click += (s, e) => ExecuteSwitchCommand(ModbusHelper.SwitchOFF);

			// 修正 tempSetButton.Click 事件，提供所需參數

			tempSetButton.Click += (s, e) =>
			{
				if (!byte.TryParse(stationNumberTextBox.Text, out byte stationNumber))
				{
					MessageBox.Show("請輸入有效的站號！", "錯誤", MessageBoxButtons.OK, MessageBoxIcon.Error);
					return;
				}

				if (!ushort.TryParse(tempTextBox.Text, out ushort temperature))
				{
					MessageBox.Show("請輸入有效的溫度值！", "錯誤", MessageBoxButtons.OK, MessageBoxIcon.Error);
					return;
				}

				ExecuteSetTemperature(stationNumber, temperature);
			};


			//20241129_新增===========================================================

			//20241202_故障測試新增====================================================
			// 綁定事件處理器
			testFaultButton.Click += (s, e) => SimulateFaultTest();
			//20241202_故障測試新增====================================================

			// 添加刷新按鈕的事件處理程序
			refreshButton.Click += (sender, args) =>
			{
				portSelector.Items.Clear();
				portSelector.Items.AddRange(SerialPort.GetPortNames());
				MessageBox.Show("COM口已刷新！", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
			};

			InitializeTimer(); // 初始化定時器
            InitializeParameters(); // 初始化參數
        }


		// 添加方法：處理開關開啟/關閉的通用邏輯
		public void ExecuteSwitchCommand(Action<SerialPort, byte> switchCommand, List<byte> stationNumbers = null)
		{
			if (serialPort == null || !serialPort.IsOpen)
			{
				MessageBox.Show("請先連接串口！");
				return;
			}

			// 如果未提供站號列表，從 TextBox 中解析站號
			if (stationNumbers == null)
			{
				stationNumbers = stationNumberTextBox.Text
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


		public void ExecuteSetTemperature(byte stationNumber, ushort temperature)
		{
			if (serialPort == null || !serialPort.IsOpen)
			{
				Console.WriteLine("串口未開啟或無效，無法設定溫度");
				return;
			}

			if (!slaveData.ContainsKey(stationNumber))
			{
				Console.WriteLine($"站號 {stationNumber} 不存在，操作中止。");
				return;
			}

			Task.Run(() =>
			{
				lock (AmqpEndpointManager.serialPortLock)
				{
					ModbusHelper.SetTemperature(serialPort, stationNumber, temperature, slaveData);
				}
			});
		}



		//20241129_新增===========================================================



		//20241121
		private DataGridView dataGridView; // DataGridView 用於顯示數據
		private Dictionary<byte, Dictionary<string, int>> slaveData = new Dictionary<byte, Dictionary<string, int>>();



        public void UpdateDataGridView()
{
    if (dataGridView.InvokeRequired)
    {
        // 如果不是在UI執行緒，使用Invoke調回UI執行緒
        dataGridView.Invoke(new Action(UpdateDataGridView));
        return;
    }

    // 保存當前選中行和第一個可視行，以便恢復視圖狀態
    int currentSelectedRowIndex = dataGridView.CurrentRow?.Index ?? -1;
    int firstDisplayedRowIndex = dataGridView.FirstDisplayedScrollingRowIndex;

    // 清理DataGridView中的所有行和列
    dataGridView.Rows.Clear();
    dataGridView.Columns.Clear();

    // 隱藏行標頭，提高視覺整潔
    dataGridView.RowHeadersVisible = false;

    // 添加「參數名稱」作為第一列
    dataGridView.Columns.Add("Parameter", "參數名稱");

    // 針對每個站號，新增一列
    foreach (var station in slaveData.Keys)
    {
        dataGridView.Columns.Add($"Slave_{station}", $"站號 {station}");
    }

    // 收集所有參數名稱（去重）
    var allParameters = slaveData.Values
                                 .SelectMany(d => d.Keys)
                                 .Distinct()
                                 .ToList();

    // 填充每個參數的值到DataGridView
    foreach (var parameter in allParameters)
    {
        var row = new List<object> { parameter }; // 第一個是參數名稱

        foreach (var station in slaveData.Keys)
        {
            // 從slaveData中取得對應的值，若無則顯示 "N/A"
            row.Add(slaveData[station].TryGetValue(parameter, out var value) ? value.ToString() : "N/A");
        }

        // 將這一行加入DataGridView
        dataGridView.Rows.Add(row.ToArray());
    }

    // 恢復之前的視圖狀態（選中行和滾動位置）
    if (currentSelectedRowIndex >= 0 && currentSelectedRowIndex < dataGridView.RowCount)
    {
        dataGridView.Rows[currentSelectedRowIndex].Selected = true;
        dataGridView.CurrentCell = dataGridView.Rows[currentSelectedRowIndex].Cells[0];
    }

    if (firstDisplayedRowIndex >= 0 && firstDisplayedRowIndex < dataGridView.RowCount)
    {
        dataGridView.FirstDisplayedScrollingRowIndex = firstDisplayedRowIndex;
    }
}



		//20241218修正==========================================================




		//updateTimer 每隔 3 秒觸發一次，並執行 UpdateValues()。如果上一次的操作尚未完成，下一次操作可能會重疊，導致執行緒競爭和性能問題。
		//添加一個 執行鎖（lock） 或標記來避免同時執行多個更新操作
		private bool isUpdating = false;
        private async void UpdateValues()
        {
            if (isUpdating) return; // 如果已經在更新，直接返回
            isUpdating = true;
            try
            {
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

			slaveData.Clear();
			foreach (var station in stationNumberTextBox.Text.Split(','))
			{
				if (byte.TryParse(station.Trim(), out var stationNumber))
				{
					slaveData[stationNumber] = new Dictionary<string, int>();
				}
			}

			serialPort = new SerialPort(portName, 9600, Parity.None, 8, StopBits.One);
			try
			{
				serialPort.Open();
				Console.WriteLine($"串口 {portName} 已成功打開");
			}
			catch (Exception ex)
			{
				Console.WriteLine($"無法打開串口 {portName}: {ex.Message}");
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
            ProtectionThreshold = 0;    
        }



		//20241202新增=======================================
		// 在 ModbusViewer 類中新增方法
		private void SimulateFaultTest()
		{
			// 模擬故障碼 0xA87
			int faultCode = 0xA87;

			// 更新 currentStatus 和 currentStatus1, currentStatus2
			currentStatus = faultCode;
			currentStatus1 = (faultCode >> 16) & 0xFFFF; // 高16位
			currentStatus2 = faultCode & 0xFFFF;        // 低16位

			// 更新 slaveData 中的 "當前狀態"
			foreach (var station in slaveData.Keys)
			{
				if (slaveData[station].ContainsKey("當前狀態"))
				{
					slaveData[station]["當前狀態"] = currentStatus;
				}
			}

			// 更新 DataGridView 顯示
			UpdateDataGridView();

			// 提示用戶操作成功
			MessageBox.Show($"已將當前狀態設置為故障碼 {faultCode:X}");
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
                "amqp://127.0.0.1:6666",//"amqp://10.181.56.175:30031",
				"amqp://127.0.0.1:6666",//"amqp://10.181.56.175:30031",
				modbusViewer,
                serialPort,
                slaveData
            );


            //這邊應該把_PC改成幾個變數，依照站別可以去命名
            // 啟動 AMQP 端點並指定端點名稱
            amqpManager.StartAmqpEndpoint("CHUNYI_PC");



			// 啟動 Windows 應用程序
			Application.Run(modbusViewer);
        }



	}

}
