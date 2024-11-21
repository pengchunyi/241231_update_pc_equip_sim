//using System;
//using System.Collections.Generic;
//using System.IO.Ports;
//using System.Linq;
//using System.Threading.Tasks;

//namespace AmqpModbusIntegration
//{
//    public static class ModbusHelper
//    {

//        public static void SwitchON(SerialPort serialPort, byte stationNumber)
//        {
//            if (serialPort != null && serialPort.IsOpen)
//            {
//                byte[] openCommand = { stationNumber, 0x06, 0x00, 0x31, 0x55, 0x88 };
//                byte[] fullCommand = AppendCRC(openCommand);
//                serialPort.Write(fullCommand, 0, fullCommand.Length);
//                Console.WriteLine($"站號 {stationNumber} 的開關已打開");
//            }
//        }

//        public static void SwitchOFF(SerialPort serialPort, byte stationNumber)
//        {
//            if (serialPort != null && serialPort.IsOpen)
//            {
//                byte[] closeCommand = { stationNumber, 0x06, 0x00, 0x31, 0x55, 0x66 };
//                byte[] fullCommand = AppendCRC(closeCommand);
//                serialPort.Write(fullCommand, 0, fullCommand.Length);
//                Console.WriteLine($"站號 {stationNumber} 的開關已關閉");
//            }
//        }



//        // 讀取所有參數
//        //public static async Task ReadAllParametersAsync(
//        //    SerialPort serialPort,
//        //    Dictionary<byte, Dictionary<string, int>> slaveData)
//        //{
//        //    if (serialPort != null && serialPort.IsOpen)
//        //    {
//        //        foreach (var station in slaveData.Keys.ToList())
//        //        {
//        //            try
//        //            {
//        //                byte[] readCommand = { station, 0x03, 0x00, 0x00, 0x00, 0x30 };
//        //                byte[] fullCommand = AppendCRC(readCommand);
//        //                serialPort.Write(fullCommand, 0, fullCommand.Length);

//        //                await Task.Delay(1000); // 延遲 1 秒等待設備回應

//        //                byte[] buffer = new byte[256];
//        //                int bytesRead = await serialPort.BaseStream.ReadAsync(buffer, 0, buffer.Length);

//        //                if (bytesRead > 5)
//        //                {
//        //                    var data = slaveData[station];
//        //                    ParseResponse(buffer, data);
//        //                }
//        //            }
//        //            catch (Exception ex)
//        //            {
//        //                Console.WriteLine($"讀取站號 {station} 時發生錯誤: {ex.Message}");
//        //            }
//        //        }
//        //    }
//        //}
//        // 讀取所有參數
//        //public static async Task ReadAllParametersAsync(
//        //    SerialPort serialPort,
//        //    ModbusViewer modbusViewer)
//        //{
//        //    if (serialPort != null && serialPort.IsOpen)
//        //    {
//        //        foreach (var station in modbusViewer.GetSlaveData().Keys.ToList())
//        //        {
//        //            try
//        //            {
//        //                byte[] readCommand = { station, 0x03, 0x00, 0x00, 0x00, 0x30 };
//        //                byte[] fullCommand = AppendCRC(readCommand);
//        //                serialPort.Write(fullCommand, 0, fullCommand.Length);

//        //                await Task.Delay(1000); // 延遲 1 秒等待設備回應

//        //                byte[] buffer = new byte[256];
//        //                int bytesRead = await serialPort.BaseStream.ReadAsync(buffer, 0, buffer.Length);

//        //                if (bytesRead > 5)
//        //                {
//        //                    // 解析數據並存儲到 ModbusViewer 的變數
//        //                    ParseResponse(buffer, modbusViewer);
//        //                }
//        //            }
//        //            catch (Exception ex)
//        //            {
//        //                Console.WriteLine($"讀取站號 {station} 時發生錯誤: {ex.Message}");
//        //            }
//        //        }
//        //    }
//        //}
//        //    public static async Task ReadAllParametersAsync(
//        //SerialPort serialPort,
//        //ModbusViewer modbusViewer)
//        //    {
//        //        if (serialPort != null && serialPort.IsOpen)
//        //        {
//        //            foreach (var station in modbusViewer.GetSlaveData().Keys.ToList())
//        //            {
//        //                try
//        //                {
//        //                    byte[] readCommand = { station, 0x03, 0x00, 0x00, 0x00, 0x30 };
//        //                    byte[] fullCommand = AppendCRC(readCommand);
//        //                    serialPort.Write(fullCommand, 0, fullCommand.Length);

//        //                    //await Task.Delay(200); // 延遲 1 秒等待設備回應
//        //                    //                        // 適當延遲，讓從站釋放總線
//        //                    System.Threading.Thread.Sleep(200); // 延遲1000毫秒

//        //                    byte[] buffer = new byte[256];
//        //                    int bytesRead = await serialPort.BaseStream.ReadAsync(buffer, 0, buffer.Length);

//        //                    if (bytesRead > 5)
//        //                    {
//        //                        ParseResponse(buffer, station, modbusViewer); // 傳遞站號和 ModbusViewer

//        //                        // 適當延遲，讓從站釋放總線
//        //                        //await Task.Delay(200); // 延遲 1 秒等待設備回應
//        //                        System.Threading.Thread.Sleep(200); // 延遲1000毫秒
//        //                    }
//        //                }
//        //                catch (Exception ex)
//        //                {
//        //                    Console.WriteLine($"讀取站號 {station} 時發生錯誤: {ex.Message}");
//        //                }
//        //            }
//        //        }
//        //    }
//        public static async Task ReadAllParametersAsync(SerialPort serialPort, ModbusViewer modbusViewer)
//        {
//            if (serialPort != null && serialPort.IsOpen)
//            {
//                foreach (var station in modbusViewer.GetSlaveData().Keys.ToList())
//                {
//                    try
//                    {
//                        // 清空緩衝區
//                        serialPort.DiscardInBuffer();

//                        // 發送讀取命令
//                        byte[] readCommand = { station, 0x03, 0x00, 0x00, 0x00, 0x30 };
//                        byte[] fullCommand = AppendCRC(readCommand);
//                        serialPort.Write(fullCommand, 0, fullCommand.Length);

//                        // 延遲等待回應
//                        await Task.Delay(300);

//                        // 讀取回應數據
//                        byte[] buffer = new byte[256];
//                        int bytesRead = await serialPort.BaseStream.ReadAsync(buffer, 0, buffer.Length);

//                        // 校驗數據長度和 CRC
//                        if (bytesRead > 5 && ValidateCRC(buffer, bytesRead))
//                        {
//                            // 解析數據
//                            ParseResponse(buffer, station, modbusViewer);
//                        }
//                        else
//                        {
//                            Console.WriteLine($"站號 {station} 收到無效數據或 CRC 校驗失敗");
//                        }

//                        // 適當延遲，避免總線衝突
//                        await Task.Delay(300);
//                    }
//                    catch (Exception ex)
//                    {
//                        Console.WriteLine($"讀取站號 {station} 時發生錯誤: {ex.Message}");
//                    }
//                }
//            }
//        }

//        // 校驗 CRC 方法
//        private static bool ValidateCRC(byte[] data, int length)
//        {
//            if (length < 2) return false;
//            ushort receivedCRC = (ushort)(data[length - 2] | (data[length - 1] << 8));
//            ushort calculatedCRC = CrcHelper.CalculateCRC(data.Take(length - 2).ToArray());
//            return receivedCRC == calculatedCRC;
//        }



//		// 解析 Modbus 回應數據
//		//private static void ParseResponse(byte[] buffer, Dictionary<string, int> data)
//		//{
//		//	data["當前狀態"] = (buffer[3] << 8) | buffer[4];
//		//	data["A相溫度"] = (buffer[9] << 8) | buffer[10];
//		//	data["B相溫度"] = (buffer[11] << 8) | buffer[12];
//		//	data["C相溫度"] = (buffer[13] << 8) | buffer[14];
//		//	data["A相電壓"] = (buffer[17] << 8) | buffer[18];
//		//	data["A相電流"] = (buffer[23] << 8) | buffer[24];
//		//	data["開關狀態"] = (buffer[53] << 8) | buffer[54];
//		//	data["總有功功率"] = (buffer[63] << 8) | buffer[64];
//		//	data["線頻率"] = (buffer[69] << 8) | buffer[70];
//		//}

//		// 解析 Modbus 回應數據並更新到 ModbusViewer 的變數
//		//private static void ParseResponse(byte[] buffer, ModbusViewer modbusViewer)
//		//{
//		//	modbusViewer.currentStatus1 = (buffer[3] << 8) | buffer[4];
//		//	modbusViewer.currentStatus2 = (buffer[5] << 8) | buffer[6];
//		//	modbusViewer.currentStatus = (modbusViewer.currentStatus1 << 16) | modbusViewer.currentStatus2;

//		//	modbusViewer.leakageCurrent = (buffer[7] << 8) | buffer[8];
//		//	modbusViewer.tempA = (buffer[9] << 8) | buffer[10];
//		//	modbusViewer.tempB = (buffer[11] << 8) | buffer[12];
//		//	modbusViewer.tempC = (buffer[13] << 8) | buffer[14];
//		//	modbusViewer.tempN = (buffer[15] << 8) | buffer[16];
//		//	modbusViewer.voltageA = (buffer[17] << 8) | buffer[18];
//		//	modbusViewer.voltageB = (buffer[19] << 8) | buffer[20];
//		//	modbusViewer.voltageC = (buffer[21] << 8) | buffer[22];
//		//	modbusViewer.currentA = (buffer[23] << 8) | buffer[24];
//		//	modbusViewer.currentB = (buffer[25] << 8) | buffer[26];
//		//	modbusViewer.currentC = (buffer[27] << 8) | buffer[28];
//		//	modbusViewer.powerFactorA = (buffer[29] << 8) | buffer[30];
//		//	modbusViewer.powerFactorB = (buffer[31] << 8) | buffer[32];
//		//	modbusViewer.powerFactorC = (buffer[33] << 8) | buffer[34];
//		//	modbusViewer.activePowerA = (buffer[35] << 8) | buffer[36];
//		//	modbusViewer.activePowerB = (buffer[37] << 8) | buffer[38];
//		//	modbusViewer.activePowerC = (buffer[39] << 8) | buffer[40];
//		//	modbusViewer.reactivePowerA = (buffer[41] << 8) | buffer[42];
//		//	modbusViewer.reactivePowerB = (buffer[43] << 8) | buffer[44];
//		//	modbusViewer.reactivePowerC = (buffer[45] << 8) | buffer[46];
//		//	modbusViewer.breakerTimes = (buffer[47] << 8) | buffer[48];

//		//	int energyHighByte = (buffer[49] << 8) | buffer[50];
//		//	int energyLowByte = (buffer[51] << 8) | buffer[52];
//		//	modbusViewer.energy = ((energyHighByte << 16) | energyLowByte) / 100.0;

//		//	modbusViewer.switchStatus = (buffer[53] << 8) | buffer[54];
//		//	modbusViewer.apparentPowerA = (buffer[55] << 8) | buffer[56];
//		//	modbusViewer.apparentPowerB = (buffer[57] << 8) | buffer[58];
//		//	modbusViewer.apparentPowerC = (buffer[59] << 8) | buffer[60];
//		//	modbusViewer.totalApparentPower = (buffer[61] << 8) | buffer[62];
//		//	modbusViewer.totalActivePower = (buffer[63] << 8) | buffer[64];
//		//	modbusViewer.totalReactivePower = (buffer[65] << 8) | buffer[66];
//		//	modbusViewer.combinedPowerFactor = (buffer[67] << 8) | buffer[68];
//		//	modbusViewer.lineFrequency = (buffer[69] << 8) | buffer[70];
//		//	modbusViewer.deviceType = (buffer[71] << 8) | buffer[72];
//		//	modbusViewer.historicalLeakage = (buffer[73] << 8) | buffer[74];
//		//	modbusViewer.historicalCurrentA = (buffer[75] << 8) | buffer[76];
//		//	modbusViewer.historicalCurrentB = (buffer[77] << 8) | buffer[78];
//		//	modbusViewer.historicalCurrentC = (buffer[79] << 8) | buffer[80];
//		//}
//		//private static void ParseResponse(byte[] buffer, byte station, ModbusViewer modbusViewer)
//		//{
//		//	// 解析數據並存入 ModbusViewer 變數
//		//	//modbusViewer.currentStatus1 = (buffer[3] << 8) | buffer[4];
//		//	//modbusViewer.currentStatus2 = (buffer[5] << 8) | buffer[6];
//		//	//modbusViewer.currentStatus = (modbusViewer.currentStatus1 << 16) | modbusViewer.currentStatus2;

//		//	//modbusViewer.leakageCurrent = (buffer[7] << 8) | buffer[8];
//		//	//modbusViewer.tempA = (buffer[9] << 8) | buffer[10];
//		//	//modbusViewer.tempB = (buffer[11] << 8) | buffer[12];
//		//	//modbusViewer.tempC = (buffer[13] << 8) | buffer[14];
//		//	//modbusViewer.tempN = (buffer[15] << 8) | buffer[16];
//		//	//modbusViewer.voltageA = (buffer[17] << 8) | buffer[18];
//		//	//modbusViewer.voltageB = (buffer[19] << 8) | buffer[20];
//		//	//modbusViewer.voltageC = (buffer[21] << 8) | buffer[22];
//		//	//modbusViewer.currentA = (buffer[23] << 8) | buffer[24];
//		//	//modbusViewer.currentB = (buffer[25] << 8) | buffer[26];
//		//	//modbusViewer.currentC = (buffer[27] << 8) | buffer[28];


//		//	modbusViewer.currentStatus1 = (buffer[3] << 8) | buffer[4];
//		//	modbusViewer.currentStatus2 = (buffer[5] << 8) | buffer[6];
//		//	modbusViewer.currentStatus = (modbusViewer.currentStatus1 << 16) | modbusViewer.currentStatus2;

//		//	modbusViewer.leakageCurrent = (buffer[7] << 8) | buffer[8];
//		//	modbusViewer.tempA = (buffer[9] << 8) | buffer[10];
//		//	modbusViewer.tempB = (buffer[11] << 8) | buffer[12];
//		//	modbusViewer.tempC = (buffer[13] << 8) | buffer[14];
//		//	modbusViewer.tempN = (buffer[15] << 8) | buffer[16];
//		//	modbusViewer.voltageA = (buffer[17] << 8) | buffer[18];
//		//	modbusViewer.voltageB = (buffer[19] << 8) | buffer[20];
//		//	modbusViewer.voltageC = (buffer[21] << 8) | buffer[22];
//		//	modbusViewer.currentA = (buffer[23] << 8) | buffer[24];
//		//	modbusViewer.currentB = (buffer[25] << 8) | buffer[26];
//		//	modbusViewer.currentC = (buffer[27] << 8) | buffer[28];
//		//	modbusViewer.powerFactorA = (buffer[29] << 8) | buffer[30];
//		//	modbusViewer.powerFactorB = (buffer[31] << 8) | buffer[32];
//		//	modbusViewer.powerFactorC = (buffer[33] << 8) | buffer[34];
//		//	modbusViewer.activePowerA = (buffer[35] << 8) | buffer[36];
//		//	modbusViewer.activePowerB = (buffer[37] << 8) | buffer[38];
//		//	modbusViewer.activePowerC = (buffer[39] << 8) | buffer[40];
//		//	modbusViewer.reactivePowerA = (buffer[41] << 8) | buffer[42];
//		//	modbusViewer.reactivePowerB = (buffer[43] << 8) | buffer[44];
//		//	modbusViewer.reactivePowerC = (buffer[45] << 8) | buffer[46];
//		//	modbusViewer.breakerTimes = (buffer[47] << 8) | buffer[48];

//		//	int energyHighByte = (buffer[49] << 8) | buffer[50];
//		//	int energyLowByte = (buffer[51] << 8) | buffer[52];
//		//	modbusViewer.energy = ((energyHighByte << 16) | energyLowByte) / 100.0;

//		//	modbusViewer.switchStatus = (buffer[53] << 8) | buffer[54];
//		//	modbusViewer.apparentPowerA = (buffer[55] << 8) | buffer[56];
//		//	modbusViewer.apparentPowerB = (buffer[57] << 8) | buffer[58];
//		//	modbusViewer.apparentPowerC = (buffer[59] << 8) | buffer[60];
//		//	modbusViewer.totalApparentPower = (buffer[61] << 8) | buffer[62];
//		//	modbusViewer.totalActivePower = (buffer[63] << 8) | buffer[64];
//		//	modbusViewer.totalReactivePower = (buffer[65] << 8) | buffer[66];
//		//	modbusViewer.combinedPowerFactor = (buffer[67] << 8) | buffer[68];
//		//	modbusViewer.lineFrequency = (buffer[69] << 8) | buffer[70];
//		//	modbusViewer.deviceType = (buffer[71] << 8) | buffer[72];
//		//	modbusViewer.historicalLeakage = (buffer[73] << 8) | buffer[74];
//		//	modbusViewer.historicalCurrentA = (buffer[75] << 8) | buffer[76];
//		//	modbusViewer.historicalCurrentB = (buffer[77] << 8) | buffer[78];
//		//	modbusViewer.historicalCurrentC = (buffer[79] << 8) | buffer[80];

//		//	// 確保 slaveData 包含對應的站號
//		//	if (!modbusViewer.GetSlaveData().ContainsKey(station))
//		//	{
//		//		modbusViewer.GetSlaveData()[station] = new Dictionary<string, int>();
//		//	}

//		//	var slaveData = modbusViewer.GetSlaveData()[station];

//		//	// 更新 slaveData 中的數據結構
//		//	//slaveData["當前狀態1"] = modbusViewer.currentStatus1;
//		//	//slaveData["當前狀態2"] = modbusViewer.currentStatus2;
//		//	slaveData["當前狀態"] = modbusViewer.currentStatus;
//		//	slaveData["漏電電流"] = modbusViewer.leakageCurrent;

//		//	slaveData["A相溫度"] = modbusViewer.tempA;
//		//	slaveData["B相溫度"] = modbusViewer.tempB;
//		//	slaveData["C相溫度"] = modbusViewer.tempC;
//		//	slaveData["N線溫度"] = modbusViewer.tempN;

//		//	slaveData["A相電壓"] = modbusViewer.voltageA;
//		//	slaveData["B相電壓"] = modbusViewer.voltageB;
//		//	slaveData["C相電壓"] = modbusViewer.voltageC;

//		//	slaveData["A相電流"] = modbusViewer.currentA;
//		//	slaveData["B相電流"] = modbusViewer.currentB;
//		//	slaveData["C相電流"] = modbusViewer.currentC;
//		//}

//		private static void ParseResponse(byte[] buffer, byte station, ModbusViewer modbusViewer)
//		{
//			// 解析數據並存入 ModbusViewer 變數
//			modbusViewer.currentStatus1 = (buffer[3] << 8) | buffer[4];
//			modbusViewer.currentStatus2 = (buffer[5] << 8) | buffer[6];
//			modbusViewer.currentStatus = (modbusViewer.currentStatus1 << 16) | modbusViewer.currentStatus2;

//			modbusViewer.leakageCurrent = ((buffer[7] << 8) | buffer[8]); // 單位: mA
//			modbusViewer.tempA = (buffer[9] << 8) | buffer[10]; // 單位: ℃
//			modbusViewer.tempB = (buffer[11] << 8) | buffer[12]; // 單位: ℃
//			modbusViewer.tempC = (buffer[13] << 8) | buffer[14]; // 單位: ℃
//			modbusViewer.tempN = (buffer[15] << 8) | buffer[16]; // 單位: ℃
//			modbusViewer.voltageA = ((buffer[17] << 8) | buffer[18]) / 10; // 單位: V
//			modbusViewer.voltageB = ((buffer[19] << 8) | buffer[20]) / 10; // 單位: V
//			modbusViewer.voltageC = ((buffer[21] << 8) | buffer[22]) / 10; // 單位: V
//			modbusViewer.currentA = ((buffer[23] << 8) | buffer[24]) / 100; // 單位: A
//			modbusViewer.currentB = ((buffer[25] << 8) | buffer[26]) / 100; // 單位: A
//			modbusViewer.currentC = ((buffer[27] << 8) | buffer[28]) / 100; // 單位: A
//			modbusViewer.powerFactorA = ((buffer[29] << 8) | buffer[30]) / 100; // 單位: 無因次
//			modbusViewer.powerFactorB = ((buffer[31] << 8) | buffer[32]) / 100; // 單位: 無因次
//			modbusViewer.powerFactorC = ((buffer[33] << 8) | buffer[34]) / 100; // 單位: 無因次
//			modbusViewer.activePowerA = (buffer[35] << 8) | buffer[36]; // 單位: W
//			modbusViewer.activePowerB = (buffer[37] << 8) | buffer[38]; // 單位: W
//			modbusViewer.activePowerC = (buffer[39] << 8) | buffer[40]; // 單位: W
//			modbusViewer.reactivePowerA = (buffer[41] << 8) | buffer[42]; // 單位: 無功功率 W
//			modbusViewer.reactivePowerB = (buffer[43] << 8) | buffer[44]; // 單位: 無功功率 W
//			modbusViewer.reactivePowerC = (buffer[45] << 8) | buffer[46]; // 單位: 無功功率 W
//			modbusViewer.breakerTimes = (buffer[47] << 8) | buffer[48]; // 單位: 次數

//			int energyHighByte = (buffer[49] << 8) | buffer[50];
//			int energyLowByte = (buffer[51] << 8) | buffer[52];
//			modbusViewer.energy = ((energyHighByte << 16) | energyLowByte) / 100; // 單位: kWh

//			modbusViewer.switchStatus = (buffer[53] << 8) | buffer[54]; // 單位: 開關狀態
//			modbusViewer.apparentPowerA = (buffer[55] << 8) | buffer[56]; // 單位: W
//			modbusViewer.apparentPowerB = (buffer[57] << 8) | buffer[58]; // 單位: W
//			modbusViewer.apparentPowerC = (buffer[59] << 8) | buffer[60]; // 單位: W
//			modbusViewer.totalApparentPower = (buffer[61] << 8) | buffer[62]; // 單位: W
//			modbusViewer.totalActivePower = (buffer[63] << 8) | buffer[64]; // 單位: W
//			modbusViewer.totalReactivePower = (buffer[65] << 8) | buffer[66]; // 單位: W
//			modbusViewer.combinedPowerFactor = ((buffer[67] << 8) | buffer[68]) / 100; // 單位: 無因次
//			modbusViewer.lineFrequency = ((buffer[69] << 8) | buffer[70]) / 10; // 單位: Hz
//			modbusViewer.deviceType = (buffer[71] << 8) | buffer[72]; // 單位: 類型代碼
//			modbusViewer.historicalLeakage = ((buffer[73] << 8) | buffer[74]) / 1; // 單位: mA
//			modbusViewer.historicalCurrentA = ((buffer[75] << 8) | buffer[76]) / 100; // 單位: A
//			modbusViewer.historicalCurrentB = ((buffer[77] << 8) | buffer[78]) / 100; // 單位: A
//			modbusViewer.historicalCurrentC = ((buffer[79] << 8) | buffer[80]) / 100; // 單位: A

//			// 確保 slaveData 包含對應的站號
//			if (!modbusViewer.GetSlaveData().ContainsKey(station))
//			{
//				modbusViewer.GetSlaveData()[station] = new Dictionary<string, int>();
//			}

//			var slaveData = modbusViewer.GetSlaveData()[station];

//			// 更新 slaveData 中的數據結構
//			// 更新 slaveData 中的數據結構，並添加對應單位的註解
//			slaveData["當前狀態"] = modbusViewer.currentStatus; // 無單位
//			slaveData["漏電電流 (mA)"] = modbusViewer.leakageCurrent; // 單位: mA
//			slaveData["A相溫度 (℃)"] = modbusViewer.tempA; // 單位: ℃
//			slaveData["B相溫度 (℃)"] = modbusViewer.tempB; // 單位: ℃
//			slaveData["C相溫度 (℃)"] = modbusViewer.tempC; // 單位: ℃
//			slaveData["N線溫度 (℃)"] = modbusViewer.tempN; // 單位: ℃
//			slaveData["A相電壓 (V)"] = modbusViewer.voltageA; // 單位: V
//			slaveData["B相電壓 (V)"] = modbusViewer.voltageB; // 單位: V
//			slaveData["C相電壓 (V)"] = modbusViewer.voltageC; // 單位: V
//			slaveData["A相電流 (A)"] = modbusViewer.currentA; // 單位: A
//			slaveData["B相電流 (A)"] = modbusViewer.currentB; // 單位: A
//			slaveData["C相電流 (A)"] = modbusViewer.currentC; // 單位: A
//			slaveData["A相功率因數"] = modbusViewer.powerFactorA; // 無單位 (值範圍: 0-1)
//			slaveData["B相功率因數"] = modbusViewer.powerFactorB; // 無單位 (值範圍: 0-1)
//			slaveData["C相功率因數"] = modbusViewer.powerFactorC; // 無單位 (值範圍: 0-1)
//			slaveData["A相有功功率 (W)"] = modbusViewer.activePowerA; // 單位: W
//			slaveData["B相有功功率 (W)"] = modbusViewer.activePowerB; // 單位: W
//			slaveData["C相有功功率 (W)"] = modbusViewer.activePowerC; // 單位: W
//			slaveData["電能 (kWh)"] = modbusViewer.energy; // 單位: kWh
//			slaveData["當前總有功功率 (W)"] = modbusViewer.totalActivePower; // 單位: W
//			slaveData["當前線頻率 (Hz)"] = modbusViewer.lineFrequency; // 單位: Hz

//		}


//		//private static void ParseResponse(byte[] buffer, byte station, ModbusViewer modbusViewer)
//		//{
//		//	// 確保 slaveData 包含對應的站號
//		//	if (!modbusViewer.GetSlaveData().ContainsKey(station))
//		//	{
//		//		modbusViewer.GetSlaveData()[station] = new Dictionary<string, int>();
//		//	}

//		//	var slaveData = modbusViewer.GetSlaveData()[station];

//		//	// 解析數據並存入到 ModbusViewer 和 slaveData
//		//	// 狀態表1
//		//	modbusViewer.currentStatus = (buffer[0] << 8) | buffer[1];
//		//	slaveData["當前狀態"] = modbusViewer.currentStatus;

//		//	// 漏電電流 (單位: mA)
//		//	modbusViewer.leakageCurrent = (buffer[2] << 8) | buffer[3];
//		//	slaveData["當前漏電值"] = modbusViewer.leakageCurrent;

//		//	// 溫度 (單位: ℃)
//		//	modbusViewer.tempA = (buffer[4] << 8) | buffer[5];
//		//	slaveData["A相溫度"] = modbusViewer.tempA;

//		//	modbusViewer.tempB = (buffer[6] << 8) | buffer[7];
//		//	slaveData["B相溫度"] = modbusViewer.tempB;

//		//	modbusViewer.tempC = (buffer[8] << 8) | buffer[9];
//		//	slaveData["C相溫度"] = modbusViewer.tempC;

//		//	modbusViewer.tempN = (buffer[10] << 8) | buffer[11];
//		//	slaveData["N線溫度"] = modbusViewer.tempN;

//		//	// 電壓 (單位: V，需要除以 10)
//		//	modbusViewer.voltageA = ((buffer[12] << 8) | buffer[13]) / 10.0;
//		//	slaveData["A相電壓"] = modbusViewer.voltageA;

//		//	modbusViewer.voltageB = ((buffer[14] << 8) | buffer[15]) / 10.0;
//		//	slaveData["B相電壓"] = modbusViewer.voltageB;

//		//	modbusViewer.voltageC = ((buffer[16] << 8) | buffer[17]) / 10.0;
//		//	slaveData["C相電壓"] = modbusViewer.voltageC;

//		//	// 電流 (單位: A，需要除以 100)
//		//	modbusViewer.currentA = ((buffer[18] << 8) | buffer[19]) / 100.0;
//		//	slaveData["A相電流"] = modbusViewer.currentA;

//		//	modbusViewer.currentB = ((buffer[20] << 8) | buffer[21]) / 100.0;
//		//	slaveData["B相電流"] = modbusViewer.currentB;

//		//	modbusViewer.currentC = ((buffer[22] << 8) | buffer[23]) / 100.0;
//		//	slaveData["C相電流"] = modbusViewer.currentC;

//		//	// 功率因數 (範圍: 0-100，需要除以 100)
//		//	modbusViewer.powerFactorA = ((buffer[24] << 8) | buffer[25]) / 100.0;
//		//	slaveData["A相功率因數"] = modbusViewer.powerFactorA;

//		//	modbusViewer.powerFactorB = ((buffer[26] << 8) | buffer[27]) / 100.0;
//		//	slaveData["B相功率因數"] = modbusViewer.powerFactorB;

//		//	modbusViewer.powerFactorC = ((buffer[28] << 8) | buffer[29]) / 100.0;
//		//	slaveData["C相功率因數"] = modbusViewer.powerFactorC;

//		//	// 有功功率 (單位: W)
//		//	modbusViewer.activePowerA = (buffer[30] << 8) | buffer[31];
//		//	slaveData["A相有功功率"] = modbusViewer.activePowerA;

//		//	modbusViewer.activePowerB = (buffer[32] << 8) | buffer[33];
//		//	slaveData["B相有功功率"] = modbusViewer.activePowerB;

//		//	modbusViewer.activePowerC = (buffer[34] << 8) | buffer[35];
//		//	slaveData["C相有功功率"] = modbusViewer.activePowerC;

//		//	// 總有功功率 (單位: W)
//		//	modbusViewer.totalActivePower = (buffer[62] << 8) | buffer[63];
//		//	slaveData["當前總有功功率"] = modbusViewer.totalActivePower;

//		//	// 線頻率 (單位: Hz，需要除以 10)
//		//	modbusViewer.lineFrequency = ((buffer[68] << 8) | buffer[69]) / 10.0;
//		//	slaveData["當前線頻率"] = modbusViewer.lineFrequency;

//		//	// 電能 (單位: kWh，需要組合高低位並除以 100)
//		//	int energyHigh = (buffer[70] << 8) | buffer[71];
//		//	int energyLow = (buffer[72] << 8) | buffer[73];
//		//	modbusViewer.energy = ((energyHigh << 16) | energyLow) / 100.0;
//		//	slaveData["電能"] = modbusViewer.energy;

//		//	// 開關狀態 (0: 分閘, 1: 合閘)
//		//	modbusViewer.switchStatus = (buffer[74] << 8) | buffer[75];
//		//	slaveData["開關狀態"] = modbusViewer.switchStatus;

//		//	// 額外數據解析可以根據需求繼續擴展...
//		//}
//		//private static void ParseResponse(byte[] buffer, byte station, ModbusViewer modbusViewer)
//		//{
//		//	// 確保 slaveData 包含對應的站號
//		//	if (!modbusViewer.GetSlaveData().ContainsKey(station))
//		//	{
//		//		modbusViewer.GetSlaveData()[station] = new Dictionary<string, int>();
//		//	}

//		//	var slaveData = modbusViewer.GetSlaveData()[station];

//		//	// 狀態表1
//		//	modbusViewer.currentStatus = (buffer[0] << 8) | buffer[1];
//		//	slaveData["當前狀態"] = modbusViewer.currentStatus;

//		//	// 漏電電流 (單位: mA)
//		//	modbusViewer.leakageCurrent = (buffer[2] << 8) | buffer[3];
//		//	slaveData["當前漏電值"] = modbusViewer.leakageCurrent;

//		//	// 溫度 (單位: ℃)
//		//	modbusViewer.tempA = (buffer[4] << 8) | buffer[5];
//		//	slaveData["A相溫度"] = modbusViewer.tempA;

//		//	modbusViewer.tempB = (buffer[6] << 8) | buffer[7];
//		//	slaveData["B相溫度"] = modbusViewer.tempB;

//		//	modbusViewer.tempC = (buffer[8] << 8) | buffer[9];
//		//	slaveData["C相溫度"] = modbusViewer.tempC;

//		//	modbusViewer.tempN = (buffer[10] << 8) | buffer[11];
//		//	slaveData["N線溫度"] = modbusViewer.tempN;

//		//	// 電壓 (單位: V，需要除以 10)
//		//	modbusViewer.voltageA = ((buffer[12] << 8) | buffer[13]) / 10.0;
//		//	slaveData["A相電壓"] = modbusViewer.voltageA;

//		//	modbusViewer.voltageB = ((buffer[14] << 8) | buffer[15]) / 10.0;
//		//	slaveData["B相電壓"] = modbusViewer.voltageB;

//		//	modbusViewer.voltageC = ((buffer[16] << 8) | buffer[17]) / 10.0;
//		//	slaveData["C相電壓"] = modbusViewer.voltageC;

//		//	// 電流 (單位: A，需要除以 100)
//		//	modbusViewer.currentA = ((buffer[18] << 8) | buffer[19]) / 100.0;
//		//	slaveData["A相電流"] = modbusViewer.currentA;

//		//	modbusViewer.currentB = ((buffer[20] << 8) | buffer[21]) / 100.0;
//		//	slaveData["B相電流"] = modbusViewer.currentB;

//		//	modbusViewer.currentC = ((buffer[22] << 8) | buffer[23]) / 100.0;
//		//	slaveData["C相電流"] = modbusViewer.currentC;

//		//	// 功率因數 (範圍: 0-100，需要除以 100)
//		//	modbusViewer.powerFactorA = ((buffer[24] << 8) | buffer[25]) / 100.0;
//		//	slaveData["A相功率因數"] = modbusViewer.powerFactorA;

//		//	modbusViewer.powerFactorB = ((buffer[26] << 8) | buffer[27]) / 100.0;
//		//	slaveData["B相功率因數"] = modbusViewer.powerFactorB;

//		//	modbusViewer.powerFactorC = ((buffer[28] << 8) | buffer[29]) / 100.0;
//		//	slaveData["C相功率因數"] = modbusViewer.powerFactorC;

//		//	// 有功功率 (單位: W)
//		//	modbusViewer.activePowerA = (buffer[30] << 8) | buffer[31];
//		//	slaveData["A相有功功率"] = modbusViewer.activePowerA;

//		//	modbusViewer.activePowerB = (buffer[32] << 8) | buffer[33];
//		//	slaveData["B相有功功率"] = modbusViewer.activePowerB;

//		//	modbusViewer.activePowerC = (buffer[34] << 8) | buffer[35];
//		//	slaveData["C相有功功率"] = modbusViewer.activePowerC;

//		//	// 總有功功率 (單位: W)
//		//	modbusViewer.totalActivePower = (buffer[62] << 8) | buffer[63];
//		//	slaveData["當前總有功功率"] = modbusViewer.totalActivePower;

//		//	// 線頻率 (單位: Hz，需要除以 10)
//		//	modbusViewer.lineFrequency = ((buffer[68] << 8) | buffer[69]) / 10.0;
//		//	slaveData["當前線頻率"] = modbusViewer.lineFrequency;

//		//	// 電能 (單位: kWh，需要組合高低位並除以 100)
//		//	int energyHigh = (buffer[70] << 8) | buffer[71];
//		//	int energyLow = (buffer[72] << 8) | buffer[73];
//		//	modbusViewer.energy = ((energyHigh << 16) | energyLow) / 100.0;
//		//	slaveData["電能"] = modbusViewer.energy;

//		//	// 開關狀態 (0: 分閘, 1: 合閘)
//		//	modbusViewer.switchStatus = (buffer[74] << 8) | buffer[75];
//		//	slaveData["開關狀態"] = modbusViewer.switchStatus;
//		//}





//		// 附加 CRC 校驗碼
//		private static byte[] AppendCRC(byte[] command)
//        {
//            ushort crc = CrcHelper.CalculateCRC(command);
//            byte[] crcBytes = BitConverter.GetBytes(crc);
//            byte[] fullCommand = new byte[command.Length + 2];
//            Array.Copy(command, fullCommand, command.Length);
//            fullCommand[fullCommand.Length - 2] = crcBytes[0];
//            fullCommand[fullCommand.Length - 1] = crcBytes[1];
//            return fullCommand;
//        }
//    }
//}
