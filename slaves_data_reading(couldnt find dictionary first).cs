//using System;
//using System.Collections.Generic;
//using System.IO.Ports;
//using System.Threading.Tasks;
//using System.Windows.Forms;

//namespace AmqpModbusIntegration
//{
//    public class ModbusViewer : Form
//    {
//        private SerialPort serialPort; // 串口通信對象
//        private ComboBox portSelector;
//        private TextBox stationNumbersTextBox;
//        private Button connectButton, readButton;

//        private Dictionary<byte, Dictionary<string, int>> slaveData = new Dictionary<byte, Dictionary<string, int>>(); // 多站號數據

//        private DataGridView dataGridView; // 用於顯示數據的表格

//        public ModbusViewer()
//        {
//            InitializeUI(); // 初始化界面
//        }

//        // 初始化界面
//        private void InitializeUI()
//        {
//            Text = "Modbus 多空開讀取";
//            Width = 900;
//            Height = 600;

//            // COM口選擇
//            var portLabel = new Label { Text = "COM口:", Location = new System.Drawing.Point(10, 10), AutoSize = true };
//            Controls.Add(portLabel);
//            portSelector = new ComboBox { Location = new System.Drawing.Point(70, 10), Width = 100 };
//            portSelector.Items.AddRange(SerialPort.GetPortNames());
//            Controls.Add(portSelector);

//            // 輸入站號
//            var stationNumbersLabel = new Label { Text = "站號(用逗號分隔):", Location = new System.Drawing.Point(200, 10), AutoSize = true };
//            Controls.Add(stationNumbersLabel);
//            stationNumbersTextBox = new TextBox { Location = new System.Drawing.Point(310, 10), Width = 150 };
//            Controls.Add(stationNumbersTextBox);

//            // 連接按鈕
//            connectButton = new Button { Text = "連接", Location = new System.Drawing.Point(500, 10), Width = 80 };
//            connectButton.Click += (s, e) => InitializeSerialPort(portSelector.SelectedItem?.ToString());
//            Controls.Add(connectButton);

//            // 讀取按鈕
//            readButton = new Button { Text = "讀取數據", Location = new System.Drawing.Point(600, 10), Width = 80 };
//            readButton.Click += (s, e) => UpdateValues();
//            Controls.Add(readButton);

//            // 初始化 DataGridView
//            dataGridView = new DataGridView
//            {
//                Location = new System.Drawing.Point(10, 50),
//                Width = 850,
//                Height = 500,
//                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
//                ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.AutoSize,
//                AllowUserToAddRows = false,
//                ReadOnly = true
//            };
//            Controls.Add(dataGridView);
//        }

//        // 初始化串口
//        private void InitializeSerialPort(string portName)
//        {
//            if (serialPort != null && serialPort.IsOpen)
//            {
//                serialPort.Close();
//                serialPort.Dispose();
//            }

//            // 解析站號
//            slaveData.Clear();
//            foreach (var station in stationNumbersTextBox.Text.Split(','))
//            {
//                if (byte.TryParse(station.Trim(), out var stationNumber))
//                {
//                    slaveData[stationNumber] = new Dictionary<string, int>(); // 初始化站號數據
//                }
//            }

//            // 創建串口
//            serialPort = new SerialPort(portName, 9600, Parity.None, 8, StopBits.One);
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

//        // 更新數據顯示
//        private async void UpdateValues()
//        {
//            await ReadAllParameters(); // 讀取所有站號的數據
//            UpdateDataGridView(); // 更新界面
//        }

//        // 讀取所有參數
//        private async Task ReadAllParameters()
//        {
//            if (serialPort != null && serialPort.IsOpen)
//            {
//                foreach (var station in slaveData.Keys)
//                {
//                    try
//                    {
//                        byte[] readCommand = { station, 0x03, 0x00, 0x00, 0x00, 0x30 };
//                        ushort crc = CalculateCRC(readCommand);
//                        byte[] crcBytes = BitConverter.GetBytes(crc);
//                        byte[] fullCommand = new byte[readCommand.Length + 2];
//                        Array.Copy(readCommand, fullCommand, readCommand.Length);
//                        fullCommand[fullCommand.Length - 2] = crcBytes[0];
//                        fullCommand[fullCommand.Length - 1] = crcBytes[1];

//                        serialPort.Write(fullCommand, 0, fullCommand.Length);

//                        await Task.Delay(100); // 等待設備回應

//                        byte[] buffer = new byte[256];
//                        int bytesRead = serialPort.Read(buffer, 0, buffer.Length);

//                        if (bytesRead > 5)
//                        {
//                            var data = slaveData[station];
//                            data["當前狀態"] = (buffer[3] << 8) | buffer[4];
//                            data["A相溫度"] = (buffer[9] << 8) | buffer[10];
//                            data["B相溫度"] = (buffer[11] << 8) | buffer[12];
//                            data["C相溫度"] = (buffer[13] << 8) | buffer[14];
//                            data["A相電壓"] = (buffer[17] << 8) | buffer[18];
//                            data["A相電流"] = (buffer[23] << 8) | buffer[24];
//                            data["開關狀態"] = (buffer[53] << 8) | buffer[54];
//                            data["總有功功率"] = (buffer[63] << 8) | buffer[64];
//                            data["線頻率"] = (buffer[69] << 8) | buffer[70];
//                        }
//                    }
//                    catch (Exception ex)
//                    {
//                        Console.WriteLine($"讀取站號 {station} 時發生錯誤: {ex.Message}");
//                    }
//                }
//            }
//        }

//        // 更新界面表格
//        private void UpdateDataGridView()
//        {
//            dataGridView.Columns.Clear();
//            dataGridView.Rows.Clear();

//            // 添加參數名稱列
//            dataGridView.Columns.Add("Parameter", "參數名稱");

//            // 添加站號列
//            foreach (var station in slaveData.Keys)
//            {
//                dataGridView.Columns.Add($"Slave_{station}", $"站號 {station}");
//            }

//            // 填充數據
//            foreach (var parameter in slaveData.Values.First().Keys)
//            {
//                var row = new List<object> { parameter }; // 第一列是參數名稱
//                foreach (var station in slaveData.Keys)
//                {
//                    row.Add(slaveData[station].ContainsKey(parameter) ? slaveData[station][parameter].ToString() : "N/A");
//                }
//                dataGridView.Rows.Add(row.ToArray());
//            }
//        }

//        // 計算CRC校驗碼
//        private ushort CalculateCRC(byte[] data)
//        {
//            ushort crc = 0xFFFF;
//            for (int pos = 0; pos < data.Length; pos++)
//            {
//                crc ^= (ushort)data[pos];
//                for (int i = 8; i != 0; i--)
//                {
//                    if ((crc & 0x0001) != 0)
//                    {
//                        crc >>= 1;
//                        crc ^= 0xA001;
//                    }
//                    else
//                    {
//                        crc >>= 1;
//                    }
//                }
//            }
//            return crc;
//        }
//    }

//    internal class Program
//    {
//        [STAThread]
//        static void Main()
//        {
//            Application.Run(new ModbusViewer());
//        }
//    }
//}
