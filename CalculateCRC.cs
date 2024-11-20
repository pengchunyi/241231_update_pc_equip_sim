using System;

namespace AmqpModbusIntegration
{
    public static class CrcHelper
    {
        // 計算CRC校驗碼的方法
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
