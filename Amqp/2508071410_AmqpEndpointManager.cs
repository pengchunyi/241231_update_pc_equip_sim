////// AmqpEndpointManager.cs
//using CFX;
//using CFX.Production;
//using CFX.ResourcePerformance;
//using CFX.Structures;
//using CFX.Transport;
//using Newtonsoft.Json;
//using System;
//using System.Collections.Concurrent;
//using System.Collections.Generic;
//using System.IO.Ports;
//using System.Linq;
//using System.Net.Sockets;
//using System.Reflection;
//using System.Threading;
//using System.Threading.Tasks;

//namespace AmqpModbusIntegration
//{
//	//public class AmqpEndpointManager
//	public class AmqpEndpointManager : IDisposable
//	{
//		private readonly string endpointUri;
//		private readonly string publishChannelUri;
//		private readonly string subscribeChannelUri;

//		private readonly ModbusViewer modbusViewer;
//		private readonly SerialPort serialPort; // 目前未用
//		private readonly Dictionary<byte, Dictionary<string, object>> slaveData;

//		private volatile bool _amqpConnected = false;
//		private int _retryCount = 0;
//		private const int _maxAttempts = 9999;
//		private readonly TimeSpan _retryInterval = TimeSpan.FromMinutes(1);
//		private CancellationTokenSource _amqpRetryCts;


//		private AmqpCFXEndpoint endpoint;
//		private readonly HashSet<string> _expectedUris = new HashSet<string>();
//		private readonly ConcurrentDictionary<string, bool> _uriState = new ConcurrentDictionary<string, bool>();


//		private volatile bool _modbusHealthy = false;


//		//250807_新增關於連線監測變數====================================
//		private volatile bool _publishLoopStarted = false;
//		private Timer _watchdogTimer;          // ← 如果你後面另有 Timer 就改成你的變數名
//		private DateTime _reconnectedAtUtc;
//		// ★ 新增：Idle watchdog 相關欄位
//		private readonly CancellationTokenSource _idleWatchdogCts = new CancellationTokenSource();
//		private DateTime _lastSendUtc = DateTime.UtcNow;
//		private volatile bool _kaSet = false;   // 已成功設過 Keep-Alive 就不重複
//												//250807_新增關於連線監測變數====================================


//		// 類別欄位
//		private int _publishLoopGate = 0;   // 0=沒有 loop, 1=已有 loop
//		private int _connectLoopGate = 0;   // ★ 避免同時多個 EnsureConnected 背景迴圈
//		private long _loopIdSeed = 0;       // 觀察/除錯用

//		private static readonly Dictionary<int, (string Code, string ErrorDescription)> faultDictionary =
//			new Dictionary<int, (string Code, string ErrorDescription)>
//		{
//			{ 0,  ("EGY_1_WARN1",  "A相過壓") },
//			{ 1,  ("EGY_1_WARN2",  "B相過壓") },
//			{ 2,  ("EGY_1_WARN3",  "C相過壓") },
//			{ 3,  ("EGY_1_WARN4",  "A相欠壓") },
//			{ 4,  ("EGY_1_WARN5",  "B相欠壓") },
//			{ 5,  ("EGY_1_WARN6",  "C相過流") },
//			{ 6,  ("EGY_1_WARN7",  "A相過流") },
//			{ 7,  ("EGY_1_WARN8",  "B相過流") },
//			{ 8,  ("EGY_1_WARN9",  "C相過流") },
//			{ 9,  ("EGY_1_WARN10", "漏電異常") },
//			{ 10, ("EGY_1_WARN11", "A相出線溫度異常") },
//			{ 11, ("EGY_1_WARN12", "B相出線溫度異常") },
//			{ 12, ("EGY_1_WARN13", "C相出線溫度異常") },
//			{ 13, ("EGY_1_WARN14", "N相出線溫度異常") },
//			{ 14, ("EGY_1_WARN15", "電弧") },
//			{ 15, ("EGY_1_WARN16", "缺相") },
//			{ 16, ("EGY_1_WARN17", "斷零") },
//			{ 17, ("EGY_1_WARN18", "三相電壓不平衡") },
//			{ 18, ("EGY_1_WARN19", "鎖定") },
//			{ 19, ("EGY_1_WARN20", "進入維修或手動模式") },
//			{ 20, ("EGY_1_WARN21", "開關狀態異常，提示客戶換設備") },
//			{ 21, ("EGY_1_WARN22", "漏電功能壞，提示客戶換設備") },
//			{ 22, ("EGY_1_WARN23", "設備離線") },
//			{ 23, ("EGY_1_WARN24", "過壓預警") },
//			{ 24, ("EGY_1_WARN25", "欠壓預警") },
//			{ 25, ("EGY_1_WARN26", "過流預警") },
//			{ 26, ("EGY_1_WARN27", "過溫預警") }
//		};

//		// 片段：AmqpEndpointManager 內
//		private readonly object _publishLock = new object();
//		private void PublishSafe(CFXMessage msg)
//		{
//			var ep = endpoint;
//			if (ep == null || !_amqpConnected) return;
//			lock (_publishLock)
//			{
//				if (!_amqpConnected) return;
//				try
//				{
//					ep.Publish(msg);
//					//250807新增關於連線監測==========================
//					_lastSendUtc = DateTime.UtcNow;      // ★ update 水位
//														 //250807新增關於連線監測==========================
//				}
//				catch (InvalidOperationException ex)
//				{
//					// CFX 內部集合列舉衝突（握手/通道切換同時發生）→ 吞掉一次避免打斷循環
//					LogThrottler.Every("cfx_pub_enum", 5,
//						delegate { AppLogger.Warn("[CFX] 發布循環內部列舉衝突（已略過一次）： " + ex.Message); });
//				}
//				catch (Exception ex)
//				{
//					AppLogger.Error(ex, "[CFX] Publish 例外");
//				}
//			}
//		}


//		public AmqpEndpointManager(
//			string endpointUri,
//			string publishChannelUri,
//			string subscribeChannelUri,
//			ModbusViewer modbusViewer,
//			SerialPort serialPort,
//			Dictionary<byte, Dictionary<string, object>> slaveData)
//		{
//			this.endpointUri = endpointUri;
//			this.publishChannelUri = publishChannelUri;
//			this.subscribeChannelUri = string.IsNullOrWhiteSpace(subscribeChannelUri) ? null : subscribeChannelUri;

//			this.modbusViewer = modbusViewer;
//			this.serialPort = serialPort;
//			this.slaveData = slaveData;

//			_expectedUris.Clear();
//			AddExpectedUri(publishChannelUri);
//			if (!string.IsNullOrWhiteSpace(this.subscribeChannelUri))
//				AddExpectedUri(this.subscribeChannelUri);
//		}

//		private static string Normalize(string u)
//		{
//			if (string.IsNullOrWhiteSpace(u)) return null;
//			return new Uri(u).ToString();
//		}

//		private void AddExpectedUri(string u)
//		{
//			var n = Normalize(u);
//			if (!string.IsNullOrWhiteSpace(n))
//				_expectedUris.Add(n);
//		}




//		public void SetModbusHealthy(bool healthy)
//		{
//			if (_modbusHealthy == healthy) return;
//			_modbusHealthy = healthy;

//			AppLogger.Info(healthy ? "[MODBUS] 恢復連線，恢復 CFX 發布。" : "[MODBUS] 中斷，暫停 CFX 發布。");

//			if (!healthy)
//			{
//				_publishLoopStarted = false;
//				_amqpConnected = false;
//				try { _amqpRetryCts?.Cancel(); } catch { }
//				try { endpoint?.Close(); } catch { }
//				AppLogger.Info("[AMQP] 已停止連線與重試（等待 Modbus 恢復）。");
//				return;
//			}

//			if (!_amqpConnected)
//				_ = EnsureConnectedAsync(SystemConfig.MachineSN ?? "UnknownMachineSN");

//			TryStartPublishLoop();
//		}

//		public async Task EnsureConnectedAsync(string endpointName, CancellationToken ct = default)
//		{
//			if (_amqpConnected) return;

//			// ★ 避免同時啟動多個連線背景迴圈
//			if (System.Threading.Interlocked.CompareExchange(ref _connectLoopGate, 1, 0) != 0) return;

//			_amqpRetryCts?.Cancel();
//			_amqpRetryCts = CancellationTokenSource.CreateLinkedTokenSource(ct);

//			_ = Task.Run(async () =>
//			{
//				bool canceledOrWaitingModbus = false;

//				try
//				{
//					while (!_amqpConnected && _retryCount < _maxAttempts && !_amqpRetryCts.IsCancellationRequested)
//					{
//						if (!_modbusHealthy)
//						{
//							try { await Task.Delay(TimeSpan.FromSeconds(5), _amqpRetryCts.Token); } catch { }
//							continue;
//						}

//						_retryCount++;
//						try
//						{
//							StartAmqpEndpoint(endpointName);
//							_kaSet = false;                 // ★ 每次新建 endpoint 先清旗標
//							bool ok = await SpinWaitUntilConnectedAsync(TimeSpan.FromSeconds(60), _amqpRetryCts.Token);
//							if (!ok) throw new Exception("handshake timeout");
//							AppLogger.Info($"[AMQP] 連線成功（第 {_retryCount}/{_maxAttempts} 次）");
//						}
//						catch (Exception ex)
//						{
//							_amqpConnected = false;
//							var sec = (int)_retryInterval.TotalSeconds;
//							AppLogger.Warn($"[AMQP] 連線失敗（第 {_retryCount}/{_maxAttempts} 次，{sec} 秒後再試）：{ex.Message}");
//							try { await Task.Delay(_retryInterval, _amqpRetryCts.Token); } catch { }
//						}
//					}
//				}
//				catch (OperationCanceledException)
//				{
//					canceledOrWaitingModbus = true;
//				}
//				finally
//				{
//					if (_amqpRetryCts.IsCancellationRequested || !_modbusHealthy)
//						canceledOrWaitingModbus = true;

//					// ★ 允許下一次再啟
//					System.Threading.Interlocked.Exchange(ref _connectLoopGate, 0);

//					if (!canceledOrWaitingModbus && !_amqpConnected && _retryCount >= _maxAttempts)
//						AppLogger.Warn("[AMQP] 重試已達上限，請檢查設定（PublishAddress / MyRequestUri / SubscribeAddress）");
//				}
//			}, _amqpRetryCts.Token);
//		}




//		public void StartAmqpEndpoint(string endpointName)
//		{
//			if (string.IsNullOrWhiteSpace(endpointName))
//				throw new ArgumentException("endpointName 不能為空白", nameof(endpointName));

//			try { endpoint?.Close(); } catch { }

//			endpoint = new AmqpCFXEndpoint
//			{
//				HeartbeatFrequency = TimeSpan.FromSeconds(60)
//			};

//			endpoint.OnConnectionEvent += HandleConnectionEvent;
//			endpoint.OnRequestReceived += OnRequestReceivedHandler;

//			_expectedUris.Clear();
//			AddExpectedUri(publishChannelUri);
//			if (!string.IsNullOrWhiteSpace(subscribeChannelUri))
//				AddExpectedUri(subscribeChannelUri);
//			foreach (var u in _expectedUris) _uriState[u] = false;

//			AppLogger.Info("[AMQP] 端點啟動，建立通道並等待握手…");

//			endpoint.Open(endpointName, new Uri(endpointUri));
//			endpoint.AddPublishChannel(new Uri(publishChannelUri), "event");
//			if (!string.IsNullOrWhiteSpace(subscribeChannelUri))
//				endpoint.AddSubscribeChannel(new Uri(subscribeChannelUri), "subscribe");


//			// ★★ 這裡不再設定 Keep-Alive，也不再寫「已啟用」的 Log ★★
//			//     真正設定挪到 HandleConnectionEvent(ConnectionEstablished) 裡
//			//     如果想保險，可保留：
//			//     AppLogger.Info("[AMQP] 已送出 Open()，等待 ConnectionEstablished…");
//			////250807新增關於連線監測==========================
//			////// --- ① 讓底層 TCP 有 Keep‑Alive frame ---
//			////try { KeepAliveHelper.EnableKeepAlive(endpoint.Connection?.Socket); } catch { }
//			////  30 秒 idle → 開始探測，每 10 秒發 1 probe
//			//try
//			//{
//			//	var sock = ExtractSocket(endpoint);
//			//	KeepAliveHelper.EnableKeepAlive(sock, 30, 10);
//			//	AppLogger.Info("[AMQP] 已啟用 TCP KeepAlive：Idle=30s, Interval=10s");

//			//}
//			//catch { /* swallow – 若反射失敗不影響主流程 */ }

//			// --- ② 啟動 idle watchdog (只啟一次) ---
//			_ = Task.Run(IdleWatchdogLoop, _idleWatchdogCts.Token);
//			//250807新增關於連線監測==========================
//		}


//		//250807新增關於連線監測==========================
//		// ★ 變動 3：IdleWatchdogLoop 實作
//		private async Task IdleWatchdogLoop()
//		{
//			while (!_idleWatchdogCts.IsCancellationRequested)
//			{
//				try
//				{
//					if (_amqpConnected && (DateTime.UtcNow - _lastSendUtc) > TimeSpan.FromSeconds(90))
//					{
//						AppLogger.Warn("[AMQP] 消息發布 idle >90s, force reconnect");
//						try { endpoint?.Close(); } catch { }
//						_amqpConnected = false;
//						//_publishLoopStarted = false;
//						if (_modbusHealthy)
//							_ = EnsureConnectedAsync(endpoint?.CFXHandle ?? "Unknown");
//					}
//				}
//				catch { /* swallow */ }

//				await Task.Delay(30_000, _idleWatchdogCts.Token);
//			}
//		}
//		//250807新增關於連線監測==========================


//		//// AmqpEndpointManager: 在握手全通過時設定水位
//		//private DateTime _reconnectedAtUtc;

//		//250806修改_偵測到重連成功且 spool > 0 時，清空端點緩衝或重建端點：
//		private void HandleConnectionEvent(ConnectionEvent evt, Uri uri, int spool, string info, Exception ex)
//		{



//			string key = Normalize(uri.ToString());
//			bool isUp = evt == ConnectionEvent.ConnectionEstablished;
//			_uriState[key] = isUp;
//			bool allOk = _expectedUris.All(u => _uriState.TryGetValue(u, out var ok) && ok);


//			// ===== 在「單一路徑完成」時，先設定 Keep-Alive =====
//			if (evt == ConnectionEvent.ConnectionEstablished && !_kaSet)
//			{
//				// 最多 retry 2 次，間隔 100 ms ── 處理「事件很早就來」的情況
//				for (int i = 0; i < 2 && !_kaSet; i++)
//				{
//					var sock = ExtractSocket(endpoint);
//					if (sock != null)
//					{
//						try
//						{
//							KeepAliveHelper.EnableKeepAlive(sock, 30, 10);
//							AppLogger.Info("[AMQP] TCP KeepAlive 已啟用：Idle=30s, Interval=10s");
//							_kaSet = true;
//						}
//						catch (Exception e)
//						{
//							AppLogger.Warn($"[AMQP] 設定 KeepAlive 失敗：{e.Message}");
//						}
//					}
//					else if (i == 0)      // 第一次沒抓到，等 100 ms 再試一次
//					{
//						await Task.Delay(100);
//					}
//					else                  // 第二次還是抓不到
//					{
//						AppLogger.Warn("[AMQP] 取得 Socket 失敗，無法設定 KeepAlive");
//					}
//				}
//			}
//			// ====================================================


//			if (allOk && !_amqpConnected)
//			{
//				_reconnectedAtUtc = DateTime.UtcNow;   // ★ 設定重連水位
//				_amqpConnected = true;
//				_reconnectedAtUtc = DateTime.UtcNow;      // ★ 重連水位
//				AppLogger.Info("[AMQP] ✓ 所有通道握手完成：\n  └ " + string.Join("\n  └ ", _expectedUris));
//				PublishSafe(new EndpointConnected());
//				TryStartPublishLoop();
//			}

//			else if (!allOk && _amqpConnected &&
//					 (evt == ConnectionEvent.ConnectionInterrupted ||
//					  evt == ConnectionEvent.ConnectionClosed ||
//					  evt == ConnectionEvent.ConnectionFailed))
//			{
//				_amqpConnected = false;
//				_publishLoopStarted = false;
//				AppLogger.Warn("[AMQP] ✘ 通道中斷：" + evt + " → " + key + "，準備重試…");

//				//250806修改===========================
//				try { endpoint?.Close(); } catch { } // ★ 立刻關掉，丟掉端點內部緩衝

//				if (_modbusHealthy) // 僅在 Modbus 健康時才重試 AMQP
//					_ = EnsureConnectedAsync(endpoint.CFXHandle);
//			}
//		}


//		// TryStartPublishLoop()：換成原子性守門
//		private void TryStartPublishLoop()
//		{
//			if (!_amqpConnected) return;
//			if (!_modbusHealthy) return;

//			// 如果原本是 0，設為 1 並啟動；否則不啟動
//			if (System.Threading.Interlocked.CompareExchange(ref _publishLoopGate, 1, 0) != 0) return;

//			_ = Task.Run(StartPublishLoop);
//		}



//		private async Task<bool> SpinWaitUntilConnectedAsync(TimeSpan timeout, CancellationToken ct)
//		{
//			var start = DateTime.UtcNow;
//			while (DateTime.UtcNow - start < timeout && !_amqpConnected && !ct.IsCancellationRequested)
//				await Task.Delay(200, ct);
//			return _amqpConnected;
//		}



//		// StartPublishLoop()：保證退出時把 gate 清回 0，並用「下一次到期」排程
//		private async Task StartPublishLoop()
//		{
//			var loopId = System.Threading.Interlocked.Increment(ref _loopIdSeed);
//			DateTime nextEnergyDue = DateTime.UtcNow;            // 立刻觸發第一次
//			var energyPeriod = TimeSpan.FromSeconds(60);


//			//測試watchdog用的
//			//var energyPeriod = TimeSpan.FromSeconds(120);

//			AppLogger.Info($"[CFX] PublishLoop 啟動 (LoopId={loopId})");

//			try
//			{
//				var lastFault = new Dictionary<byte, int>();

//				while (_amqpConnected)
//				{
//					if (!_modbusHealthy)
//					{
//						await Task.Delay(500);
//						continue;
//					}

//					try
//					{
//						// 1) 故障（變化才送）
//						foreach (var stn in slaveData.Keys.ToList())
//						{
//							if (slaveData[stn].TryGetValue("Fault_WarningCode", out var obj))
//							{
//								int cur = Convert.ToInt32(obj);
//								if (!lastFault.TryGetValue(stn, out var prev) || prev != cur)
//								{
//									await PublishFaultOccurredMessages(stn, endpoint);
//									lastFault[stn] = cur;
//								}
//							}
//						}

//						// 2) 能耗/參數：到期才送（嚴格 60s）
//						if (DateTime.UtcNow >= nextEnergyDue)
//						{
//							foreach (var stn in slaveData.Keys.ToList())
//							{
//								PublishStationParametersModifiedMessages(stn, endpoint);
//								PublishEnergyConsumedMessages(stn, endpoint, _reconnectedAtUtc);
//							}
//							nextEnergyDue = DateTime.UtcNow + energyPeriod;
//						}


//					}
//					catch (Exception ex)
//					{
//						AppLogger.Error(ex, "[CFX] 發布循環錯誤");
//					}

//					await Task.Delay(1000);
//				}

//				AppLogger.Warn("[CFX] PublishLoop 結束（AMQP 未連線）");
//			}
//			finally
//			{
//				System.Threading.Interlocked.Exchange(ref _publishLoopGate, 0); // 允許下一次啟動
//			}
//		}








//		private CFXEnvelope OnRequestReceivedHandler(CFXEnvelope request)
//		{
//			try
//			{
//				if (request.MessageBody is ModifyStationParametersRequest modifyRequest)
//				{
//					AppLogger.Info($"[CFX] 收到 ModifyStationParametersRequest: {JsonConvert.SerializeObject(modifyRequest, Formatting.None)}");

//					var stationNumbers = modifyRequest.NewParameters
//						.OfType<GenericParameter>()
//						.Where(g => g.Name == "站號")
//						.Select(g => byte.TryParse(g.Value?.ToString(), out var stn) ? (byte?)stn : null)
//						.Where(p => p.HasValue)
//						.Select(p => p.Value)
//						.ToList();

//					if (!stationNumbers.Any())
//						return CreateErrorResponse(request.RequestID, "No valid station numbers provided.");

//					bool hasProcessed = false;
//					bool hasError = false;
//					bool shouldPublishStateChanged = false;

//					foreach (var param in modifyRequest.NewParameters.OfType<GenericParameter>())
//					{
//						switch (param.Name)
//						{
//							case "TemperatureSV":
//								if (ushort.TryParse(param.Value?.ToString(), out var tempVal))
//								{
//									foreach (var stn in stationNumbers)
//									{
//										if (!slaveData.ContainsKey(stn)) { hasError = true; continue; }
//										try { modbusViewer.ExecuteSetTemperature(stn, tempVal); hasProcessed = true; }
//										catch (Exception ex) { hasError = true; AppLogger.Error(ex, $"[CFX] 設置溫度失敗，站號 {stn}"); }
//									}
//								}
//								break;

//							case "EnergyMode":
//								if (int.TryParse(param.Value?.ToString(), out var mode) && mode >= 0 && mode <= 2)
//								{
//									foreach (var stn in stationNumbers)
//									{
//										if (!slaveData.ContainsKey(stn)) { hasError = true; continue; }
//										try
//										{
//											slaveData[stn]["EnergyMode"] = mode;
//											hasProcessed = true;
//											if (mode != 0) shouldPublishStateChanged = true;
//										}
//										catch (Exception ex) { hasError = true; AppLogger.Error(ex, $"[CFX] 設置節能模式失敗，站號 {stn}"); }
//									}
//								}
//								break;

//							case "PowerSwitch":
//								if (param.Value?.ToString() == "1" || param.Value?.ToString() == "2")
//								{
//									bool isSwitchOn = param.Value.ToString() == "1";
//									foreach (var stn in stationNumbers)
//									{
//										if (!slaveData.ContainsKey(stn)) { hasError = true; continue; }
//										try
//										{
//											if (isSwitchOn)
//											{
//												modbusViewer.ExecuteSwitchCommand(ModbusHelper.SwitchON, new List<byte> { stn });
//												shouldPublishStateChanged = true;
//											}
//											else
//											{
//												modbusViewer.ExecuteSwitchCommand(ModbusHelper.SwitchOFF, new List<byte> { stn });
//											}
//											hasProcessed = true;
//										}
//										catch (Exception ex) { hasError = true; AppLogger.Error(ex, $"[CFX] 開關操作失敗，站號 {stn}"); }
//									}
//								}
//								break;
//						}
//					}

//					if (hasError && !hasProcessed)
//						return CreateErrorResponse(request.RequestID, "No parameters were processed successfully.");

//					if (!hasProcessed)
//						return CreateErrorResponse(request.RequestID, "No valid actions were performed.");

//					var successEnvelope = CreateSuccessResponse(request.RequestID, "Parameters updated successfully.");

//					if (shouldPublishStateChanged)
//					{
//						var stateChangedMessage = new StationStateChanged { NewState = ResourceState.NST_ShutdownAndStartup };
//						PublishSafe(stateChangedMessage);
//					}

//					return successEnvelope;
//				}
//				else
//				{
//					return CreateErrorResponse(request.RequestID, "Unsupported request type.");
//				}
//			}
//			catch (Exception ex)
//			{
//				AppLogger.Error(ex, "[CFX] Request 處理錯誤");
//				return CreateErrorResponse(request.RequestID, ex.Message);
//			}
//		}

//		private CFXEnvelope CreateSuccessResponse(string requestId, string message)
//		{
//			var response = new ModifyStationParametersResponse
//			{
//				Result = new RequestResult
//				{
//					Result = StatusResult.Success,
//					ResultCode = 1,
//					Message = message
//				}
//			};
//			var env = CFXEnvelope.FromCFXMessage(response);
//			env.RequestID = requestId;
//			return env;
//		}

//		private CFXEnvelope CreateErrorResponse(string requestId, string errorMessage)
//		{
//			var response = new NotSupportedResponse
//			{
//				RequestResult = new RequestResult
//				{
//					Result = StatusResult.Failed,
//					ResultCode = 2,
//					Message = errorMessage
//				}
//			};
//			var env = CFXEnvelope.FromCFXMessage(response);
//			env.RequestID = requestId;
//			return env;
//		}

//		private async Task PublishFaultOccurredMessages(byte stationNumber, AmqpCFXEndpoint endpoint)
//		{
//			if (!slaveData.TryGetValue(stationNumber, out var data) || !data.ContainsKey("Fault_WarningCode"))
//				return;

//			int statusRegister = Convert.ToInt32(data["Fault_WarningCode"]);

//			for (int bitPosition = 0; bitPosition <= 27; bitPosition++)
//			{
//				if (((statusRegister) & (1 << bitPosition)) != 0 && faultDictionary.ContainsKey(bitPosition))
//				{
//					var (faultCode, description) = faultDictionary[bitPosition];
//					var faultOccurred = new FaultOccurred
//					{
//						Fault = new Fault
//						{
//							FaultCode = faultCode,
//							FaultOccurrenceId = Guid.NewGuid(),
//							Description = description,
//							OccurredAt = DateTime.Now,
//							Severity = FaultSeverity.Error
//						}
//					};
//					await Task.Run(() => PublishSafe(faultOccurred));
//				}
//			}
//		}

//		private void PublishStationParametersModifiedMessages(byte stationNumber, AmqpCFXEndpoint endpoint)
//		{
//			if (!slaveData.TryGetValue(stationNumber, out var data) || data == null || !data.Any())
//				return;

//			bool anyNonZero = data.Values.Any(obj =>
//			{
//				if (obj == null) return false;
//				if (obj is int i) return i != 0;
//				if (obj is double d) return Math.Abs(d) > 0.000001;
//				return false;
//			});
//			if (!anyNonZero) return;

//			var parameters = new List<Parameter>
//			{
//				new GenericParameter
//				{
//					Name = "PowerSwitch",
//					Value = ConvertPowerSwitchToState(data.ContainsKey("PowerSwitch") ? Convert.ToInt32(data["PowerSwitch"]) : 0).ToString()
//				},
//				new GenericParameter { Name = "EnergyMode",       Value = data.ContainsKey("EnergyMode") ? data["EnergyMode"].ToString() : "0" },
//				new GenericParameter { Name = "PowerTemperatureA",Value = data.ContainsKey("PowerTemperature_A") ? data["PowerTemperature_A"].ToString() : "0" },
//				new GenericParameter { Name = "PowerTemperatureB",Value = data.ContainsKey("PowerTemperature_B") ? data["PowerTemperature_B"].ToString() : "0" },
//				new GenericParameter { Name = "PowerTemperatureC",Value = data.ContainsKey("PowerTemperature_C") ? data["PowerTemperature_C"].ToString() : "0" },
//			};

//			PublishSafe(new StationParametersModified { ModifiedParameters = parameters });
//		}

//		private void PublishEnergyConsumedMessages(byte stationNumber, AmqpCFXEndpoint endpoint, DateTime notBeforeUtc)
//		{
//			if (!slaveData.TryGetValue(stationNumber, out var data) || data == null || !data.Any())
//				return;

//			bool anyNonZero = data.Values.Any(obj =>
//			{
//				if (obj == null) return false;
//				if (obj is int i) return i != 0;
//				if (obj is double d) return Math.Abs(d) > 0.000001;
//				return false;
//			});
//			if (!anyNonZero) return;

//			double currentA = ConvertToDouble(data, "CurrentRYB_A");
//			double currentB = ConvertToDouble(data, "CurrentRYB_B");
//			double currentC = ConvertToDouble(data, "CurrentRYB_C");
//			var currentRYB = new List<double> { currentA, currentB, currentC };

//			double powerA = ConvertToDouble(data, "PowerRYB_A");
//			double powerB = ConvertToDouble(data, "PowerRYB_B");
//			double powerC = ConvertToDouble(data, "PowerRYB_C");
//			var powerRYB = new List<double> { powerA, powerB, powerC };

//			double energyUsed = ConvertToDouble(data, "EnergyUsed");

//			var nowUtc = DateTime.UtcNow;
//			var energyConsumed = new EnergyConsumed
//			{
//				EnergyUsed = energyUsed,
//				StartTime = nowUtc,
//				EndTime = nowUtc,
//				CurrentNowRYB = currentRYB,
//				PowerNowRYB = powerRYB,
//			};

//			var msgTimeUtc = energyConsumed.EndTime.ToUniversalTime();
//			if (msgTimeUtc >= notBeforeUtc)
//			{
//				PublishSafe(energyConsumed);
//				AppLogger.Info($"[CFX] Publish st.{stationNumber} EnergyConsumed: {energyUsed:F2} kWh → LM");
//			}
//			else
//			{
//				LogThrottler.Every("energy_skip_old_" + stationNumber, 60,
//					() => AppLogger.Info($"[CFX] Skip old EnergyConsumed st.{stationNumber} {msgTimeUtc:o} < watermark {notBeforeUtc:o}"));
//			}
//		}






//		private double ConvertToDouble(Dictionary<string, object> data, string key)
//		{
//			if (!data.ContainsKey(key)) return 0.0;
//			var obj = data[key];
//			if (obj == null) return 0.0;
//			if (obj is double dd) return dd;
//			if (obj is int ii) return ii;
//			return double.TryParse(obj.ToString(), out var result) ? result : 0.0;
//		}

//		public static int ConvertPowerSwitchToState(int powerSwitchValue)
//		{
//			// 這裡保留你原本的轉換邏輯（0/1 對應），以免相容性問題
//			if (powerSwitchValue == 1) return 1; // On
//			if (powerSwitchValue == 0) return 2; // Off
//			return -1;
//		}




//		#region IDisposable
//		public void Dispose()
//		{
//			// 1. 停掉任何背景 loop
//			try { _amqpRetryCts?.Cancel(); } catch { }

//			// 2. 釋放托管資源
//			_amqpRetryCts?.Dispose();
//			_watchdogTimer?.Dispose();
//			endpoint?.Close();
//		}
//		#endregion

//		#region Utilities
//		/// <summary>
//		/// 嘗試從 AmqpCFXEndpoint 內部反射取得第一條 Channel 的 Socket。
//		/// 支援 CFX 2.x / 3.x / 4.x 常見欄位名稱。
//		/// </summary>
//		private static Socket ExtractSocket(AmqpCFXEndpoint ep)
//		{
//			if (ep == null) return null;

//			// === 1. 找承載 Channel 字典的私有欄位 ===
//			var fld = ep.GetType().GetField("_channels", BindingFlags.Instance | BindingFlags.NonPublic) ??
//					   ep.GetType().GetField("_cfxChannels", BindingFlags.Instance | BindingFlags.NonPublic) ??
//					   ep.GetType().GetField("_channelsByUri", BindingFlags.Instance | BindingFlags.NonPublic);
//			if (fld == null) return null;

//			var dict = fld.GetValue(ep) as System.Collections.IDictionary;
//			if (dict == null || dict.Count == 0) return null;

//			// === 2. 取第一條 Channel，再反射拿 Amqp.Connection ===
//			foreach (var chObj in dict.Values)
//			{
//				if (chObj == null) continue;
//				var connProp = chObj.GetType().GetProperty("Connection", BindingFlags.Instance | BindingFlags.Public);
//				var conn = connProp?.GetValue(chObj);
//				if (conn == null) continue;

//				// === 3. Amqp.Connection 的 socket 私有欄位名稱也做兼容 ===
//				var sockFld = conn.GetType().GetField("_socket", BindingFlags.Instance | BindingFlags.NonPublic) ??
//							  conn.GetType().GetField("socket", BindingFlags.Instance | BindingFlags.NonPublic);
//				if (sockFld?.GetValue(conn) is Socket s) return s;
//			}

//			return null;        // 仍然抓不到
//		}
//		#endregion
//	}
//}


// AmqpEndpointManager.cs  （C# 8 相容版）
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
using System.Net.Sockets;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;

namespace AmqpModbusIntegration
{
	public class AmqpEndpointManager : IDisposable
	{
		private readonly string endpointUri;
		private readonly string publishChannelUri;
		private readonly string subscribeChannelUri;

		private readonly ModbusViewer modbusViewer;
		private readonly SerialPort serialPort;               // 目前未用
		private readonly Dictionary<byte, Dictionary<string, object>> slaveData;

		private volatile bool _amqpConnected = false;
		private int _retryCount = 0;
		private const int _maxAttempts = 9999;
		private readonly TimeSpan _retryInterval = TimeSpan.FromMinutes(1);
		private CancellationTokenSource _amqpRetryCts;

		private AmqpCFXEndpoint endpoint;
		private readonly HashSet<string> _expectedUris = new HashSet<string>();
		private readonly ConcurrentDictionary<string, bool> _uriState = new ConcurrentDictionary<string, bool>();

		private volatile bool _modbusHealthy = false;

		// ───── 連線監測 ─────
		private volatile bool _publishLoopStarted = false;
		private Timer _watchdogTimer;            // 目前僅保留，日後如需額外 timer 可沿用
		private DateTime _reconnectedAtUtc;
		private readonly CancellationTokenSource _idleWatchdogCts = new CancellationTokenSource();
		private DateTime _lastSendUtc = DateTime.UtcNow;
		private volatile bool _kaSet = false;
		// ────────────────────

		// gate／seed
		private int _publishLoopGate = 0;
		private int _connectLoopGate = 0;
		private long _loopIdSeed = 0;

		// ───── 故障對照表 ─────
		private static readonly Dictionary<int, (string Code, string ErrorDescription)> faultDictionary =
			new Dictionary<int, (string Code, string ErrorDescription)>
		{
			{ 0 , ("EGY_1_WARN1" , "A相過壓") },
			{ 1 , ("EGY_1_WARN2" , "B相過壓") },
			{ 2 , ("EGY_1_WARN3" , "C相過壓") },
			{ 3 , ("EGY_1_WARN4" , "A相欠壓") },
			{ 4 , ("EGY_1_WARN5" , "B相欠壓") },
			{ 5 , ("EGY_1_WARN6" , "C相過流") },
			{ 6 , ("EGY_1_WARN7" , "A相過流") },
			{ 7 , ("EGY_1_WARN8" , "B相過流") },
			{ 8 , ("EGY_1_WARN9" , "C相過流") },
			{ 9 , ("EGY_1_WARN10", "漏電異常") },
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
		// ────────────────────

		// ---------- 發布包裝 ----------
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
					_lastSendUtc = DateTime.UtcNow;   // 更新 idle 水位
				}
				catch (InvalidOperationException ex)
				{
					LogThrottler.Every("cfx_pub_enum", 5,
						() => AppLogger.Warn("[CFX] 發布循環內部列舉衝突（已略過一次）： " + ex.Message));
				}
				catch (Exception ex)
				{
					AppLogger.Error(ex, "[CFX] Publish 例外");
				}
			}
		}
		// --------------------------------

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

			AddExpectedUri(publishChannelUri);
			if (!string.IsNullOrWhiteSpace(this.subscribeChannelUri))
				AddExpectedUri(this.subscribeChannelUri);
		}

		// ───── URI 轉正規化/收集 ─────
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
		// ──────────────────────────────

		#region 對外：Modbus 健康狀態切換
		public void SetModbusHealthy(bool healthy)
		{
			if (_modbusHealthy == healthy) return;
			_modbusHealthy = healthy;

			AppLogger.Info(healthy ? "[MODBUS] 恢復連線，恢復 CFX 發布。" :
									 "[MODBUS] 中斷，暫停 CFX 發布。");

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
		#endregion

		#region AMQP 連線確保
		public async Task EnsureConnectedAsync(string endpointName, CancellationToken ct = default)
		{
			if (_amqpConnected) return;

			if (Interlocked.CompareExchange(ref _connectLoopGate, 1, 0) != 0) return;   // gate

			_amqpRetryCts?.Cancel();
			_amqpRetryCts = CancellationTokenSource.CreateLinkedTokenSource(ct);

			_ = Task.Run(async () =>
			{
				bool aborted = false;
				try
				{
					while (!_amqpConnected &&
						   _retryCount < _maxAttempts &&
						   !_amqpRetryCts.IsCancellationRequested)
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
							_kaSet = false;                    // 每次新起端點先清旗標
							var ok = await SpinWaitUntilConnectedAsync(TimeSpan.FromSeconds(60), _amqpRetryCts.Token);
							if (!ok) throw new Exception("handshake timeout");
							AppLogger.Info($"[AMQP] 連線成功（第 {_retryCount}/{_maxAttempts} 次）");
						}
						catch (Exception ex)
						{
							_amqpConnected = false;
							AppLogger.Warn($"[AMQP] 連線失敗（第 {_retryCount}/{_maxAttempts} 次，{_retryInterval.TotalSeconds:N0}s 後再試）：{ex.Message}");
							try { await Task.Delay(_retryInterval, _amqpRetryCts.Token); } catch { }
						}
					}
				}
				catch (OperationCanceledException) { aborted = true; }
				finally
				{
					Interlocked.Exchange(ref _connectLoopGate, 0);
					if (!aborted && !_amqpConnected && _retryCount >= _maxAttempts)
						AppLogger.Warn("[AMQP] 重試已達上限，請檢查設定（PublishAddress / MyRequestUri / SubscribeAddress）");
				}
			}, _amqpRetryCts.Token);
		}
		#endregion

		#region 啟動 Endpoint
		public void StartAmqpEndpoint(string endpointName)
		{
			if (string.IsNullOrWhiteSpace(endpointName))
				throw new ArgumentException("endpointName 不能為空白", nameof(endpointName));

			try { endpoint?.Close(); } catch { }

			endpoint = new AmqpCFXEndpoint { HeartbeatFrequency = TimeSpan.FromSeconds(60) };
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

			// Idle-watchdog（單一實例即可）
			_ = Task.Run(IdleWatchdogLoop, _idleWatchdogCts.Token);
		}
		#endregion

		#region Idle-watchdog
		private async Task IdleWatchdogLoop()
		{
			while (!_idleWatchdogCts.IsCancellationRequested)
			{
				try
				{
					if (_amqpConnected &&
						(DateTime.UtcNow - _lastSendUtc) > TimeSpan.FromSeconds(90))
					{
						AppLogger.Warn("[AMQP] 消息發布 idle >90s, force reconnect");
						try { endpoint?.Close(); } catch { }
						_amqpConnected = false;
						_publishLoopStarted = false;
						if (_modbusHealthy)
							_ = EnsureConnectedAsync(endpoint?.CFXHandle ?? "Unknown");
					}
				}
				catch { /* swallow */ }

				await Task.Delay(30_000, _idleWatchdogCts.Token);
			}
		}
		#endregion

		#region 連線事件處理（★改成 async void）
		private async void HandleConnectionEvent(
			ConnectionEvent evt, Uri uri, int spool, string info, Exception ex)
		{
			string key = Normalize(uri.ToString());
			bool isUp = evt == ConnectionEvent.ConnectionEstablished;
			_uriState[key] = isUp;

			bool allOk = _expectedUris.All(u =>
			{
				bool ok;
				return _uriState.TryGetValue(u, out ok) && ok;
			});

			// 先在單一路徑 ready 時嘗試設 Keep-Alive
			//if (evt == ConnectionEvent.ConnectionEstablished && !_kaSet)
			//{
			//	for (int i = 0; i < 2 && !_kaSet; i++)
			//	{
			//		var sock = ExtractSocket(endpoint);
			//		if (sock != null)
			//		{
			//			try
			//			{
			//				KeepAliveHelper.EnableKeepAlive(sock, 30, 10);
			//				AppLogger.Info("[AMQP] TCP KeepAlive 已啟用：Idle=30s, Interval=10s");
			//				_kaSet = true;
			//			}
			//			catch (Exception e)
			//			{
			//				AppLogger.Warn($"[AMQP] 設定 KeepAlive 失敗：{e.Message}");
			//			}
			//		}
			//		else if (i == 0)
			//		{
			//			await Task.Delay(100);          // 第一輪抓不到 → 稍候再試
			//		}
			//		else
			//		{
			//			AppLogger.Warn("[AMQP] 取得 Socket 失敗，無法設定 KeepAlive");
			//		}
			//	}
			//}
			// === 修改開始：簡化為呼叫公用方法 ---------------------------------------------
			if (evt == ConnectionEvent.ConnectionEstablished)
			{
				//_ = TryEnableKeepAliveAsync();   // 背景嘗試，不阻塞事件執行緒
				// === 修改開始 --------------------------------------------------------------
				_ = TryEnableKeepAliveAsync(
						attempts: 12,          // <-- 共掃描 12 次
						delayMs: 500,         // <-- 每 500 ms 一次，總等 6 秒
						verbose: true);       // <-- 把為何失敗寫進日誌
				// === 修改結束 --------------------------------------------------------------


			}
			// === 修改結束 ------------------------------------------------------------------





			// 所有通道 OK → 標記 Connected, 啟動 PublishLoop
			if (allOk && !_amqpConnected)
			{
				_reconnectedAtUtc = DateTime.UtcNow;
				_amqpConnected = true;

				// === 修改開始：通道全開後再補一次 Keep-Alive -------------------------------
				//_ = TryEnableKeepAliveAsync();

				// === 修改開始 --------------------------------------------------------------
				_ = TryEnableKeepAliveAsync(
						attempts: 12,          // <-- 共掃描 12 次
						delayMs: 500,         // <-- 每 500 ms 一次，總等 6 秒
						verbose: true);       // <-- 把為何失敗寫進日誌
											  // === 修改結束 -------------------------------------------
											  // === 修改結束 --------------------------------------------------------------

				AppLogger.Info("[AMQP] ✓ 所有通道握手完成：\n  └ " +
							   string.Join("\n  └ ", _expectedUris));
				PublishSafe(new EndpointConnected());
				TryStartPublishLoop();
			}
			// 有任一通道掉線 → 關閉端點，重試
			else if (!allOk && _amqpConnected &&
					 (evt == ConnectionEvent.ConnectionInterrupted ||
					  evt == ConnectionEvent.ConnectionClosed ||
					  evt == ConnectionEvent.ConnectionFailed))
			{
				_amqpConnected = false;
				_publishLoopStarted = false;
				AppLogger.Warn("[AMQP] ✘ 通道中斷：" + evt + " → " + key + "，準備重試…");
				try { endpoint?.Close(); } catch { }

				if (_modbusHealthy)
					_ = EnsureConnectedAsync(endpoint.CFXHandle);
			}
		}
		#endregion

		#region Publish-loop Gate
		private void TryStartPublishLoop()
		{
			if (!_amqpConnected || !_modbusHealthy) return;
			if (Interlocked.CompareExchange(ref _publishLoopGate, 1, 0) != 0) return;
			_ = Task.Run(StartPublishLoop);
		}
		#endregion

		#region Spin-wait until Connected
		private async Task<bool> SpinWaitUntilConnectedAsync(TimeSpan timeout, CancellationToken ct)
		{
			var start = DateTime.UtcNow;
			while (DateTime.UtcNow - start < timeout &&
				   !_amqpConnected &&
				   !ct.IsCancellationRequested)
			{
				await Task.Delay(200, ct);
			}
			return _amqpConnected;
		}
		#endregion

		#region Publish-loop
		private async Task StartPublishLoop()
		{
			var loopId = Interlocked.Increment(ref _loopIdSeed);
			var energyPeriod = TimeSpan.FromSeconds(60);
			var nextEnergyDue = DateTime.UtcNow;

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
						// 1) Fault（僅變化才送）
						foreach (var stn in slaveData.Keys.ToList())
						{
							object obj;
							if (slaveData[stn].TryGetValue("Fault_WarningCode", out obj))
							{
								int cur = Convert.ToInt32(obj);
								if (!lastFault.TryGetValue(stn, out var prev) || prev != cur)
								{
									await PublishFaultOccurredMessages(stn, endpoint);
									lastFault[stn] = cur;
								}
							}
						}

						// 2) Energy / Parameters 每 60s
						if (DateTime.UtcNow >= nextEnergyDue)
						{
							foreach (var stn in slaveData.Keys.ToList())
							{
								PublishStationParametersModifiedMessages(stn, endpoint);
								PublishEnergyConsumedMessages(stn, endpoint, _reconnectedAtUtc);
							}
							nextEnergyDue = DateTime.UtcNow + energyPeriod;
						}
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
				Interlocked.Exchange(ref _publishLoopGate, 0);
			}
		}
		#endregion

		#region Request-handler
		private CFXEnvelope OnRequestReceivedHandler(CFXEnvelope request)
		{
			try
			{
				if (!(request.MessageBody is ModifyStationParametersRequest modifyRequest))
					return CreateErrorResponse(request.RequestID, "Unsupported request type.");

				AppLogger.Info("[CFX] 收到 ModifyStationParametersRequest: " +
							   JsonConvert.SerializeObject(modifyRequest, Formatting.None));

				// 1) 解析站號
				var stationNumbers = modifyRequest.NewParameters
					.OfType<GenericParameter>()
					.Where(g => g.Name == "站號")
					.Select(g =>
					{
						byte v;
						return byte.TryParse(g.Value?.ToString(), out v) ? (byte?)v : null;
					})
					.Where(p => p.HasValue)
					.Select(p => p.Value)
					.ToList();

				if (stationNumbers.Count == 0)
					return CreateErrorResponse(request.RequestID, "No valid station numbers provided.");

				bool hasProcessed = false;
				bool hasError = false;
				bool shouldPublishStateChanged = false;

				foreach (var param in modifyRequest.NewParameters.OfType<GenericParameter>())
				{
					switch (param.Name)
					{
						case "TemperatureSV":
							ushort tempVal;
							if (ushort.TryParse(param.Value?.ToString(), out tempVal))
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
							int mode;
							if (int.TryParse(param.Value?.ToString(), out mode) && mode >= 0 && mode <= 2)
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
							var str = param.Value?.ToString();
							if (str == "1" || str == "2")
							{
								bool isOn = str == "1";
								foreach (var stn in stationNumbers)
								{
									if (!slaveData.ContainsKey(stn)) { hasError = true; continue; }
									try
									{
										if (isOn)
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

				if (!hasProcessed)
					return CreateErrorResponse(request.RequestID,
						hasError ? "No parameters were processed successfully." :
								   "No valid actions were performed.");

				if (shouldPublishStateChanged)
					PublishSafe(new StationStateChanged { NewState = ResourceState.NST_ShutdownAndStartup });

				return CreateSuccessResponse(request.RequestID, "Parameters updated successfully.");
			}
			catch (Exception ex)
			{
				AppLogger.Error(ex, "[CFX] Request 處理錯誤");
				return CreateErrorResponse(request.RequestID, ex.Message);
			}
		}
		#endregion

		#region Response-helpers
		private static CFXEnvelope CreateSuccessResponse(string requestId, string msg)
		{
			var rsp = new ModifyStationParametersResponse
			{
				Result = new RequestResult
				{
					Result = StatusResult.Success,
					ResultCode = 1,
					Message = msg
				}
			};
			var env = CFXEnvelope.FromCFXMessage(rsp);
			env.RequestID = requestId;
			return env;
		}
		private static CFXEnvelope CreateErrorResponse(string requestId, string error)
		{
			var rsp = new NotSupportedResponse
			{
				RequestResult = new RequestResult
				{
					Result = StatusResult.Failed,
					ResultCode = 2,
					Message = error
				}
			};
			var env = CFXEnvelope.FromCFXMessage(rsp);
			env.RequestID = requestId;
			return env;
		}
		#endregion

		#region Fault / Energy / Parameter 發布
		private async Task PublishFaultOccurredMessages(byte stn, AmqpCFXEndpoint ep)
		{
			Dictionary<string, object> data;
			if (!slaveData.TryGetValue(stn, out data) || !data.ContainsKey("Fault_WarningCode"))
				return;

			int statusRegister = Convert.ToInt32(data["Fault_WarningCode"]);
			for (int bit = 0; bit <= 27; bit++)
			{
				if (((statusRegister >> bit) & 1) == 0) continue;
				if (!faultDictionary.ContainsKey(bit)) continue;

				var tuple = faultDictionary[bit];
				var msg = new FaultOccurred
				{
					Fault = new Fault
					{
						FaultCode = tuple.Code,
						FaultOccurrenceId = Guid.NewGuid(),
						Description = tuple.ErrorDescription,
						OccurredAt = DateTime.Now,
						Severity = FaultSeverity.Error
					}
				};
				await Task.Run(() => PublishSafe(msg));
			}
		}

		private void PublishStationParametersModifiedMessages(byte stn, AmqpCFXEndpoint ep)
		{
			Dictionary<string, object> data;
			if (!slaveData.TryGetValue(stn, out data) || data == null || data.Count == 0) return;

			bool anyNonZero = data.Values.Any(v =>
			{
				if (v == null) return false;
				if (v is int i) return i != 0;
				if (v is double d) return Math.Abs(d) > 0.000001;
				return false;
			});
			if (!anyNonZero) return;

			var parameters = new List<Parameter>
			{
				new GenericParameter
				{
					Name  = "PowerSwitch",
					Value = ConvertPowerSwitchToState(
								data.ContainsKey("PowerSwitch") ? Convert.ToInt32(data["PowerSwitch"]) : 0).ToString()
				},
				new GenericParameter { Name = "EnergyMode",        Value = data.ContainsKey("EnergyMode")         ? data["EnergyMode"].ToString()         : "0" },
				new GenericParameter { Name = "PowerTemperatureA", Value = data.ContainsKey("PowerTemperature_A") ? data["PowerTemperature_A"].ToString() : "0" },
				new GenericParameter { Name = "PowerTemperatureB", Value = data.ContainsKey("PowerTemperature_B") ? data["PowerTemperature_B"].ToString() : "0" },
				new GenericParameter { Name = "PowerTemperatureC", Value = data.ContainsKey("PowerTemperature_C") ? data["PowerTemperature_C"].ToString() : "0" }
			};

			PublishSafe(new StationParametersModified { ModifiedParameters = parameters });
		}

		private void PublishEnergyConsumedMessages(byte stn, AmqpCFXEndpoint ep, DateTime notBeforeUtc)
		{
			Dictionary<string, object> data;
			if (!slaveData.TryGetValue(stn, out data) || data == null || data.Count == 0) return;

			bool anyNonZero = data.Values.Any(v =>
			{
				if (v == null) return false;
				if (v is int i) return i != 0;
				if (v is double d) return Math.Abs(d) > 0.000001;
				return false;
			});
			if (!anyNonZero) return;

			double currentA = ConvertToDouble(data, "CurrentRYB_A");
			double currentB = ConvertToDouble(data, "CurrentRYB_B");
			double currentC = ConvertToDouble(data, "CurrentRYB_C");

			double powerA = ConvertToDouble(data, "PowerRYB_A");
			double powerB = ConvertToDouble(data, "PowerRYB_B");
			double powerC = ConvertToDouble(data, "PowerRYB_C");

			double energyUsed = ConvertToDouble(data, "EnergyUsed");
			var nowUtc = DateTime.UtcNow;

			var msg = new EnergyConsumed
			{
				EnergyUsed = energyUsed,
				StartTime = nowUtc,
				EndTime = nowUtc,
				CurrentNowRYB = new List<double> { currentA, currentB, currentC },
				PowerNowRYB = new List<double> { powerA, powerB, powerC }
			};

			var msgTimeUtc = msg.EndTime.ToUniversalTime();
			if (msgTimeUtc >= notBeforeUtc)
			{
				PublishSafe(msg);
				AppLogger.Info($"[CFX] Publish st.{stn} EnergyConsumed: {energyUsed:F2} kWh → LM");
			}
		}
		#endregion

		#region Utilities / helpers
		private static double ConvertToDouble(Dictionary<string, object> data, string key)
		{
			if (!data.ContainsKey(key)) return 0.0;
			var obj = data[key];
			if (obj == null) return 0.0;
			if (obj is double d) return d;
			if (obj is int i) return i;
			double v;
			return double.TryParse(obj.ToString(), out v) ? v : 0.0;
		}

		public static int ConvertPowerSwitchToState(int v) => v == 1 ? 1 : (v == 0 ? 2 : -1);
		#endregion

		#region IDisposable
		public void Dispose()
		{
			try { _amqpRetryCts?.Cancel(); } catch { }
			_amqpRetryCts?.Dispose();
			_watchdogTimer?.Dispose();
			try { endpoint?.Close(); } catch { }
		}
		#endregion

		#region 反射抓 Socket
		//private static Socket ExtractSocket(AmqpCFXEndpoint ep)
		//{
		//	if (ep == null) return null;

		//	var fld = ep.GetType().GetField("_channels", BindingFlags.Instance | BindingFlags.NonPublic)
		//		   ?? ep.GetType().GetField("_cfxChannels", BindingFlags.Instance | BindingFlags.NonPublic)
		//		   ?? ep.GetType().GetField("_channelsByUri", BindingFlags.Instance | BindingFlags.NonPublic);
		//	if (fld == null) return null;

		//	var dict = fld.GetValue(ep) as System.Collections.IDictionary;
		//	if (dict == null || dict.Count == 0) return null;

		//	foreach (var ch in dict.Values)
		//	{
		//		if (ch == null) continue;
		//		var connProp = ch.GetType().GetProperty("Connection", BindingFlags.Instance | BindingFlags.Public);
		//		var conn = connProp?.GetValue(ch);
		//		if (conn == null) continue;

		//		//var sockFld = conn.GetType().GetField("_socket", BindingFlags.Instance | BindingFlags.NonPublic)
		//		//		   ?? conn.GetType().GetField("socket", BindingFlags.Instance | BindingFlags.NonPublic);
		//		var sockFld = conn.GetType().GetField("_socket", BindingFlags.Instance | BindingFlags.NonPublic)
		//   ?? conn.GetType().GetField("socket", BindingFlags.Instance | BindingFlags.NonPublic)
		//   ?? conn.GetType().GetField("streamSocket", BindingFlags.Instance | BindingFlags.NonPublic); // <== 新增


		//		if (sockFld?.GetValue(conn) is Socket s) return s;
		//	}
		//	return null;
		//}

		// === 修改開始：完整替換 ExtractSocket(...) ======================================
		//private static Socket ExtractSocket(object obj, int depth = 0)
		//{
		//	if (obj == null || depth > 4) return null;                // 防止深到爆

		//	// 1) 直接就是 Socket ?
		//	var s = obj as Socket;
		//	if (s != null) return s;

		//	var t = obj.GetType();

		//	// 2) 嘗試已知欄位名稱
		//	var fld = t.GetField("_socket", BindingFlags.Instance | BindingFlags.NonPublic)
		//		   ?? t.GetField("socket", BindingFlags.Instance | BindingFlags.NonPublic)
		//		   ?? t.GetField("streamSocket", BindingFlags.Instance | BindingFlags.NonPublic)
		//		   ?? t.GetField("tcp", BindingFlags.Instance | BindingFlags.NonPublic);
		//	if (fld != null)
		//	{
		//		s = fld.GetValue(obj) as Socket;
		//		if (s != null) return s;
		//	}

		//	// 3) AmqpCFXEndpoint → 先進到 channel / connection
		//	if (t.FullName == "CFX.Transport.AmqpCFXEndpoint")
		//	{
		//		var dict = t.GetField("_channels", BindingFlags.Instance | BindingFlags.NonPublic)?.GetValue(obj)
		//				?? t.GetField("_cfxChannels", BindingFlags.Instance | BindingFlags.NonPublic)?.GetValue(obj)
		//				?? t.GetField("_channelsByUri", BindingFlags.Instance | BindingFlags.NonPublic)?.GetValue(obj);

		//		var enumDict = dict as System.Collections.IDictionary;
		//		if (enumDict != null && enumDict.Count > 0)
		//			return ExtractSocket(enumDict.Values.OfType<object>().FirstOrDefault(), depth + 1);
		//	}

		//	// 4) Channel → Connection
		//	var connProp = t.GetProperty("Connection", BindingFlags.Instance | BindingFlags.Public);
		//	if (connProp != null)
		//		return ExtractSocket(connProp.GetValue(obj), depth + 1);

		//	// 5) Transport / InnerStream
		//	var inner = t.GetField("innerStream", BindingFlags.Instance | BindingFlags.NonPublic)?.GetValue(obj)
		//			 ?? t.GetField("_innerStream", BindingFlags.Instance | BindingFlags.NonPublic)?.GetValue(obj)
		//			 ?? t.GetField("transport", BindingFlags.Instance | BindingFlags.NonPublic)?.GetValue(obj);
		//	if (inner != null)
		//		return ExtractSocket(inner, depth + 1);

		//	// 找不到
		//	return null;
		//}


		// 🔍 ExtractSocket
		private static Socket ExtractSocket(AmqpCFXEndpoint ep)
		{
			if (ep == null) return null;

			// 1) 先找到 Channels 字典
			var chFld = ep.GetType().GetField("_channels", BF) ??
						ep.GetType().GetField("_cfxChannels", BF) ??
						ep.GetType().GetField("_channelsByUri", BF);
			if (chFld == null) return null;

			var dict = chFld.GetValue(ep) as System.Collections.IDictionary;
			if (dict == null || dict.Count == 0) return null;

			foreach (var ch in dict.Values)                // 2) Channel → Connection
			{
				if (ch == null) continue;
				var conn = ch.GetType().GetProperty("Connection", BF)?.GetValue(ch);
				if (conn == null) continue;

				// 2-A 先直接在 Connection 裡找 (舊版)
				var sock = TryGetSocketFromObject(conn);
				if (sock != null) return sock;

				// 2-B 再往 Connection.Transport.InnerTransport 走 (新版)
				var transport = conn.GetType().GetProperty("Transport", BF)?.GetValue(conn);
				while (transport != null)
				{
					sock = TryGetSocketFromObject(transport);
					if (sock != null) return sock;
					// 遞迴往內層 transport
					transport = transport.GetType()
										 .GetProperty("InnerTransport", BF)?
										 .GetValue(transport);

				}
			}
			return null;

			//// ---- local helpers ----
			//Socket TryGetSocketFromObject(object obj)
			//{
			//	if (obj == null) return null;
			//	var sField = obj.GetType().GetField("_socket", BF) ??
			//				 obj.GetType().GetField("socket", BF);
			//	return sField?.GetValue(obj) as Socket;
			//}

			// 把這段放回 ExtractSocket 協助函式
			Socket TryGetSocketFromObject(object obj)
			{
				if (obj == null) return null;

				// 1) 直接找常見欄位名
				foreach (var name in new[] { "_socket", "socket", "tcp", "streamSocket" })
				{
					var fld = obj.GetType().GetField(name, BF);
					if (fld != null && fld.GetValue(obj) is Socket s) return s;
				}

				// 2) 若還沒找到，再掃描所有私有欄位，第一個型別就是 Socket 的就回傳
				var any = obj.GetType()
							 .GetFields(BF)
							 .FirstOrDefault(f => typeof(Socket).IsAssignableFrom(f.FieldType));
				return any?.GetValue(obj) as Socket;
			}

		}

		// 共用 BindingFlags
		private const BindingFlags BF = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;

		// === 修改結束 ==================================================================

		#endregion

		// === 修改開始：新增 TryEnableKeepAliveAsync 方法 -------------------------------
		/// <summary>
		/// 嘗試在背景連續幾次抓 Socket；成功就設 Keep-Alive，失敗不影響主流程。
		/// </summary>
		//private async Task TryEnableKeepAliveAsync(int attempts = 4, int delayMs = 150)
		//{
		//	if (_kaSet) return;                       // 已成功就跳過

		//	for (int i = 0; i < attempts && !_kaSet; i++)
		//	{
		//		var sock = ExtractSocket(endpoint);
		//		if (sock != null)
		//		{
		//			try
		//			{
		//				KeepAliveHelper.EnableKeepAlive(sock, 30, 10);
		//				AppLogger.Info("[AMQP] TCP KeepAlive 已啟用：Idle=30s, Interval=10s");
		//				_kaSet = true;
		//				return;
		//			}
		//			catch (Exception ex)
		//			{
		//				AppLogger.Warn("[AMQP] 設定 KeepAlive 失敗：" + ex.Message);
		//			}
		//		}
		//		await Task.Delay(delayMs);
		//	}

		//	// === 修改開始：補 Warn ----------------------------------------------------------
		//	if (!_kaSet)
		//		AppLogger.Warn("[AMQP] 無法在限制時間內取得 Socket，未設定 Keep-Alive");
		//	// === 修改結束 -------------------------------------------------------------------
		//}


		private async Task TryEnableKeepAliveAsync(int attempts, int delayMs, bool verbose = false)
		{
			for (int i = 0; i < attempts && !_kaSet; i++)
			{
				var sock = ExtractSocket(endpoint);
				if (sock != null)
				{
					try
					{
						KeepAliveHelper.EnableKeepAlive(sock, 30, 10);
						AppLogger.Info("[AMQP] TCP KeepAlive 已啟用：Idle=30s, Interval=10s");
						_kaSet = true;
						return;
					}
					catch (Exception ex)
					{
						AppLogger.Warn($"[AMQP] 設定 KeepAlive 失敗：{ex.Message}");
						return;                     // 有抓到但設失敗，直接離開
					}
				}

				if (verbose)
					AppLogger.Info($"[AMQP] (KA) still no socket, pass {i + 1}/{attempts}");

				await Task.Delay(delayMs);
			}

			if (!_kaSet)
				AppLogger.Warn("[AMQP] 無法在限制時間內取得 Socket，未設定 Keep-Alive");
		}

		// === 修改結束 -----------------------------------------------------------------





	}
}

