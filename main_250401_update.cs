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
using System.IO;
using Confluent.Kafka;
using CFX.Structures;


using System.Text;
using CFX.InformationSystem;
using CFX.InformationSystem.DataTransfer;
using CFX.InformationSystem.OperatorValidation;
using CFX.InformationSystem.ProductionScheduling;
using CFX.InformationSystem.UnitValidation;
using CFX.InformationSystem.WorkOrderManagement;
using CFX.Structures.GenericEquipment;



namespace AmqpModbusIntegration
{
	//250401新增==============================
	public class Config
	{

		public string factory { get; set; }
		public string line { get; set; }
		public string station { get; set; }
		public string machineSN { get; set; }
		public string Remote_IP { get; set; }
		public string Remote_port { get; set; }
		public string MC_IP { get; set; }
		public string MC_Port { get; set; }
		public string MC_Name { get; set; }
		public bool UseCFX { get; set; }
		public string user { get; set; }
		public string password { get; set; }
		public string publishAddress { get; set; }
		public string SubPublishAddress { get; set; } = "/queue/Broadcast-Delta.Srcrew.A01";
		public string MyrequestUri { get; set; }
		public List<string> Topics { get; set; }
		public string ModelNumber { get; set; }//設備的銷售類型編號
		public int NumberOfLanes { get; set; } = 1;//軌道數量
		public string SN { get; set; }
		public List<StageInformation> Stages { get; set; } = new List<StageInformation>(); //設備工位描述
		public Guid Guid { get; set; }
		public Stage Stage { get; set; }//阶段用
		public int sequence { get; set; } = 1;
		public List<UnitSetPoint> unitSetPoints = new List<UnitSetPoint>();
		public List<SupportedTopic> SupportTopics { get; set; } = new List<SupportedTopic>();//設備支持的CFX能力表
		public List<Fault> Faults { get; set; } = new List<Fault>();
		public string UniqueIdentifier { get; set; } //設條條碼
		public string Vendor { get; set; } = "Delta";//供應商"原本 AMBU"
		public List<List<UnitPosition>> ListUnit_UnTest = new List<List<UnitPosition>>();
		public Recipe CurrentRecipe = new Recipe();
		//ValidateUnitsRequestURl=amqp://certification.connectedfactoryexchange.com:5672/  QPL用
		public string ValidateUnitsRequestURl { get; set; } = "amqp://certification.connectedfactoryexchange.com:5672/";
		/// <summary>
		/// CFX.Certification.Requests  QPL用
		/// </summary>
		public string ValidateUnitsHandle { get; set; } = "CFX.Certification.Requests";
		//空開測試節能
		public float fTemperature { get; set; }
		public string COM { get; set; }
		public string StationNumber { get; set; }
		public bool bEnergyConsumption { get; set; }
		public Guid TransactionID;
	}
	//250401新增==============================

	public class ModbusViewer : Form
	{
		// 建議只保留一個「公開屬性」，並提供get/set或提供一個GetSlaveData()方法
		public Dictionary<byte, Dictionary<string, object>> slaveData = new Dictionary<byte, Dictionary<string, object>>();
		//250401新增==============================
		// 定義一個存放配置參數的物件
		public Config _para;
		//250401新增==============================


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

			_para = new Config(); // 初始化配置參數
			//InitializeComponent();

			// 初始化UI
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

				//250327新增===================
				out TextBox CFXhandlerInputBox
				//250327新增===================

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

			testFaultButton.Click += (s, e) =>
			{
				ModbusHelper.SimulateFaultTest(this, slaveData);
			};

			refreshButton.Click += (s, e) =>
			{
				portSelector.Items.Clear();
				portSelector.Items.AddRange(SerialPort.GetPortNames());
				MessageBox.Show("COM口已刷新！", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
			};

			InitializeTimer();
			InitializeParameters();
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
			//250401新增===========================
			// 在程式啟動前，讀取 CFX.ini 文件
			string iniPath = Path.Combine(Application.StartupPath, "CFX.ini");
			if (!System.IO.File.Exists(iniPath))
			{
				MessageBox.Show("找不到 CFX.ini 檔案，無法啟動", "錯誤", MessageBoxButtons.OK, MessageBoxIcon.Error);
				return;
			}

			// 使用 IniHelper 載入配置
			IniHelper ini = new IniHelper(iniPath);
			// 將讀取的參數設定到 modbusViewer._para（假設 _para 是您用來保存配置的物件）
			modbusViewer._para.factory = ini.Read("Configuration", "Factory");
			modbusViewer._para.line = ini.Read("Configuration", "Line");
			modbusViewer._para.station = ini.Read("Configuration", "Station");
			modbusViewer._para.machineSN = ini.Read("Configuration", "MachineSN");
			modbusViewer._para.MC_IP = ini.Read("Configuration", "MC_IP");
			modbusViewer._para.MC_Port = ini.Read("Configuration", "MC_Port");
			//modbusViewer._para.MC_Port = int.Parse(ini.Read("Configuration", "MC_Port"));

			modbusViewer._para.Remote_IP = ini.Read("Configuration", "Remote_IP");
			modbusViewer._para.Remote_port = ini.Read("Configuration", "Remote_Port");
			modbusViewer._para.publishAddress = ini.Read("Configuration", "PublishAddress");
			modbusViewer._para.MyrequestUri = ini.Read("Configuration", "MyrequestUri");
			modbusViewer._para.ModelNumber = ini.Read("Configuration", "ModelName");
			modbusViewer._para.MC_Name = ini.Read("Configuration", "MC_Name");
			modbusViewer._para.UseCFX = Convert.ToBoolean(ini.Read("Configuration", "UseCFX"));
			modbusViewer._para.SubPublishAddress = ini.Read("Configuration", "SubPublishAddress");

			// 讀取 SwitchDevice 區塊
			modbusViewer._para.COM = ini.Read("SwitchDevice", "COM");
			modbusViewer._para.StationNumber = ini.Read("SwitchDevice", "StationNumber");
			modbusViewer._para.fTemperature = float.Parse(ini.Read("SwitchDevice", "fTemperature"));
			modbusViewer._para.bEnergyConsumption = Convert.ToBoolean(ini.Read("SwitchDevice", "bEnergyConsumption"));
			//250401新增===========================



			// 取得 modbusViewer 中的 slaveData
			var serialPort = modbusViewer.GetSerialPort();
			var slaveData = modbusViewer.GetSlaveData();


			

			//250327新增===========================================
			// 取得使用者輸入的 AMQP Endpoint 名稱
			var amqpEndpoint = modbusViewer.Controls.OfType<TextBox>()
				.FirstOrDefault(txt => txt.Name == "CFXhandlerInputBox")?.Text;

			if (string.IsNullOrEmpty(amqpEndpoint))
			{
				MessageBox.Show("請輸入有效的 AMQP Endpoint 名稱！", "錯誤", MessageBoxButtons.OK, MessageBoxIcon.Error);
				return;
			}
			//250327新增===========================================


			//250401新增===========================
			// 建立 AmqpEndpointManager，參數由 modbusViewer._para 提供
			var amqpManager = new AmqpEndpointManager(
				modbusViewer._para.MyrequestUri.ToString(),   // endpointUri，例如 "amqp://127.0.0.1:30000"
				$"amqp://{modbusViewer._para.Remote_IP}:{modbusViewer._para.Remote_port}",  // publishChannelUri
				$"amqp://{modbusViewer._para.Remote_IP}:{modbusViewer._para.Remote_port}",  // subscribeChannelUri
				modbusViewer,
				serialPort,
				slaveData);
			//250401新增===========================


			//var amqpManager = new AmqpEndpointManager(
			//	"amqp://127.0.0.1:8888",
			//	"amqp://127.0.0.1:6666",
			//	"amqp://127.0.0.1:6666",
			//	modbusViewer,
			//	serialPort,
			//	slaveData);


			//下面這個是勁諺給的I01插件機，也是sie上118顯示的
			//amqpManager.StartAmqpEndpoint("CFX.A00.S056421215");
			//下面這個是jianshan給的I01插件機，也是MES上顯示最終的正確版
			//amqpManager.StartAmqpEndpoint("CFX.A00.ST07220001");


			// 使用使用者輸入的 AMQP endpoint 來啟動
			amqpManager.StartAmqpEndpoint(amqpEndpoint);


			Application.Run(modbusViewer);
		}
	}
}
