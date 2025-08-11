//using AmqpModbusIntegration.Services;
//using CFX;
//using CFX.ResourcePerformance;
//using CFX.Transport;
//using System;
//using System.Collections.Generic;
//using System.Drawing;
//using System.IO;
//using System.IO.Ports;
//using System.Linq;
//using System.Threading;
//using System.Threading.Tasks;
//using System.Windows.Forms;

//namespace AmqpModbusIntegration
//{
//	// 簡單檔案 Logger（AppendLog 會呼叫）
//	internal static class FileLogger
//	{
//		private static readonly object _lock = new object();
//		private static string _path;
//		public static void Init()
//		{
//			string dir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "logs");
//			if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
//			_path = Path.Combine(dir, $"CFX_{DateTime.Now:yyyyMMdd_HHmmss}.log");
//			SafeAppend($"==== App Start {DateTime.Now:yyyy-MM-dd HH:mm:ss} ====\r\n");
//		}
//		public static void SafeAppend(string text)
//		{
//			lock (_lock)
//			{
//				try { File.AppendAllText(_path, text); } catch { }
//			}
//		}
//	}

//	internal class Program
//	{
//		static ModbusViewer viewer;
//		private static ConnectionManager connMgr;

//		[STAThread]
//		static void Main()
//		{
//			bool newInstance;
//			using (var mutex = new Mutex(true, @"Global\CFX_SmartBreaker_Upload_Mutex", out newInstance))
//			{
//				if (!newInstance)
//				{
//					MessageBox.Show("已有一個 CFX 智慧空開上傳程式正在執行。", "單一執行", MessageBoxButtons.OK, MessageBoxIcon.Information);
//					return;
//				}

//				FileLogger.Init();

//				Application.EnableVisualStyles();
//				Application.SetCompatibleTextRenderingDefault(false);
//				viewer = new ModbusViewer();

//				try
//				{
//					ConfigHelper.LoadConfiguration();
//					viewer.RefreshIniView();
//					viewer.SetComPortUI();
//				}
//				catch (Exception ex)
//				{
//					MessageBox.Show("載入設定檔失敗: " + ex.Message);
//					return;
//				}

//				if (!string.IsNullOrWhiteSpace(SwitchDeviceConfig.ComPort))
//				{
//					try
//					{
//						viewer.InitializeSerialPort(SwitchDeviceConfig.ComPort);
//						viewer.AppendLog("初始化配置檔串口成功: " + SwitchDeviceConfig.ComPort);
//					}
//					catch (Exception ex)
//					{
//						viewer.AppendLog("初始化配置檔串口失敗: " + ex.Message);
//					}
//				}
//				else
//				{
//					viewer.AppendLog("配置檔未賦予 COM 口，請確認配置或手動選擇。");
//				}

//				try
//				{
//					var amqpManager = new AmqpEndpointManager(
//						SystemConfig.PublishAddress ?? "amqp://127.0.0.1:8888",
//						SystemConfig.MyRequestUri ?? "amqp://127.0.0.1:6666",
//						SystemConfig.MyRequestUri ?? "amqp://127.0.0.1:6666",
//						viewer,
//						viewer.slaveData);

//					connMgr = new ConnectionManager(viewer, amqpManager);
//					connMgr.Start();

//					viewer.AppendLog("AMQP 與背景連線已啟動，等待空開連接...");
//				}
//				catch (Exception ex)
//				{
//					viewer.AppendLog("AMQP 或背景連線初始化失敗: " + ex.Message);
//				}

//				Application.Run(viewer);

//				connMgr?.Stop();
//				mutex.ReleaseMutex();
//			}
//		}
//	}

//	public static class ConfigHelper
//	{
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

//				if (section.Equals("Configuration", StringComparison.OrdinalIgnoreCase))
//					ApplyConfiguration(key, val);
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
//				case "COM":
//					SwitchDeviceConfig.ComPort = value;
//					break;
//				case "StationNumber":
//					var stationList = value.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
//					SwitchDeviceConfig.StationNumber = stationList
//						.Select(s => s.Trim())
//						.Where(s => byte.TryParse(s, out _))
//						.Select(byte.Parse)
//						.ToList();
//					break;
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
//		public static List<byte> StationNumber { get; set; } = new List<byte>();
//	}

//	public class ModbusViewer : Form
//	{
//		public Dictionary<byte, Dictionary<string, object>> slaveData = new Dictionary<byte, Dictionary<string, object>>();

//		private SerialPort serialPort;
//		private ComboBox portSelector;
//		private TextBox stationNumberTextBox;
//		private Button connectButton, readButton;
//		private TextBox tempTextBox;
//		private Button tempSetButton;
//		private Button testFaultButton;
//		private DataGridView dataGridView;
//		private bool isUpdating = false;
//		private ListView iniListView;
//		private TextBox logTextBox;
//		private System.Windows.Forms.Timer updateTimer;

//		public SerialPort GetSerialPort() => serialPort;

//		public int currentStatus1, currentStatus2, currentStatus;
//		public int tempA, tempB, tempC, tempN;
//		public int powerFactorA, powerFactorB, powerFactorC, activePowerA, activePowerB, activePowerC;
//		public int reactivePowerA, reactivePowerB, reactivePowerC, breakerTimes;
//		public int switchStatus, apparentPowerA;
//		public int lineFrequency, deviceType;
//		public int historicalLeakage, historicalCurrentA, historicalCurrentB, historicalCurrentC;
//		public int ProtectionThreshold;
//		public double energy;
//		public double voltageA, voltageB, voltageC, currentA, currentB, currentC;
//		public double apparentPowerB, apparentPowerC, totalApparentPower, totalActivePower, totalReactivePower, combinedPowerFactor;

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
//			try { this.Icon = new Icon("APP_ICON.ico"); } catch { }

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
//			);

//			RefreshIniView();

//			if (!string.IsNullOrWhiteSpace(SwitchDeviceConfig.ComPort))
//			{
//				portSelector.Items.Clear();
//				portSelector.Items.Add(SwitchDeviceConfig.ComPort);
//				portSelector.SelectedItem = SwitchDeviceConfig.ComPort;
//			}
//			if (SwitchDeviceConfig.StationNumber?.Count > 0 &&
//				SwitchDeviceConfig.StationNumber.All(stn => stn > 0))
//			{
//				stationNumberTextBox.Text = string.Join(",", SwitchDeviceConfig.StationNumber);
//			}

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
//				finally { readButton.Enabled = true; }
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

//			testFaultButton.Click += (s, e) => ModbusHelper.SimulateFaultTest(this, slaveData);

//			refreshButton.Click += (s, e) =>
//			{
//				portSelector.Items.Clear();
//				portSelector.Items.AddRange(SerialPort.GetPortNames());
//				MessageBox.Show("COM口已刷新！", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
//			};

//			InitializeTimer();
//			InitializeParameters();

//			this.Shown += (_, __) => updateTimer.Start();

//			var controlsToLock = new List<Control> {
//				connectButton, readButton, tempSetButton, testFaultButton,
//				switchOnButton, switchOffButton, refreshButton, portSelector,
//				stationNumberTextBox, tempTextBox
//			};

//			bool isDevMode = false;
//			var devModeButton = new Button { Text = "開發者模式", Location = new Point(700, 50), Size = new Size(100, 25) };
//			this.Controls.Add(devModeButton);

//			void SetUserMode()
//			{
//				foreach (var ctl in controlsToLock) { ctl.Enabled = false; ctl.BackColor = SystemColors.ControlLight; }
//				devModeButton.Text = "開發者模式"; isDevMode = false;
//			}
//			void SetDevMode()
//			{
//				foreach (var ctl in controlsToLock) { ctl.Enabled = true; ctl.BackColor = Color.White; }
//				devModeButton.Text = "使用者模式"; isDevMode = true;
//			}
//			SetUserMode();

//			devModeButton.Click += (s, e) =>
//			{
//				if (!isDevMode)
//				{
//					string pwd = "";
//					using (var prompt = new Form())
//					{
//						prompt.Width = 300; prompt.Height = 150; prompt.Text = "開發者模式";
//						prompt.FormBorderStyle = FormBorderStyle.FixedDialog;
//						prompt.StartPosition = FormStartPosition.CenterParent;

//						var lbl = new Label() { Left = 10, Top = 10, Text = "請輸入密碼：" };
//						var txt = new TextBox() { Left = 10, Top = 40, Width = 260, PasswordChar = '●' };
//						var btnOk = new Button() { Text = "確定", Left = 200, Top = 70, DialogResult = DialogResult.OK };

//						prompt.Controls.Add(lbl); prompt.Controls.Add(txt); prompt.Controls.Add(btnOk);
//						prompt.AcceptButton = btnOk;

//						if (prompt.ShowDialog(this) == DialogResult.OK) pwd = txt.Text;
//					}
//					if (pwd == "pengchunyi") SetDevMode();
//					else MessageBox.Show("密碼錯誤", "警告", MessageBoxButtons.OK, MessageBoxIcon.Warning);
//				}
//				else SetUserMode();
//			};

//			this.FormClosing += (s, e) => System.Diagnostics.Process.Start(Application.ExecutablePath);
//		}

//		private void UpdateSlaveDataFromTextBox()
//		{
//			lock (slaveData)
//			{
//				slaveData.Clear();
//				foreach (var st in stationNumberTextBox.Text.Split(','))
//				{
//					if (byte.TryParse(st.Trim(), out var station))
//						if (!slaveData.ContainsKey(station))
//							slaveData[station] = new Dictionary<string, object>();
//				}
//			}
//		}

//		public void SetComPortUI()
//		{
//			if (!string.IsNullOrWhiteSpace(SwitchDeviceConfig.ComPort))
//			{
//				portSelector.Items.Clear();
//				portSelector.Items.Add(SwitchDeviceConfig.ComPort);
//				portSelector.SelectedItem = SwitchDeviceConfig.ComPort;
//			}
//			if (SwitchDeviceConfig.StationNumber?.Count > 0)
//				stationNumberTextBox.Text = string.Join(",", SwitchDeviceConfig.StationNumber);
//		}

//		public void RefreshIniView()
//		{
//			iniListView.BeginUpdate();
//			iniListView.Items.Clear();

//			foreach (var p in typeof(SystemConfig).GetProperties())
//			{
//				string key = p.Name;
//				object raw = p.GetValue(null);
//				iniListView.Items.Add(new ListViewItem(new[] { key, raw?.ToString() ?? "" }));
//			}
//			foreach (var p in typeof(SwitchDeviceConfig).GetProperties())
//			{
//				string key = p.Name;
//				object raw = p.GetValue(null);
//				if (raw is IEnumerable<byte> list)
//					iniListView.Items.Add(new ListViewItem(new[] { key, string.Join(",", list) }));
//				else
//					iniListView.Items.Add(new ListViewItem(new[] { key, raw?.ToString() ?? "" }));
//			}
//			iniListView.EndUpdate();
//		}

//		public void AppendLog(string txt)
//		{
//			if (logTextBox.InvokeRequired)
//			{
//				logTextBox.Invoke(new Action<string>(AppendLog), txt);
//				return;
//			}
//			string line = $"[{DateTime.Now:HH:mm:ss}] {txt}\r\n";
//			logTextBox.AppendText(line);
//			FileLogger.SafeAppend(line);
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

//			Thread.Sleep(100);
//			foreach (var station in stationNumbers)
//			{
//				if (!slaveData.ContainsKey(station)) continue;
//				try
//				{
//					switchCommand(serialPort, station);
//					Thread.Sleep(200);
//				}
//				catch (Exception ex)
//				{
//					Console.WriteLine($"站號 {station} 開關操作錯誤: {ex.Message}");
//				}
//			}
//		}

//		public void ExecuteSetTemperature(byte stationNumber, ushort temperature)
//		{
//			if (serialPort == null || !serialPort.IsOpen) return;
//			if (!slaveData.ContainsKey(stationNumber)) return;

//			Task.Run(() =>
//			{
//				ModbusHelper.SetTemperature(serialPort, stationNumber, temperature, slaveData);
//			});
//		}

//		private void InitializeTimer()
//		{
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

//		public void UpdateDataGridView()
//		{
//			if (dataGridView.InvokeRequired)
//			{
//				dataGridView.Invoke(new Action(UpdateDataGridView));
//				return;
//			}

//			int selRow = dataGridView.CurrentRow?.Index ?? -1;
//			int firstRow = dataGridView.FirstDisplayedScrollingRowIndex;

//			dataGridView.Rows.Clear();
//			dataGridView.Columns.Clear();
//			dataGridView.RowHeadersVisible = false;

//			dataGridView.Columns.Add("Parameter", "參數名稱");
//			foreach (var station in slaveData.Keys)
//				dataGridView.Columns.Add($"Slave_{station}", $"站號 {station}");

//			var allParams = slaveData.Values.SelectMany(d => d.Keys).Distinct().ToList();
//			foreach (var p in allParams)
//			{
//				var row = new List<object> { p };
//				foreach (var station in slaveData.Keys)
//					row.Add(slaveData[station].TryGetValue(p, out var v) ? (v?.ToString() ?? "N/A") : "N/A");
//				dataGridView.Rows.Add(row.ToArray());
//			}

//			if (selRow >= 0 && selRow < dataGridView.RowCount)
//			{
//				dataGridView.Rows[selRow].Selected = true;
//				dataGridView.CurrentCell = dataGridView.Rows[selRow].Cells[0];
//			}
//			if (firstRow >= 0 && firstRow < dataGridView.RowCount)
//				dataGridView.FirstDisplayedScrollingRowIndex = firstRow;
//		}

//		public void InitializeSerialPort(string portName)
//		{
//			if (string.IsNullOrWhiteSpace(portName))
//			{
//				AppendLog("請選擇有效的 COM 口。");
//				return;
//			}

//			try
//			{
//				if (serialPort != null)
//				{
//					if (serialPort.IsOpen) serialPort.Close();
//					serialPort.Dispose();
//				}
//			}
//			catch { }

//			updateTimer?.Stop();

//			slaveData.Clear();
//			foreach (var st in stationNumberTextBox.Text.Split(','))
//			{
//				if (byte.TryParse(st.Trim(), out var stationNumber))
//					slaveData[stationNumber] = new Dictionary<string, object>();
//			}

//			serialPort = new SerialPort(portName, 9600, Parity.None, 8, StopBits.One);
//			serialPort.ReadTimeout = 500;
//			serialPort.WriteTimeout = 500;
//			serialPort.Handshake = Handshake.None;

//			try
//			{
//				serialPort.Open();
//				AppendLog($"串口 {portName} 已成功打開");
//				updateTimer.Start();
//			}
//			catch (Exception ex)
//			{
//				AppendLog($"無法打開串口 {portName}: {ex.Message}");
//			}
//		}

//		private void InitializeParameters()
//		{
//			currentStatus1 = currentStatus2 = currentStatus = 0;
//			tempA = tempB = tempC = tempN = 0;
//			voltageA = voltageB = voltageC = 0;
//			currentA = currentB = currentC = 0;
//			powerFactorA = powerFactorB = powerFactorC = 0;
//			activePowerA = activePowerB = activePowerC = 0;
//			reactivePowerA = reactivePowerB = reactivePowerC = 0;
//			breakerTimes = 0;
//			energy = 0.0;
//			switchStatus = 0;
//			apparentPowerA = 0; apparentPowerB = 0; apparentPowerC = 0;
//			totalApparentPower = totalActivePower = totalReactivePower = 0;
//			combinedPowerFactor = 0; lineFrequency = 0; deviceType = 0;
//			historicalLeakage = historicalCurrentA = historicalCurrentB = historicalCurrentC = 0;
//			ProtectionThreshold = 0;
//		}
//	}
//}
