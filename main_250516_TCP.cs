//using System;
//using System.Collections.Generic;
//using System.Linq;
//using System.Net.Sockets;
//using System.Threading.Tasks;
//using System.Windows.Forms;

//namespace AmqpModbusIntegration
//{


//	static class Program
//	{
//		[STAThread]
//		static void Main()
//		{
//			// 讀取 CFX.ini 並設定全域 IP/Port
//			ConfigHelper.LoadConfiguration();

//			Application.EnableVisualStyles();
//			Application.SetCompatibleTextRenderingDefault(false);
//			Application.Run(new ModbusViewer());
//		}
//	}

//	public class ModbusViewer : Form
//	{
//		// 全域資料字典
//		public Dictionary<byte, Dictionary<string, object>> slaveData = new Dictionary<byte, Dictionary<string, object>>();

//		// UI 控件
//		private TextBox stationNumberTextBox;
//		private Button connectButton, readButton, switchOnButton, switchOffButton, tempSetButton, testFaultButton, refreshButton;
//		private TextBox tempTextBox;
//		private DataGridView dataGridView;
//		private Timer updateTimer;
//		private bool isUpdating = false;

//		// 監控/參數欄位
//		public int currentStatus1, currentStatus2, currentStatus;
//		public int tempA, tempB, tempC, tempN;
//		public int activePowerA, activePowerB, activePowerC, breakerTimes;
//		public double energy, voltageA, voltageB, voltageC, currentA, currentB, currentC;
//		public double totalActivePower, totalReactivePower, totalApparentPower, combinedPowerFactor;
//		public int switchStatus, ProtectionThreshold;

//		public ModbusViewer()
//		{
//			// 初始化 UI
//			UIInitializer.InitializeUI(
//				this,
//				out stationNumberTextBox,
//				out connectButton,
//				out readButton,
//				out switchOnButton,
//				out switchOffButton,
//				out tempTextBox,
//				out tempSetButton,
//				out testFaultButton,
//				out refreshButton,
//				out dataGridView);

//			// 事件綁定
//			connectButton.Click += async (s, e) =>
//				await ModbusHelper.ConnectAsync(SystemConfig.IpAddress, SystemConfig.Port);
//			tcpClient.ReceiveTimeout = 3000;   // 3 秒
//			tcpClient.SendTimeout = 3000;

//			readButton.Click += async (s, e) =>
//			{
//				UpdateSlaveDataFromTextBox();
//				if (isUpdating) return;
//				readButton.Enabled = false;
//				try
//				{
//					await ModbusHelper.ReadAllParametersAsync(SystemConfig.IpAddress, SystemConfig.Port, this);
//					UpdateDataGridView();
//				}
//				finally { readButton.Enabled = true; }
//			};

//			switchOnButton.Click += async (s, e) =>
//			{
//				if (byte.TryParse(stationNumberTextBox.Text, out var stn))
//					await ModbusHelper.SwitchON(SystemConfig.IpAddress, SystemConfig.Port, stn);
//			};

//			switchOffButton.Click += async (s, e) =>
//			{
//				if (byte.TryParse(stationNumberTextBox.Text, out var stn))
//					await ModbusHelper.SwitchOFF(SystemConfig.IpAddress, SystemConfig.Port, stn);
//			};

//			tempSetButton.Click += (s, e) =>
//			{
//				if (!byte.TryParse(stationNumberTextBox.Text, out var stn)) { MessageBox.Show("請輸入有效站號"); return; }
//				if (!ushort.TryParse(tempTextBox.Text, out var temp)) { MessageBox.Show("請輸入有效溫度"); return; }
//				ModbusHelper.SetTemperature(SystemConfig.IpAddress, SystemConfig.Port, stn, temp, slaveData);
//			};

//			testFaultButton.Click += (s, e) =>
//			{
//				ModbusHelper.SimulateFaultTest(this, slaveData);
//			};

//			refreshButton.Click += (s, e) => MessageBox.Show("已刷新");

//			InitializeTimer();
//			InitializeParameters();

//			slaveData[SwitchDeviceConfig.StationNumber] = new Dictionary<string, object>();
//			stationNumberTextBox.Text = SwitchDeviceConfig.StationNumber.ToString();
//		}

//		private void UpdateSlaveDataFromTextBox()
//		{
//			lock (slaveData)
//			{
//				slaveData.Clear();
//				foreach (var tok in stationNumberTextBox.Text.Split(','))
//				{
//					if (byte.TryParse(tok.Trim(), out var stn))
//						if (!slaveData.ContainsKey(stn))
//							slaveData[stn] = new Dictionary<string, object>();
//				}
//			}
//		}

//		private void InitializeTimer()
//		{
//			updateTimer = new Timer { Interval = 1000 };
//			updateTimer.Tick += (s, e) => UpdateValues();
//			updateTimer.Start();
//		}

//		private async void UpdateValues()
//		{
//			if (isUpdating) return;
//			isUpdating = true;
//			try
//			{
//				await ModbusHelper.ReadAllParametersAsync(SystemConfig.IpAddress, SystemConfig.Port, this);
//				UpdateDataGridView();
//			}
//			finally { isUpdating = false; }
//		}

//		public void UpdateDataGridView()
//		{
//			if (dataGridView.InvokeRequired)
//			{
//				dataGridView.Invoke(new Action(UpdateDataGridView));
//				return;
//			}

//			var oldSel = dataGridView.CurrentRow?.Index ?? -1;
//			dataGridView.Rows.Clear(); dataGridView.Columns.Clear();
//			dataGridView.Columns.Add("Param", "參數");
//			foreach (var st in slaveData.Keys)
//				dataGridView.Columns.Add($"S{st}", $"站 {st}");

//			var allKeys = slaveData.Values.SelectMany(d => d.Keys).Distinct().ToList();
//			foreach (var key in allKeys)
//			{
//				var row = new List<object> { key };
//				foreach (var st in slaveData.Keys)
//					row.Add(slaveData[st].TryGetValue(key, out var v) ? v : "N/A");
//				dataGridView.Rows.Add(row.ToArray());
//			}

//			if (oldSel >= 0 && oldSel < dataGridView.Rows.Count)
//				dataGridView.Rows[oldSel].Selected = true;
//		}

//		private void InitializeParameters()
//		{
//			currentStatus1 = currentStatus2 = currentStatus = 0;
//			tempA = tempB = tempC = tempN = 0;
//			energy = voltageA = voltageB = voltageC = currentA = currentB = currentC =
//			totalActivePower = totalReactivePower = totalApparentPower = combinedPowerFactor = 0.0;
//			activePowerA = activePowerB = activePowerC = breakerTimes = switchStatus = ProtectionThreshold = 0;
//		}



//	}
//}
