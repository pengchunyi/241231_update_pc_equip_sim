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
                serialPort.Write(fullCommand, 0, fullCommand.Length);
                Console.WriteLine($"站號 {stationNumber} 的開關已關閉");
            }
        }



        // 讀取所有參數
        //public static async Task ReadAllParametersAsync(
        //    SerialPort serialPort,
        //    Dictionary<byte, Dictionary<string, int>> slaveData)
        //{
        //    if (serialPort != null && serialPort.IsOpen)
        //    {
        //        foreach (var station in slaveData.Keys.ToList())
        //        {
        //            try
        //            {
        //                byte[] readCommand = { station, 0x03, 0x00, 0x00, 0x00, 0x30 };
        //                byte[] fullCommand = AppendCRC(readCommand);
        //                serialPort.Write(fullCommand, 0, fullCommand.Length);

        //                await Task.Delay(1000); // 延遲 1 秒等待設備回應

        //                byte[] buffer = new byte[256];
        //                int bytesRead = await serialPort.BaseStream.ReadAsync(buffer, 0, buffer.Length);

        //                if (bytesRead > 5)
        //                {
        //                    var data = slaveData[station];
        //                    ParseResponse(buffer, data);
        //                }
        //            }
        //            catch (Exception ex)
        //            {
        //                Console.WriteLine($"讀取站號 {station} 時發生錯誤: {ex.Message}");
        //            }
        //        }
        //    }
        //}
        // 讀取所有參數
        //public static async Task ReadAllParametersAsync(
        //    SerialPort serialPort,
        //    ModbusViewer modbusViewer)
        //{
        //    if (serialPort != null && serialPort.IsOpen)
        //    {
        //        foreach (var station in modbusViewer.GetSlaveData().Keys.ToList())
        //        {
        //            try
        //            {
        //                byte[] readCommand = { station, 0x03, 0x00, 0x00, 0x00, 0x30 };
        //                byte[] fullCommand = AppendCRC(readCommand);
        //                serialPort.Write(fullCommand, 0, fullCommand.Length);

        //                await Task.Delay(1000); // 延遲 1 秒等待設備回應

        //                byte[] buffer = new byte[256];
        //                int bytesRead = await serialPort.BaseStream.ReadAsync(buffer, 0, buffer.Length);

        //                if (bytesRead > 5)
        //                {
        //                    // 解析數據並存儲到 ModbusViewer 的變數
        //                    ParseResponse(buffer, modbusViewer);
        //                }
        //            }
        //            catch (Exception ex)
        //            {
        //                Console.WriteLine($"讀取站號 {station} 時發生錯誤: {ex.Message}");
        //            }
        //        }
        //    }
        //}
        //    public static async Task ReadAllParametersAsync(
        //SerialPort serialPort,
        //ModbusViewer modbusViewer)
        //    {
        //        if (serialPort != null && serialPort.IsOpen)
        //        {
        //            foreach (var station in modbusViewer.GetSlaveData().Keys.ToList())
        //            {
        //                try
        //                {
        //                    byte[] readCommand = { station, 0x03, 0x00, 0x00, 0x00, 0x30 };
        //                    byte[] fullCommand = AppendCRC(readCommand);
        //                    serialPort.Write(fullCommand, 0, fullCommand.Length);

        //                    //await Task.Delay(200); // 延遲 1 秒等待設備回應
        //                    //                        // 適當延遲，讓從站釋放總線
        //                    System.Threading.Thread.Sleep(200); // 延遲1000毫秒

        //                    byte[] buffer = new byte[256];
        //                    int bytesRead = await serialPort.BaseStream.ReadAsync(buffer, 0, buffer.Length);

        //                    if (bytesRead > 5)
        //                    {
        //                        ParseResponse(buffer, station, modbusViewer); // 傳遞站號和 ModbusViewer

        //                        // 適當延遲，讓從站釋放總線
        //                        //await Task.Delay(200); // 延遲 1 秒等待設備回應
        //                        System.Threading.Thread.Sleep(200); // 延遲1000毫秒
        //                    }
        //                }
        //                catch (Exception ex)
        //                {
        //                    Console.WriteLine($"讀取站號 {station} 時發生錯誤: {ex.Message}");
        //                }
        //            }
        //        }
        //    }
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
                        serialPort.Write(fullCommand, 0, fullCommand.Length);

                        // 延遲等待回應
                        await Task.Delay(300);

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
                        await Task.Delay(300);
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
            ushort calculatedCRC = CrcHelper.CalculateCRC(data.Take(length - 2).ToArray());
            return receivedCRC == calculatedCRC;
        }



        // 解析 Modbus 回應數據
        //private static void ParseResponse(byte[] buffer, Dictionary<string, int> data)
        //{
        //    data["當前狀態"] = (buffer[3] << 8) | buffer[4];
        //    data["A相溫度"] = (buffer[9] << 8) | buffer[10];
        //    data["B相溫度"] = (buffer[11] << 8) | buffer[12];
        //    data["C相溫度"] = (buffer[13] << 8) | buffer[14];
        //    data["A相電壓"] = (buffer[17] << 8) | buffer[18];
        //    data["A相電流"] = (buffer[23] << 8) | buffer[24];
        //    data["開關狀態"] = (buffer[53] << 8) | buffer[54];
        //    data["總有功功率"] = (buffer[63] << 8) | buffer[64];
        //    data["線頻率"] = (buffer[69] << 8) | buffer[70];
        //}

        //// 解析 Modbus 回應數據並更新到 ModbusViewer 的變數
        //private static void ParseResponse(byte[] buffer, ModbusViewer modbusViewer)
        //{
        //    modbusViewer.currentStatus1 = (buffer[3] << 8) | buffer[4];
        //    modbusViewer.currentStatus2 = (buffer[5] << 8) | buffer[6];
        //    modbusViewer.currentStatus = (modbusViewer.currentStatus1 << 16) | modbusViewer.currentStatus2;

        //    modbusViewer.leakageCurrent = (buffer[7] << 8) | buffer[8];
        //    modbusViewer.tempA = (buffer[9] << 8) | buffer[10];
        //    modbusViewer.tempB = (buffer[11] << 8) | buffer[12];
        //    modbusViewer.tempC = (buffer[13] << 8) | buffer[14];
        //    modbusViewer.tempN = (buffer[15] << 8) | buffer[16];
        //    modbusViewer.voltageA = (buffer[17] << 8) | buffer[18];
        //    modbusViewer.voltageB = (buffer[19] << 8) | buffer[20];
        //    modbusViewer.voltageC = (buffer[21] << 8) | buffer[22];
        //    modbusViewer.currentA = (buffer[23] << 8) | buffer[24];
        //    modbusViewer.currentB = (buffer[25] << 8) | buffer[26];
        //    modbusViewer.currentC = (buffer[27] << 8) | buffer[28];
        //    modbusViewer.powerFactorA = (buffer[29] << 8) | buffer[30];
        //    modbusViewer.powerFactorB = (buffer[31] << 8) | buffer[32];
        //    modbusViewer.powerFactorC = (buffer[33] << 8) | buffer[34];
        //    modbusViewer.activePowerA = (buffer[35] << 8) | buffer[36];
        //    modbusViewer.activePowerB = (buffer[37] << 8) | buffer[38];
        //    modbusViewer.activePowerC = (buffer[39] << 8) | buffer[40];
        //    modbusViewer.reactivePowerA = (buffer[41] << 8) | buffer[42];
        //    modbusViewer.reactivePowerB = (buffer[43] << 8) | buffer[44];
        //    modbusViewer.reactivePowerC = (buffer[45] << 8) | buffer[46];
        //    modbusViewer.breakerTimes = (buffer[47] << 8) | buffer[48];

        //    int energyHighByte = (buffer[49] << 8) | buffer[50];
        //    int energyLowByte = (buffer[51] << 8) | buffer[52];
        //    modbusViewer.energy = ((energyHighByte << 16) | energyLowByte) / 100.0;

        //    modbusViewer.switchStatus = (buffer[53] << 8) | buffer[54];
        //    modbusViewer.apparentPowerA = (buffer[55] << 8) | buffer[56];
        //    modbusViewer.apparentPowerB = (buffer[57] << 8) | buffer[58];
        //    modbusViewer.apparentPowerC = (buffer[59] << 8) | buffer[60];
        //    modbusViewer.totalApparentPower = (buffer[61] << 8) | buffer[62];
        //    modbusViewer.totalActivePower = (buffer[63] << 8) | buffer[64];
        //    modbusViewer.totalReactivePower = (buffer[65] << 8) | buffer[66];
        //    modbusViewer.combinedPowerFactor = (buffer[67] << 8) | buffer[68];
        //    modbusViewer.lineFrequency = (buffer[69] << 8) | buffer[70];
        //    modbusViewer.deviceType = (buffer[71] << 8) | buffer[72];
        //    modbusViewer.historicalLeakage = (buffer[73] << 8) | buffer[74];
        //    modbusViewer.historicalCurrentA = (buffer[75] << 8) | buffer[76];
        //    modbusViewer.historicalCurrentB = (buffer[77] << 8) | buffer[78];
        //    modbusViewer.historicalCurrentC = (buffer[79] << 8) | buffer[80];
        //}
        private static void ParseResponse(byte[] buffer, byte station, ModbusViewer modbusViewer)
        {
            // 解析數據並存入 ModbusViewer 變數
            modbusViewer.currentStatus1 = (buffer[3] << 8) | buffer[4];
            modbusViewer.currentStatus2 = (buffer[5] << 8) | buffer[6];
            modbusViewer.currentStatus = (modbusViewer.currentStatus1 << 16) | modbusViewer.currentStatus2;

            modbusViewer.leakageCurrent = (buffer[7] << 8) | buffer[8];
            modbusViewer.tempA = (buffer[9] << 8) | buffer[10];
            modbusViewer.tempB = (buffer[11] << 8) | buffer[12];
            modbusViewer.tempC = (buffer[13] << 8) | buffer[14];
            modbusViewer.tempN = (buffer[15] << 8) | buffer[16];
            modbusViewer.voltageA = (buffer[17] << 8) | buffer[18];
            modbusViewer.voltageB = (buffer[19] << 8) | buffer[20];
            modbusViewer.voltageC = (buffer[21] << 8) | buffer[22];
            modbusViewer.currentA = (buffer[23] << 8) | buffer[24];
            modbusViewer.currentB = (buffer[25] << 8) | buffer[26];
            modbusViewer.currentC = (buffer[27] << 8) | buffer[28];

            // 確保 slaveData 包含對應的站號
            if (!modbusViewer.GetSlaveData().ContainsKey(station))
            {
                modbusViewer.GetSlaveData()[station] = new Dictionary<string, int>();
            }

            var slaveData = modbusViewer.GetSlaveData()[station];

            // 更新 slaveData 中的數據結構
            //slaveData["當前狀態1"] = modbusViewer.currentStatus1;
            //slaveData["當前狀態2"] = modbusViewer.currentStatus2;
            slaveData["當前狀態"] = modbusViewer.currentStatus;
            slaveData["漏電電流"] = modbusViewer.leakageCurrent;

            slaveData["A相溫度"] = modbusViewer.tempA;
            slaveData["B相溫度"] = modbusViewer.tempB;
            slaveData["C相溫度"] = modbusViewer.tempC;
            slaveData["N線溫度"] = modbusViewer.tempN;

            slaveData["A相電壓"] = modbusViewer.voltageA;
            slaveData["B相電壓"] = modbusViewer.voltageB;
            slaveData["C相電壓"] = modbusViewer.voltageC;

            slaveData["A相電流"] = modbusViewer.currentA;
            slaveData["B相電流"] = modbusViewer.currentB;
            slaveData["C相電流"] = modbusViewer.currentC;
        }



        // 附加 CRC 校驗碼
        private static byte[] AppendCRC(byte[] command)
        {
            ushort crc = CrcHelper.CalculateCRC(command);
            byte[] crcBytes = BitConverter.GetBytes(crc);
            byte[] fullCommand = new byte[command.Length + 2];
            Array.Copy(command, fullCommand, command.Length);
            fullCommand[fullCommand.Length - 2] = crcBytes[0];
            fullCommand[fullCommand.Length - 1] = crcBytes[1];
            return fullCommand;
        }
    }
}
