
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

////250505為了TCP新增
//using System.Net.Sockets;
//using static System.Collections.Specialized.BitVector32;



//namespace AmqpModbusIntegration
//{
//	public class ModbusViewer : Form
//	{

//		// 新增必要字段
//		public string ip = "192.168.1.254";
//		public int port = 502;
//		public byte station = 1;


//		// 建議只保留一個「公開屬性」，並提供get/set或提供一個GetSlaveData()方法
//		public Dictionary<byte, Dictionary<string, object>> slaveData = new Dictionary<byte, Dictionary<string, object>>();

//		//private SerialPort serialPort;
//		//private ComboBox portSelector;
//		private TextBox stationNumberTextBox;
//		private Button connectButton, readButton, switchButton;
//		private TextBox tempTextBox;
//		private Button tempSetButton;
//		private Button testFaultButton;

//		private List<Label> parameterLabels = new List<Label>();
//		private List<Label> valueLabels = new List<Label>();
//		private System.Windows.Forms.Timer updateTimer;

//		// 若需要可以再定義屬性
//		//public SerialPort GetSerialPort() => serialPort;
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

//		public ModbusViewer()
//		{
//			// 初始化UI
//			UIInitializer.InitializeUI(
//				this,
//				out stationNumberTextBox,
//				out connectButton,
//				out readButton,
//				out var switchOnButton,
//				out var switchOffButton,
//				out tempTextBox,
//				out tempSetButton,
//				out testFaultButton,
//				out var refreshButton,
//				out dataGridView

//				);

//			// ===== 异步 合闸/分闸 =====
//			switchOnButton.Click += async (s, e) =>
//			{
//				if (byte.TryParse(stationNumberTextBox.Text, out byte station))
//					await ModbusHelper.SwitchON(this.ip, this.port, station);
//			};
//			switchOffButton.Click += async (s, e) =>
//			{
//				if (byte.TryParse(stationNumberTextBox.Text, out byte station))
//					await ModbusHelper.SwitchOFF(this.ip, this.port, station);
//			};


//			readButton.Click += async (s, e) =>
//			{
//				UpdateSlaveDataFromTextBox();
//				if (isUpdating) return;
//				readButton.Enabled = false;
//				try
//				{
//					await ModbusHelper.ReadAllParametersAsync(ip, port, this); // 補充必要參數
//					UpdateDataGridView();
//				}
//				finally
//				{
//					readButton.Enabled = true;
//				}
//			};

//			//switchOnButton.Click += (s, e) => ExecuteSwitchCommand((ip, port, station) =>
//			//async ModbusHelper.SwitchON(ip, port, station)		);

//			//switchOffButton.Click += (s, e) => ExecuteSwitchCommand((ip, port, station) =>
//			//async ModbusHelper.SwitchOFF(ip, port, station)		);

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
//				//portSelector.Items.Clear();
//				//portSelector.Items.AddRange(SerialPort.GetPortNames());
//				MessageBox.Show("COM口已刷新！", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
//			};

//			InitializeTimer();
//			InitializeParameters();
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

//		//public void ExecuteSwitchCommand(Action<SerialPort, byte> switchCommand, List<byte> stationNumbers = null)
//		// 修改后（使用 TCP 参数）
//		public void ExecuteSwitchCommand(Action<string, int, byte> switchCommand, List<byte> stationNumbers = null)
//		{
//			//if (serialPort == null || !serialPort.IsOpen)
//			//{
//			//	MessageBox.Show("請先連接串口！");
//			//	return;
//			//}
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
//					switchCommand(ip, port, station);
//					Console.WriteLine($"站號 {station} 的開關操作執行成功");
//					//250423新增==========
//					Thread.Sleep(500);
//					//250423新增==========
//				}
//				catch (Exception ex)
//				{
//					Console.WriteLine($"執行站號 {station} 的開關操作時出現錯誤: {ex.Message}");
//				}
//			}
//		}

//		public void ExecuteSetTemperature(byte stationNumber, ushort temperature)
//		{
//			Task.Run(() =>
//			{
//				lock (ModbusHelper.tcpLock)
//				{
//					ModbusHelper.SetTemperature(ip, port, stationNumber, temperature, slaveData);
//					ModbusHelper.ReadAllParametersAsync(ip, port, this).Wait(); // 補充必要參數
//				}
//			});
//		}

//		private void InitializeTimer()
//		{
//			updateTimer = new System.Windows.Forms.Timer();
//			updateTimer.Interval = 1000;
//			updateTimer.Tick += (s, e) => UpdateValues();
//			updateTimer.Start();
//		}

//		private async void UpdateValues()
//		{
//			if (isUpdating) return;
//			isUpdating = true;
//			try
//			{
//				await ModbusHelper.ReadAllParametersAsync(ip, port, this); // 補充參數
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
//		static ModbusViewer modbusViewer = new ModbusViewer();

//		[STAThread]
//		static void Main()
//		{
//			modbusViewer.ip = "192.168.1.254";
//			modbusViewer.port = 502;
//			modbusViewer.station = 1;

//			var slaveData = modbusViewer.GetSlaveData();

//			var amqpManager = new AmqpEndpointManager(
//														"amqp://127.0.0.1:8888",
//														"amqp://127.0.0.1:6666",
//														"amqp://127.0.0.1:6666",
//														modbusViewer,
//														//modbusViewer.GetSerialPort(),
//														modbusViewer.slaveData
//													);


//			//下面這個是勁諺給的I01插件機，也是sie上118顯示的
//			//amqpManager.StartAmqpEndpoint("CFX.A00.S056421215");
//			//下面這個是jianshan給的I01插件機，也是MES上顯示最終的正確版
//			amqpManager.StartAmqpEndpoint("CFX.A00.ST07220001");


//			//// 使用使用者輸入的 AMQP endpoint 來啟動
//			//amqpManager.StartAmqpEndpoint(amqpEndpoint);


//			Application.Run(modbusViewer);
//		}
//	}
//}
