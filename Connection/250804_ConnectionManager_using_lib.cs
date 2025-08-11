// Services/ConnectionManager.cs
using System;
using System.IO.Ports;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace AmqpModbusIntegration.Services
{
	/// <summary>
	/// 串口/Modbus 生命週期管理：
	/// 連線時序（重要）：
	/// 1) 先確保 Modbus 首次就緒（COM 開啟且至少成功讀到一次；若尚未設定站號則只要 COM 開啟即視為就緒）
	/// 2) 再啟動 AMQP (EnsureConnectedAsync)
	/// 3) 只有 AMQP 已連線 + ModbusHealthy==true，AmqpEndpointManager 內部才會啟動 PublishLoop
	///
	/// 埠中斷：
	/// - 連續讀取失敗達門檻或系統偵測 COM 消失 → 視為中斷，SetModbusHealthy(false) 並呼叫 viewer.OnSerialDisconnected() 清空畫面
	/// - 進入 AutoReconnectLoop：僅嘗試設定檔指定的 COM；成功驗證讀取一次即恢復
	/// </summary>
	public class ConnectionManager
	{
		private readonly ModbusViewer _viewer;
		private readonly AmqpEndpointManager _amqp;
		private readonly CancellationTokenSource _cts = new CancellationTokenSource();

		private int _readFailStreak = 0;
		private const int MaxFail = 5;
		private bool _modbusHealthy = false;
		private bool _amqpRequestedOnce = false; // 避免重覆呼叫 EnsureConnectedAsync

		public ConnectionManager(ModbusViewer viewer, AmqpEndpointManager amqp)
		{
			_viewer = viewer;
			_amqp = amqp;
		}

		public void Start()
		{
			_ = RunAsync(_cts.Token);
		}

		public void Stop() => _cts.Cancel();




		private async Task RunAsync(CancellationToken ct)
		{
			// 1) 先確保 Modbus 首次連線與驗證
			await EnsureModbusFirstConnectAsync(ct);

			// 2) 成功後才請 AMQP 連線（只請求一次）
			if (!_amqpRequestedOnce)
			{
				_amqpRequestedOnce = true;
				_ = _amqp.EnsureConnectedAsync(SystemConfig.MachineSN ?? "UnknownMachineSN", ct);
				AppLogger.Info("[AMQP] 連線請求已送出（在 Modbus 首次就緒後）");
			}

			// 3) 進入正常輪詢
			while (!ct.IsCancellationRequested)
			{
				bool ok = false;

				try
				{
					var port = _viewer.GetSerialPort();
					if (port == null || !port.IsOpen)
					{
						// 視為掉線，走重連流程
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
					LogThrottler.Every($"mb_read_loop_exc:{ex.GetType().Name}", 5, () =>
					AppLogger.Error(ex, "[MODBUS] 讀取參數例外"));
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
				if (!restored) return; // 被取消

				if (!_modbusHealthy)
				{
					_modbusHealthy = true;
					_amqp.SetModbusHealthy(true);
					_viewer.OnSerialReconnected();
					AppLogger.Info("[MODBUS] 初次連線就緒。");
				}
				return; // 成功
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

			//await AutoReconnectLoopAsync(ct);

			var restored = await AutoReconnectLoopAsync(ct);
			if (!restored) return;  // 被取消或尚未成功就結束

			// 驗證成功後，恢復狀態
			if (!_modbusHealthy)
			{

				//250730=====================================
				_modbusHealthy = true;
				_amqp.SetModbusHealthy(true);
				_viewer.OnSerialReconnected();
				AppLogger.Info("[SERIAL] 串口已驗證可讀，準備就緒，等待重連AMQP....");
			}

			// AMQP 若尚未請求過，這裡補一次（保守作法）
			if (!_amqpRequestedOnce)
			{
				_amqpRequestedOnce = true;
				_ = _amqp.EnsureConnectedAsync(SystemConfig.MachineSN ?? "UnknownMachineSN", ct);
				AppLogger.Info("[AMQP] 連線請求已送出（於掉線重連後）");
			}
		}



		//250804_串口重連訊息
		//這個方法是最容易洗版的地方。以下是替換整個方法的版本，
		//把常見訊息用 5 秒節流包住（LOG_THROTTLE_SEC = 5）：
		//private async Task<bool> AutoReconnectLoopAsync(CancellationToken ct)
		//{
		//	const int LOG_THROTTLE_SEC = 5;

		//	while (!ct.IsCancellationRequested)
		//	{
		//		try
		//		{
		//			bool manual = SystemConfig.ManualComOverride;

		//			// 目前已開啟的埠
		//			var current = _viewer.GetSerialPort();

		//			if (manual)
		//			{
		//				// 手動模式：若目前已有開啟的埠 → 直接驗證讀取
		//				if (current != null && current.IsOpen)
		//				{
		//					var hasStationsNow = _viewer.slaveData != null && _viewer.slaveData.Keys.Any();
		//					bool okNow = hasStationsNow ? await ModbusHelper.ReadAllParametersAsync(current, _viewer) : true;
		//					if (okNow) return true;

		//					LogThrottler.Every($"serial_manual_open_but_fail:{current.PortName}", LOG_THROTTLE_SEC, () =>
		//						AppLogger.Warn("[SERIAL] 手動模式：目前埠已開啟但讀取失敗，續試中…"));
		//				}
		//				else
		//				{
		//					// 取 UI 選到的埠（或 null）
		//					string selected = _viewer.GetUserSelectedComPort();

		//					// 若 UI 尚未選定任何埠 → 不刷錯，只等待使用者操作
		//					if (string.IsNullOrWhiteSpace(selected))
		//					{
		//						await Task.Delay(1000, ct);
		//						continue;
		//					}

		//					// 檢查該埠是否存在
		//					var names = SerialPort.GetPortNames();
		//					if (!names.Contains(selected, StringComparer.OrdinalIgnoreCase))
		//					{
		//						// 不洗版提示
		//						LogThrottler.Every($"serial_manual_missing:{selected}", LOG_THROTTLE_SEC, () =>
		//							AppLogger.Info($"[SERIAL] 手動模式：{selected} 尚未出現，請接回或改選其他埠…"));
		//						await Task.Delay(1000, ct);
		//						continue;
		//					}

		//					// 嘗試開啟 UI 選定的埠
		//					_viewer.SafeInitializeSerialPort(selected);
		//					current = _viewer.GetSerialPort();

		//					if (current != null && current.IsOpen)
		//					{
		//						var hasStations = _viewer.slaveData != null && _viewer.slaveData.Keys.Any();
		//						bool ok = hasStations ? await ModbusHelper.ReadAllParametersAsync(current, _viewer) : true;
		//						if (ok) return true;

		//						LogThrottler.Every($"serial_manual_open_but_fail:{selected}", LOG_THROTTLE_SEC, () =>
		//							AppLogger.Warn($"[SERIAL] 手動模式：{selected} 已開啟，但讀取失敗，續試中…"));
		//					}
		//				}
		//			}
		//			else
		//			{
		//				// 依照 ini COM
		//				string configured = SwitchDeviceConfig.ComPort;
		//				if (string.IsNullOrWhiteSpace(configured))
		//				{
		//					LogThrottler.Every("serial_ini_missing", LOG_THROTTLE_SEC, () =>
		//						AppLogger.Warn("[SERIAL] 設定檔未賦予 COM 口。"));
		//					await Task.Delay(2000, ct);
		//					continue;
		//				}

		//				var names = SerialPort.GetPortNames();
		//				if (!names.Contains(configured, StringComparer.OrdinalIgnoreCase))
		//				{
		//					LogThrottler.Every($"serial_disconnected:{configured}", LOG_THROTTLE_SEC, () =>
		//						AppLogger.Info($"[SERIAL] {configured} 已斷開，請檢查異常並復位 …"));
		//					await Task.Delay(2000, ct);
		//					continue;
		//				}

		//				// 若沒有埠、或未開啟、或名字不同於設定檔 → 才重新初始化
		//				if (current == null || !current.IsOpen ||
		//					!string.Equals(current.PortName, configured, StringComparison.OrdinalIgnoreCase))
		//				{
		//					_viewer.SafeInitializeSerialPort(configured);
		//					current = _viewer.GetSerialPort();
		//				}

		//				if (current != null && current.IsOpen)
		//				{
		//					var hasStations = _viewer.slaveData != null && _viewer.slaveData.Keys.Any();
		//					bool ok = hasStations ? await ModbusHelper.ReadAllParametersAsync(current, _viewer) : true;

		//					if (ok) return true; // 成功 → 跳出重連 loop

		//					LogThrottler.Every($"serial_open_but_fail:{configured}", LOG_THROTTLE_SEC, () =>
		//						AppLogger.Warn($"[SERIAL] {configured} 已開啟，但讀取失敗，續試中…"));
		//				}
		//			}
		//		}
		//		catch (Exception ex)
		//		{
		//			// 重連例外也很容易洗版，按例外型別節流
		//			LogThrottler.Every($"serial_autoreconn_exc:{ex.GetType().Name}", 5, () =>
		//				AppLogger.Error(ex, "[SERIAL] 自動重連例外"));
		//		}

		//		await Task.Delay(1000, ct);   // 注意：這是重試節拍，保留 1 秒以維持反應速度
		//	}

		//	return false;  // 被取消或結束
		//}

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
							bool hasStations = _viewer.slaveData != null && _viewer.slaveData.Keys.Any();
							bool ok = hasStations ? await ModbusHelper.ReadAllParametersAsync(current, _viewer) : true;
							if (ok) return true;

							LogThrottler.Every("manual_open_fail", 5,
								delegate { AppLogger.Info("[SERIAL] 手動模式：目前埠已開啟但讀取失敗，續試中…"); });
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
								LogThrottler.Every("manual_missing_" + selected, 5,
									delegate { AppLogger.Info("[SERIAL] 手動模式：" + selected + " 尚未出現，請接回或改選其他埠…"); });
								await Task.Delay(5000, ct);
								continue;
							}

							_viewer.SafeInitializeSerialPort(selected);
							current = _viewer.GetSerialPort();

							if (current != null && current.IsOpen)
							{
								bool hasStations2 = _viewer.slaveData != null && _viewer.slaveData.Keys.Any();
								bool ok2 = hasStations2 ? await ModbusHelper.ReadAllParametersAsync(current, _viewer) : true;
								if (ok2) return true;

								LogThrottler.Every("manual_open_fail2_" + selected, 5,
									delegate { AppLogger.Info("[SERIAL] 手動模式：" + selected + " 已開啟，但讀取失敗，續試中…"); });
							}
						}
					}
					else
					{
						string configured = SwitchDeviceConfig.ComPort;
						if (string.IsNullOrWhiteSpace(configured))
						{
							LogThrottler.Every("no_cfg_com", 5,
								delegate { AppLogger.Warn("[SERIAL] 設定檔未賦予 COM 口。"); });
							await Task.Delay(5000, ct);
							continue;
						}

						var names = SerialPort.GetPortNames();
						if (!names.Contains(configured, StringComparer.OrdinalIgnoreCase))
						{
							LogThrottler.Every("cfg_missing_" + configured, 5,
								delegate { AppLogger.Info("[SERIAL] " + configured + " 已斷開，請檢查異常並復位 …"); });
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
							bool hasStations = _viewer.slaveData != null && _viewer.slaveData.Keys.Any();
							bool ok = hasStations ? await ModbusHelper.ReadAllParametersAsync(current, _viewer) : true;
							if (ok) return true;

							LogThrottler.Every("cfg_open_fail_" + configured, 5,
								delegate { AppLogger.Info("[SERIAL] " + configured + " 已開啟，但讀取失敗，續試中…"); });
						}
					}
				}
				catch (Exception ex)
				{
					AppLogger.Error(ex, "[SERIAL] 自動重連例外");
				}

				await Task.Delay(5000, ct); // 重連節奏：5 秒
			}

			return false;
		}






	}
}
