// File: IModbusViewer.cs
using System;
using System.Collections.Generic;
using System.IO.Ports;

namespace AmqpModbusIntegration
{
	/// <summary>
	/// 列出 ModbusViewer 所有公開方法，方便快速查閱
	/// </summary>
	public interface IModbusViewer
	{


		// ---- 視窗生命週期 / 事件回調 ----
		/// <summary>串口斷線時呼叫</summary>
		void OnSerialDisconnected();
		/// <summary>串口重連時呼叫</summary>
		void OnSerialReconnected();



		// ---- 狀態 & 顯示更新 ----
		/// <summary>在主標題顯示 Online/Offline 並啟停定時器</summary>
		void SetOnlineState(bool online);
		/// <summary>清空 DataGridView 與參數快取</summary>
		void ClearDisplay();
		/// <summary>把文字加到下方日誌框</summary>
		void AppendLog(string txt);
		/// <summary>重新讀取並顯示系統與切換檔設定</summary>
		void RefreshIniView();



		// ---- 串口 (SerialPort) 初始化與讀寫 ----
		/// <summary>安全模式（UI 執行緒）初始化串口</summary>
		void SafeInitializeSerialPort(string portName);
		/// <summary>實際初始化並開啟串口連線</summary>
		void InitializeSerialPort(string portName);
		/// <summary>把 TextBox 中的站號文字解析並更新 slaveData 結構</summary>
		void UpdateSlaveDataFromTextBox();
		/// <summary>把 slaveData 的最新參數寫到 DataGridView</summary>
		void UpdateDataGridView();
		/// <summary>根據 config 顯示預設的 COM port 與站號</summary>
		void SetComPortUI();



		// ---- Modbus 操作命令 ----
		/// <summary>
		/// 對一或多個站號執行開關命令（ON/OFF）  
		/// switchCommand: ModbusHelper.SwitchON 或 SwitchOFF  
		/// stationNumbers: 可選，若為 null 則解析 TextBox 站號列表
		/// </summary>
		void ExecuteSwitchCommand(Action<SerialPort, byte> switchCommand, List<byte> stationNumbers = null);
		/// <summary>對單一站號設定溫度</summary>
		void ExecuteSetTemperature(byte stationNumber, ushort temperature);

		// ---- 輔助初始化 ----
		/// <summary>建立並綁定每秒刷新 DataGridView 的 Timer</summary>
		void InitializeTimer();
		/// <summary>把所有監控參數欄位重設為初始值</summary>
		void InitializeParameters();




		// ---- 其他 ----
		/// <summary>回傳目前使用中的 SerialPort 實例</summary>
		SerialPort GetSerialPort();
	}
}
