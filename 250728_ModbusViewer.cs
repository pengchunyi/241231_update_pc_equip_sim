//using AmqpModbusIntegration.Services;
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

//		private System.Windows.Forms.Timer uiRefreshTimer;

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

//		public double energy;
//		public double voltageA, voltageB, voltageC, currentA, currentB, currentC;
//		public double apparentPowerB, apparentPowerC, totalApparentPower, totalActivePower, totalReactivePower, combinedPowerFactor;

//		private DataGridView dataGridView;
//		private bool isUpdating = false;

//		private ListView iniListView;
//		private TextBox logTextBox;

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
//			this.Icon = new System.Drawing.Icon("APP_ICON.ico");

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
//			if (SwitchDeviceConfig.StationNumber != null &&
//				SwitchDeviceConfig.StationNumber.Count > 0 &&
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
//					// 單次讀取（保留此手動功能），但一般輪詢交給 ConnectionManager
//					var ok = await ModbusHelper.ReadAllParametersAsync(serialPort, this);
//					UILog.Info("手動讀取完成: " + (ok ? "成功" : "失敗"));
//					UpdateDataGridView();
//				}
//				catch (Exception ex)
//				{
//					UILog.Exception(ex, "手動讀取發生錯誤");
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

//			testFaultButton.Click += (s, e) => ModbusHelper.SimulateFaultTest(this, slaveData);

//			refreshButton.Click += (s, e) =>
//			{
//				portSelector.Items.Clear();
//				portSelector.Items.AddRange(SerialPort.GetPortNames());
//				MessageBox.Show("COM口已刷新！", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
//			};

//			InitializeTimer();
//			InitializeParameters();

//			this.Shown += (_, __) => uiRefreshTimer.Start();

//			var controlsToLock = new List<Control>
//			{
//				connectButton, readButton, tempSetButton, testFaultButton,
//				switchOnButton, switchOffButton,
//				refreshButton, portSelector, stationNumberTextBox, tempTextBox
//			};

//			bool isDevMode = false;
//			var devModeButton = new Button
//			{
//				Text = "開發者模式",
//				Location = new Point(700, 50),
//				Size = new Size(100, 25)
//			};
//			this.Controls.Add(devModeButton);

//			void SetUserMode()
//			{
//				foreach (var ctl in controlsToLock)
//				{
//					ctl.Enabled = false;
//					ctl.BackColor = SystemColors.ControlLight;
//				}
//				devModeButton.Text = "開發者模式";
//				isDevMode = false;
//			}
//			void SetDevMode()
//			{
//				foreach (var ctl in controlsToLock)
//				{
//					ctl.Enabled = true;
//					ctl.BackColor = Color.White;
//				}
//				devModeButton.Text = "使用者模式";
//				isDevMode = true;
//			}

//			SetUserMode();

//			devModeButton.Click += (s, e) =>
//			{
//				if (!isDevMode)
//				{
//					string pwd = "";
//					using (var prompt = new Form())
//					{
//						prompt.Width = 300;
//						prompt.Height = 150;
//						prompt.Text = "開發者模式";
//						prompt.FormBorderStyle = FormBorderStyle.FixedDialog;
//						prompt.StartPosition = FormStartPosition.CenterParent;

//						var lbl = new Label() { Left = 10, Top = 10, Text = "請輸入密碼：" };
//						var txt = new TextBox() { Left = 10, Top = 40, Width = 260, PasswordChar = '●' };
//						var btnOk = new Button() { Text = "確定", Left = 200, Top = 70, DialogResult = DialogResult.OK };

//						prompt.Controls.Add(lbl);
//						prompt.Controls.Add(txt);
//						prompt.Controls.Add(btnOk);
//						prompt.AcceptButton = btnOk;

//						if (prompt.ShowDialog(this) == DialogResult.OK)
//							pwd = txt.Text;
//					}
//					if (pwd == "pengchunyi") SetDevMode();
//					else MessageBox.Show("密碼錯誤", "警告", MessageBoxButtons.OK, MessageBoxIcon.Warning);
//				}
//				else
//				{
//					SetUserMode();
//				}
//			};
//		}

//		private void UpdateSlaveDataFromTextBox()
//		{
//			lock (slaveData)
//			{
//				slaveData.Clear();
//				foreach (var st in stationNumberTextBox.Text.Split(','))
//				{
//					if (byte.TryParse(st.Trim(), out var station))
//					{
//						if (!slaveData.ContainsKey(station))
//							slaveData[station] = new Dictionary<string, object>();
//					}
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
//				string val;
//				object raw = p.GetValue(null);
//				if (raw is IEnumerable<byte> list) val = string.Join(",", list);
//				else val = raw?.ToString() ?? "";
//				iniListView.Items.Add(new ListViewItem(new[] { key, val }));
//			}

//			foreach (var p in typeof(SwitchDeviceConfig).GetProperties())
//			{
//				string key = p.Name;
//				string val;
//				object raw = p.GetValue(null);
//				if (raw is IEnumerable<byte> list) val = string.Join(",", list);
//				else val = raw?.ToString() ?? "";
//				iniListView.Items.Add(new ListViewItem(new[] { key, val }));
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
//			Thread.Sleep(100);
//			foreach (var station in stationNumbers)
//			{
//				if (!slaveData.ContainsKey(station))
//				{
//					UILog.Warn($"站號 {station} 不存在，操作跳過。");
//					continue;
//				}
//				try
//				{
//					switchCommand(serialPort, station);
//					UILog.Info($"站號 {station} 的開關操作執行成功");
//					Thread.Sleep(500);
//				}
//				catch (Exception ex)
//				{
//					UILog.Exception(ex, $"執行站號 {station} 的開關操作時出現錯誤");
//				}
//			}
//		}

//		public void ExecuteSetTemperature(byte stationNumber, ushort temperature)
//		{
//			if (serialPort == null || !serialPort.IsOpen)
//			{
//				UILog.Warn("串口未開啟或無效，無法設定溫度");
//				return;
//			}
//			if (!slaveData.ContainsKey(stationNumber))
//			{
//				UILog.Warn($"站號 {stationNumber} 不存在，操作中止。");
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
//			uiRefreshTimer = new System.Windows.Forms.Timer { Interval = 1000 };
//			uiRefreshTimer.Tick += (s, e) =>
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

//			int currentSelectedRowIndex = dataGridView.CurrentRow?.Index ?? -1;
//			int firstDisplayedRowIndex = dataGridView.FirstDisplayedScrollingRowIndex;

//			dataGridView.Rows.Clear();
//			dataGridView.Columns.Clear();
//			dataGridView.RowHeadersVisible = false;

//			dataGridView.Columns.Add("Parameter", "參數名稱");

//			foreach (var station in slaveData.Keys)
//			{
//				dataGridView.Columns.Add($"Slave_{station}", $"站號 {station}");
//			}

//			var allParameters = slaveData.Values
//				.SelectMany(d => d.Keys)
//				.Distinct()
//				.ToList();

//			foreach (var parameter in allParameters)
//			{
//				var row = new List<object> { parameter };
//				foreach (var station in slaveData.Keys)
//				{
//					if (slaveData[station].TryGetValue(parameter, out var valObj))
//						row.Add(valObj?.ToString() ?? "N/A");
//					else
//						row.Add("N/A");
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

//		public void InitializeSerialPort(string portName)
//		{
//			try
//			{
//				if (serialPort != null)
//				{
//					if (serialPort.IsOpen) serialPort.Close();
//					serialPort.Dispose();
//				}

//				// 站號重新收集（避免初始化後是空）
//				slaveData.Clear();
//				foreach (var st in stationNumberTextBox.Text.Split(','))
//				{
//					if (byte.TryParse(st.Trim(), out var stationNumber))
//						slaveData[stationNumber] = new Dictionary<string, object>();
//				}

//				serialPort = new SerialPort(portName, 9600, Parity.None, 8, StopBits.One);
//				serialPort.Open();
//				UILog.Info($"串口 {portName} 已成功打開");
//			}
//			catch (Exception ex)
//			{
//				UILog.Exception(ex, $"無法打開串口 {portName}");
//				throw;
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
//			breakerTimes = 0; energy = 0.0; switchStatus = 0;
//			apparentPowerA = 0; apparentPowerB = 0; apparentPowerC = 0;
//			totalApparentPower = 0; totalActivePower = 0; totalReactivePower = 0;
//			combinedPowerFactor = 0; lineFrequency = 0; deviceType = 0;
//			historicalLeakage = 0; historicalCurrentA = 0; historicalCurrentB = 0; historicalCurrentC = 0;
//			ProtectionThreshold = 0;
//		}
//	}
//}
