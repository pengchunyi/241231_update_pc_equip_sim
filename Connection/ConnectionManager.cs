// Services/ConnectionManager.cs
using System;
using System.Collections.Generic;
using System.ComponentModel.Composition.Primitives;
using System.IO.Ports;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace AmqpModbusIntegration.Services
{
	/// <summary>
	/// 專責：
	/// 1. 週期性嘗試與裝置 (Modbus) 建立連線（最多 999 次，每 1 分鐘重試一次）。
	/// 2. 連線成功 → 啟動 AMQP Endpoint（StartAmqpEndpoint 內含持續上報邏輯）。
	/// 3. 持續讀取 Slave Data → 給 UI 顯示。
	/// </summary>
	public class ConnectionManager
	{
		private readonly ModbusViewer _viewer;

		private readonly AmqpEndpointManager _amqp;
		private readonly CancellationTokenSource _cts = new CancellationTokenSource();
		// ConnectionManager.cs
		private int _readFailStreak = 0;
		private const int MaxFail = 5;
		private bool _modbusHealthy = false;


		//建構子
		public ConnectionManager(
			ModbusViewer viewer,
			AmqpEndpointManager amqp)
		{

			_viewer = viewer;
			_amqp = amqp;
		}


		public void Start()
		{
			_ = RunAsync(_cts.Token);
		}



		public void Stop()
		{
			_cts.Cancel();
		}



		private async Task RunAsync(CancellationToken ct)
		{

			const int MAX_ATTEMPTS = 9999;// 最多重試 9999 次
			const int RETRY_INTERVAL_MS = 1 * 60 * 1000; // 1 分鐘
			int attempts = 0;
			bool connected = false;


			while (attempts < MAX_ATTEMPTS && !connected && !ct.IsCancellationRequested)
			{
				attempts++;
				try
				{
					var port = _viewer.GetSerialPort();
					if (!port.IsOpen)
						port.Open();

					// 測試一次 Modbus 讀取
					await ModbusHelper.ReadAllParametersAsync(port, _viewer);
					connected = true;
					_viewer.AppendLog($"與空開連接成功（第 {attempts}/{MAX_ATTEMPTS} 次）");
				}
				catch
				{
					_viewer.AppendLog($"與空開連接失敗（第 {attempts}/{MAX_ATTEMPTS} 次，1分鐘後再連線...)");
					// 失敗 → 1分鐘後再試
					await Task.Delay(RETRY_INTERVAL_MS, ct);
					//.ConfigureAwait(false);
				}
			}

			if (!connected || ct.IsCancellationRequested)
			{
				_viewer.AppendLog($"與空開連接失敗已達上限{MAX_ATTEMPTS}，請檢查設備與通訊參數。");
				return;
			}
			// ===== 這裡呼叫 AMQP 連線確保 =====
			await _amqp.EnsureConnectedAsync(SystemConfig.MachineSN ?? "UnknownMachineSN", ct);

			//// 2) 建立 AMQP Endpoint（裡面已自帶「持續上報」迴圈）
			//string endpointName = SystemConfig.MachineSN ?? "DefaultEndpoint";
			//_amqp.StartAmqpEndpoint(endpointName);

			// 3) 連線後，每秒刷新一次 slaveData，供 UI 顯示
			while (!ct.IsCancellationRequested)
			{

				bool ok;

				try
				{
					ok = await ModbusHelper.ReadAllParametersAsync(_viewer.GetSerialPort(), _viewer);
					//await ModbusHelper.ReadAllParametersAsync(_viewer.GetSerialPort(), _viewer).ConfigureAwait(false); 
				}
				catch (Exception ex) when (!ct.IsCancellationRequested)
				{
					// 讀取失敗不終止，只記錄
					_viewer.AppendLog($"讀取參數失敗：{ex.Message}");
					ok = false;
				}

				if (ok)
				{
					if (!_modbusHealthy)
					{
						_viewer.AppendLog("與空開連線恢復。");
						_amqp.SetModbusHealthy(true);
						_modbusHealthy = true;
					}
					_readFailStreak = 0;
				}
				else
				{
					_readFailStreak++;
					if (_readFailStreak >= MaxFail && _modbusHealthy)
					{
						_viewer.AppendLog("與空開連接中斷（讀取失敗），暫停發布並嘗試重連…");
						_amqp.SetModbusHealthy(false);
						_modbusHealthy = false;

						TryReconnectSerialPort();   // ↓ 見下段
						_readFailStreak = 0;        // 重算
					}
				}

				await Task.Delay(1000, ct);
				//.ConfigureAwait(false);
			}
		}

		private void TryReconnectSerialPort()
		{
			try
			{
				var port = _viewer.GetSerialPort();
				if (port != null)
				{
					if (port.IsOpen) port.Close();
					port.Dispose();
				}

				// 重新開啟
				var com = SwitchDeviceConfig.ComPort;
				_viewer.InitializeSerialPort(com);
				_viewer.AppendLog($"請切至開發者模式，手動重連：{com}");
			}
			catch (Exception ex)
			{
				_viewer.AppendLog($"重連 COM 失敗：{ex.Message}");
			}
		}


	}
}
