// ModbusViewer.cs
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO.Ports;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace AmqpModbusIntegration
{
	//public class ModbusViewer : Form
	public class ModbusViewer : Form, IDisposable
	{
		public Dictionary<byte, Dictionary<string, object>> slaveData = new Dictionary<byte, Dictionary<string, object>>();

		private SerialPort serialPort;
		private ComboBox portSelector;
		private TextBox stationNumberTextBox;
		private Button connectButton, readButton;
		private TextBox tempTextBox;
		private Button tempSetButton;
		private Button testFaultButton;
		private DataGridView dataGridView;
		private bool isUpdating = false;
		private ListView iniListView;
		private TextBox logTextBox;
		private System.Windows.Forms.Timer updateTimer;

		private volatile bool _online = false;
		private string _baseTitle;

		private bool _isDevMode = false;
		private string _userSelectedComPort = null;

		public bool IsDevMode => _isDevMode;
		public string GetUserSelectedComPort() => _userSelectedComPort;
		public SerialPort GetSerialPort() => serialPort;

		public int currentStatus1, currentStatus2, currentStatus;
		public int tempA, tempB, tempC, tempN;
		public int powerFactorA, powerFactorB, powerFactorC, activePowerA, activePowerB, activePowerC;
		public int reactivePowerA, reactivePowerB, reactivePowerC, breakerTimes;
		public int switchStatus, apparentPowerA;
		public int lineFrequency, deviceType;
		public int historicalLeakage, historicalCurrentA, historicalCurrentB, historicalCurrentC;
		public int ProtectionThreshold;
		public double energy;
		public double voltageA, voltageB, voltageC, currentA, currentB, currentC;
		public double apparentPowerB, apparentPowerC, totalApparentPower, totalActivePower, totalReactivePower, combinedPowerFactor;

		protected override CreateParams CreateParams
		{
			get
			{
				const int CS_NOCLOSE = 0x200;
				var cp = base.CreateParams;
				cp.ClassStyle |= CS_NOCLOSE;
				return cp;
			}
		}

		public ModbusViewer()
		{
			try { this.Icon = new Icon("APP_ICON.ico"); } catch { }
			_baseTitle = "PC Base Equipment Monitor";
			this.Text = _baseTitle;

			UIInitializer.InitializeUI(
				this,
				out portSelector,
				out stationNumberTextBox,
				out connectButton,
				out readButton,
				out var switchOnButton,
				out var switchOffButton,
				out tempTextBox,
				out tempSetButton,
				out testFaultButton,
				out var refreshButton,
				out dataGridView,
				out iniListView,
				out logTextBox
			);

			portSelector.SelectedIndexChanged += (s, e) =>
			{
				_userSelectedComPort = portSelector.SelectedItem?.ToString();
				if (_isDevMode)
				{
					SystemConfig.ManualComOverride = true;
					SystemConfig.ManualComPort = _userSelectedComPort;
				}
			};

			RefreshIniView();

			if (!string.IsNullOrWhiteSpace(SwitchDeviceConfig.ComPort))
			{
				portSelector.Items.Clear();
				portSelector.Items.Add(SwitchDeviceConfig.ComPort);
				portSelector.SelectedItem = SwitchDeviceConfig.ComPort;
				_userSelectedComPort = SwitchDeviceConfig.ComPort;
			}
			if (SwitchDeviceConfig.StationNumber?.Count > 0 &&
				SwitchDeviceConfig.StationNumber.All(stn => stn > 0))
			{
				stationNumberTextBox.Text = string.Join(",", SwitchDeviceConfig.StationNumber);
			}

			connectButton.Click += (s, e) =>
			{
				var target = portSelector.SelectedItem?.ToString();
				_userSelectedComPort = target;
				if (_isDevMode)
				{
					SystemConfig.ManualComOverride = true;
					SystemConfig.ManualComPort = _userSelectedComPort;
				}
				SafeInitializeSerialPort(target);
			};

			readButton.Click += async (s, e) =>
			{
				UpdateSlaveDataFromTextBox();
				if (isUpdating) return;
				readButton.Enabled = false;
				try
				{
					if (serialPort == null || !serialPort.IsOpen)
					{
						MessageBox.Show("請先連接串口！");
					}
					else
					{
						await ModbusHelper.ReadAllParametersAsync(serialPort, this);
						UpdateDataGridView();
					}
				}
				finally { readButton.Enabled = true; }
			};

			switchOnButton.Click += (s, e) => ExecuteSwitchCommand(ModbusHelper.SwitchON);
			switchOffButton.Click += (s, e) => ExecuteSwitchCommand(ModbusHelper.SwitchOFF);

			tempSetButton.Click += (s, e) =>
			{
				if (!byte.TryParse(stationNumberTextBox.Text, out byte stn))
				{
					MessageBox.Show("請輸入有效的站號！", "錯誤", MessageBoxButtons.OK, MessageBoxIcon.Error);
					return;
				}
				if (!ushort.TryParse(tempTextBox.Text, out ushort temperature))
				{
					MessageBox.Show("請輸入有效的溫度值！", "錯誤", MessageBoxButtons.OK, MessageBoxIcon.Error);
					return;
				}
				ExecuteSetTemperature(stn, temperature);
			};

			refreshButton.Click += (s, e) =>
			{
				portSelector.Items.Clear();
				var names = SerialPort.GetPortNames();
				portSelector.Items.AddRange(names);
				if (names.Length > 0)
				{
					portSelector.SelectedIndex = 0;
					_userSelectedComPort = portSelector.SelectedItem?.ToString();
					if (_isDevMode)
					{
						SystemConfig.ManualComOverride = true;
						SystemConfig.ManualComPort = _userSelectedComPort;
					}
				}
				MessageBox.Show("COM口已刷新！", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
			};

			InitializeTimer();
			InitializeParameters();
			this.Shown += (_, __) => updateTimer.Start();

			var controlsToLock = new List<Control> {
				connectButton, readButton, tempSetButton, testFaultButton,
				switchOnButton, switchOffButton, refreshButton, portSelector,
				stationNumberTextBox, tempTextBox
			};

			var devModeButton = new Button { Text = "開發者模式", Location = new Point(700, 50), Size = new Size(100, 25) };
			this.Controls.Add(devModeButton);

			void SetUserMode()
			{
				foreach (var ctl in controlsToLock) { ctl.Enabled = false; ctl.BackColor = SystemColors.ControlLight; }
				devModeButton.Text = "開發者模式";
				_isDevMode = false;
				SystemConfig.ManualComOverride = false;
				SystemConfig.ManualComPort = null;
			}
			void SetDevMode()
			{
				foreach (var ctl in controlsToLock) { ctl.Enabled = true; ctl.BackColor = Color.White; }
				devModeButton.Text = "使用者模式";
				_isDevMode = true;
				SystemConfig.ManualComOverride = true;
				SystemConfig.ManualComPort = _userSelectedComPort ?? portSelector.SelectedItem?.ToString();
			}
			SetUserMode();

			devModeButton.Click += (s, e) =>
			{
				if (!_isDevMode)
				{
					string pwd = "";
					using (var prompt = new Form())
					{
						prompt.Width = 300; prompt.Height = 150; prompt.Text = "開發者模式";
						prompt.FormBorderStyle = FormBorderStyle.FixedDialog;
						prompt.StartPosition = FormStartPosition.CenterParent;

						var lbl = new Label() { Left = 10, Top = 10, Text = "請輸入密碼：" };
						var txt = new TextBox() { Left = 10, Top = 40, Width = 260, PasswordChar = '●' };
						var btnOk = new Button() { Text = "確定", Left = 200, Top = 70, DialogResult = DialogResult.OK };

						prompt.Controls.Add(lbl); prompt.Controls.Add(txt); prompt.Controls.Add(btnOk);
						prompt.AcceptButton = btnOk;

						if (prompt.ShowDialog(this) == DialogResult.OK) pwd = txt.Text;
					}
					if (pwd == "pengchunyi") SetDevMode();
					else MessageBox.Show("密碼錯誤", "警告", MessageBoxButtons.OK, MessageBoxIcon.Warning);
				}
				else SetUserMode();
			};



			 //這邊改為使用工作排成器來作程式重啟
			 // 將自動重啟移除，讓我們能看到真正的崩潰訊息
			  //this.FormClosing += (s, e) => System.Diagnostics.Process.Start(Application.ExecutablePath);


			SetOnlineState(false);
		}

		public void OnSerialDisconnected()
		{
			if (this.IsDisposed) return;
			if (this.InvokeRequired) { try { this.BeginInvoke(new Action(OnSerialDisconnected)); } catch { } return; }
			try { SetOnlineState(false); ClearDisplay(); } catch { }
		}

		public void OnSerialReconnected()
		{
			if (this.IsDisposed) return;
			if (this.InvokeRequired) { try { this.BeginInvoke(new Action(OnSerialReconnected)); } catch { } return; }
			try { SetOnlineState(true); } catch { }
		}

		private void SetOnlineState(bool online)
		{
			_online = online;
			this.Text = _baseTitle + (online ? "  [Online]" : "  [Offline]");
			if (!online) updateTimer?.Stop(); else updateTimer?.Start();
		}

		private void ClearDisplay()
		{
			lock (slaveData) slaveData.Clear();
			InitializeParameters();

			if (dataGridView != null && !dataGridView.IsDisposed)
			{
				if (dataGridView.InvokeRequired) { try { dataGridView.BeginInvoke(new Action(ClearDisplay)); } catch { } return; }
				try
				{
					dataGridView.Rows.Clear();
					dataGridView.Columns.Clear();
					dataGridView.RowHeadersVisible = false;
					dataGridView.Columns.Add("Parameter", "參數名稱");
				}
				catch { }
			}
		}

		public void SafeInitializeSerialPort(string portName)
		{
			if (this.InvokeRequired) { this.BeginInvoke(new Action<string>(SafeInitializeSerialPort), portName); return; }
			InitializeSerialPort(portName);
		}

		private void UpdateSlaveDataFromTextBox()
		{
			lock (slaveData)
			{
				slaveData.Clear();
				foreach (var st in stationNumberTextBox.Text.Split(','))
				{
					if (byte.TryParse(st.Trim(), out var station))
						if (!slaveData.ContainsKey(station))
							slaveData[station] = new Dictionary<string, object>();
				}
			}
		}

		public void SetComPortUI()
		{
			if (!string.IsNullOrWhiteSpace(SwitchDeviceConfig.ComPort))
			{
				portSelector.Items.Clear();
				portSelector.Items.Add(SwitchDeviceConfig.ComPort);
				portSelector.SelectedItem = SwitchDeviceConfig.ComPort;
				_userSelectedComPort = SwitchDeviceConfig.ComPort;
			}
			if (SwitchDeviceConfig.StationNumber?.Count > 0)
				stationNumberTextBox.Text = string.Join(",", SwitchDeviceConfig.StationNumber);
		}

		public void RefreshIniView()
		{
			iniListView.BeginUpdate();
			iniListView.Items.Clear();

			foreach (var p in typeof(SystemConfig).GetProperties())
			{
				string key = p.Name;
				object raw = p.GetValue(null);
				iniListView.Items.Add(new ListViewItem(new[] { key, raw?.ToString() ?? "" }));
			}
			foreach (var p in typeof(SwitchDeviceConfig).GetProperties())
			{
				string key = p.Name;
				object raw = p.GetValue(null);
				if (raw is IEnumerable<byte> list)
					iniListView.Items.Add(new ListViewItem(new[] { key, string.Join(",", list) }));
				else
					iniListView.Items.Add(new ListViewItem(new[] { key, raw?.ToString() ?? "" }));
			}
			iniListView.EndUpdate();
		}

		public void AppendLog(string txt)
		{
			if (IsDisposed || !IsHandleCreated) return;
			if (logTextBox.IsDisposed) return;

			if (logTextBox.InvokeRequired) { try { logTextBox.BeginInvoke(new Action<string>(AppendLog), txt); } catch { } return; }
			try { logTextBox.AppendText(txt.EndsWith("\r\n") ? txt : (txt + "\r\n")); } catch { }
		}

		public void ExecuteSwitchCommand(Action<SerialPort, byte> switchCommand, List<byte> stationNumbers = null)
		{
			if (serialPort == null || !serialPort.IsOpen)
			{
				MessageBox.Show("請先連接串口！");
				return;
			}
			if (stationNumbers == null)
			{
				stationNumbers = stationNumberTextBox.Text
					.Split(',')
					.Select(x => x.Trim())
					.Where(x => byte.TryParse(x, out _))
					.Select(byte.Parse)
					.ToList();
			}

			Thread.Sleep(100);
			foreach (var station in stationNumbers)
			{
				if (!slaveData.ContainsKey(station)) continue;
				try { switchCommand(serialPort, station); Thread.Sleep(200); }
				catch (Exception ex) { AppLogger.Error(ex, $"[MODBUS] 站號 {station} 開關操作錯誤"); }
			}
		}

		public void ExecuteSetTemperature(byte stationNumber, ushort temperature)
		{
			if (serialPort == null || !serialPort.IsOpen)
			{
				AppLogger.Warn("[SERIAL] 串口未開啟或無效，無法設定溫度");
				return;
			}
			if (!slaveData.ContainsKey(stationNumber))
			{
				AppLogger.Warn($"[APP] 站號 {stationNumber} 不存在，操作中止。");
				return;
			}

			Task.Run(() =>
			{
				lock (ModbusHelper.SerialSync)
				{
					ModbusHelper.SetTemperature(serialPort, stationNumber, temperature, slaveData);
				}
			});
		}

		private void InitializeTimer()
		{
			updateTimer = new System.Windows.Forms.Timer { Interval = 2000 };
			updateTimer.Tick += (s, e) => { try { UpdateDataGridView(); } catch { } };
		}

		public void UpdateDataGridView()
		{
			if (dataGridView.InvokeRequired) { dataGridView.Invoke(new Action(UpdateDataGridView)); return; }

			// ★ 使用快照避免集合同時被修改
			List<byte> stationKeys;
			List<string> allParams;
			lock (slaveData)
			{
				stationKeys = slaveData.Keys.ToList();
				allParams = slaveData.Values.SelectMany(d => d.Keys).Distinct().ToList();
			}

			int selRow = dataGridView.CurrentRow?.Index ?? -1;
			int firstRow = dataGridView.FirstDisplayedScrollingRowIndex;

			dataGridView.Rows.Clear();
			dataGridView.Columns.Clear();
			dataGridView.RowHeadersVisible = false;

			dataGridView.Columns.Add("Parameter", "參數名稱");
			foreach (var station in stationKeys)
				dataGridView.Columns.Add($"Slave_{station}", $"站號 {station}");

			foreach (var p in allParams)
			{
				var row = new List<object> { p };
				foreach (var station in stationKeys)
				{
					object v = null;
					lock (slaveData) slaveData[station].TryGetValue(p, out v);
					row.Add(v?.ToString() ?? "N/A");
				}
				dataGridView.Rows.Add(row.ToArray());
			}

			if (selRow >= 0 && selRow < dataGridView.RowCount)
			{
				dataGridView.Rows[selRow].Selected = true;
				dataGridView.CurrentCell = dataGridView.Rows[selRow].Cells[0];
			}
			if (firstRow >= 0 && firstRow < dataGridView.RowCount)
				dataGridView.FirstDisplayedScrollingRowIndex = firstRow;
		}

		public void InitializeSerialPort(string portName)
		{
			if (string.IsNullOrWhiteSpace(portName))
			{
				AppLogger.Warn("[SERIAL] 請選擇有效的 COM 口。");
				return;
			}

			try
			{
				updateTimer?.Stop();

				lock (ModbusHelper.SerialSync)
				{
					if (serialPort != null && serialPort.IsOpen &&
						string.Equals(serialPort.PortName, portName, StringComparison.OrdinalIgnoreCase))
					{
						AppLogger.Info("[SERIAL] " + portName + " 已處於開啟狀態（略過重新開啟）");
						SetOnlineState(true);
						return;
					}

					if (serialPort != null)
					{
						try { if (serialPort.IsOpen) serialPort.Close(); } catch { }
						try { serialPort.Dispose(); } catch { }
						serialPort = null;
					}

					slaveData.Clear();
					foreach (var st in stationNumberTextBox.Text.Split(','))
					{
						if (byte.TryParse(st.Trim(), out var sn))
							slaveData[sn] = new Dictionary<string, object>();
					}

					//serialPort = new SerialPort(portName, 9600, Parity.None, 8, StopBits.One)
					//{
					//	ReadTimeout = 2000,
					//	WriteTimeout = 1000,
					//	Handshake = Handshake.None,
					//	RtsEnable = false
					//};
					serialPort = new SerialPort(portName, 9600, Parity.None, 8, StopBits.One)
					{
						ReadTimeout = 2000,
						WriteTimeout = 1000,
						Handshake = Handshake.None,
						RtsEnable = false,   // 保持關閉（常見轉換器為自動方向）
						DtrEnable = true     // ★ 開 DTR，提升穩定性
					};




					serialPort.Open();
				}

				AppLogger.Info("[SERIAL] " + portName + " 已成功連接");

				if (_isDevMode)
				{
					_userSelectedComPort = portName;
					SystemConfig.ManualComOverride = true;
					SystemConfig.ManualComPort = portName;
				}

				SetOnlineState(true);
			}
			catch (Exception ex)
			{
				AppLogger.Error(ex, "[SERIAL] 無法連接 " + portName);
				SetOnlineState(false);
			}
		}

		private void InitializeParameters()
		{
			currentStatus1 = currentStatus2 = currentStatus = 0;
			tempA = tempB = tempC = tempN = 0;
			voltageA = voltageB = voltageC = 0;
			currentA = currentB = currentC = 0;
			powerFactorA = powerFactorB = powerFactorC = 0;
			activePowerA = activePowerB = activePowerC = 0;
			reactivePowerA = reactivePowerB = reactivePowerC = 0;
			breakerTimes = 0;
			energy = 0.0;
			switchStatus = 0;
			apparentPowerA = 0; apparentPowerB = 0; apparentPowerC = 0;
			totalApparentPower = totalActivePower = totalReactivePower = 0;
			combinedPowerFactor = 0; lineFrequency = 0; deviceType = 0;
			historicalLeakage = historicalCurrentA = historicalCurrentB = historicalCurrentC = 0;
			ProtectionThreshold = 0;
		}


		protected override void Dispose(bool disposing)
		{
			if (disposing)
			{
				updateTimer?.Dispose();
				serialPort?.Dispose();
			}
			base.Dispose(disposing);
		}


	}
}
