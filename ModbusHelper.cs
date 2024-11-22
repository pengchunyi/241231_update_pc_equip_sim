using System;
using System.Collections.Generic;
using System.IO.Ports;
using System.Linq;
using System.Threading.Tasks;

namespace AmqpModbusIntegration
{
    public static class ModbusHelper
    {

        public static void SwitchON(SerialPort serialPort, byte stationNumber)
        {
            if (serialPort != null && serialPort.IsOpen)
            {
                byte[] openCommand = { stationNumber, 0x06, 0x00, 0x31, 0x55, 0x88 };
                byte[] fullCommand = AppendCRC(openCommand);
				serialPort.DiscardInBuffer(); // 清空輸入緩衝區
				serialPort.DiscardOutBuffer(); // 清空輸出緩衝區
				serialPort.Write(fullCommand, 0, fullCommand.Length);
                Console.WriteLine($"站號 {stationNumber} 的開關已打開");
            }
        }

        public static void SwitchOFF(SerialPort serialPort, byte stationNumber)
        {
            if (serialPort != null && serialPort.IsOpen)
            {
                byte[] closeCommand = { stationNumber, 0x06, 0x00, 0x31, 0x55, 0x66 };
                byte[] fullCommand = AppendCRC(closeCommand);
				serialPort.DiscardInBuffer(); // 清空輸入緩衝區
				serialPort.DiscardOutBuffer(); // 清空輸出緩衝區
				serialPort.Write(fullCommand, 0, fullCommand.Length);
                Console.WriteLine($"站號 {stationNumber} 的開關已關閉");
            }
        }

        public static async Task ReadAllParametersAsync(SerialPort serialPort, ModbusViewer modbusViewer)
        {
            if (serialPort != null && serialPort.IsOpen)
            {
                foreach (var station in modbusViewer.GetSlaveData().Keys.ToList())
                {
                    try
                    {
                        // 清空緩衝區
                        serialPort.DiscardInBuffer();

                        // 發送讀取命令
                        byte[] readCommand = { station, 0x03, 0x00, 0x00, 0x00, 0x30 };
                        byte[] fullCommand = AppendCRC(readCommand);
						serialPort.DiscardInBuffer(); // 清空輸入緩衝區
						serialPort.DiscardOutBuffer(); // 清空輸出緩衝區
						serialPort.Write(fullCommand, 0, fullCommand.Length);

                        // 延遲等待回應
                        await Task.Delay(200);

                        // 讀取回應數據
                        byte[] buffer = new byte[256];
                        int bytesRead = await serialPort.BaseStream.ReadAsync(buffer, 0, buffer.Length);

                        // 校驗數據長度和 CRC
                        if (bytesRead > 5 && ValidateCRC(buffer, bytesRead))
                        {
                            // 解析數據
                            ParseResponse(buffer, station, modbusViewer);
                        }
                        else
                        {
                            Console.WriteLine($"站號 {station} 收到無效數據或 CRC 校驗失敗");
                        }

                        // 適當延遲，避免總線衝突
                        await Task.Delay(200);
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"讀取站號 {station} 時發生錯誤: {ex.Message}");
                    }
                }
            }
        }

        // 校驗 CRC 方法
        private static bool ValidateCRC(byte[] data, int length)
        {
            if (length < 2) return false;
            ushort receivedCRC = (ushort)(data[length - 2] | (data[length - 1] << 8));
            ushort calculatedCRC = CalculateCRC(data.Take(length - 2).ToArray());
            return receivedCRC == calculatedCRC;
        }


		private static void ParseResponse(byte[] buffer, byte station, ModbusViewer modbusViewer)
		{
			// 解析數據並存入 ModbusViewer 變數
			modbusViewer.currentStatus1 = (buffer[3] << 8) | buffer[4];
			modbusViewer.currentStatus2 = (buffer[5] << 8) | buffer[6];
			modbusViewer.currentStatus = (modbusViewer.currentStatus1 << 16) | modbusViewer.currentStatus2;

			modbusViewer.leakageCurrent = ((buffer[7] << 8) | buffer[8]); // 單位: mA
			modbusViewer.tempA = (buffer[9] << 8) | buffer[10]; // 單位: ℃
			modbusViewer.tempB = (buffer[11] << 8) | buffer[12]; // 單位: ℃
			modbusViewer.tempC = (buffer[13] << 8) | buffer[14]; // 單位: ℃
			modbusViewer.tempN = (buffer[15] << 8) | buffer[16]; // 單位: ℃
			modbusViewer.voltageA = ((buffer[17] << 8) | buffer[18]) / 10; // 單位: V
			modbusViewer.voltageB = ((buffer[19] << 8) | buffer[20]) / 10; // 單位: V
			modbusViewer.voltageC = ((buffer[21] << 8) | buffer[22]) / 10; // 單位: V
			modbusViewer.currentA = ((buffer[23] << 8) | buffer[24]) / 100; // 單位: A
			modbusViewer.currentB = ((buffer[25] << 8) | buffer[26]) / 100; // 單位: A
			modbusViewer.currentC = ((buffer[27] << 8) | buffer[28]) / 100; // 單位: A
			modbusViewer.powerFactorA = ((buffer[29] << 8) | buffer[30]) / 100; // 單位: 無因次
			modbusViewer.powerFactorB = ((buffer[31] << 8) | buffer[32]) / 100; // 單位: 無因次
			modbusViewer.powerFactorC = ((buffer[33] << 8) | buffer[34]) / 100; // 單位: 無因次
			modbusViewer.activePowerA = (buffer[35] << 8) | buffer[36]; // 單位: W
			modbusViewer.activePowerB = (buffer[37] << 8) | buffer[38]; // 單位: W
			modbusViewer.activePowerC = (buffer[39] << 8) | buffer[40]; // 單位: W
			modbusViewer.reactivePowerA = (buffer[41] << 8) | buffer[42]; // 單位: 無功功率 W
			modbusViewer.reactivePowerB = (buffer[43] << 8) | buffer[44]; // 單位: 無功功率 W
			modbusViewer.reactivePowerC = (buffer[45] << 8) | buffer[46]; // 單位: 無功功率 W
			modbusViewer.breakerTimes = (buffer[47] << 8) | buffer[48]; // 單位: 次數

			int energyHighByte = (buffer[49] << 8) | buffer[50];
			int energyLowByte = (buffer[51] << 8) | buffer[52];
			modbusViewer.energy = ((energyHighByte << 16) | energyLowByte) / 100; // 單位: kWh

			modbusViewer.switchStatus = (buffer[53] << 8) | buffer[54]; // 單位: 開關狀態
			modbusViewer.apparentPowerA = (buffer[55] << 8) | buffer[56]; // 單位: W
			modbusViewer.apparentPowerB = (buffer[57] << 8) | buffer[58]; // 單位: W
			modbusViewer.apparentPowerC = (buffer[59] << 8) | buffer[60]; // 單位: W
			modbusViewer.totalApparentPower = (buffer[61] << 8) | buffer[62]; // 單位: W
			modbusViewer.totalActivePower = (buffer[63] << 8) | buffer[64]; // 單位: W
			modbusViewer.totalReactivePower = (buffer[65] << 8) | buffer[66]; // 單位: W
			modbusViewer.combinedPowerFactor = ((buffer[67] << 8) | buffer[68]) / 100; // 單位: 無因次
			modbusViewer.lineFrequency = ((buffer[69] << 8) | buffer[70]) / 10; // 單位: Hz
			modbusViewer.deviceType = (buffer[71] << 8) | buffer[72]; // 單位: 類型代碼
			modbusViewer.historicalLeakage = ((buffer[73] << 8) | buffer[74]) / 1; // 單位: mA
			modbusViewer.historicalCurrentA = ((buffer[75] << 8) | buffer[76]) / 100; // 單位: A
			modbusViewer.historicalCurrentB = ((buffer[77] << 8) | buffer[78]) / 100; // 單位: A
			modbusViewer.historicalCurrentC = ((buffer[79] << 8) | buffer[80]) / 100; // 單位: A

			// 確保 slaveData 包含對應的站號
			if (!modbusViewer.GetSlaveData().ContainsKey(station))
			{
				modbusViewer.GetSlaveData()[station] = new Dictionary<string, int>();
			}

			var slaveData = modbusViewer.GetSlaveData()[station];

			// 更新 slaveData 中的數據結構
			// 更新 slaveData 中的數據結構，並添加對應單位的註解
			slaveData["當前狀態"] = modbusViewer.currentStatus; // 無單位
			slaveData["漏電電流 (mA)"] = modbusViewer.leakageCurrent; // 單位: mA
			slaveData["A相溫度 (℃)"] = modbusViewer.tempA; // 單位: ℃
			slaveData["B相溫度 (℃)"] = modbusViewer.tempB; // 單位: ℃
			slaveData["C相溫度 (℃)"] = modbusViewer.tempC; // 單位: ℃
			slaveData["N線溫度 (℃)"] = modbusViewer.tempN; // 單位: ℃
			slaveData["A相電壓 (V)"] = modbusViewer.voltageA; // 單位: V
			slaveData["B相電壓 (V)"] = modbusViewer.voltageB; // 單位: V
			slaveData["C相電壓 (V)"] = modbusViewer.voltageC; // 單位: V
			slaveData["A相電流 (A)"] = modbusViewer.currentA; // 單位: A
			slaveData["B相電流 (A)"] = modbusViewer.currentB; // 單位: A
			slaveData["C相電流 (A)"] = modbusViewer.currentC; // 單位: A
			slaveData["A相功率因數"] = modbusViewer.powerFactorA; // 無單位 (值範圍: 0-1)
			slaveData["B相功率因數"] = modbusViewer.powerFactorB; // 無單位 (值範圍: 0-1)
			slaveData["C相功率因數"] = modbusViewer.powerFactorC; // 無單位 (值範圍: 0-1)
			slaveData["A相有功功率 (W)"] = modbusViewer.activePowerA; // 單位: W
			slaveData["B相有功功率 (W)"] = modbusViewer.activePowerB; // 單位: W
			slaveData["C相有功功率 (W)"] = modbusViewer.activePowerC; // 單位: W
			slaveData["電能 (kWh)"] = modbusViewer.energy; // 單位: kWh
			slaveData["當前總有功功率 (W)"] = modbusViewer.totalActivePower; // 單位: W
			slaveData["當前線頻率 (Hz)"] = modbusViewer.lineFrequency; // 單位: Hz

		}




		// 附加 CRC 校驗碼
		private static byte[] AppendCRC(byte[] command)
        {
            ushort crc = CalculateCRC(command);
            byte[] crcBytes = BitConverter.GetBytes(crc);
            byte[] fullCommand = new byte[command.Length + 2];
            Array.Copy(command, fullCommand, command.Length);
            fullCommand[fullCommand.Length - 2] = crcBytes[0];
            fullCommand[fullCommand.Length - 1] = crcBytes[1];
            return fullCommand;
        }


		public static ushort CalculateCRC(byte[] data)
		{
			ushort crc = 0xFFFF; // 設定初始值
			for (int pos = 0; pos < data.Length; pos++) // 遍歷數據每一位元組
			{
				crc ^= (ushort)data[pos]; // 與當前字節異或
				for (int i = 8; i != 0; i--) // 進行8次迴圈
				{
					if ((crc & 0x0001) != 0) // 如果最低位為1
					{
						crc >>= 1; // 右移一位
						crc ^= 0xA001; // 異或0xA001
					}
					else
					{
						crc >>= 1; // 否則直接右移
					}
				}
			}
			return crc; // 返回計算結果
		}
	}
}
