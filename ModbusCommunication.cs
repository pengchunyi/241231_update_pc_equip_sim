//using System;
//using System.IO.Ports;
//using System.Threading.Tasks;

//namespace AmqpModbusIntegration
//{
//    public class ModbusCommunication
//    {
//        public byte StationNumber { get; set; } = 0xFF;

//        public ushort CalculateCRC(byte[] data)
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

//        public void SendCommand(SerialPort serialPort, byte[] command)
//        {
//            ushort crc = CalculateCRC(command);
//            byte[] crcBytes = BitConverter.GetBytes(crc);
//            byte[] fullCommand = new byte[command.Length + 2];
//            Array.Copy(command, fullCommand, command.Length);
//            fullCommand[fullCommand.Length - 2] = crcBytes[0];
//            fullCommand[fullCommand.Length - 1] = crcBytes[1];

//            serialPort.Write(fullCommand, 0, fullCommand.Length);
//        }
//    }
//}
