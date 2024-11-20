//using System;
//using System.Collections.Generic;
//using System.IO.Ports;
//using System.Windows.Forms;

//namespace AmqpModbusIntegration
//{
//    public class ModbusViewer : Form
//    {
//        private SerialPort serialPort;
//        private ComboBox portSelector;
//        private TextBox stationNumberTextBox;
//        private Button connectButton, readButton, switchOnButton, switchOffButton;
//        private System.Windows.Forms.Timer updateTimer;

//        private ModbusCommunication modbusCommunication;
//        private ModbusParameters parameters;

//        public ModbusViewer()
//        {
//            modbusCommunication = new ModbusCommunication();
//            parameters = new ModbusParameters();
//            InitializeUI();
//            InitializeTimer();
//        }

//        private void InitializeUI()
//        {
//            Text = "Modbus Control";
//            Width = 800;
//            Height = 600;

//            // 添加控件
//            var portLabel = new Label { Text = "COM口:", Location = new System.Drawing.Point(10, 10), AutoSize = true };
//            Controls.Add(portLabel);

//            portSelector = new ComboBox { Location = new System.Drawing.Point(70, 10), Width = 100 };
//            portSelector.Items.AddRange(SerialPort.GetPortNames());
//            Controls.Add(portSelector);

//            var stationNumberLabel = new Label { Text = "站號:", Location = new System.Drawing.Point(10, 50), AutoSize = true };
//            Controls.Add(stationNumberLabel);

//            stationNumberTextBox = new TextBox { Location = new System.Drawing.Point(70, 50), Width = 100 };
//            Controls.Add(stationNumberTextBox);

//            connectButton = new Button { Text = "連接", Location = new System.Drawing.Point(200, 10), Width = 80 };
//            connectButton.Click += (s, e) => InitializeSerialPort(portSelector.SelectedItem?.ToString());
//            Controls.Add(connectButton);

//            readButton = new Button { Text = "讀取數值", Location = new System.Drawing.Point(200, 50), Width = 80 };
//            readButton.Click += (s, e) => ReadAllParameters();
//            Controls.Add(readButton);

//            switchOnButton = new Button { Text = "開啟開關", Location = new System.Drawing.Point(300, 50), Width = 80 };
//            switchOnButton.Click += (s, e) => modbusCommunication.SwitchON(serialPort);
//            Controls.Add(switchOnButton);

//            switchOffButton = new Button { Text = "關閉開關", Location = new System.Drawing.Point(400, 50), Width = 80 };
//            switchOffButton.Click += (s, e) => modbusCommunication.SwitchOFF(serialPort);
//            Controls.Add(switchOffButton);

//            // 初始化參數顯示
//            int yPosition = 100;
//            foreach (var parameterName in GetParameterNames())
//            {
//                var parameterLabel = new Label
//                {
//                    Text = parameterName,
//                    Location = new System.Drawing.Point(10, yPosition),
//                    AutoSize = true
//                };
//                Controls.Add(parameterLabel);

//                var valueLabel = new Label
//                {
//                    Text = "0",
//                    Location = new System.Drawing.Point(150, yPosition),
//                    AutoSize = true,
//                    Name = $"{parameterName}Value"
//                };
//                Controls.Add(valueLabel);

//                yPosition += 30;
//            }
//        }

//        private void InitializeTimer()
//        {
//            updateTimer = new System.Windows.Forms.Timer { Interval = 3000 };
//            updateTimer.Tick += (s, e) => UpdateValues();
//            updateTimer.Start();
//        }

//        private void InitializeSerialPort(string portName)
//        {
//            if (serialPort != null && serialPort.IsOpen)
//            {
//                serialPort.Close();
//                serialPort.Dispose();
//            }

//            if (!byte.TryParse(stationNumberTextBox.Text, out byte stationNumber))
//            {
//                MessageBox.Show("請輸入有效的站號 (0-255)");
//                return;
//            }

//            serialPort = new SerialPort(portName, 9600, Parity.None, 8, StopBits.One);
//            modbusCommunication.StationNumber = stationNumber;

//            try
//            {
//                serialPort.Open();
//                MessageBox.Show($"串口 {portName} 連接成功");
//            }
//            catch (Exception ex)
//            {
//                MessageBox.Show($"串口連接失敗: {ex.Message}");
//            }
//        }

//        public void ReadAllParameters()
//        {
//            if (serialPort != null && serialPort.IsOpen)
//            {
//                modbusCommunication.ReadAllParameters(serialPort, parameters);
//                UpdateUI();
//            }
//            else
//            {
//                MessageBox.Show("請先連接串口！");
//            }
//        }

//        private void UpdateUI()
//        {
//            foreach (Control control in Controls)
//            {
//                if (control is Label label && label.Name.EndsWith("Value"))
//                {
//                    string parameterName = label.Name.Replace("Value", "");
//                    var property = parameters.GetType().GetProperty(parameterName);
//                    if (property != null)
//                    {
//                        label.Text = property.GetValue(parameters)?.ToString();
//                    }
//                }
//            }
//        }

//        private void UpdateValues()
//        {
//            ReadAllParameters();
//        }

//        private IEnumerable<string> GetParameterNames()
//        {
//            return new List<string>
//    {
//        nameof(parameters.CurrentStatus1),
//        nameof(parameters.CurrentStatus2),
//        nameof(parameters.CurrentStatus),
//        nameof(parameters.VoltageA),
//        nameof(parameters.VoltageB),
//        nameof(parameters.VoltageC),
//        nameof(parameters.TotalActivePower),
//        nameof(parameters.TotalReactivePower),
//        nameof(parameters.TotalApparentPower),
//        nameof(parameters.LineFrequency),
//        nameof(parameters.PowerFactorA),
//        nameof(parameters.PowerFactorB),
//        nameof(parameters.PowerFactorC),
//        nameof(parameters.ReactivePowerA),
//        nameof(parameters.ReactivePowerB),
//        nameof(parameters.ReactivePowerC),
//        nameof(parameters.ActivePowerA),
//        nameof(parameters.ActivePowerB),
//        nameof(parameters.ActivePowerC),
//        nameof(parameters.SwitchStatus),
//        nameof(parameters.LeakageCurrent),
//        nameof(parameters.HistoricalLeakage),
//        nameof(parameters.HistoricalCurrentA),
//        nameof(parameters.HistoricalCurrentB),
//        nameof(parameters.HistoricalCurrentC),
//        nameof(parameters.DeviceType),
//        nameof(parameters.BreakerTimes),
//        nameof(parameters.TemperatureA),
//        nameof(parameters.TemperatureB),
//        nameof(parameters.TemperatureC),
//        nameof(parameters.TemperatureN),
//        nameof(parameters.EnergyHighByte),
//        nameof(parameters.EnergyLowByte),
//        nameof(parameters.Energy)
//    };
//        }

//    }
//}
