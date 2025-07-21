
using System;
using System.Collections.Generic;
using System.IO.Ports;
using System.Threading.Tasks;
using System.Windows.Forms;
using CFX;
using CFX.Transport;
using CFX.ResourcePerformance;
using System.Timers;
using Newtonsoft.Json;
using System.Linq;
using System.Threading;
using System.IO;

namespace AmqpModbusIntegration
{
	public class ModbusViewer : Form
	{
		// 建議只保留一個「公開屬性」，並提供get/set或提供一個GetSlaveData()方法
		public Dictionary<byte, Dictionary<string, object>> slaveData = new Dictionary<byte, Dictionary<string, object>>();

		private SerialPort serialPort;
		private ComboBox portSelector;
		private TextBox stationNumberTextBox;
		private Button connectButton, readButton, switchButton;
		private TextBox tempTextBox;
		private Button tempSetButton;
		private Button testFaultButton;

		private List<Label> parameterLabels = new List<Label>();
		private List<Label> valueLabels = new List<Label>();
		private System.Windows.Forms.Timer updateTimer;

		// 若需要可以再定義屬性
		public SerialPort GetSerialPort() => serialPort;
		public Dictionary<byte, Dictionary<string, object>> GetSlaveData() => slaveData;

		public int currentStatus1, currentStatus2, currentStatus;
		public int tempA, tempB, tempC, tempN;
		public int powerFactorA, powerFactorB, powerFactorC, activePowerA, activePowerB, activePowerC;
		public int reactivePowerA, reactivePowerB, reactivePowerC, breakerTimes;
		public int switchStatus, apparentPowerA;
		public int lineFrequency, deviceType;
		public int historicalLeakage, historicalCurrentA, historicalCurrentB, historicalCurrentC;
		public int ProtectionThreshold;


		public double energy; // 用 double
		public double voltageA, voltageB, voltageC, currentA, currentB, currentC;
		public double apparentPowerB, apparentPowerC, totalApparentPower, totalActivePower, totalReactivePower, combinedPowerFactor;



		// DataGridView
		private DataGridView dataGridView;

		private bool isUpdating = false;

		public ModbusViewer()
		{
			// 初始化UI
			UIInitializer.InitializeUI(
				this,
				out portSelector,            // ← 要加這個
				out stationNumberTextBox,
				out connectButton,
				out readButton,

				out var switchOnButton,
				out var switchOffButton,
				out tempTextBox,
				out tempSetButton,
				out testFaultButton,

				out var refreshButton,
				out dataGridView

				);

			connectButton.Click += (s, e) => InitializeSerialPort(portSelector.SelectedItem?.ToString());

			readButton.Click += async (s, e) =>
			{
				UpdateSlaveDataFromTextBox();
				if (isUpdating) return;
				readButton.Enabled = false;
				try
				{
					await ModbusHelper.ReadAllParametersAsync(serialPort, this);
					UpdateDataGridView();
				}
				finally
				{
					readButton.Enabled = true;
				}
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
				portSelector.Items.AddRange(SerialPort.GetPortNames());
				MessageBox.Show("COM口已刷新！", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
			};

			InitializeTimer();
			InitializeParameters();


			//250710新增=============================
			// 在 ModbusViewer 建構子最後加
			if (!string.IsNullOrEmpty(SwitchDeviceConfig.ComPort))
			{
				var ports = SerialPort.GetPortNames();
				if (ports.Contains(SwitchDeviceConfig.ComPort))
				{
					portSelector.SelectedItem = SwitchDeviceConfig.ComPort;
				}
			}

			if (SwitchDeviceConfig.StationNumber != 0)
			{
				stationNumberTextBox.Text = SwitchDeviceConfig.StationNumber.ToString();
			}

			//250710新增=============================
		}

		private void UpdateSlaveDataFromTextBox()
		{
			lock (slaveData)  // 使用鎖來確保同時只有一個線程操作 slaveData
			{
				slaveData.Clear();
				foreach (var st in stationNumberTextBox.Text.Split(','))
				{
					if (byte.TryParse(st.Trim(), out var station))
					{
						// 若該站號不存在，就建立一個新的字典
						if (!slaveData.ContainsKey(station))
							slaveData[station] = new Dictionary<string, object>();
					}
				}
			}
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
			// 在發送第一個命令前添加100毫秒延遲，確保設備穩定
			Thread.Sleep(100);
			foreach (var station in stationNumbers)
			{
				if (!slaveData.ContainsKey(station))
				{
					Console.WriteLine($"站號 {station} 不存在，操作跳過。");
					continue;
				}
				try
				{
					switchCommand(serialPort, station);
					Console.WriteLine($"站號 {station} 的開關操作執行成功");
					
					Thread.Sleep(500);
					
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

		private void InitializeTimer()
		{
			updateTimer = new System.Windows.Forms.Timer();
			updateTimer.Interval = 1000;
			updateTimer.Tick += (s, e) => UpdateValues();
			updateTimer.Start();
		}

		private async void UpdateValues()
		{
			if (isUpdating) return;
			isUpdating = true;
			try
			{
				await ModbusHelper.ReadAllParametersAsync(serialPort, this);
				this.Invoke(new Action(() => UpdateDataGridView()));
			}
			finally
			{
				isUpdating = false;
			}
		}

		public void UpdateDataGridView()
		{
			if (dataGridView.InvokeRequired)
			{
				dataGridView.Invoke(new Action(UpdateDataGridView));
				return;
			}

			int currentSelectedRowIndex = dataGridView.CurrentRow?.Index ?? -1;
			int firstDisplayedRowIndex = dataGridView.FirstDisplayedScrollingRowIndex;

			dataGridView.Rows.Clear();
			dataGridView.Columns.Clear();
			dataGridView.RowHeadersVisible = false;

			dataGridView.Columns.Add("Parameter", "參數名稱");

			// 針對每個站號，新增一欄
			foreach (var station in slaveData.Keys)
			{
				dataGridView.Columns.Add($"Slave_{station}", $"站號 {station}");
			}

			var allParameters = slaveData.Values
										 .SelectMany(d => d.Keys)
										 .Distinct()
										 .ToList();

			foreach (var parameter in allParameters)
			{
				var row = new List<object> { parameter };
				foreach (var station in slaveData.Keys)
				{
					if (slaveData[station].TryGetValue(parameter, out var valObj))
					{
						row.Add(valObj?.ToString() ?? "N/A");
					}
					else
					{
						row.Add("N/A");
					}
				}
				dataGridView.Rows.Add(row.ToArray());
			}

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

		private void InitializeSerialPort(string portName)
		{
			if (serialPort != null && serialPort.IsOpen)
			{
				serialPort.Close();
				serialPort.Dispose();
			}
			updateTimer.Stop();

			slaveData.Clear();
			foreach (var st in stationNumberTextBox.Text.Split(','))
			{
				if (byte.TryParse(st.Trim(), out var stationNumber))
				{
					slaveData[stationNumber] = new Dictionary<string, object>();
				}
			}

			serialPort = new SerialPort(portName, 9600, Parity.None, 8, StopBits.One);
			try
			{
				serialPort.Open();
				Console.WriteLine($"串口 {portName} 已成功打開");
				updateTimer.Start();
			}
			catch (Exception ex)
			{
				Console.WriteLine($"無法打開串口 {portName}: {ex.Message}");
			}
		}


		//初始化所有參數
		private void InitializeParameters()
		{
			currentStatus1 = 0;
			currentStatus2 = 0;
			currentStatus = 0;
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
			energy = 0.0;
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
	}

	internal class Program
	{
		static ModbusViewer modbusViewer = new ModbusViewer();

		[STAThread]
		static void Main()
		{

			// ===== 先讀取 CFX.ini 設定 =====
			try
			{
				ConfigHelper.LoadConfiguration();
			}
			catch (Exception ex)
			{
				MessageBox.Show("載入設定檔失敗: " + ex.Message);
				return;
			}
			// ================================

			//（其餘不動）

			//// 取得 modbusViewer 中的 slaveData
			//var serialPort = new SerialPort(SwitchDeviceConfig.ComPort ?? "COM1", 9600, Parity.None, 8, StopBits.One);
			//serialPort.Open();

			//// 啟動 UI（serial port 由 UI 初始化，自動抓 ini 設定）
			//Application.Run(new ModbusViewer());

			//var slaveData = modbusViewer.GetSlaveData();

			//var amqpManager = new AmqpEndpointManager(
			//	SystemConfig.PublishAddress ?? "amqp://127.0.0.1:8888",
			//	SystemConfig.PublishAddress ?? "amqp://127.0.0.1:6666",
			//	SystemConfig.PublishAddress ?? "amqp://127.0.0.1:6666",
			//	modbusViewer,
			//	serialPort,
			//	slaveData
			//);

			//amqpManager.StartAmqpEndpoint(SystemConfig.DeviceID ?? "CFX.A00.ST07220001");


			//Application.Run(modbusViewer);


			// ② 啟動 UI（SerialPort 讓使用者在視窗裡按「連接」時再開）
			Application.EnableVisualStyles();
			Application.SetCompatibleTextRenderingDefault(false);

			modbusViewer = new ModbusViewer();

			/* 如果想啟動時就把 COM 口、站號預先帶入 */
			if (!string.IsNullOrWhiteSpace(SwitchDeviceConfig.ComPort))
			{
				modbusViewer.Controls.OfType<ComboBox>()
					  .FirstOrDefault(cb => cb.Items.Contains(SwitchDeviceConfig.ComPort))
					  ?.Select((object)SwitchDeviceConfig.ComPort);
			}
			if (SwitchDeviceConfig.StationNumber > 0)
			{
				modbusViewer.Controls.OfType<TextBox>()
					  .FirstOrDefault(tb => tb.Name == string.Empty)  // 只有一個 TextBox 就這樣抓
					  ?.SetText(SwitchDeviceConfig.StationNumber.ToString());
			}

			// ③ 跑視窗 ── 這行會「阻塞」到視窗關閉
			Application.Run(modbusViewer);

			// ④ 視窗關閉後，如有打開 SerialPort，再啟動 AMQP
			SerialPort serial = modbusViewer.GetSerialPort();
			if (serial == null || !serial.IsOpen) return;          // 使用者可能沒連

			var amqp = new AmqpEndpointManager(
				SystemConfig.PublishAddress ?? "amqp://127.0.0.1:8888",
				SystemConfig.PublishAddress ?? "amqp://127.0.0.1:6666",
				SystemConfig.PublishAddress ?? "amqp://127.0.0.1:6666",
				modbusViewer,
				serial,
				modbusViewer.GetSlaveData());

			amqp.StartAmqpEndpoint(SystemConfig.DeviceID ?? "CFX.A00.ST07220001");
		}




	}

	//250710新增=======================
	// 加入在 AmqpModbusIntegration namespace 內
	public static class ConfigHelper
	{
		private static readonly string IniPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "250710_MLinkDCFX");

		public static void LoadConfiguration()
		{
			if (!File.Exists(IniPath))
				throw new FileNotFoundException("找不到配置檔: " + IniPath);

			string section = string.Empty;
			foreach (var raw in File.ReadAllLines(IniPath))
			{
				var line = raw.Trim();
				if (string.IsNullOrEmpty(line) || line.StartsWith(";") || line.StartsWith("//")) continue;

				if (line.StartsWith("[") && line.EndsWith("]"))
				{
					section = line.Substring(1, line.Length - 2);
					continue;
				}

				int idx = line.IndexOf('=');
				if (idx < 0) continue;

				string key = line.Substring(0, idx).Trim();
				string val = line.Substring(idx + 1).Trim();

				switch (section)
				{
					case "Machine":
						SystemConfig.ApplyMachine(key, val); break;
					case "DeviceSettings":
						SystemConfig.ApplyDeviceSettings(key, val); break;
					case "SwitchDevice":
						SwitchDeviceConfig.Apply(key, val); break;
					case "DevicePosition":
						SystemConfig.ApplyDevicePosition(key, val); break;
					case "Level":
						SystemConfig.ApplyLevel(key, val); break;
					case "UsingTopicItem":
						SystemConfig.ApplyTopic(key, val); break;
					case "CFXInfo":
						SystemConfig.ApplyCFXInfo(key, val); break;
				}
			}
		}
	}

	public static class SystemConfig
	{
		// [Machine]
		public static bool Enable { get; set; }
		public static int UsageMode { get; set; }

		// [DeviceSettings]
		public static string IP { get; set; }
		public static int Port { get; set; }
		public static string UserName { get; set; }
		public static string Password { get; set; }
		public static string ClientID { get; set; }
		public static string DeviceID { get; set; }
		public static string LaneID { get; set; }
		public static string MC_IP { get; set; }
		public static int MC_Port { get; set; }
		public static string Remote_IP { get; set; }
		public static int Remote_Port { get; set; }
		public static string PublishAddress { get; set; }
		public static string DeviceType { get; set; }

		// [DevicePosition]
		public static string Factory { get; set; }
		public static string Line { get; set; }
		public static string Section { get; set; }
		public static string Group { get; set; }
		public static string Station { get; set; }

		// [Level]
		public static int ClientLogLevel { get; set; }

		// [UsingTopicItem]
		public static string UsingItem { get; set; }

		// [CFXInfo]
		public static string OfflineTime { get; set; }

		public static void ApplyMachine(string key, string value)
		{
			switch (key)
			{
				case "Enable": Enable = value.ToLower() == "true"; break;
				case "UsageMode": int v; if (int.TryParse(value, out v)) UsageMode = v; break;
			}
		}
		public static void ApplyDeviceSettings(string key, string value)
		{
			switch (key)
			{
				case "IP": IP = value; break;
				case "Port": int v; if (int.TryParse(value, out v)) Port = v; break;
				case "UserName": UserName = value; break;
				case "Password": Password = value; break;
				case "ClientID": ClientID = value; break;
				case "DeviceID": DeviceID = value; break;
				case "LaneID": LaneID = value; break;
				case "MC_IP": MC_IP = value; break;
				case "MC_Port": int p; if (int.TryParse(value, out p)) MC_Port = p; break;
				case "Remote_IP": Remote_IP = value; break;
				case "Remote_Port": int r; if (int.TryParse(value, out r)) Remote_Port = r; break;
				case "PublishAddress": PublishAddress = value; break;
				case "DeviceType": DeviceType = value; break;
			}
		}
		public static void ApplyDevicePosition(string key, string value)
		{
			switch (key)
			{
				case "Factory": Factory = value; break;
				case "Line": Line = value; break;
				case "Section": Section = value; break;
				case "Group": Group = value; break;
				case "Station": Station = value; break;
				case "DeviceType": DeviceType = value; break;
				case "DeviceID": DeviceID = value; break;
			}
		}
		public static void ApplyLevel(string key, string value)
		{
			if (key == "ClientLogLevel")
			{
				int v; if (int.TryParse(value, out v)) ClientLogLevel = v;
			}
		}
		public static void ApplyTopic(string key, string value)
		{
			if (key == "UsingItem") UsingItem = value;
		}
		public static void ApplyCFXInfo(string key, string value)
		{
			if (key == "OfflineTime") OfflineTime = value;
		}
	}

	public static class SwitchDeviceConfig
	{
		public static string ComPort { get; set; }
		public static byte StationNumber { get; set; }

		public static void Apply(string key, string value)
		{
			switch (key)
			{
				case "COM": ComPort = value; break;
				case "StationNumber": byte stn; if (byte.TryParse(value, out stn)) StationNumber = stn; break;
			}
		}
	}

}
