
//using System;
//using System.Collections.Generic;
//using System.IO.Ports;
//using System.Threading.Tasks;
//using System.Windows.Forms;
//using CFX;
//using CFX.Transport;
//using CFX.ResourcePerformance;
//using System.Timers;
//using Newtonsoft.Json;
//using System.Linq;
//using System.Threading;
//using System.IO;

//namespace AmqpModbusIntegration
//{
//	public class ModbusViewer : Form
//	{
//		// 建議只保留一個「公開屬性」，並提供get/set或提供一個GetSlaveData()方法
//		public Dictionary<byte, Dictionary<string, object>> slaveData = new Dictionary<byte, Dictionary<string, object>>();

//		private SerialPort serialPort;
//		private ComboBox portSelector;
//		private TextBox stationNumberTextBox;
//		private Button connectButton, readButton, switchButton;
//		private TextBox tempTextBox;
//		private Button tempSetButton;
//		private Button testFaultButton;

//		private List<Label> parameterLabels = new List<Label>();
//		private List<Label> valueLabels = new List<Label>();
//		private System.Windows.Forms.Timer updateTimer;

//		// 若需要可以再定義屬性
//		public SerialPort GetSerialPort() => serialPort;
//		public Dictionary<byte, Dictionary<string, object>> GetSlaveData() => slaveData;

//		public int currentStatus1, currentStatus2, currentStatus;
//		public int tempA, tempB, tempC, tempN;
//		public int powerFactorA, powerFactorB, powerFactorC, activePowerA, activePowerB, activePowerC;
//		public int reactivePowerA, reactivePowerB, reactivePowerC, breakerTimes;
//		public int switchStatus, apparentPowerA;
//		public int lineFrequency, deviceType;
//		public int historicalLeakage, historicalCurrentA, historicalCurrentB, historicalCurrentC;
//		public int ProtectionThreshold;


//		public double energy; // 用 double
//		public double voltageA, voltageB, voltageC, currentA, currentB, currentC;
//		public double apparentPowerB, apparentPowerC, totalApparentPower, totalActivePower, totalReactivePower, combinedPowerFactor;



//		// DataGridView
//		private DataGridView dataGridView;

//		private bool isUpdating = false;

//		private ListView iniListView;   // 顯示 INI
//		private TextBox logTextBox;    // 顯示送出的 CFX LOG





//		/// <summary>
//		/// 移除標題列上的 X 按鈕（CS_NOCLOSE）。
//		/// </summary>
//		protected override CreateParams CreateParams
//		{
//			get
//			{
//				const int CS_NOCLOSE = 0x200;
//				var cp = base.CreateParams;
//				cp.ClassStyle |= CS_NOCLOSE;
//				return cp;
//			}
//		}

//		public ModbusViewer()
//		{
//			// 設定工作列與 Alt+Tab 顯示的 icon
//			this.Icon = new System.Drawing.Icon("APP_ICON.ico");

//			// 初始化UI
//			UIInitializer.InitializeUI(
//				this,
//				out portSelector,
//				out stationNumberTextBox,
//				out connectButton,
//				out readButton,

//				out var switchOnButton,
//				out var switchOffButton,
//				out tempTextBox,
//				out tempSetButton,
//				out testFaultButton,

//				out var refreshButton,
//				out dataGridView,
//				out iniListView,
//				out logTextBox
//				);
//			// 建構式最後呼叫
//			RefreshIniView();





//			connectButton.Click += (s, e) => InitializeSerialPort(portSelector.SelectedItem?.ToString());

//			readButton.Click += async (s, e) =>
//			{
//				UpdateSlaveDataFromTextBox();
//				if (isUpdating) return;
//				readButton.Enabled = false;
//				try
//				{
//					await ModbusHelper.ReadAllParametersAsync(serialPort, this);
//					UpdateDataGridView();
//				}
//				finally
//				{
//					readButton.Enabled = true;
//				}
//			};

//			switchOnButton.Click += (s, e) => ExecuteSwitchCommand(ModbusHelper.SwitchON);
//			switchOffButton.Click += (s, e) => ExecuteSwitchCommand(ModbusHelper.SwitchOFF);

//			tempSetButton.Click += (s, e) =>
//			{
//				if (!byte.TryParse(stationNumberTextBox.Text, out byte stn))
//				{
//					MessageBox.Show("請輸入有效的站號！", "錯誤", MessageBoxButtons.OK, MessageBoxIcon.Error);
//					return;
//				}
//				if (!ushort.TryParse(tempTextBox.Text, out ushort temperature))
//				{
//					MessageBox.Show("請輸入有效的溫度值！", "錯誤", MessageBoxButtons.OK, MessageBoxIcon.Error);
//					return;
//				}
//				ExecuteSetTemperature(stn, temperature);
//			};

//			testFaultButton.Click += (s, e) =>
//			{
//				ModbusHelper.SimulateFaultTest(this, slaveData);
//			};

//			refreshButton.Click += (s, e) =>
//			{
//				portSelector.Items.Clear();
//				portSelector.Items.AddRange(SerialPort.GetPortNames());
//				MessageBox.Show("COM口已刷新！", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
//			};

//			InitializeTimer();
//			InitializeParameters();

//			// 當 Form 第一次顯示時自動嘗試連 COM 並讀取，直到成功為止
//			this.Shown += async (s, e) =>
//			{
//				connectButton.Enabled = false;         // 讀取期間不讓手動按「連接」
//				bool connected = false;
//				while (!connected)
//				{
//					try
//					{
//						// 用目前下拉選單裡的 port name
//						var portName = portSelector.SelectedItem?.ToString();
//						InitializeSerialPort(portName);

//						// 讀一次，若成功就跳出迴圈
//						await ModbusHelper.ReadAllParametersAsync(serialPort, this);
//						UpdateDataGridView();
//						connected = true;
//					}
//					catch
//					{
//						// 失敗就等 1 秒後再試
//						await Task.Delay(1000);
//					}
//				}

//				// 連線、讀取成功後，一直使用定時器自動更新
//				updateTimer.Start();
//			};


//			//250710新增=============================
//			// 設定 COM 口（顯示灰色且不可改）
//			if (!string.IsNullOrWhiteSpace(SwitchDeviceConfig.ComPort))
//			{
//				// 若 ComboBox 尚未包含 ini 設定的 port，則自動加入
//				if (!portSelector.Items.Contains(SwitchDeviceConfig.ComPort))
//				{
//					portSelector.Items.Add(SwitchDeviceConfig.ComPort);
//				}
//				portSelector.SelectedItem = SwitchDeviceConfig.ComPort;
//				portSelector.Enabled = false; // 禁止用戶選擇
//				portSelector.BackColor = System.Drawing.Color.LightGray; // 顯示灰色
//			}


//			// 設定站號（顯示灰色且不可改）
//			if (SwitchDeviceConfig.StationNumber != 0)
//			{
//				stationNumberTextBox.Text = SwitchDeviceConfig.StationNumber.ToString();
//				stationNumberTextBox.ReadOnly = true; // 禁止編輯
//				stationNumberTextBox.BackColor = System.Drawing.Color.LightGray;
//			}

//			//鎖住「分閘」按鈕（顯示灰色且不可改）
//			switchOffButton.Enabled = false;
//			switchOffButton.BackColor = System.Drawing.Color.LightGray;
//			//250710新增=============================




//			// ========================
//			// 下面這段是自動重啟自己，請一定放最後！
//			// ========================
//			this.FormClosing += (s, e) =>
//			{
//				// 重新啟動自己
//				System.Diagnostics.Process.Start(Application.ExecutablePath);
//			};



//		}

//		private void UpdateSlaveDataFromTextBox()
//		{
//			lock (slaveData)  // 使用鎖來確保同時只有一個線程操作 slaveData
//			{
//				slaveData.Clear();
//				foreach (var st in stationNumberTextBox.Text.Split(','))
//				{
//					if (byte.TryParse(st.Trim(), out var station))
//					{
//						// 若該站號不存在，就建立一個新的字典
//						if (!slaveData.ContainsKey(station))
//							slaveData[station] = new Dictionary<string, object>();
//					}
//				}
//			}
//		}


//		//--------------------------------------------------------------------
//		//  將 SystemConfig & SwitchDeviceConfig 內容刷新至 ListView
//		//--------------------------------------------------------------------
//		public void RefreshIniView()
//		{
//			iniListView.BeginUpdate();
//			iniListView.Items.Clear();


//			// 讀取 SystemConfig 反射屬性
//			foreach (var p in typeof(SystemConfig).GetProperties())
//			{
//				string key = p.Name;
//				string val = p.GetValue(null)?.ToString() ?? "";
//				iniListView.Items.Add(new ListViewItem(new[] { key, val }));
//			}
//			// 讀取 SwitchDeviceConfig
//			foreach (var p in typeof(SwitchDeviceConfig).GetProperties())
//			{
//				string key = p.Name;
//				string val = p.GetValue(null)?.ToString() ?? "";
//				iniListView.Items.Add(new ListViewItem(new[] { key, val }));
//			}
//			iniListView.EndUpdate();
//		}

//		//--------------------------------------------------------------------
//		//  讓外部呼叫追加 LOG
//		//--------------------------------------------------------------------
//		public void AppendLog(string txt)
//		{
//			if (logTextBox.InvokeRequired)
//			{
//				logTextBox.Invoke(new Action<string>(AppendLog), txt);
//				return;
//			}
//			logTextBox.AppendText($"[{DateTime.Now:HH:mm:ss}] {txt}\r\n");
//		}



//		public void ExecuteSwitchCommand(Action<SerialPort, byte> switchCommand, List<byte> stationNumbers = null)
//		{
//			if (serialPort == null || !serialPort.IsOpen)
//			{
//				MessageBox.Show("請先連接串口！");
//				return;
//			}
//			if (stationNumbers == null)
//			{
//				stationNumbers = stationNumberTextBox.Text
//					.Split(',')
//					.Select(x => x.Trim())
//					.Where(x => byte.TryParse(x, out _))
//					.Select(byte.Parse)
//					.ToList();
//			}
//			// 在發送第一個命令前添加100毫秒延遲，確保設備穩定
//			Thread.Sleep(100);
//			foreach (var station in stationNumbers)
//			{
//				if (!slaveData.ContainsKey(station))
//				{
//					Console.WriteLine($"站號 {station} 不存在，操作跳過。");
//					continue;
//				}
//				try
//				{
//					switchCommand(serialPort, station);
//					Console.WriteLine($"站號 {station} 的開關操作執行成功");

//					Thread.Sleep(500);

//				}
//				catch (Exception ex)
//				{
//					Console.WriteLine($"執行站號 {station} 的開關操作時出現錯誤: {ex.Message}");
//				}
//			}
//		}

//		public void ExecuteSetTemperature(byte stationNumber, ushort temperature)
//		{
//			if (serialPort == null || !serialPort.IsOpen)
//			{
//				Console.WriteLine("串口未開啟或無效，無法設定溫度");
//				return;
//			}
//			if (!slaveData.ContainsKey(stationNumber))
//			{
//				Console.WriteLine($"站號 {stationNumber} 不存在，操作中止。");
//				return;
//			}
//			Task.Run(() =>
//			{
//				lock (AmqpEndpointManager.serialPortLock)
//				{
//					ModbusHelper.SetTemperature(serialPort, stationNumber, temperature, slaveData);
//				}
//			});
//		}

//		private void InitializeTimer()
//		{
//			//updateTimer = new System.Windows.Forms.Timer();
//			//updateTimer.Interval = 1000;
//			//updateTimer.Tick += (s, e) => UpdateValues();
//			//updateTimer.Start();

//			//250714更新
//			updateTimer = new System.Windows.Forms.Timer { Interval = 1000 };
//			updateTimer.Tick += async (s, e) =>
//			{
//				try
//				{
//					await ModbusHelper.ReadAllParametersAsync(serialPort, this);
//					UpdateDataGridView();
//				}
//				catch { }
//			};


//		}

//		private async void UpdateValues()
//		{
//			if (isUpdating) return;
//			isUpdating = true;
//			try
//			{
//				await ModbusHelper.ReadAllParametersAsync(serialPort, this);
//				this.Invoke(new Action(() => UpdateDataGridView()));
//			}
//			finally
//			{
//				isUpdating = false;
//			}
//		}

//		public void UpdateDataGridView()
//		{
//			if (dataGridView.InvokeRequired)
//			{
//				dataGridView.Invoke(new Action(UpdateDataGridView));
//				return;
//			}

//			int currentSelectedRowIndex = dataGridView.CurrentRow?.Index ?? -1;
//			int firstDisplayedRowIndex = dataGridView.FirstDisplayedScrollingRowIndex;

//			dataGridView.Rows.Clear();
//			dataGridView.Columns.Clear();
//			dataGridView.RowHeadersVisible = false;

//			dataGridView.Columns.Add("Parameter", "參數名稱");

//			// 針對每個站號，新增一欄
//			foreach (var station in slaveData.Keys)
//			{
//				dataGridView.Columns.Add($"Slave_{station}", $"站號 {station}");
//			}

//			var allParameters = slaveData.Values
//										 .SelectMany(d => d.Keys)
//										 .Distinct()
//										 .ToList();

//			foreach (var parameter in allParameters)
//			{
//				var row = new List<object> { parameter };
//				foreach (var station in slaveData.Keys)
//				{
//					if (slaveData[station].TryGetValue(parameter, out var valObj))
//					{
//						row.Add(valObj?.ToString() ?? "N/A");
//					}
//					else
//					{
//						row.Add("N/A");
//					}
//				}
//				dataGridView.Rows.Add(row.ToArray());
//			}

//			if (currentSelectedRowIndex >= 0 && currentSelectedRowIndex < dataGridView.RowCount)
//			{
//				dataGridView.Rows[currentSelectedRowIndex].Selected = true;
//				dataGridView.CurrentCell = dataGridView.Rows[currentSelectedRowIndex].Cells[0];
//			}
//			if (firstDisplayedRowIndex >= 0 && firstDisplayedRowIndex < dataGridView.RowCount)
//			{
//				dataGridView.FirstDisplayedScrollingRowIndex = firstDisplayedRowIndex;
//			}
//		}

//		private void InitializeSerialPort(string portName)
//		{
//			if (serialPort != null && serialPort.IsOpen)
//			{
//				serialPort.Close();
//				serialPort.Dispose();
//			}
//			updateTimer.Stop();

//			slaveData.Clear();
//			foreach (var st in stationNumberTextBox.Text.Split(','))
//			{
//				if (byte.TryParse(st.Trim(), out var stationNumber))
//				{
//					slaveData[stationNumber] = new Dictionary<string, object>();
//				}
//			}

//			serialPort = new SerialPort(portName, 9600, Parity.None, 8, StopBits.One);
//			try
//			{
//				serialPort.Open();
//				Console.WriteLine($"串口 {portName} 已成功打開");
//				updateTimer.Start();
//			}
//			catch (Exception ex)
//			{
//				Console.WriteLine($"無法打開串口 {portName}: {ex.Message}");
//			}
//		}

//		private void InitializeParameters()
//		{
//			currentStatus1 = 0;
//			currentStatus2 = 0;
//			currentStatus = 0;

//			tempA = 0;
//			tempB = 0;
//			tempC = 0;
//			tempN = 0;

//			voltageA = 0;
//			voltageB = 0;
//			voltageC = 0;

//			currentA = 0;
//			currentB = 0;
//			currentC = 0;

//			powerFactorA = 0;
//			powerFactorB = 0;
//			powerFactorC = 0;

//			activePowerA = 0;
//			activePowerB = 0;
//			activePowerC = 0;

//			reactivePowerA = 0;
//			reactivePowerB = 0;
//			reactivePowerC = 0;

//			breakerTimes = 0;
//			energy = 0.0;
//			switchStatus = 0;

//			apparentPowerA = 0;
//			apparentPowerB = 0;
//			apparentPowerC = 0;

//			totalApparentPower = 0;
//			totalActivePower = 0;
//			totalReactivePower = 0;

//			combinedPowerFactor = 0;
//			lineFrequency = 0;
//			deviceType = 0;

//			historicalLeakage = 0;
//			historicalCurrentA = 0;
//			historicalCurrentB = 0;
//			historicalCurrentC = 0;

//			ProtectionThreshold = 0;
//		}
//	}

//	internal class Program
//	{
//		//類別 Program 在載入時就先 new ModbusViewer()
//		//-> 這時 SwitchDeviceConfig.ComPort 仍是 null（因為還沒讀 ini）。
//		//ModbusViewer 建構式看到 ComPort == null，就不會把 ComboBox 鎖定成灰色。
//		//之後 Main() 才呼叫 ConfigHelper.LoadConfiguration()，ComPort 才被填入 COM9，但 UI 已經建好，來不及更新。
//		//所以你不管怎麼改建構式邏輯，都抓不到 ini 的值。
//		//static ModbusViewer modbusViewer = new ModbusViewer();
//		// ❶ 先不要 new
//		static ModbusViewer modbusViewer;

//		//static Mutex mutex;


//		[STAThread]
//		static void Main()
//		{
//			// 單實例 Mutex 檢查
//			bool newInstance;
//			// 專案唯一名稱
//			using (var mutex = new Mutex(true, @"Global\CFX_SmartBreaker_Upload_Mutex", out newInstance))
//			{
//				if (!newInstance)
//				{
//					MessageBox.Show("已有一個 CFX 智慧空開上傳程式正在執行。", "單一執行", MessageBoxButtons.OK, MessageBoxIcon.Information);
//					return;
//				}



//				// ===== 先讀取 CFX.ini 設定 =====
//				try
//				{
//					ConfigHelper.LoadConfiguration();
//					Console.WriteLine("讀取到的 CFX.ini COM = " + SwitchDeviceConfig.ComPort);

//				}
//				catch (Exception ex)
//				{
//					MessageBox.Show("載入設定檔失敗: " + ex.Message);
//					return;
//				}
//				// ================================
//				// ❷ 讀完 ini 再 new，這時 SwitchDeviceConfig.ComPort 已有值
//				modbusViewer = new ModbusViewer();

//				// 取得 modbusViewer 中的 slaveData
//				var serialPort = modbusViewer.GetSerialPort();
//				var slaveData = modbusViewer.GetSlaveData();


//				var amqpManager = new AmqpEndpointManager(
//					SystemConfig.PublishAddress ?? "amqp://127.0.0.1:8888",
//					SystemConfig.MyRequestUri ?? "amqp://127.0.0.1:6666",
//					SystemConfig.MyRequestUri ?? "amqp://127.0.0.1:6666",
//					modbusViewer,
//					serialPort,
//					slaveData);


//				//勁諺給的I01插件機，也是sie上118顯示的
//				//amqpManager.StartAmqpEndpoint("CFX.A00.S056421215");
//				//I01插件機
//				//amqpManager.StartAmqpEndpoint("CFX.A00.ST07220001");
//				amqpManager.StartAmqpEndpoint(SystemConfig.MachineSN ?? "failed to read CFX.ini MachineSN");



//				Application.Run(modbusViewer);
//				//結束釋放 Mutex
//				mutex.ReleaseMutex();
//			}

//		}

//	}




//	//250710新增=======================
//	// 加入在 AmqpModbusIntegration namespace 內
//	public static class ConfigHelper
//	{

//		//配置檔路徑
//		private static readonly string IniPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "CFX.ini");


//		public static void LoadConfiguration()
//		{

//			Console.WriteLine("讀取的配置檔路徑: " + IniPath);

//			if (!File.Exists(IniPath))
//				throw new FileNotFoundException("找不到配置檔: " + IniPath);

//			string section = string.Empty;
//			foreach (var raw in File.ReadAllLines(IniPath))
//			{
//				var line = raw.Trim();
//				if (string.IsNullOrEmpty(line) || line.StartsWith(";")) continue;

//				if (line.StartsWith("[") && line.EndsWith("]"))
//				{
//					section = line.Substring(1, line.Length - 2);
//					continue;
//				}

//				int idx = line.IndexOf('=');
//				if (idx < 0) continue;

//				string key = line.Substring(0, idx).Trim();
//				string val = line.Substring(idx + 1).Trim();

//				//這邊這樣寫，是為了依照參數去做對應的函數處理
//				if (section.Equals("Configuration", StringComparison.OrdinalIgnoreCase))
//					ApplyConfiguration(key, val);

//				//這邊這樣寫，是為了依照參數去做對應的函數處理
//				else if (section.Equals("SwitchDevice", StringComparison.OrdinalIgnoreCase))
//					ApplySwitchDevice(key, val);
//			}
//		}

//		private static void ApplyConfiguration(string key, string value)
//		{
//			switch (key)
//			{
//				case "EquipmentName": SystemConfig.EquipmentName = value; break;
//				case "MachineSN": SystemConfig.MachineSN = value; break;
//				case "PublishAddress": SystemConfig.PublishAddress = value; break;
//				case "MyrequestUri": SystemConfig.MyRequestUri = value; break;
//			}
//		}

//		private static void ApplySwitchDevice(string key, string value)
//		{
//			switch (key)
//			{
//				case "COM": SwitchDeviceConfig.ComPort = value; break;
//				case "StationNumber": byte stn; if (byte.TryParse(value, out stn)) SwitchDeviceConfig.StationNumber = stn; break;
//			}
//		}
//	}

//	public static class SystemConfig
//	{
//		public static string EquipmentName { get; set; }
//		public static string MachineSN { get; set; }
//		public static string PublishAddress { get; set; }
//		public static string MyRequestUri { get; set; }
//	}

//	public static class SwitchDeviceConfig
//	{
//		public static string ComPort { get; set; }
//		public static byte StationNumber { get; set; }
//	}




//}
