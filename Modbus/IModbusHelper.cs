// File: IModbusHelper.cs
using System;
using System.Collections.Generic;
using System.IO.Ports;
using System.Threading.Tasks;

namespace AmqpModbusIntegration
{
	/// <summary>
	/// 列出 ModbusHelper 提供的所有方法與欄位，方便快速查閱
	/// </summary>
	public interface IModbusHelper
	{
		/// <summary>全域唯一鎖。所有 SerialPort I/O（Write/Read/RtsEnable/Discard）統一鎖住。</summary>
		object SerialSync { get; }

		// ========== 開關命令 ==========
		/// <summary>啟動開關命令 (ON) for 指定站號</summary>
		void SwitchON(SerialPort serialPort, byte stationNumber);
		/// <summary>啟動關閉命令 (OFF) for 指定站號</summary>
		void SwitchOFF(SerialPort serialPort, byte stationNumber);
		/// <summary>設定指定站號的溫度</summary>
		void SetTemperature(SerialPort serialPort, byte stationNumber, ushort temperature, Dictionary<byte, Dictionary<string, object>> slaveData);

		// ========== 讀取（每站） ==========
		/// <summary>
		/// 讀取所有站 0x0000 起 48 regs（0x30），回傳是否至少成功一次
		/// </summary>
		Task<bool> ReadAllParametersAsync(SerialPort serialPort, ModbusViewer modbusViewer);

		// ========== CRC ==========
		/// <summary>於指令後附加 2 bytes CRC 校驗</summary>
		byte[] AppendCRC(byte[] command);
		/// <summary>計算給定資料的 CRC</summary>
		ushort CalculateCRC(byte[] data);
		/// <summary>驗證資料尾端 CRC 是否正確</summary>
		bool ValidateCRC(byte[] data, int length);
	}
}
