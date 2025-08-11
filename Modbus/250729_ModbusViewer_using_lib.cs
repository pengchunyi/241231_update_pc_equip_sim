////// ModbusViewer.cs
////using System;
////using System.Collections.Generic;
////using System.Drawing;
////using System.IO.Ports;
////using System.Linq;
////using System.Threading;
////using System.Threading.Tasks;
////using System.Windows.Forms;

////namespace AmqpModbusIntegration
////{
////	/// <summary>
////	/// 這個視窗負責顯示和操作 Modbus 設備的參數
////	/// </summary>
////	public class ModbusViewer : Form
////	{

////		// --------------------------
////		// 一、欄位與屬性
////		// --------------------------
////		public Dictionary<byte, Dictionary<string, object>> slaveData = new Dictionary<byte, Dictionary<string, object>>();

////		private SerialPort serialPort;
////		private ComboBox portSelector;
////		private TextBox stationNumberTextBox;
////		private Button connectButton, readButton;
////		private TextBox tempTextBox;
////		private Button tempSetButton;
////		private Button testFaultButton;
////		private DataGridView dataGridView;
////		private bool isUpdating = false;
////		private ListView iniListView;
////		private TextBox logTextBox;
////		private System.Windows.Forms.Timer updateTimer;


////		private volatile bool _online = false;
////		private string _baseTitle;


////		// 各種監控參數欄位（初始化在下面的 InitializeParameters）


////		public int currentStatus1, currentStatus2, currentStatus;
////		public int tempA, tempB, tempC, tempN;
////		public int powerFactorA, powerFactorB, powerFactorC, activePowerA, activePowerB, activePowerC;
////		public int reactivePowerA, reactivePowerB, reactivePowerC, breakerTimes;
////		public int switchStatus, apparentPowerA;
////		public int lineFrequency, deviceType;
////		public int historicalLeakage, historicalCurrentA, historicalCurrentB, historicalCurrentC;
////		public int ProtectionThreshold;
////		public double energy;
////		public double voltageA, voltageB, voltageC, currentA, currentB, currentC;
////		public double apparentPowerB, apparentPowerC, totalApparentPower, totalActivePower, totalReactivePower, combinedPowerFactor;

////		// ============ 開發者模式控制區 ============
////		/// <summary>列出要鎖定/解鎖的所有控制元件</summary>
////		private List<Control> controlsToLock;
////		/// <summary>目前是否為開發者模式</summary>
////		private bool isDevMode = false;
////		/// <summary>切換模式的按鈕</summary>
////		private Button devModeButton;



////		// 禁止關閉按鈕
////		protected override CreateParams CreateParams
////		{
////			get
////			{
////				const int CS_NOCLOSE = 0x200;
////				var cp = base.CreateParams;
////				cp.ClassStyle |= CS_NOCLOSE;
////				return cp;
////			}
////		}




////		// --------------------------
////		// 二、建構子
////		// --------------------------
////		public ModbusViewer()
////		{


////			// 1. 設定UI圖示、標題
////			try { this.Icon = new Icon("APP_ICON.ico"); } catch { }
////			_baseTitle = "PC Base Equipment Monitor";
////			this.Text = _baseTitle;



////			// 2. 初始化/產生 UI 控制元件
////			UIInitializer.InitializeUI(
////				this,
////				out portSelector,
////				out stationNumberTextBox,
////				out connectButton,
////				out readButton,
////				out var switchOnButton,
////				out var switchOffButton,
////				out tempTextBox,
////				out tempSetButton,
////				out testFaultButton,
////				out var refreshButton,
////				out dataGridView,
////				out iniListView,
////				out logTextBox
////			);

////			// 3. 設定UI內容
////			// 初始化/刷新配置顯示
////			RefreshIniView();

////			// 4. 載入預設 ComPort 與站號
////			if (!string.IsNullOrWhiteSpace(SwitchDeviceConfig.ComPort))
////			{
////				portSelector.Items.Clear();
////				portSelector.Items.Add(SwitchDeviceConfig.ComPort);
////				portSelector.SelectedItem = SwitchDeviceConfig.ComPort;
////			}
////			if (SwitchDeviceConfig.StationNumber?.Count > 0 &&
////				SwitchDeviceConfig.StationNumber.All(stn => stn > 0))
////			{
////				stationNumberTextBox.Text = string.Join(",", SwitchDeviceConfig.StationNumber);
////			}


////			// 5. 綁定UI button觸發事件
////			connectButton.Click += (s, e) => SafeInitializeSerialPort(portSelector.SelectedItem?.ToString());
////			readButton.Click += async (s, e) =>
////			{
////				UpdateSlaveDataFromTextBox();
////				if (isUpdating) 
////					return;

////				readButton.Enabled = false;

////				try
////				{
////					if (serialPort == null || !serialPort.IsOpen)
////					{
////						MessageBox.Show("請先連接串口！");
////					}
////					else
////					{
////						await ModbusHelper.ReadAllParametersAsync(serialPort, this);
////						UpdateDataGridView();
////					}
////				}

////				finally { readButton.Enabled = true; }
////			};

////			switchOnButton.Click += (s, e) => ExecuteSwitchCommand(ModbusHelper.SwitchON);
////			switchOffButton.Click += (s, e) => ExecuteSwitchCommand(ModbusHelper.SwitchOFF);
////			tempSetButton.Click += (s, e) =>
////			{
////				if (!byte.TryParse(stationNumberTextBox.Text, out byte stn))
////				{
////					MessageBox.Show("請輸入有效的站號！", "錯誤", MessageBoxButtons.OK, MessageBoxIcon.Error);
////					return;
////				}
////				if (!ushort.TryParse(tempTextBox.Text, out ushort temperature))
////				{
////					MessageBox.Show("請輸入有效的溫度值！", "錯誤", MessageBoxButtons.OK, MessageBoxIcon.Error);
////					return;
////				}
////				ExecuteSetTemperature(stn, temperature);
////			};
////			 //testFaultButton.Click += (s, e) => ModbusHelper.SimulateFaultTest(this, slaveData);
////			refreshButton.Click += (s, e) =>
////			{
////				portSelector.Items.Clear();
////				portSelector.Items.AddRange(SerialPort.GetPortNames());
////				MessageBox.Show("COM口已刷新！", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
////			};


////			// 6. 初始化 Timer 與參數
////			InitializeTimer();
////			InitializeParameters();
////			this.Shown += (_, __) => updateTimer.Start();


////			// 7. 準備開發者模式控制元件
////			controlsToLock = new List<Control> {
////				connectButton, readButton, tempSetButton, testFaultButton,
////				switchOnButton, switchOffButton, refreshButton, portSelector,
////				stationNumberTextBox, tempTextBox
////			};

////			//bool isDevMode = false;
////			devModeButton = new Button { Text = "開發者模式", Location = new Point(700, 50), Size = new Size(100, 25) };
////			this.Controls.Add(devModeButton);

////			SetUserMode();

////			devModeButton.Click += (s, e) =>
////			{
////				if (!isDevMode)
////				{
////					string pwd = "";
////					using (var prompt = new Form())
////					{
////						prompt.Width = 300; 
////						prompt.Height = 150; 
////						prompt.Text = "開發者模式";
////						prompt.FormBorderStyle = FormBorderStyle.FixedDialog;
////						prompt.StartPosition = FormStartPosition.CenterParent;

////						var lbl = new Label() { Left = 10, Top = 10, Text = "請輸入密碼：" };
////						var txt = new TextBox() { Left = 10, Top = 40, Width = 260, PasswordChar = '●' };
////						var btnOk = new Button() { Text = "確定", Left = 200, Top = 70, DialogResult = DialogResult.OK };

////						prompt.Controls.Add(lbl); 
////						prompt.Controls.Add(txt); 
////						prompt.Controls.Add(btnOk);
////						prompt.AcceptButton = btnOk;

////						if (prompt.ShowDialog(this) == DialogResult.OK) 
////							pwd = txt.Text;
////					}
////					if (pwd == "pengchunyi") SetDevMode();
////					else MessageBox.Show("密碼錯誤", "警告", MessageBoxButtons.OK, MessageBoxIcon.Warning);
////				}
////				else SetUserMode();
////			};

////			// 8. 關閉畫面時自動重啟程式
////			this.FormClosing += (s, e) => System.Diagnostics.Process.Start(Application.ExecutablePath);


////			// 9. 預設離線
////			SetOnlineState(false);
////		}

////		// ==================== 連線事件 (給 ConnectionManager 呼叫) ====================

////		public void OnSerialDisconnected()
////		{
////			if (this.IsDisposed) return;

////			if (this.InvokeRequired)
////			{
////				try { this.BeginInvoke(new Action(OnSerialDisconnected)); } catch { }
////				return;
////			}

////			try
////			{
////				SetOnlineState(false);
////				ClearDisplay();
////			}
////			catch { }
////		}

////		public void OnSerialReconnected()
////		{
////			if (this.IsDisposed) return;

////			if (this.InvokeRequired)
////			{
////				try { this.BeginInvoke(new Action(OnSerialReconnected)); } catch { }
////				return;
////			}

////			try
////			{
////				SetOnlineState(true);
////			}
////			catch { }
////		}

////		// ==================== UI 更新相關 ====================
////		private void SetOnlineState(bool online)
////		{
////			_online = online;
////			this.Text = _baseTitle + (online ? "  [Online]" : "  [Offline]");
////			if (!online) updateTimer?.Stop();
////			else updateTimer?.Start();
////		}


////		private void ClearDisplay()
////		{
////			// 清空所有顯示與快取
////			lock (slaveData) slaveData.Clear();

////			InitializeParameters();

////			if (dataGridView != null && !dataGridView.IsDisposed)
////			{
////				if (dataGridView.InvokeRequired)
////				{
////					try { dataGridView.BeginInvoke(new Action(ClearDisplay)); } catch { }
////					return;
////				}

////				try
////				{
////					dataGridView.Rows.Clear();
////					dataGridView.Columns.Clear();
////					dataGridView.RowHeadersVisible = false;
////					dataGridView.Columns.Add("Parameter", "參數名稱");
////				}
////				catch { }
////			}
////		}


////		public void AppendLog(string txt)
////		{
////			if (IsDisposed || !IsHandleCreated) return;
////			if (logTextBox.IsDisposed) return;

////			if (logTextBox.InvokeRequired)
////			{
////				try { logTextBox.BeginInvoke(new Action<string>(AppendLog), txt); } catch { }
////				return;
////			}

////			try
////			{
////				// AppLogger 已加時間戳，這裡直接 append
////				logTextBox.AppendText(txt.EndsWith("\r\n") ? txt : (txt + "\r\n"));
////			}
////			catch { /* ignore UI errors */ }
////		}

////		public void RefreshIniView()
////		{
////			iniListView.BeginUpdate();
////			iniListView.Items.Clear();

////			foreach (var p in typeof(SystemConfig).GetProperties())
////			{
////				string key = p.Name;
////				object raw = p.GetValue(null);
////				iniListView.Items.Add(new ListViewItem(new[] { key, raw?.ToString() ?? "" }));
////			}
////			foreach (var p in typeof(SwitchDeviceConfig).GetProperties())
////			{
////				string key = p.Name;
////				object raw = p.GetValue(null);
////				if (raw is IEnumerable<byte> list)
////					iniListView.Items.Add(new ListViewItem(new[] { key, string.Join(",", list) }));
////				else
////					iniListView.Items.Add(new ListViewItem(new[] { key, raw?.ToString() ?? "" }));
////			}
////			iniListView.EndUpdate();
////		}








////		// ==================== 串口初始化與資料更新 ====================
////		// --- UI 安全地開啟 SerialPort ---
////		public void SafeInitializeSerialPort(string portName)
////		{
////			if (this.InvokeRequired)
////			{
////				this.BeginInvoke(new Action<string>(SafeInitializeSerialPort), portName);
////				return;
////			}
////			InitializeSerialPort(portName);
////		}

////		public void InitializeSerialPort(string portName)
////		{
////			if (string.IsNullOrWhiteSpace(portName))
////			{
////				AppLogger.Warn("[SERIAL] 請選擇有效的 COM 口。");
////				return;
////			}

////			try
////			{
////				if (serialPort != null)
////				{
////					if (serialPort.IsOpen) serialPort.Close();
////					serialPort.Dispose();
////				}

////				updateTimer?.Stop();

////				slaveData.Clear();
////				foreach (var st in stationNumberTextBox.Text.Split(','))
////					if (byte.TryParse(st.Trim(), out var sn))
////						slaveData[sn] = new Dictionary<string, object>();

////				serialPort = new SerialPort(portName, 9600, Parity.None, 8, StopBits.One)
////				{
////					ReadTimeout = 2000,
////					WriteTimeout = 1000,
////					Handshake = Handshake.None,
////					RtsEnable = false
////				};

////				serialPort.Open();
////				AppLogger.Info($"[SERIAL] {portName} 已成功連接");

////				SetOnlineState(true);
////			}
////			catch (Exception ex)
////			{
////				AppLogger.Error(ex, $"[SERIAL] 無法連接 {portName}");
////				SetOnlineState(false);
////			}
////		}

////		public void UpdateDataGridView()
////		{
////			if (dataGridView.InvokeRequired)
////			{
////				dataGridView.Invoke(new Action(UpdateDataGridView));
////				return;
////			}

////			int selRow = dataGridView.CurrentRow?.Index ?? -1;
////			int firstRow = dataGridView.FirstDisplayedScrollingRowIndex;

////			dataGridView.Rows.Clear();
////			dataGridView.Columns.Clear();
////			dataGridView.RowHeadersVisible = false;

////			dataGridView.Columns.Add("Parameter", "參數名稱");
////			foreach (var station in slaveData.Keys)
////				dataGridView.Columns.Add($"Slave_{station}", $"站號 {station}");

////			var allParams = slaveData.Values.SelectMany(d => d.Keys).Distinct().ToList();
////			foreach (var p in allParams)
////			{
////				var row = new List<object> { p };
////				foreach (var station in slaveData.Keys)
////					row.Add(slaveData[station].TryGetValue(p, out var v) ? (v?.ToString() ?? "N/A") : "N/A");
////				dataGridView.Rows.Add(row.ToArray());
////			}

////			if (selRow >= 0 && selRow < dataGridView.RowCount)
////			{
////				dataGridView.Rows[selRow].Selected = true;
////				dataGridView.CurrentCell = dataGridView.Rows[selRow].Cells[0];
////			}
////			if (firstRow >= 0 && firstRow < dataGridView.RowCount)
////				dataGridView.FirstDisplayedScrollingRowIndex = firstRow;
////		}

////			private void UpdateSlaveDataFromTextBox()
////		{
////			lock (slaveData)
////			{
////				slaveData.Clear();
////				foreach (var st in stationNumberTextBox.Text.Split(','))
////				{
////					if (byte.TryParse(st.Trim(), out var station))
////						if (!slaveData.ContainsKey(station))
////							slaveData[station] = new Dictionary<string, object>();
////				}
////			}
////		}

////		public void SetComPortUI()
////		{
////			if (!string.IsNullOrWhiteSpace(SwitchDeviceConfig.ComPort))
////			{
////				portSelector.Items.Clear();
////				portSelector.Items.Add(SwitchDeviceConfig.ComPort);
////				portSelector.SelectedItem = SwitchDeviceConfig.ComPort;
////			}
////			if (SwitchDeviceConfig.StationNumber?.Count > 0)
////				stationNumberTextBox.Text = string.Join(",", SwitchDeviceConfig.StationNumber);
////		}





////		public void ExecuteSwitchCommand(Action<SerialPort, byte> switchCommand, List<byte> stationNumbers = null)
////		{
////			if (serialPort == null || !serialPort.IsOpen)
////			{
////				MessageBox.Show("請先連接串口！");
////				return;
////			}
////			if (stationNumbers == null)
////			{
////				stationNumbers = stationNumberTextBox.Text
////					.Split(',')
////					.Select(x => x.Trim())
////					.Where(x => byte.TryParse(x, out _))
////					.Select(byte.Parse)
////					.ToList();
////			}

////			Thread.Sleep(100);
////			foreach (var station in stationNumbers)
////			{
////				if (!slaveData.ContainsKey(station)) continue;
////				try
////				{
////					switchCommand(serialPort, station);
////					Thread.Sleep(200);
////				}
////				catch (Exception ex)
////				{
////					AppLogger.Error(ex, $"[MODBUS] 站號 {station} 開關操作錯誤");
////				}
////			}
////		}

////		public void ExecuteSetTemperature(byte stationNumber, ushort temperature)
////		{
////			if (serialPort == null || !serialPort.IsOpen)
////			{
////				AppLogger.Warn("[SERIAL] 串口未開啟或無效，無法設定溫度");
////				return;
////			}
////			if (!slaveData.ContainsKey(stationNumber))
////			{
////				AppLogger.Warn($"[APP] 站號 {stationNumber} 不存在，操作中止。");
////				return;
////			}

////			Task.Run(() =>
////			{
////				lock (ModbusHelper.SerialSync)
////				{
////					ModbusHelper.SetTemperature(serialPort, stationNumber, temperature, slaveData);
////				}
////			});
////		}



////		private void InitializeTimer()
////		{
////			updateTimer = new System.Windows.Forms.Timer { Interval = 1000 };
////			updateTimer.Tick += (s, e) =>
////			{
////				try { UpdateDataGridView(); } catch { }
////			};
////		}



////		private void InitializeParameters()
////		{
////			currentStatus1 = currentStatus2 = currentStatus = 0;
////			tempA = tempB = tempC = tempN = 0;
////			voltageA = voltageB = voltageC = 0;
////			currentA = currentB = currentC = 0;
////			powerFactorA = powerFactorB = powerFactorC = 0;
////			activePowerA = activePowerB = activePowerC = 0;
////			reactivePowerA = reactivePowerB = reactivePowerC = 0;
////			breakerTimes = 0;
////			energy = 0.0;
////			switchStatus = 0;
////			apparentPowerA = 0; apparentPowerB = 0; apparentPowerC = 0;
////			totalApparentPower = totalActivePower = totalReactivePower = 0;
////			combinedPowerFactor = 0; lineFrequency = 0; deviceType = 0;
////			historicalLeakage = historicalCurrentA = historicalCurrentB = historicalCurrentC = 0;
////			ProtectionThreshold = 0;
////		}


////		public SerialPort GetSerialPort() => serialPort;



////		// ============ 開發者模式切換函數 ============
////		/// <summary>切換到「一般使用者模式」：鎖定所有控制元件</summary>
////		void SetUserMode()
////		{
////			foreach (var ctl in controlsToLock)
////			{
////				ctl.Enabled = false; ctl.BackColor = SystemColors.ControlLight;
////			}
////			devModeButton.Text = "開發者模式";
////			isDevMode = false;
////		}



////		void SetDevMode()
////		{
////			foreach (var ctl in controlsToLock)
////			{
////				ctl.Enabled = true; ctl.BackColor = Color.White;
////			}
////			devModeButton.Text = "使用者模式";
////			isDevMode = true;
////		}

////	}
////}




//// ModbusViewer.cs
//using System;
//using System.Collections.Generic;
//using System.Drawing;
//using System.IO.Ports;
//using System.Linq;
//using System.Threading;
//using System.Threading.Tasks;
//using System.Windows.Forms;

//namespace AmqpModbusIntegration
//{
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

//		private volatile bool _online = false;
//		private string _baseTitle;

//		// === 新增：開發者模式旗標與 UI 選擇的 COM ===
//		private bool _isDevMode = false;
//		private string _userSelectedComPort = null;

//		public bool IsDevMode => _isDevMode;
//		public string GetUserSelectedComPort() => _userSelectedComPort;

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
//			_baseTitle = "PC Base Equipment Monitor";
//			this.Text = _baseTitle;

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

//			// Track 使用者在 UI 選擇的 COM
//			portSelector.SelectedIndexChanged += (s, e) =>
//			{
//				_userSelectedComPort = portSelector.SelectedItem?.ToString();
//				if (_isDevMode)
//				{
//					SystemConfig.ManualComOverride = true;
//					SystemConfig.ManualComPort = _userSelectedComPort;
//				}
//			};

//			RefreshIniView();

//			if (!string.IsNullOrWhiteSpace(SwitchDeviceConfig.ComPort))
//			{
//				portSelector.Items.Clear();
//				portSelector.Items.Add(SwitchDeviceConfig.ComPort);
//				portSelector.SelectedItem = SwitchDeviceConfig.ComPort;
//				_userSelectedComPort = SwitchDeviceConfig.ComPort;
//			}
//			if (SwitchDeviceConfig.StationNumber?.Count > 0 &&
//				SwitchDeviceConfig.StationNumber.All(stn => stn > 0))
//			{
//				stationNumberTextBox.Text = string.Join(",", SwitchDeviceConfig.StationNumber);
//			}

//			connectButton.Click += (s, e) =>
//			{
//				var target = portSelector.SelectedItem?.ToString();
//				_userSelectedComPort = target;
//				if (_isDevMode)
//				{
//					SystemConfig.ManualComOverride = true;
//					SystemConfig.ManualComPort = _userSelectedComPort;
//				}
//				SafeInitializeSerialPort(target);
//			};

//			readButton.Click += async (s, e) =>
//			{
//				UpdateSlaveDataFromTextBox();
//				if (isUpdating) return;
//				readButton.Enabled = false;
//				try
//				{
//					if (serialPort == null || !serialPort.IsOpen)
//					{
//						MessageBox.Show("請先連接串口！");
//					}
//					else
//					{
//						await ModbusHelper.ReadAllParametersAsync(serialPort, this);
//						UpdateDataGridView();
//					}
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

//			// testFaultButton.Click += (s, e) => ModbusHelper.SimulateFaultTest(this, slaveData);

//			refreshButton.Click += (s, e) =>
//			{
//				portSelector.Items.Clear();
//				var names = SerialPort.GetPortNames();
//				portSelector.Items.AddRange(names);
//				if (names.Length > 0)
//				{
//					portSelector.SelectedIndex = 0;
//					_userSelectedComPort = portSelector.SelectedItem?.ToString();
//					if (_isDevMode)
//					{
//						SystemConfig.ManualComOverride = true;
//						SystemConfig.ManualComPort = _userSelectedComPort;
//					}
//				}
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

//			var devModeButton = new Button { Text = "開發者模式", Location = new Point(700, 50), Size = new Size(100, 25) };
//			this.Controls.Add(devModeButton);

//			void SetUserMode()
//			{
//				foreach (var ctl in controlsToLock) { ctl.Enabled = false; ctl.BackColor = SystemColors.ControlLight; }
//				devModeButton.Text = "開發者模式";
//				_isDevMode = false;

//				// 退出手動覆蓋
//				SystemConfig.ManualComOverride = false;
//				SystemConfig.ManualComPort = null;
//			}
//			void SetDevMode()
//			{
//				foreach (var ctl in controlsToLock) { ctl.Enabled = true; ctl.BackColor = Color.White; }
//				devModeButton.Text = "使用者模式";
//				_isDevMode = true;

//				// 進入手動覆蓋
//				SystemConfig.ManualComOverride = true;
//				SystemConfig.ManualComPort = _userSelectedComPort ?? portSelector.SelectedItem?.ToString();
//			}
//			SetUserMode();

//			devModeButton.Click += (s, e) =>
//			{
//				if (!_isDevMode)
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

//			// 關閉視窗時重新啟動程式（你的需求）
//			this.FormClosing += (s, e) => System.Diagnostics.Process.Start(Application.ExecutablePath);

//			SetOnlineState(false); // 初始顯示為離線
//		}

//		// ========= 供 ConnectionManager 呼叫的連線事件 =========

//		public void OnSerialDisconnected()
//		{
//			if (this.IsDisposed) return;

//			if (this.InvokeRequired)
//			{
//				try { this.BeginInvoke(new Action(OnSerialDisconnected)); } catch { }
//				return;
//			}

//			try
//			{
//				SetOnlineState(false);
//				ClearDisplay();
//			}
//			catch { }
//		}

//		public void OnSerialReconnected()
//		{
//			if (this.IsDisposed) return;

//			if (this.InvokeRequired)
//			{
//				try { this.BeginInvoke(new Action(OnSerialReconnected)); } catch { }
//				return;
//			}

//			try
//			{
//				SetOnlineState(true);
//			}
//			catch { }
//		}

//		private void SetOnlineState(bool online)
//		{
//			_online = online;
//			this.Text = _baseTitle + (online ? "  [Online]" : "  [Offline]");
//			if (!online) updateTimer?.Stop();
//			else updateTimer?.Start();
//		}

//		private void ClearDisplay()
//		{
//			// 清空所有顯示與快取
//			lock (slaveData) slaveData.Clear();

//			InitializeParameters();

//			if (dataGridView != null && !dataGridView.IsDisposed)
//			{
//				if (dataGridView.InvokeRequired)
//				{
//					try { dataGridView.BeginInvoke(new Action(ClearDisplay)); } catch { }
//					return;
//				}

//				try
//				{
//					dataGridView.Rows.Clear();
//					dataGridView.Columns.Clear();
//					dataGridView.RowHeadersVisible = false;
//					dataGridView.Columns.Add("Parameter", "參數名稱");
//				}
//				catch { }
//			}
//		}

//		// --- UI 安全地開啟 SerialPort ---
//		public void SafeInitializeSerialPort(string portName)
//		{
//			if (this.InvokeRequired)
//			{
//				this.BeginInvoke(new Action<string>(SafeInitializeSerialPort), portName);
//				return;
//			}
//			InitializeSerialPort(portName);
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
//				_userSelectedComPort = SwitchDeviceConfig.ComPort;
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
//			if (IsDisposed || !IsHandleCreated) return;
//			if (logTextBox.IsDisposed) return;

//			if (logTextBox.InvokeRequired)
//			{
//				try { logTextBox.BeginInvoke(new Action<string>(AppendLog), txt); } catch { }
//				return;
//			}

//			try
//			{
//				// AppLogger 已加時間戳，這裡直接 append
//				logTextBox.AppendText(txt.EndsWith("\r\n") ? txt : (txt + "\r\n"));
//			}
//			catch { /* ignore UI errors */ }
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
//					AppLogger.Error(ex, $"[MODBUS] 站號 {station} 開關操作錯誤");
//				}
//			}
//		}

//		public void ExecuteSetTemperature(byte stationNumber, ushort temperature)
//		{
//			if (serialPort == null || !serialPort.IsOpen)
//			{
//				AppLogger.Warn("[SERIAL] 串口未開啟或無效，無法設定溫度");
//				return;
//			}
//			if (!slaveData.ContainsKey(stationNumber))
//			{
//				AppLogger.Warn($"[APP] 站號 {stationNumber} 不存在，操作中止。");
//				return;
//			}

//			Task.Run(() =>
//			{
//				lock (ModbusHelper.SerialSync)
//				{
//					ModbusHelper.SetTemperature(serialPort, stationNumber, temperature, slaveData);
//				}
//			});
//		}

//		private void InitializeTimer()
//		{
//			updateTimer = new System.Windows.Forms.Timer { Interval = 1000 };
//			updateTimer.Tick += (s, e) =>
//			{
//				try { UpdateDataGridView(); } catch { }
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
//				AppLogger.Warn("[SERIAL] 請選擇有效的 COM 口。");
//				return;
//			}

//			try
//			{
//				if (serialPort != null)
//				{
//					if (serialPort.IsOpen) serialPort.Close();
//					serialPort.Dispose();
//				}

//				updateTimer?.Stop();

//				slaveData.Clear();
//				foreach (var st in stationNumberTextBox.Text.Split(','))
//					if (byte.TryParse(st.Trim(), out var sn))
//						slaveData[sn] = new Dictionary<string, object>();

//				serialPort = new SerialPort(portName, 9600, Parity.None, 8, StopBits.One)
//				{
//					ReadTimeout = 5000,
//					WriteTimeout = 5000,
//					Handshake = Handshake.None,
//					RtsEnable = false
//				};

//				serialPort.Open();
//				AppLogger.Info($"[SERIAL] {portName} 已成功連接");

//				// 若在開發者模式，記錄手動覆蓋的埠
//				if (_isDevMode)
//				{
//					_userSelectedComPort = portName;
//					SystemConfig.ManualComOverride = true;
//					SystemConfig.ManualComPort = portName;
//				}

//				SetOnlineState(true);
//			}
//			catch (Exception ex)
//			{
//				AppLogger.Error(ex, $"[SERIAL] 無法連接 {portName}");
//				SetOnlineState(false);
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
