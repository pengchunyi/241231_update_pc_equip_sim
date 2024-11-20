
////using System;
////using System.IO;
////using System.Windows.Forms;
////using System.IO.Ports;
////using Newtonsoft.Json.Linq;
////using Timer = System.Windows.Forms.Timer;
////using System.Threading.Tasks;

////public class ModbusViewer : Form
////{
////    private SerialPort serialPort;
////    private Timer refreshTimer;
////    private byte stationNumber = 0xFF;

////    // 各參數變數
////    private int currentStatus1, currentStatus2, leakageCurrent;
////    private int tempA, tempB, tempC, tempN;
////    private int voltageA, voltageB, voltageC;
////    private int currentA, currentB, currentC;
////    private int powerFactorA, powerFactorB, powerFactorC;
////    private int activePowerA, activePowerB, activePowerC;
////    private int reactivePowerA, reactivePowerB, reactivePowerC;
////    private int breakerTimes, energyHighByte, energyLowByte;
////    private int switchStatus, apparentPowerA, apparentPowerB, apparentPowerC;
////    private int totalApparentPower, totalActivePower, totalReactivePower;
////    private int combinedPowerFactor, lineFrequency, deviceType;
////    private int historicalLeakage, historicalCurrentA, historicalCurrentB, historicalCurrentC, lineColorMark;

////    public ModbusViewer()
////    {
////        InitializeUI();
////        InitializeRefreshTimer();
////    }

////    private void InitializeSerialPort(string portName)
////    {
////        try
////        {
////            if (serialPort != null && serialPort.IsOpen)
////            {
////                serialPort.Close();
////                serialPort.Dispose();
////            }

////            serialPort = new SerialPort(portName, 9600, Parity.None, 8, StopBits.One);
////            serialPort.Open();
////            MessageBox.Show($"串口 {portName} 初始化成功");
////        }
////        catch (Exception ex)
////        {
////            MessageBox.Show($"串口初始化失敗: {ex.Message}");
////        }
////    }

////    private void InitializeUI()
////    {
////        Text = "Modbus Control";
////        Width = 800;
////        Height = 600;

////        var portLabel = new Label
////        {
////            Text = "串口:",
////            Location = new System.Drawing.Point(10, 10),
////            AutoSize = true
////        };
////        Controls.Add(portLabel);

////        var portSelector = new ComboBox
////        {
////            Location = new System.Drawing.Point(80, 10),
////            Width = 100
////        };
////        portSelector.Items.AddRange(SerialPort.GetPortNames());
////        Controls.Add(portSelector);

////        var connectButton = new Button
////        {
////            Text = "連接",
////            Location = new System.Drawing.Point(190, 10),
////            Width = 80
////        };
////        connectButton.Click += (s, e) =>
////        {
////            if (portSelector.SelectedItem != null)
////            {
////                InitializeSerialPort(portSelector.SelectedItem.ToString());
////            }
////            else
////            {
////                MessageBox.Show("請選擇一個串口");
////            }
////        };
////        Controls.Add(connectButton);

////        var stationNumberLabel = new Label
////        {
////            Text = "站號:",
////            Location = new System.Drawing.Point(10, 50),
////            AutoSize = true
////        };
////        Controls.Add(stationNumberLabel);

////        var stationNumberTextBox = new TextBox
////        {
////            Location = new System.Drawing.Point(60, 50),
////            Width = 50
////        };
////        stationNumberTextBox.TextChanged += (s, e) =>
////        {
////            if (byte.TryParse(stationNumberTextBox.Text, out byte result))
////            {
////                stationNumber = result;
////            }
////            else
////            {
////                MessageBox.Show("請輸入有效的站號 (0-255)");
////            }
////        };
////        Controls.Add(stationNumberTextBox);
////    }

////    private void InitializeRefreshTimer()
////    {
////        refreshTimer = new Timer
////        {
////            Interval = 3000 // 3秒
////        };
////        refreshTimer.Tick += (s, e) => ReadAndSaveJson();
////        refreshTimer.Start();
////    }

////    private ushort CalculateCRC(byte[] data)
////    {
////        ushort crc = 0xFFFF;
////        for (int pos = 0; pos < data.Length; pos++)
////        {
////            crc ^= (ushort)data[pos];
////            for (int i = 8; i != 0; i--)
////            {
////                if ((crc & 0x0001) != 0)
////                {
////                    crc >>= 1;
////                    crc ^= 0xA001;
////                }
////                else
////                {
////                    crc >>= 1;
////                }
////            }
////        }
////        return crc;
////    }

////    private void ReadAllParameters()
////    {
////        if (serialPort != null && serialPort.IsOpen)
////        {
////            byte[] readCommand = { stationNumber, 0x03, 0x00, 0x00, 0x00, 0x30 };
////            ushort crc = CalculateCRC(readCommand);
////            byte[] crcBytes = BitConverter.GetBytes(crc);
////            byte[] fullCommand = new byte[readCommand.Length + 2];
////            Array.Copy(readCommand, fullCommand, readCommand.Length);
////            fullCommand[fullCommand.Length - 2] = crcBytes[0];
////            fullCommand[fullCommand.Length - 1] = crcBytes[1];

////            serialPort.Write(fullCommand, 0, fullCommand.Length);

////            Task.Run(() =>
////            {
////                try
////                {
////                    byte[] buffer = new byte[256];
////                    int bytesRead = serialPort.Read(buffer, 0, buffer.Length);

////                    if (bytesRead > 5)
////                    {
////                        // 按照讀取順序依次填入各個變數
////                        currentStatus1 = (buffer[3] << 8) | buffer[4];
////                        currentStatus2 = (buffer[5] << 8) | buffer[6];
////                        leakageCurrent = (buffer[7] << 8) | buffer[8];
////                        tempA = (buffer[9] << 8) | buffer[10];
////                        tempB = (buffer[11] << 8) | buffer[12];
////                        tempC = (buffer[13] << 8) | buffer[14];
////                        tempN = (buffer[15] << 8) | buffer[16];
////                        voltageA = (buffer[17] << 8) | buffer[18];
////                        voltageB = (buffer[19] << 8) | buffer[20];
////                        voltageC = (buffer[21] << 8) | buffer[22];
////                        currentA = (buffer[23] << 8) | buffer[24];
////                        currentB = (buffer[25] << 8) | buffer[26];
////                        currentC = (buffer[27] << 8) | buffer[28];
////                        powerFactorA = (buffer[29] << 8) | buffer[30];
////                        powerFactorB = (buffer[31] << 8) | buffer[32];
////                        powerFactorC = (buffer[33] << 8) | buffer[34];
////                        activePowerA = (buffer[35] << 8) | buffer[36];
////                        activePowerB = (buffer[37] << 8) | buffer[38];
////                        activePowerC = (buffer[39] << 8) | buffer[40];
////                        reactivePowerA = (buffer[41] << 8) | buffer[42];
////                        reactivePowerB = (buffer[43] << 8) | buffer[44];
////                        reactivePowerC = (buffer[45] << 8) | buffer[46];
////                        breakerTimes = (buffer[47] << 8) | buffer[48];
////                        energyHighByte = (buffer[49] << 8) | buffer[50];
////                        energyLowByte = (buffer[51] << 8) | buffer[52];
////                        switchStatus = (buffer[53] << 8) | buffer[54];
////                        apparentPowerA = (buffer[55] << 8) | buffer[56];
////                        apparentPowerB = (buffer[57] << 8) | buffer[58];
////                        apparentPowerC = (buffer[59] << 8) | buffer[60];
////                        totalApparentPower = (buffer[61] << 8) | buffer[62];
////                        totalActivePower = (buffer[63] << 8) | buffer[64];
////                        totalReactivePower = (buffer[65] << 8) | buffer[66];
////                        combinedPowerFactor = (buffer[67] << 8) | buffer[68];
////                        lineFrequency = (buffer[69] << 8) | buffer[70];
////                        deviceType = (buffer[71] << 8) | buffer[72];
////                        historicalLeakage = (buffer[73] << 8) | buffer[74];
////                        historicalCurrentA = (buffer[75] << 8) | buffer[76];
////                        historicalCurrentB = (buffer[77] << 8) | buffer[78];
////                        historicalCurrentC = (buffer[79] << 8) | buffer[80];
////                        lineColorMark = (buffer[81] << 8) | buffer[82];
////                    }
////                }
////                catch (Exception ex)
////                {
////                    MessageBox.Show($"讀取參數時發生錯誤: {ex.Message}");
////                }
////            });
////        }
////    }

////    private void SaveToJsonFile()
////    {
////        var json = new JObject(
////            new JProperty("Result", new JObject(
////                new JProperty("Result", "Success"),
////                new JProperty("ResultCode", 0),
////                new JProperty("Message", "OK")
////            )),
////            new JProperty("StartTime", DateTime.Now.ToString("o")),
////            new JProperty("EndTime", DateTime.Now.AddMinutes(5).ToString("o")),
////            new JProperty("當前狀態1", currentStatus1),
////            new JProperty("當前狀態2", currentStatus2),
////            new JProperty("漏電電流", leakageCurrent),
////            new JProperty("A相溫度", tempA),
////            new JProperty("B相溫度", tempB),
////            new JProperty("C相溫度", tempC),
////            new JProperty("N相溫度", tempN),
////            new JProperty("A相電壓", voltageA),
////            new JProperty("B相電壓", voltageB),
////            new JProperty("C相電壓", voltageC),
////            new JProperty("A相電流", currentA),
////            new JProperty("B相電流", currentB),
////            new JProperty("C相電流", currentC),
////            new JProperty("A相功率因數", powerFactorA),
////            new JProperty("B相功率因數", powerFactorB),
////            new JProperty("C相功率因數", powerFactorC),
////            new JProperty("A相有功功率", activePowerA),
////            new JProperty("B相有功功率", activePowerB),
////            new JProperty("C相有功功率", activePowerC),
////            new JProperty("A相無功功率", reactivePowerA),
////            new JProperty("B相無功功率", reactivePowerB),
////            new JProperty("C相無功功率", reactivePowerC),
////            new JProperty("合閉次數", breakerTimes),
////            new JProperty("電能高字節", energyHighByte),
////            new JProperty("電能低字節", energyLowByte),
////            new JProperty("開關狀態", switchStatus),
////            new JProperty("A相視在功率", apparentPowerA),
////            new JProperty("B相視在功率", apparentPowerB),
////            new JProperty("C相視在功率", apparentPowerC),
////            new JProperty("總視在功率", totalApparentPower),
////            new JProperty("總有功功率", totalActivePower),
////            new JProperty("總無功功率", totalReactivePower),
////            new JProperty("合相功率因數", combinedPowerFactor),
////            new JProperty("線頻率", lineFrequency),
////            new JProperty("設備類型", deviceType),
////            new JProperty("歷史漏電值", historicalLeakage),
////            new JProperty("歷史A相電流", historicalCurrentA),
////            new JProperty("歷史B相電流", historicalCurrentB),
////            new JProperty("歷史C相電流", historicalCurrentC),
////            new JProperty("線路顏色標誌", lineColorMark)
////        );

////        File.WriteAllText("_data.json", json.ToString());
////    }

////    private void ReadAndSaveJson()
////    {
////        ReadAllParameters();
////        SaveToJsonFile();
////    }

////    [STAThread]
////    public static void Main()
////    {
////        Application.Run(new ModbusViewer());
////    }
////}


//using System;
//using System.IO;
//using System.Windows.Forms;
//using System.IO.Ports;
//using Newtonsoft.Json.Linq;
//using Timer = System.Windows.Forms.Timer;
//using System.Threading.Tasks;

//public class ModbusViewer : Form
//{
//    private SerialPort serialPort;
//    private Timer refreshTimer;
//    private byte stationNumber = 0xFF;

//    // 各參數變數
//    private int currentStatus1, currentStatus2, leakageCurrent;
//    private int tempA, tempB, tempC, tempN;
//    private int voltageA, voltageB, voltageC;
//    private int currentA, currentB, currentC;
//    private int powerFactorA, powerFactorB, powerFactorC;
//    private int activePowerA, activePowerB, activePowerC;
//    private int reactivePowerA, reactivePowerB, reactivePowerC;
//    private int breakerTimes, energyHighByte, energyLowByte;
//    private int switchStatus, apparentPowerA, apparentPowerB, apparentPowerC;
//    private int totalApparentPower, totalActivePower, totalReactivePower;
//    private int combinedPowerFactor, lineFrequency, deviceType;
//    private int historicalLeakage, historicalCurrentA, historicalCurrentB, historicalCurrentC, lineColorMark;

//    public ModbusViewer()
//    {
//        InitializeUI();
//        InitializeRefreshTimer();
//    }

//    private void InitializeSerialPort(string portName)
//    {
//        try
//        {
//            if (serialPort != null && serialPort.IsOpen)
//            {
//                serialPort.Close();
//                serialPort.Dispose();
//            }

//            serialPort = new SerialPort(portName, 9600, Parity.None, 8, StopBits.One);
//            serialPort.Open();
//            MessageBox.Show($"串口 {portName} 初始化成功");
//        }
//        catch (Exception ex)
//        {
//            MessageBox.Show($"串口初始化失敗: {ex.Message}");
//        }
//    }

//    private void InitializeUI()
//    {
//        Text = "Modbus Control";
//        Width = 800;
//        Height = 600;

//        var portLabel = new Label
//        {
//            Text = "串口:",
//            Location = new System.Drawing.Point(10, 10),
//            AutoSize = true
//        };
//        Controls.Add(portLabel);

//        var portSelector = new ComboBox
//        {
//            Location = new System.Drawing.Point(80, 10),
//            Width = 100
//        };
//        portSelector.Items.AddRange(SerialPort.GetPortNames());
//        Controls.Add(portSelector);

//        var connectButton = new Button
//        {
//            Text = "連接",
//            Location = new System.Drawing.Point(190, 10),
//            Width = 80
//        };
//        connectButton.Click += (s, e) =>
//        {
//            if (portSelector.SelectedItem != null)
//            {
//                InitializeSerialPort(portSelector.SelectedItem.ToString());
//            }
//            else
//            {
//                MessageBox.Show("請選擇一個串口");
//            }
//        };
//        Controls.Add(connectButton);

//        var stationNumberLabel = new Label
//        {
//            Text = "站號:",
//            Location = new System.Drawing.Point(10, 50),
//            AutoSize = true
//        };
//        Controls.Add(stationNumberLabel);

//        var stationNumberTextBox = new TextBox
//        {
//            Location = new System.Drawing.Point(60, 50),
//            Width = 50
//        };
//        stationNumberTextBox.TextChanged += (s, e) =>
//        {
//            if (byte.TryParse(stationNumberTextBox.Text, out byte result))
//            {
//                stationNumber = result;
//            }
//            else
//            {
//                MessageBox.Show("請輸入有效的站號 (0-255)");
//            }
//        };
//        Controls.Add(stationNumberTextBox);

//        // 新增手動讀取並儲存按鈕
//        var manualReadButton = new Button
//        {
//            Text = "手動讀取並儲存",
//            Location = new System.Drawing.Point(300, 10),
//            Width = 120
//        };
//        manualReadButton.Click += (s, e) => ReadAndSaveJson();
//        Controls.Add(manualReadButton);
//    }

//    private void InitializeRefreshTimer()
//    {
//        refreshTimer = new Timer
//        {
//            Interval = 3000 // 3秒
//        };
//        refreshTimer.Tick += (s, e) => ReadAndSaveJson();
//        refreshTimer.Start();
//    }

//    private ushort CalculateCRC(byte[] data)
//    {
//        ushort crc = 0xFFFF;
//        for (int pos = 0; pos < data.Length; pos++)
//        {
//            crc ^= (ushort)data[pos];
//            for (int i = 8; i != 0; i--)
//            {
//                if ((crc & 0x0001) != 0)
//                {
//                    crc >>= 1;
//                    crc ^= 0xA001;
//                }
//                else
//                {
//                    crc >>= 1;
//                }
//            }
//        }
//        return crc;
//    }

//    private void ReadAllParameters()
//    {
//        if (serialPort != null && serialPort.IsOpen)
//        {
//            byte[] readCommand = { stationNumber, 0x03, 0x00, 0x00, 0x00, 0x30 };
//            ushort crc = CalculateCRC(readCommand);
//            byte[] crcBytes = BitConverter.GetBytes(crc);
//            byte[] fullCommand = new byte[readCommand.Length + 2];
//            Array.Copy(readCommand, fullCommand, readCommand.Length);
//            fullCommand[fullCommand.Length - 2] = crcBytes[0];
//            fullCommand[fullCommand.Length - 1] = crcBytes[1];

//            serialPort.Write(fullCommand, 0, fullCommand.Length);

//            Task.Run(() =>
//            {
//                try
//                {
//                    byte[] buffer = new byte[256];
//                    int bytesRead = serialPort.Read(buffer, 0, buffer.Length);

//                    if (bytesRead > 5)
//                    {
//                        // 按照讀取順序依次填入各個變數
//                        currentStatus1 = (buffer[3] << 8) | buffer[4];
//                        currentStatus2 = (buffer[5] << 8) | buffer[6];
//                        leakageCurrent = (buffer[7] << 8) | buffer[8];
//                        tempA = (buffer[9] << 8) | buffer[10];
//                        tempB = (buffer[11] << 8) | buffer[12];
//                        tempC = (buffer[13] << 8) | buffer[14];
//                        tempN = (buffer[15] << 8) | buffer[16];
//                        voltageA = (buffer[17] << 8) | buffer[18];
//                        voltageB = (buffer[19] << 8) | buffer[20];
//                        voltageC = (buffer[21] << 8) | buffer[22];
//                        currentA = (buffer[23] << 8) | buffer[24];
//                        currentB = (buffer[25] << 8) | buffer[26];
//                        currentC = (buffer[27] << 8) | buffer[28];
//                        powerFactorA = (buffer[29] << 8) | buffer[30];
//                        powerFactorB = (buffer[31] << 8) | buffer[32];
//                        powerFactorC = (buffer[33] << 8) | buffer[34];
//                        activePowerA = (buffer[35] << 8) | buffer[36];
//                        activePowerB = (buffer[37] << 8) | buffer[38];
//                        activePowerC = (buffer[39] << 8) | buffer[40];
//                        reactivePowerA = (buffer[41] << 8) | buffer[42];
//                        reactivePowerB = (buffer[43] << 8) | buffer[44];
//                        reactivePowerC = (buffer[45] << 8) | buffer[46];
//                        breakerTimes = (buffer[47] << 8) | buffer[48];
//                        energyHighByte = (buffer[49] << 8) | buffer[50];
//                        energyLowByte = (buffer[51] << 8) | buffer[52];
//                        switchStatus = (buffer[53] << 8) | buffer[54];
//                        apparentPowerA = (buffer[55] << 8) | buffer[56];
//                        apparentPowerB = (buffer[57] << 8) | buffer[58];
//                        apparentPowerC = (buffer[59] << 8) | buffer[60];
//                        totalApparentPower = (buffer[61] << 8) | buffer[62];
//                        totalActivePower = (buffer[63] << 8) | buffer[64];
//                        totalReactivePower = (buffer[65] << 8) | buffer[66];
//                        combinedPowerFactor = (buffer[67] << 8) | buffer[68];
//                        lineFrequency = (buffer[69] << 8) | buffer[70];
//                        deviceType = (buffer[71] << 8) | buffer[72];
//                        historicalLeakage = (buffer[73] << 8) | buffer[74];
//                        historicalCurrentA = (buffer[75] << 8) | buffer[76];
//                        historicalCurrentB = (buffer[77] << 8) | buffer[78];
//                        historicalCurrentC = (buffer[79] << 8) | buffer[80];
//                        lineColorMark = (buffer[81] << 8) | buffer[82];
//                    }
//                }
//                catch (Exception ex)
//                {
//                    MessageBox.Show($"讀取參數時發生錯誤: {ex.Message}");
//                }
//            });
//        }
//    }

//    private void SaveToJsonFile()
//    {
//        var json = new JObject(
//            new JProperty("Result", new JObject(
//                new JProperty("Result", "Success"),
//                new JProperty("ResultCode", 0),
//                new JProperty("Message", "OK")
//            )),
//            new JProperty("StartTime", DateTime.Now.ToString("o")),
//            new JProperty("EndTime", DateTime.Now.AddMinutes(5).ToString("o")),
//            new JProperty("當前狀態1", currentStatus1),
//            new JProperty("當前狀態2", currentStatus2),
//            new JProperty("漏電電流", leakageCurrent),
//            new JProperty("A相溫度", tempA),
//            new JProperty("B相溫度", tempB),
//            new JProperty("C相溫度", tempC),
//            new JProperty("N相溫度", tempN),
//            new JProperty("A相電壓", voltageA),
//            new JProperty("B相電壓", voltageB),
//            new JProperty("C相電壓", voltageC),
//            new JProperty("A相電流", currentA),
//            new JProperty("B相電流", currentB),
//            new JProperty("C相電流", currentC),
//            new JProperty("A相功率因數", powerFactorA),
//            new JProperty("B相功率因數", powerFactorB),
//            new JProperty("C相功率因數", powerFactorC),
//            new JProperty("A相有功功率", activePowerA),
//            new JProperty("B相有功功率", activePowerB),
//            new JProperty("C相有功功率", activePowerC),
//            new JProperty("A相無功功率", reactivePowerA),
//            new JProperty("B相無功功率", reactivePowerB),
//            new JProperty("C相無功功率", reactivePowerC),
//            new JProperty("合閉次數", breakerTimes),
//            new JProperty("電能高字節", energyHighByte),
//            new JProperty("電能低字節", energyLowByte),
//            new JProperty("開關狀態", switchStatus),
//            new JProperty("A相視在功率", apparentPowerA),
//            new JProperty("B相視在功率", apparentPowerB),
//            new JProperty("C相視在功率", apparentPowerC),
//            new JProperty("總視在功率", totalApparentPower),
//            new JProperty("總有功功率", totalActivePower),
//            new JProperty("總無功功率", totalReactivePower),
//            new JProperty("合相功率因數", combinedPowerFactor),
//            new JProperty("線頻率", lineFrequency),
//            new JProperty("設備類型", deviceType),
//            new JProperty("歷史漏電值", historicalLeakage),
//            new JProperty("歷史A相電流", historicalCurrentA),
//            new JProperty("歷史B相電流", historicalCurrentB),
//            new JProperty("歷史C相電流", historicalCurrentC),
//            new JProperty("線路顏色標誌", lineColorMark)
//        );

//        File.WriteAllText("_data.json", json.ToString());
//    }

//    private void ReadAndSaveJson()
//    {
//        ReadAllParameters();
//        SaveToJsonFile();
//    }

//    [STAThread]
//    public static void Main()
//    {
//        Application.Run(new ModbusViewer());
//    }
//}
