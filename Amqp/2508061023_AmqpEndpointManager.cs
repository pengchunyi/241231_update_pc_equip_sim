// AmqpEndpointManager.cs
using CFX;
using CFX.Production;
using CFX.ResourcePerformance;
using CFX.Structures;
using CFX.Transport;
using Newtonsoft.Json;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO.Ports;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace AmqpModbusIntegration
{
	public class AmqpEndpointManager
	{
		private readonly string endpointUri;
		private readonly string publishChannelUri;
		private readonly string subscribeChannelUri;

		private readonly ModbusViewer modbusViewer;
		private readonly SerialPort serialPort; // 目前未用
		private readonly Dictionary<byte, Dictionary<string, object>> slaveData;

		private volatile bool _amqpConnected = false;
		private int _retryCount = 0;
		private const int _maxAttempts = 9999;
		private readonly TimeSpan _retryInterval = TimeSpan.FromMinutes(1);
		private CancellationTokenSource _amqpRetryCts;


		private AmqpCFXEndpoint endpoint;
		private readonly HashSet<string> _expectedUris = new HashSet<string>();
		private readonly ConcurrentDictionary<string, bool> _uriState = new ConcurrentDictionary<string, bool>();

		private volatile bool _publishLoopStarted = false;
		private volatile bool _modbusHealthy = false;

		// 類別欄位
		private int _publishLoopGate = 0;   // 0=沒有 loop, 1=已有 loop
		private int _connectLoopGate = 0;   // ★ 避免同時多個 EnsureConnected 背景迴圈
		private long _loopIdSeed = 0;       // 觀察/除錯用

		private static readonly Dictionary<int, (string Code, string ErrorDescription)> faultDictionary =
			new Dictionary<int, (string Code, string ErrorDescription)>
		{
			{ 0,  ("EGY_1_WARN1",  "A相過壓") },
			{ 1,  ("EGY_1_WARN2",  "B相過壓") },
			{ 2,  ("EGY_1_WARN3",  "C相過壓") },
			{ 3,  ("EGY_1_WARN4",  "A相欠壓") },
			{ 4,  ("EGY_1_WARN5",  "B相欠壓") },
			{ 5,  ("EGY_1_WARN6",  "C相過流") },
			{ 6,  ("EGY_1_WARN7",  "A相過流") },
			{ 7,  ("EGY_1_WARN8",  "B相過流") },
			{ 8,  ("EGY_1_WARN9",  "C相過流") },
			{ 9,  ("EGY_1_WARN10", "漏電異常") },
			{ 10, ("EGY_1_WARN11", "A相出線溫度異常") },
			{ 11, ("EGY_1_WARN12", "B相出線溫度異常") },
			{ 12, ("EGY_1_WARN13", "C相出線溫度異常") },
			{ 13, ("EGY_1_WARN14", "N相出線溫度異常") },
			{ 14, ("EGY_1_WARN15", "電弧") },
			{ 15, ("EGY_1_WARN16", "缺相") },
			{ 16, ("EGY_1_WARN17", "斷零") },
			{ 17, ("EGY_1_WARN18", "三相電壓不平衡") },
			{ 18, ("EGY_1_WARN19", "鎖定") },
			{ 19, ("EGY_1_WARN20", "進入維修或手動模式") },
			{ 20, ("EGY_1_WARN21", "開關狀態異常，提示客戶換設備") },
			{ 21, ("EGY_1_WARN22", "漏電功能壞，提示客戶換設備") },
			{ 22, ("EGY_1_WARN23", "設備離線") },
			{ 23, ("EGY_1_WARN24", "過壓預警") },
			{ 24, ("EGY_1_WARN25", "欠壓預警") },
			{ 25, ("EGY_1_WARN26", "過流預警") },
			{ 26, ("EGY_1_WARN27", "過溫預警") }
		};

		// 片段：AmqpEndpointManager 內
		private readonly object _publishLock = new object();
		private void PublishSafe(CFXMessage msg)
		{
			var ep = endpoint;
			if (ep == null || !_amqpConnected) return;
			lock (_publishLock)
			{
				if (!_amqpConnected) return;
				try
				{
					ep.Publish(msg);
				}
				catch (InvalidOperationException ex)
				{
					// CFX 內部集合列舉衝突（握手/通道切換同時發生）→ 吞掉一次避免打斷循環
					LogThrottler.Every("cfx_pub_enum", 5,
						delegate { AppLogger.Warn("[CFX] 發布循環內部列舉衝突（已略過一次）： " + ex.Message); });
				}
				catch (Exception ex)
				{
					AppLogger.Error(ex, "[CFX] Publish 例外");
				}
			}
		}


		public AmqpEndpointManager(
			string endpointUri,
			string publishChannelUri,
			string subscribeChannelUri,
			ModbusViewer modbusViewer,
			SerialPort serialPort,
			Dictionary<byte, Dictionary<string, object>> slaveData)
		{
			this.endpointUri = endpointUri;
			this.publishChannelUri = publishChannelUri;
			this.subscribeChannelUri = string.IsNullOrWhiteSpace(subscribeChannelUri) ? null : subscribeChannelUri;

			this.modbusViewer = modbusViewer;
			this.serialPort = serialPort;
			this.slaveData = slaveData;

			_expectedUris.Clear();
			AddExpectedUri(publishChannelUri);
			if (!string.IsNullOrWhiteSpace(this.subscribeChannelUri))
				AddExpectedUri(this.subscribeChannelUri);
		}

		private static string Normalize(string u)
		{
			if (string.IsNullOrWhiteSpace(u)) return null;
			return new Uri(u).ToString();
		}

		private void AddExpectedUri(string u)
		{
			var n = Normalize(u);
			if (!string.IsNullOrWhiteSpace(n))
				_expectedUris.Add(n);
		}




		public void SetModbusHealthy(bool healthy)
		{
			if (_modbusHealthy == healthy) return;
			_modbusHealthy = healthy;

			AppLogger.Info(healthy ? "[MODBUS] 恢復連線，恢復 CFX 發布。" : "[MODBUS] 中斷，暫停 CFX 發布。");

			if (!healthy)
			{
				_publishLoopStarted = false;
				_amqpConnected = false;
				try { _amqpRetryCts?.Cancel(); } catch { }
				try { endpoint?.Close(); } catch { }
				AppLogger.Info("[AMQP] 已停止連線與重試（等待 Modbus 恢復）。");
				return;
			}

			if (!_amqpConnected)
				_ = EnsureConnectedAsync(SystemConfig.MachineSN ?? "UnknownMachineSN");

			TryStartPublishLoop();
		}


		//public async Task EnsureConnectedAsync(string endpointName, CancellationToken ct = default)
		//{
		//	if (_amqpConnected) return;

		//	_amqpRetryCts?.Cancel();
		//	_amqpRetryCts = CancellationTokenSource.CreateLinkedTokenSource(ct);

		//	_ = Task.Run(async () =>
		//	{

		//		while (!_amqpConnected && _retryCount < _maxAttempts && !_amqpRetryCts.IsCancellationRequested)
		//		{
		//			if (!_modbusHealthy)
		//			{
		//				try { await Task.Delay(TimeSpan.FromSeconds(5), _amqpRetryCts.Token); } catch { }
		//				continue;
		//			}

		//			_retryCount++;
		//			try
		//			{
		//				StartAmqpEndpoint(endpointName);
		//				bool ok = await SpinWaitUntilConnectedAsync(TimeSpan.FromSeconds(60), _amqpRetryCts.Token);
		//				if (!ok) throw new Exception("handshake timeout");
		//				AppLogger.Info("[AMQP] 連線成功（第 " + _retryCount + "/" + _maxAttempts + " 次）");
		//			}
		//			catch (Exception ex)
		//			{
		//				_amqpConnected = false;
		//				LogThrottler.Every("amqp_retry", 5,
		//				delegate { AppLogger.Warn("[AMQP] 連線失敗（第 " + _retryCount + "/" + _maxAttempts + " 次，5 秒後再試）：" + ex.Message); });
		//				try { await Task.Delay(_retryInterval, _amqpRetryCts.Token); } catch { }
		//			}
		//		}

		//		if (!_amqpConnected)
		//			AppLogger.Warn("[AMQP] 重試已達上限，請檢查設定（PublishAddress / MyRequestUri / SubscribeAddress）");
		//	}, _amqpRetryCts.Token);
		//}
		//public async Task EnsureConnectedAsync(string endpointName, CancellationToken ct = default)
		//{
		//	if (_amqpConnected) return;

		//	_amqpRetryCts?.Cancel();
		//	_amqpRetryCts = CancellationTokenSource.CreateLinkedTokenSource(ct);

		//	_ = Task.Run(async () =>
		//	{
		//		bool canceledOrWaitingModbus = false;

		//		try
		//		{
		//			while (!_amqpConnected && _retryCount < _maxAttempts && !_amqpRetryCts.IsCancellationRequested)
		//			{
		//				if (!_modbusHealthy)
		//				{
		//					try { await Task.Delay(TimeSpan.FromSeconds(5), _amqpRetryCts.Token); } catch { }
		//					continue;
		//				}

		//				_retryCount++;
		//				try
		//				{
		//					StartAmqpEndpoint(endpointName);
		//					bool ok = await SpinWaitUntilConnectedAsync(TimeSpan.FromSeconds(60), _amqpRetryCts.Token);
		//					if (!ok) throw new Exception("handshake timeout");
		//					AppLogger.Info($"[AMQP] 連線成功（第 {_retryCount}/{_maxAttempts} 次）");
		//				}
		//				catch (Exception ex)
		//				{
		//					_amqpConnected = false;
		//					var sec = (int)_retryInterval.TotalSeconds;
		//					AppLogger.Warn($"[AMQP] 連線失敗（第 {_retryCount}/{_maxAttempts} 次，{sec} 秒後再試）：{ex.Message}");
		//					try { await Task.Delay(_retryInterval, _amqpRetryCts.Token); } catch { }
		//				}
		//			}
		//		}
		//		catch (OperationCanceledException)
		//		{
		//			canceledOrWaitingModbus = true;
		//		}
		//		finally
		//		{
		//			if (_amqpRetryCts.IsCancellationRequested || !_modbusHealthy)
		//				canceledOrWaitingModbus = true;
		//		}

		//		// 結束後再決定是否要印“達上限”
		//		if (!canceledOrWaitingModbus && !_amqpConnected && _retryCount >= _maxAttempts)
		//		{
		//			AppLogger.Warn("[AMQP] 重試已達上限，請檢查設定（PublishAddress / MyRequestUri / SubscribeAddress）");
		//		}
		//	}, _amqpRetryCts.Token);
		//}

		public async Task EnsureConnectedAsync(string endpointName, CancellationToken ct = default)
		{
			if (_amqpConnected) return;

			// ★ 避免同時啟動多個連線背景迴圈
			if (System.Threading.Interlocked.CompareExchange(ref _connectLoopGate, 1, 0) != 0) return;

			_amqpRetryCts?.Cancel();
			_amqpRetryCts = CancellationTokenSource.CreateLinkedTokenSource(ct);

			_ = Task.Run(async () =>
			{
				bool canceledOrWaitingModbus = false;

				try
				{
					while (!_amqpConnected && _retryCount < _maxAttempts && !_amqpRetryCts.IsCancellationRequested)
					{
						if (!_modbusHealthy)
						{
							try { await Task.Delay(TimeSpan.FromSeconds(5), _amqpRetryCts.Token); } catch { }
							continue;
						}

						_retryCount++;
						try
						{
							StartAmqpEndpoint(endpointName);
							bool ok = await SpinWaitUntilConnectedAsync(TimeSpan.FromSeconds(60), _amqpRetryCts.Token);
							if (!ok) throw new Exception("handshake timeout");
							AppLogger.Info($"[AMQP] 連線成功（第 {_retryCount}/{_maxAttempts} 次）");
						}
						catch (Exception ex)
						{
							_amqpConnected = false;
							var sec = (int)_retryInterval.TotalSeconds;
							AppLogger.Warn($"[AMQP] 連線失敗（第 {_retryCount}/{_maxAttempts} 次，{sec} 秒後再試）：{ex.Message}");
							try { await Task.Delay(_retryInterval, _amqpRetryCts.Token); } catch { }
						}
					}
				}
				catch (OperationCanceledException)
				{
					canceledOrWaitingModbus = true;
				}
				finally
				{
					if (_amqpRetryCts.IsCancellationRequested || !_modbusHealthy)
						canceledOrWaitingModbus = true;

					// ★ 允許下一次再啟
					System.Threading.Interlocked.Exchange(ref _connectLoopGate, 0);

					if (!canceledOrWaitingModbus && !_amqpConnected && _retryCount >= _maxAttempts)
						AppLogger.Warn("[AMQP] 重試已達上限，請檢查設定（PublishAddress / MyRequestUri / SubscribeAddress）");
				}
			}, _amqpRetryCts.Token);
		}











		public void StartAmqpEndpoint(string endpointName)
		{
			if (string.IsNullOrWhiteSpace(endpointName))
				throw new ArgumentException("endpointName 不能為空白", nameof(endpointName));

			try { endpoint?.Close(); } catch { }

			endpoint = new AmqpCFXEndpoint
			{
				HeartbeatFrequency = TimeSpan.FromSeconds(60)
			};

			endpoint.OnConnectionEvent += HandleConnectionEvent;
			endpoint.OnRequestReceived += OnRequestReceivedHandler;

			_expectedUris.Clear();
			AddExpectedUri(publishChannelUri);
			if (!string.IsNullOrWhiteSpace(subscribeChannelUri))
				AddExpectedUri(subscribeChannelUri);
			foreach (var u in _expectedUris) _uriState[u] = false;

			AppLogger.Info("[AMQP] 端點啟動，建立通道並等待握手…");

			endpoint.Open(endpointName, new Uri(endpointUri));
			endpoint.AddPublishChannel(new Uri(publishChannelUri), "event");
			if (!string.IsNullOrWhiteSpace(subscribeChannelUri))
				endpoint.AddSubscribeChannel(new Uri(subscribeChannelUri), "subscribe");
		}


		// AmqpEndpointManager: 在握手全通過時設定水位
		private DateTime _reconnectedAtUtc;

		//250806修改_偵測到重連成功且 spool > 0 時，清空端點緩衝或重建端點：
		private void HandleConnectionEvent(ConnectionEvent evt, Uri uri, int spool, string info, Exception ex)
		{
			string key = Normalize(uri.ToString());
			bool isUp = evt == ConnectionEvent.ConnectionEstablished;
			_uriState[key] = isUp;
			bool allOk = _expectedUris.All(u => _uriState.TryGetValue(u, out var ok) && ok);



			//if (allOk && !_amqpConnected)
			//{
			//	//_amqpConnected = true;
			//	//AppLogger.Info("[AMQP] ✓ 所有通道握手完成：\n  └ " + string.Join("\n  └ ", _expectedUris));
			//	//PublishSafe(new EndpointConnected());
			//	//TryStartPublishLoop();

			//	 //★ 在這裡處理 spool
			//	if (spool > 0)
			//	{
			//		AppLogger.Warn($"[AMQP] 重連但發現 {spool} 筆舊訊息，丟棄舊緩衝。");
			//		try
			//		{
			//			// 如果 CFX 有提供清空 API（名稱可能叫 ClearSpool/PurgeQueue），直接呼叫；
			//			// 假如沒有，就用「關閉＋重建端點」的方式清掉。
			//			endpoint?.Close();
			//		}
			//		catch { }
			//		// 重建端點，乾淨起步
			//		StartAmqpEndpoint(endpoint?.CFXHandle ?? (SystemConfig.MachineSN ?? "UnknownMachineSN"));
			//	}

			//	_amqpConnected = true;
			//	AppLogger.Info("[AMQP] ✓ 所有通道握手完成：\n  └ " + string.Join("\n  └ ", _expectedUris));
			//	PublishSafe(new EndpointConnected());
			//	TryStartPublishLoop();

			//}

			if (allOk && !_amqpConnected)
			{
				_reconnectedAtUtc = DateTime.UtcNow;   // ★ 設定重連水位
				_amqpConnected = true;
				_reconnectedAtUtc = DateTime.UtcNow;      // ★ 重連水位
				AppLogger.Info("[AMQP] ✓ 所有通道握手完成：\n  └ " + string.Join("\n  └ ", _expectedUris));
				PublishSafe(new EndpointConnected());
				TryStartPublishLoop();
			}




			else if (!allOk && _amqpConnected &&
					 (evt == ConnectionEvent.ConnectionInterrupted ||
					  evt == ConnectionEvent.ConnectionClosed ||
					  evt == ConnectionEvent.ConnectionFailed))
			{
				_amqpConnected = false;
				_publishLoopStarted = false;
				AppLogger.Warn("[AMQP] ✘ 通道中斷：" + evt + " → " + key + "，準備重試…");


				//250806修改===========================
				try { endpoint?.Close(); } catch { } // ★ 立刻關掉，丟掉端點內部緩衝



				if (_modbusHealthy) // 僅在 Modbus 健康時才重試 AMQP
					_ = EnsureConnectedAsync(endpoint.CFXHandle);
			}
		}





		//private void TryStartPublishLoop()
		//{
		//	if (_publishLoopStarted) return;
		//	if (!_amqpConnected) return;
		//	if (!_modbusHealthy) return;


		//	_publishLoopStarted = true;
		//	_ = Task.Run(StartPublishLoop);
		//}

		// TryStartPublishLoop()：換成原子性守門
		private void TryStartPublishLoop()
		{
			if (!_amqpConnected) return;
			if (!_modbusHealthy) return;

			// 如果原本是 0，設為 1 並啟動；否則不啟動
			if (System.Threading.Interlocked.CompareExchange(ref _publishLoopGate, 1, 0) != 0) return;

			_ = Task.Run(StartPublishLoop);
		}



		private async Task<bool> SpinWaitUntilConnectedAsync(TimeSpan timeout, CancellationToken ct)
		{
			var start = DateTime.UtcNow;
			while (DateTime.UtcNow - start < timeout && !_amqpConnected && !ct.IsCancellationRequested)
				await Task.Delay(200, ct);
			return _amqpConnected;
		}




		//private async Task StartPublishLoop()
		//{
		//	var lastFault = new Dictionary<byte, int>();
		//	var lastEnergy = DateTime.MinValue;

		//	AppLogger.Info("[CFX] PublishLoop 啟動");

		//	while (_amqpConnected)
		//	{
		//		// 這裡仍保持二次保護：若中途 Modbus 變不健康，就暫停發布
		//		if (!_modbusHealthy)
		//		{
		//			await Task.Delay(500);
		//			continue;
		//		}

		//		try
		//		{
		//			// 1) 故障（變化才送）
		//			foreach (var stn in slaveData.Keys.ToList())
		//			{
		//				if (slaveData[stn].TryGetValue("Fault_WarningCode", out var obj))
		//				{
		//					int cur = Convert.ToInt32(obj);
		//					if (lastFault.TryGetValue(stn, out var prev))
		//					{
		//						if (prev != cur) await PublishFaultOccurredMessages(stn, endpoint);
		//					}
		//					else
		//					{
		//						if (cur != 0) await PublishFaultOccurredMessages(stn, endpoint);
		//					}
		//					lastFault[stn] = cur;
		//				}
		//			}

		//			// 2) 每 60 秒送一次能耗/參數
		//			if ((DateTime.Now - lastEnergy).TotalSeconds >= 60)
		//			{
		//				foreach (var stn in slaveData.Keys.ToList())
		//				{
		//					PublishStationParametersModifiedMessages(stn, endpoint);
		//					PublishEnergyConsumedMessages(stn, endpoint);
		//				}
		//				lastEnergy = DateTime.Now;
		//			}
		//		}
		//		catch (Exception ex)
		//		{
		//			AppLogger.Error(ex, "[CFX] 發布循環錯誤");
		//		}

		//		await Task.Delay(1000);
		//	}

		//	AppLogger.Warn("[CFX] PublishLoop 結束（AMQP 未連線）");
		//}


		// StartPublishLoop()：保證退出時把 gate 清回 0，並用「下一次到期」排程
		private async Task StartPublishLoop()
		{
			var loopId = System.Threading.Interlocked.Increment(ref _loopIdSeed);
			DateTime nextEnergyDue = DateTime.UtcNow;            // 立刻觸發第一次
			var energyPeriod = TimeSpan.FromSeconds(60);

			AppLogger.Info($"[CFX] PublishLoop 啟動 (LoopId={loopId})");

			try
			{
				var lastFault = new Dictionary<byte, int>();

				while (_amqpConnected)
				{
					if (!_modbusHealthy)
					{
						await Task.Delay(500);
						continue;
					}

					try
					{
						// 1) 故障（變化才送）
						foreach (var stn in slaveData.Keys.ToList())
						{
							if (slaveData[stn].TryGetValue("Fault_WarningCode", out var obj))
							{
								int cur = Convert.ToInt32(obj);
								if (!lastFault.TryGetValue(stn, out var prev) || prev != cur)
								{
									await PublishFaultOccurredMessages(stn, endpoint);
									lastFault[stn] = cur;
								}
							}
						}

						// 2) 能耗/參數：到期才送（嚴格 60s）
						//if (DateTime.UtcNow >= nextEnergyDue)
						//{
						//	foreach (var stn in slaveData.Keys.ToList())
						//	{
						//		PublishStationParametersModifiedMessages(stn, endpoint);
						//		PublishEnergyConsumedMessages(stn, endpoint);
						//	}
						//	nextEnergyDue = DateTime.UtcNow + energyPeriod;
						//}

						if (DateTime.UtcNow >= nextEnergyDue)
						{
							foreach (var stn in slaveData.Keys.ToList())
							{
								PublishStationParametersModifiedMessages(stn, endpoint);
								PublishEnergyConsumedMessages(stn, endpoint, _reconnectedAtUtc);
							}
							nextEnergyDue = DateTime.UtcNow + energyPeriod;
						}



						//// StartPublishLoop 裡送出前過濾
						//if (DateTime.UtcNow >= nextEnergyDue)
						//{
						//	foreach (var stn in slaveData.Keys.ToList())
						//	{
						//		// 只送新資料
						//		if (TryBuildEnergy(stn, out var energyMsg) &&
						//			energyMsg.EndTime.ToUniversalTime() >= _reconnectedAtUtc)
						//		{
						//			PublishSafe(energyMsg);
						//		}
						//	}
						//	nextEnergyDue = DateTime.UtcNow + energyPeriod;
						//}



					}
					catch (Exception ex)
					{
						AppLogger.Error(ex, "[CFX] 發布循環錯誤");
					}

					await Task.Delay(1000);
				}

				AppLogger.Warn("[CFX] PublishLoop 結束（AMQP 未連線）");
			}
			finally
			{
				System.Threading.Interlocked.Exchange(ref _publishLoopGate, 0); // 允許下一次啟動
			}
		}








		private CFXEnvelope OnRequestReceivedHandler(CFXEnvelope request)
		{
			try
			{
				if (request.MessageBody is ModifyStationParametersRequest modifyRequest)
				{
					AppLogger.Info($"[CFX] 收到 ModifyStationParametersRequest: {JsonConvert.SerializeObject(modifyRequest, Formatting.None)}");

					var stationNumbers = modifyRequest.NewParameters
						.OfType<GenericParameter>()
						.Where(g => g.Name == "站號")
						.Select(g => byte.TryParse(g.Value?.ToString(), out var stn) ? (byte?)stn : null)
						.Where(p => p.HasValue)
						.Select(p => p.Value)
						.ToList();

					if (!stationNumbers.Any())
						return CreateErrorResponse(request.RequestID, "No valid station numbers provided.");

					bool hasProcessed = false;
					bool hasError = false;
					bool shouldPublishStateChanged = false;

					foreach (var param in modifyRequest.NewParameters.OfType<GenericParameter>())
					{
						switch (param.Name)
						{
							case "TemperatureSV":
								if (ushort.TryParse(param.Value?.ToString(), out var tempVal))
								{
									foreach (var stn in stationNumbers)
									{
										if (!slaveData.ContainsKey(stn)) { hasError = true; continue; }
										try { modbusViewer.ExecuteSetTemperature(stn, tempVal); hasProcessed = true; }
										catch (Exception ex) { hasError = true; AppLogger.Error(ex, $"[CFX] 設置溫度失敗，站號 {stn}"); }
									}
								}
								break;

							case "EnergyMode":
								if (int.TryParse(param.Value?.ToString(), out var mode) && mode >= 0 && mode <= 2)
								{
									foreach (var stn in stationNumbers)
									{
										if (!slaveData.ContainsKey(stn)) { hasError = true; continue; }
										try
										{
											slaveData[stn]["EnergyMode"] = mode;
											hasProcessed = true;
											if (mode != 0) shouldPublishStateChanged = true;
										}
										catch (Exception ex) { hasError = true; AppLogger.Error(ex, $"[CFX] 設置節能模式失敗，站號 {stn}"); }
									}
								}
								break;

							case "PowerSwitch":
								if (param.Value?.ToString() == "1" || param.Value?.ToString() == "2")
								{
									bool isSwitchOn = param.Value.ToString() == "1";
									foreach (var stn in stationNumbers)
									{
										if (!slaveData.ContainsKey(stn)) { hasError = true; continue; }
										try
										{
											if (isSwitchOn)
											{
												modbusViewer.ExecuteSwitchCommand(ModbusHelper.SwitchON, new List<byte> { stn });
												shouldPublishStateChanged = true;
											}
											else
											{
												modbusViewer.ExecuteSwitchCommand(ModbusHelper.SwitchOFF, new List<byte> { stn });
											}
											hasProcessed = true;
										}
										catch (Exception ex) { hasError = true; AppLogger.Error(ex, $"[CFX] 開關操作失敗，站號 {stn}"); }
									}
								}
								break;
						}
					}

					if (hasError && !hasProcessed)
						return CreateErrorResponse(request.RequestID, "No parameters were processed successfully.");

					if (!hasProcessed)
						return CreateErrorResponse(request.RequestID, "No valid actions were performed.");

					var successEnvelope = CreateSuccessResponse(request.RequestID, "Parameters updated successfully.");

					if (shouldPublishStateChanged)
					{
						var stateChangedMessage = new StationStateChanged { NewState = ResourceState.NST_ShutdownAndStartup };
						PublishSafe(stateChangedMessage);
					}

					return successEnvelope;
				}
				else
				{
					return CreateErrorResponse(request.RequestID, "Unsupported request type.");
				}
			}
			catch (Exception ex)
			{
				AppLogger.Error(ex, "[CFX] Request 處理錯誤");
				return CreateErrorResponse(request.RequestID, ex.Message);
			}
		}

		private CFXEnvelope CreateSuccessResponse(string requestId, string message)
		{
			var response = new ModifyStationParametersResponse
			{
				Result = new RequestResult
				{
					Result = StatusResult.Success,
					ResultCode = 1,
					Message = message
				}
			};
			var env = CFXEnvelope.FromCFXMessage(response);
			env.RequestID = requestId;
			return env;
		}

		private CFXEnvelope CreateErrorResponse(string requestId, string errorMessage)
		{
			var response = new NotSupportedResponse
			{
				RequestResult = new RequestResult
				{
					Result = StatusResult.Failed,
					ResultCode = 2,
					Message = errorMessage
				}
			};
			var env = CFXEnvelope.FromCFXMessage(response);
			env.RequestID = requestId;
			return env;
		}

		private async Task PublishFaultOccurredMessages(byte stationNumber, AmqpCFXEndpoint endpoint)
		{
			if (!slaveData.TryGetValue(stationNumber, out var data) || !data.ContainsKey("Fault_WarningCode"))
				return;

			int statusRegister = Convert.ToInt32(data["Fault_WarningCode"]);

			for (int bitPosition = 0; bitPosition <= 27; bitPosition++)
			{
				if (((statusRegister) & (1 << bitPosition)) != 0 && faultDictionary.ContainsKey(bitPosition))
				{
					var (faultCode, description) = faultDictionary[bitPosition];
					var faultOccurred = new FaultOccurred
					{
						Fault = new Fault
						{
							FaultCode = faultCode,
							FaultOccurrenceId = Guid.NewGuid(),
							Description = description,
							OccurredAt = DateTime.Now,
							Severity = FaultSeverity.Error
						}
					};
					await Task.Run(() => PublishSafe(faultOccurred));
				}
			}
		}

		private void PublishStationParametersModifiedMessages(byte stationNumber, AmqpCFXEndpoint endpoint)
		{
			if (!slaveData.TryGetValue(stationNumber, out var data) || data == null || !data.Any())
				return;

			bool anyNonZero = data.Values.Any(obj =>
			{
				if (obj == null) return false;
				if (obj is int i) return i != 0;
				if (obj is double d) return Math.Abs(d) > 0.000001;
				return false;
			});
			if (!anyNonZero) return;

			var parameters = new List<Parameter>
			{
				new GenericParameter
				{
					Name = "PowerSwitch",
					Value = ConvertPowerSwitchToState(data.ContainsKey("PowerSwitch") ? Convert.ToInt32(data["PowerSwitch"]) : 0).ToString()
				},
				new GenericParameter { Name = "EnergyMode",       Value = data.ContainsKey("EnergyMode") ? data["EnergyMode"].ToString() : "0" },
				new GenericParameter { Name = "PowerTemperatureA",Value = data.ContainsKey("PowerTemperature_A") ? data["PowerTemperature_A"].ToString() : "0" },
				new GenericParameter { Name = "PowerTemperatureB",Value = data.ContainsKey("PowerTemperature_B") ? data["PowerTemperature_B"].ToString() : "0" },
				new GenericParameter { Name = "PowerTemperatureC",Value = data.ContainsKey("PowerTemperature_C") ? data["PowerTemperature_C"].ToString() : "0" },
			};

			PublishSafe(new StationParametersModified { ModifiedParameters = parameters });
		}

		//private void PublishEnergyConsumedMessages(byte stationNumber, AmqpCFXEndpoint endpoint)
		//{
		//	if (!slaveData.TryGetValue(stationNumber, out var data) || data == null || !data.Any())
		//		return;

		//	bool anyNonZero = data.Values.Any(obj =>
		//	{
		//		if (obj == null) return false;
		//		if (obj is int i) return i != 0;
		//		if (obj is double d) return Math.Abs(d) > 0.000001;
		//		return false;
		//	});
		//	if (!anyNonZero) return;

		//	double currentA = ConvertToDouble(data, "CurrentRYB_A");
		//	double currentB = ConvertToDouble(data, "CurrentRYB_B");
		//	double currentC = ConvertToDouble(data, "CurrentRYB_C");
		//	var currentRYB = new List<double> { currentA, currentB, currentC };

		//	double powerA = ConvertToDouble(data, "PowerRYB_A");
		//	double powerB = ConvertToDouble(data, "PowerRYB_B");
		//	double powerC = ConvertToDouble(data, "PowerRYB_C");
		//	var powerRYB = new List<double> { powerA, powerB, powerC };

		//	double energyUsed = ConvertToDouble(data, "EnergyUsed");

		//	var energyConsumed = new EnergyConsumed
		//	{
		//		EnergyUsed = energyUsed,
		//		StartTime = DateTime.Now,
		//		EndTime = DateTime.Now,
		//		CurrentNowRYB = currentRYB,
		//		PowerNowRYB = powerRYB,
		//	};

		//	PublishSafe(energyConsumed);
		//	AppLogger.Info($"[CFX] Publish st.{stationNumber} EnergyConsumed: {energyUsed:F2} kWh → LM");
		//}
		private void PublishEnergyConsumedMessages(byte stationNumber, AmqpCFXEndpoint endpoint, DateTime notBeforeUtc)
		{
			if (!slaveData.TryGetValue(stationNumber, out var data) || data == null || !data.Any())
				return;

			bool anyNonZero = data.Values.Any(obj =>
			{
				if (obj == null) return false;
				if (obj is int i) return i != 0;
				if (obj is double d) return Math.Abs(d) > 0.000001;
				return false;
			});
			if (!anyNonZero) return;

			double currentA = ConvertToDouble(data, "CurrentRYB_A");
			double currentB = ConvertToDouble(data, "CurrentRYB_B");
			double currentC = ConvertToDouble(data, "CurrentRYB_C");
			var currentRYB = new List<double> { currentA, currentB, currentC };

			double powerA = ConvertToDouble(data, "PowerRYB_A");
			double powerB = ConvertToDouble(data, "PowerRYB_B");
			double powerC = ConvertToDouble(data, "PowerRYB_C");
			var powerRYB = new List<double> { powerA, powerB, powerC };

			double energyUsed = ConvertToDouble(data, "EnergyUsed");

			var nowUtc = DateTime.UtcNow;
			var energyConsumed = new EnergyConsumed
			{
				EnergyUsed = energyUsed,
				StartTime = nowUtc,
				EndTime = nowUtc,
				CurrentNowRYB = currentRYB,
				PowerNowRYB = powerRYB,
			};

			var msgTimeUtc = energyConsumed.EndTime.ToUniversalTime();
			if (msgTimeUtc >= notBeforeUtc)
			{
				PublishSafe(energyConsumed);
				AppLogger.Info($"[CFX] Publish st.{stationNumber} EnergyConsumed: {energyUsed:F2} kWh → LM");
			}
			else
			{
				LogThrottler.Every("energy_skip_old_" + stationNumber, 60,
					() => AppLogger.Info($"[CFX] Skip old EnergyConsumed st.{stationNumber} {msgTimeUtc:o} < watermark {notBeforeUtc:o}"));
			}
		}






		private double ConvertToDouble(Dictionary<string, object> data, string key)
		{
			if (!data.ContainsKey(key)) return 0.0;
			var obj = data[key];
			if (obj == null) return 0.0;
			if (obj is double dd) return dd;
			if (obj is int ii) return ii;
			return double.TryParse(obj.ToString(), out var result) ? result : 0.0;
		}

		public static int ConvertPowerSwitchToState(int powerSwitchValue)
		{
			// 這裡保留你原本的轉換邏輯（0/1 對應），以免相容性問題
			if (powerSwitchValue == 1) return 1; // On
			if (powerSwitchValue == 0) return 2; // Off
			return -1;
		}
	}
}
