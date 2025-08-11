// Services/ConnectionManager.cs
using System;
using System.IO.Ports;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace AmqpModbusIntegration.Services
{
	//public class ConnectionManager
	public class ConnectionManager : IDisposable
	{
		private readonly ModbusViewer _viewer;
		private readonly AmqpEndpointManager _amqp;
		private readonly CancellationTokenSource _cts = new CancellationTokenSource();

		private int _readFailStreak = 0;
		private const int MaxFail = 5;
		private bool _modbusHealthy = false;
		private bool _amqpRequestedOnce = false;

		private async Task<bool> VerifyPortByReadingAsync(SerialPort port, ModbusViewer viewer)
		{
			var hasStations = viewer.slaveData != null && viewer.slaveData.Keys.Any();
			if (!hasStations) return false;
			return await ModbusHelper.ReadAllParametersAsync(port, viewer);
		}

		public ConnectionManager(ModbusViewer viewer, AmqpEndpointManager amqp)
		{
			_viewer = viewer;
			_amqp = amqp;
		}

		public void Start() => _ = RunAsync(_cts.Token);
		//public void Stop() => _cts.Cancel();

		//250807新增================================
		 public void Dispose()
		 {
			 _cts.Cancel();
			 _cts.Dispose();
		 }
		//250807新增================================


		private async Task RunAsync(CancellationToken ct)
		{
			await EnsureModbusFirstConnectAsync(ct);

			if (!_amqpRequestedOnce)
			{
				_amqpRequestedOnce = true;
				_ = _amqp.EnsureConnectedAsync(SystemConfig.MachineSN ?? "UnknownMachineSN", ct);
				AppLogger.Info("[AMQP] 連線請求已送出（在 Modbus 首次就緒後）");
			}

			while (!ct.IsCancellationRequested)
			{
				bool ok = false;

				try
				{
					var port = _viewer.GetSerialPort();
					if (port == null || !port.IsOpen)
					{
						await HandleDisconnectAndReconnectAsync(ct);
						ok = _modbusHealthy;
					}
					else
					{
						ok = await ModbusHelper.ReadAllParametersAsync(port, _viewer);
					}
				}
				catch (Exception ex) when (!ct.IsCancellationRequested)
				{
					LogThrottler.Every($"mb_read_loop_exc:{ex.GetType().Name}", 5,
						() => AppLogger.Error(ex, "[MODBUS] 讀取參數例外"));
					ok = false;
				}

				if (ok)
				{
					if (!_modbusHealthy)
					{
						_modbusHealthy = true;
						_amqp.SetModbusHealthy(true);
						_viewer.OnSerialReconnected();
						AppLogger.Info("[MODBUS] 與空開連線恢復。");
					}
					_readFailStreak = 0;
				}
				else
				{
					_readFailStreak++;
					if (_readFailStreak >= MaxFail)
					{
						await HandleDisconnectAndReconnectAsync(ct);
						_readFailStreak = 0;
					}
				}

				await Task.Delay(1000, ct);
			}
		}

		private async Task EnsureModbusFirstConnectAsync(CancellationToken ct)
		{
			while (!ct.IsCancellationRequested)
			{
				var restored = await AutoReconnectLoopAsync(ct);
				if (!restored) return;

				if (!_modbusHealthy)
				{
					_modbusHealthy = true;
					_amqp.SetModbusHealthy(true);
					_viewer.OnSerialReconnected();
					AppLogger.Info("[MODBUS] 初次連線就緒。");
				}
				return;
			}
		}

		private async Task HandleDisconnectAndReconnectAsync(CancellationToken ct)
		{
			if (_modbusHealthy)
			{
				_modbusHealthy = false;
				_amqp.SetModbusHealthy(false);
				_viewer.OnSerialDisconnected();
				AppLogger.Warn("[MODBUS] 與空開連接中斷，暫停發布並開始重連…");
			}

			var restored = await AutoReconnectLoopAsync(ct);
			if (!restored) return;

			if (!_modbusHealthy)
			{
				_modbusHealthy = true;
				_amqp.SetModbusHealthy(true);
				_viewer.OnSerialReconnected();
				AppLogger.Info("[SERIAL] 串口已驗證可讀，準備就緒，等待重連AMQP....");
			}

			if (!_amqpRequestedOnce)
			{
				_amqpRequestedOnce = true;
				_ = _amqp.EnsureConnectedAsync(SystemConfig.MachineSN ?? "UnknownMachineSN", ct);
				AppLogger.Info("[AMQP] 連線請求已送出（於掉線重連後）");
			}
		}

		private async Task<bool> AutoReconnectLoopAsync(CancellationToken ct)
		{
			while (!ct.IsCancellationRequested)
			{
				try
				{
					bool manual = SystemConfig.ManualComOverride;
					var current = _viewer.GetSerialPort();

					if (manual)
					{
						if (current != null && current.IsOpen)
						{
							bool ok = await VerifyPortByReadingAsync(current, _viewer);
							if (ok) return true;

							LogThrottler.Every("manual_open_fail", 30,
								delegate { AppLogger.Info("[SERIAL] 手動模式：目前埠已開啟但尚未驗證（讀取失敗），續試中…"); });

							await Task.Delay(1500, ct);
							continue;
						}
						else
						{
							string selected = _viewer.GetUserSelectedComPort();
							if (string.IsNullOrWhiteSpace(selected))
							{
								await Task.Delay(500, ct);
								continue;
							}

							var names = SerialPort.GetPortNames();
							if (!names.Contains(selected, StringComparer.OrdinalIgnoreCase))
							{
								LogThrottler.Every("manual_missing_" + selected, 30,
									delegate { AppLogger.Info("[SERIAL] 手動模式：" + selected + " 未偵測到連結，請接回或改選其他埠…"); });
								await Task.Delay(5000, ct);
								continue;
							}

							_viewer.SafeInitializeSerialPort(selected);
							await Task.Delay(300, ct);
							continue;
						}
					}
					else
					{
						string configured = SwitchDeviceConfig.ComPort;
						if (string.IsNullOrWhiteSpace(configured))
						{
							LogThrottler.Every("no_cfg_com", 30,
								delegate { AppLogger.Warn("[SERIAL] 自動模式：設定檔未賦予 COM 口。"); });
							await Task.Delay(5000, ct);
							continue;
						}

						var names = SerialPort.GetPortNames();
						if (!names.Contains(configured, StringComparer.OrdinalIgnoreCase))
						{
							LogThrottler.Every("cfg_missing_" + configured, 30,
								delegate { AppLogger.Info("[SERIAL] 自動模式：" + configured + " 已斷開，請檢查異常並復位 …"); });
							await Task.Delay(5000, ct);
							continue;
						}

						if (current == null || !current.IsOpen ||
							!string.Equals(current.PortName, configured, StringComparison.OrdinalIgnoreCase))
						{
							_viewer.SafeInitializeSerialPort(configured);
							current = _viewer.GetSerialPort();
						}

						if (current != null && current.IsOpen)
						{
							bool ok = await VerifyPortByReadingAsync(current, _viewer);
							if (ok) return true;

							LogThrottler.Every("cfg_open_fail_" + configured, 30,
								delegate { AppLogger.Info("[SERIAL] 自動模式：" + configured + " 已開啟，但讀取失敗，續試中…"); });
						}
					}
				}
				catch (Exception ex)
				{
					AppLogger.Error(ex, "[SERIAL] 自動重連例外");
				}

				await Task.Delay(5000, ct);
			}

			return false;
		}
	}
}
