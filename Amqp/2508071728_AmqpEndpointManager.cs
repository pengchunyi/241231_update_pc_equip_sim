////// AmqpEndpointManager.cs  （C# 8 相容版）
////using CFX;
////using CFX.Production;
////using CFX.ResourcePerformance;
////using CFX.Structures;
////using CFX.Transport;
////using Newtonsoft.Json;
////using System;
////using System.Collections.Concurrent;
////using System.Collections.Generic;
////using System.IO.Ports;
////using System.Linq;
////using System.Net.Sockets;
////using System.Reflection;
////using System.Threading;
////using System.Threading.Tasks;

////namespace AmqpModbusIntegration
////{
////	public class AmqpEndpointManager : IDisposable
////	{
////		private readonly string endpointUri;
////		private readonly string publishChannelUri;
////		private readonly string subscribeChannelUri;

////		private readonly ModbusViewer modbusViewer;
////		private readonly SerialPort serialPort;               // 目前未用
////		private readonly Dictionary<byte, Dictionary<string, object>> slaveData;

////		private volatile bool _amqpConnected = false;
////		private int _retryCount = 0;
////		private const int _maxAttempts = 9999;
////		private readonly TimeSpan _retryInterval = TimeSpan.FromMinutes(1);
////		private CancellationTokenSource _amqpRetryCts;

////		private AmqpCFXEndpoint endpoint;
////		private readonly HashSet<string> _expectedUris = new HashSet<string>();
////		private readonly ConcurrentDictionary<string, bool> _uriState = new ConcurrentDictionary<string, bool>();

////		private volatile bool _modbusHealthy = false;

////		// ───── 連線監測 ─────
////		private volatile bool _publishLoopStarted = false;
////		private Timer _watchdogTimer;            // 目前僅保留，日後如需額外 timer 可沿用
////		private DateTime _reconnectedAtUtc;
////		private readonly CancellationTokenSource _idleWatchdogCts = new CancellationTokenSource();
////		private DateTime _lastSendUtc = DateTime.UtcNow;
////		private volatile bool _kaSet = false;
////		// 共用 BindingFlags（放檔案最上方或類別內）
////		private const BindingFlags BF = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;
////		// ────────────────────

////		// gate／seed
////		private int _publishLoopGate = 0;
////		private int _connectLoopGate = 0;
////		private long _loopIdSeed = 0;

////		// ───── 故障對照表 ─────
////		private static readonly Dictionary<int, (string Code, string ErrorDescription)> faultDictionary =
////			new Dictionary<int, (string Code, string ErrorDescription)>
////		{
////			{ 0 , ("EGY_1_WARN1" , "A相過壓") },
////			{ 1 , ("EGY_1_WARN2" , "B相過壓") },
////			{ 2 , ("EGY_1_WARN3" , "C相過壓") },
////			{ 3 , ("EGY_1_WARN4" , "A相欠壓") },
////			{ 4 , ("EGY_1_WARN5" , "B相欠壓") },
////			{ 5 , ("EGY_1_WARN6" , "C相過流") },
////			{ 6 , ("EGY_1_WARN7" , "A相過流") },
////			{ 7 , ("EGY_1_WARN8" , "B相過流") },
////			{ 8 , ("EGY_1_WARN9" , "C相過流") },
////			{ 9 , ("EGY_1_WARN10", "漏電異常") },
////			{ 10, ("EGY_1_WARN11", "A相出線溫度異常") },
////			{ 11, ("EGY_1_WARN12", "B相出線溫度異常") },
////			{ 12, ("EGY_1_WARN13", "C相出線溫度異常") },
////			{ 13, ("EGY_1_WARN14", "N相出線溫度異常") },
////			{ 14, ("EGY_1_WARN15", "電弧") },
////			{ 15, ("EGY_1_WARN16", "缺相") },
////			{ 16, ("EGY_1_WARN17", "斷零") },
////			{ 17, ("EGY_1_WARN18", "三相電壓不平衡") },
////			{ 18, ("EGY_1_WARN19", "鎖定") },
////			{ 19, ("EGY_1_WARN20", "進入維修或手動模式") },
////			{ 20, ("EGY_1_WARN21", "開關狀態異常，提示客戶換設備") },
////			{ 21, ("EGY_1_WARN22", "漏電功能壞，提示客戶換設備") },
////			{ 22, ("EGY_1_WARN23", "設備離線") },
////			{ 23, ("EGY_1_WARN24", "過壓預警") },
////			{ 24, ("EGY_1_WARN25", "欠壓預警") },
////			{ 25, ("EGY_1_WARN26", "過流預警") },
////			{ 26, ("EGY_1_WARN27", "過溫預警") }
////		};
////		// ────────────────────




////		// 取 Socket 的工具函式：放在 AmqpEndpointManager 類別內任意位置即可
////		private static Socket TryGetSocketFromObject(object obj)
////		{
////			if (obj == null) return null;

////			// 1) 常見私有欄位名稱
////			foreach (var name in new[] { "_socket", "socket", "tcp", "streamSocket" })
////			{
////				var f = obj.GetType().GetField(name, BF);
////				if (f != null && f.GetValue(obj) is Socket s) return s;
////			}

////			// 2) 其它私有欄位裡只要型別是 Socket 也接受
////			var any = obj.GetType()
////						 .GetFields(BF)
////						 .FirstOrDefault(f => typeof(Socket).IsAssignableFrom(f.FieldType));
////			return any?.GetValue(obj) as Socket;
////		}

////		// ---------- 發布包裝 ----------
////		private readonly object _publishLock = new object();
////		private void PublishSafe(CFXMessage msg)
////		{
////			var ep = endpoint;
////			if (ep == null || !_amqpConnected) return;

////			lock (_publishLock)
////			{
////				if (!_amqpConnected) return;
////				try
////				{
////					ep.Publish(msg);
////					_lastSendUtc = DateTime.UtcNow;   // 更新 idle 水位
////				}
////				catch (InvalidOperationException ex)
////				{
////					LogThrottler.Every("cfx_pub_enum", 5,
////						() => AppLogger.Warn("[CFX] 發布循環內部列舉衝突（已略過一次）： " + ex.Message));
////				}
////				catch (Exception ex)
////				{
////					AppLogger.Error(ex, "[CFX] Publish 例外");
////				}
////			}
////		}
////		// --------------------------------

////		public AmqpEndpointManager(
////			string endpointUri,
////			string publishChannelUri,
////			string subscribeChannelUri,
////			ModbusViewer modbusViewer,
////			SerialPort serialPort,
////			Dictionary<byte, Dictionary<string, object>> slaveData)
////		{
////			this.endpointUri = endpointUri;
////			this.publishChannelUri = publishChannelUri;
////			this.subscribeChannelUri = string.IsNullOrWhiteSpace(subscribeChannelUri) ? null : subscribeChannelUri;

////			this.modbusViewer = modbusViewer;
////			this.serialPort = serialPort;
////			this.slaveData = slaveData;

////			AddExpectedUri(publishChannelUri);
////			if (!string.IsNullOrWhiteSpace(this.subscribeChannelUri))
////				AddExpectedUri(this.subscribeChannelUri);
////		}

////		// ───── URI 轉正規化/收集 ─────
////		private static string Normalize(string u)
////		{
////			if (string.IsNullOrWhiteSpace(u)) return null;
////			return new Uri(u).ToString();
////		}
////		private void AddExpectedUri(string u)
////		{
////			var n = Normalize(u);
////			if (!string.IsNullOrWhiteSpace(n))
////				_expectedUris.Add(n);
////		}
////		// ──────────────────────────────

////		#region 對外：Modbus 健康狀態切換
////		public void SetModbusHealthy(bool healthy)
////		{
////			if (_modbusHealthy == healthy) return;
////			_modbusHealthy = healthy;

////			AppLogger.Info(healthy ? "[MODBUS] 恢復連線，恢復 CFX 發布。" :
////									 "[MODBUS] 中斷，暫停 CFX 發布。");

////			if (!healthy)
////			{
////				_publishLoopStarted = false;
////				_amqpConnected = false;
////				try { _amqpRetryCts?.Cancel(); } catch { }
////				try { endpoint?.Close(); } catch { }
////				AppLogger.Info("[AMQP] 已停止連線與重試（等待 Modbus 恢復）。");
////				return;
////			}

////			if (!_amqpConnected)
////				_ = EnsureConnectedAsync(SystemConfig.MachineSN ?? "UnknownMachineSN");

////			TryStartPublishLoop();
////		}
////		#endregion

////		#region AMQP 連線確保
////		public async Task EnsureConnectedAsync(string endpointName, CancellationToken ct = default)
////		{
////			if (_amqpConnected) return;

////			if (Interlocked.CompareExchange(ref _connectLoopGate, 1, 0) != 0) return;   // gate

////			_amqpRetryCts?.Cancel();
////			_amqpRetryCts = CancellationTokenSource.CreateLinkedTokenSource(ct);

////			_ = Task.Run(async () =>
////			{
////				bool aborted = false;
////				try
////				{
////					while (!_amqpConnected &&
////						   _retryCount < _maxAttempts &&
////						   !_amqpRetryCts.IsCancellationRequested)
////					{
////						if (!_modbusHealthy)
////						{
////							try { await Task.Delay(TimeSpan.FromSeconds(5), _amqpRetryCts.Token); } catch { }
////							continue;
////						}

////						_retryCount++;
////						try
////						{
////							StartAmqpEndpoint(endpointName);
////							_kaSet = false;                    // 每次新起端點先清旗標
////							var ok = await SpinWaitUntilConnectedAsync(TimeSpan.FromSeconds(60), _amqpRetryCts.Token);
////							if (!ok) throw new Exception("handshake timeout");
////							AppLogger.Info($"[AMQP] 連線成功（第 {_retryCount}/{_maxAttempts} 次）");
////						}
////						catch (Exception ex)
////						{
////							_amqpConnected = false;
////							AppLogger.Warn($"[AMQP] 連線失敗（第 {_retryCount}/{_maxAttempts} 次，{_retryInterval.TotalSeconds:N0}s 後再試）：{ex.Message}");
////							try { await Task.Delay(_retryInterval, _amqpRetryCts.Token); } catch { }
////						}
////					}
////				}
////				catch (OperationCanceledException) { aborted = true; }
////				finally
////				{
////					Interlocked.Exchange(ref _connectLoopGate, 0);
////					if (!aborted && !_amqpConnected && _retryCount >= _maxAttempts)
////						AppLogger.Warn("[AMQP] 重試已達上限，請檢查設定（PublishAddress / MyRequestUri / SubscribeAddress）");
////				}
////			}, _amqpRetryCts.Token);
////		}
////		#endregion

////		#region 啟動 Endpoint
////		public void StartAmqpEndpoint(string endpointName)
////		{
////			if (string.IsNullOrWhiteSpace(endpointName))
////				throw new ArgumentException("endpointName 不能為空白", nameof(endpointName));

////			try { endpoint?.Close(); } catch { }

////			endpoint = new AmqpCFXEndpoint { HeartbeatFrequency = TimeSpan.FromSeconds(60) };
////			endpoint.OnConnectionEvent += HandleConnectionEvent;
////			endpoint.OnRequestReceived += OnRequestReceivedHandler;

////			_expectedUris.Clear();
////			AddExpectedUri(publishChannelUri);
////			if (!string.IsNullOrWhiteSpace(subscribeChannelUri))
////				AddExpectedUri(subscribeChannelUri);
////			foreach (var u in _expectedUris) _uriState[u] = false;

////			AppLogger.Info("[AMQP] 端點啟動，建立通道並等待握手…");

////			endpoint.Open(endpointName, new Uri(endpointUri));
////			endpoint.AddPublishChannel(new Uri(publishChannelUri), "event");
////			if (!string.IsNullOrWhiteSpace(subscribeChannelUri))
////				endpoint.AddSubscribeChannel(new Uri(subscribeChannelUri), "subscribe");

////			// Idle-watchdog（單一實例即可）
////			_ = Task.Run(IdleWatchdogLoop, _idleWatchdogCts.Token);
////		}
////		#endregion

////		#region Idle-watchdog
////		private async Task IdleWatchdogLoop()
////		{
////			while (!_idleWatchdogCts.IsCancellationRequested)
////			{
////				try
////				{
////					if (_amqpConnected &&
////						(DateTime.UtcNow - _lastSendUtc) > TimeSpan.FromSeconds(90))
////					{
////						AppLogger.Warn("[AMQP] 消息發布 idle >90s, force reconnect");
////						try { endpoint?.Close(); } catch { }
////						_amqpConnected = false;
////						_publishLoopStarted = false;
////						if (_modbusHealthy)
////							_ = EnsureConnectedAsync(endpoint?.CFXHandle ?? "Unknown");
////					}
////				}
////				catch { /* swallow */ }

////				await Task.Delay(30_000, _idleWatchdogCts.Token);
////			}
////		}
////		#endregion

////		#region 連線事件處理（★改成 async void）
////		private async void HandleConnectionEvent(
////			ConnectionEvent evt, Uri uri, int spool, string info, Exception ex)
////		{
////			string key = Normalize(uri.ToString());
////			bool isUp = evt == ConnectionEvent.ConnectionEstablished;
////			_uriState[key] = isUp;

////			bool allOk = _expectedUris.All(u =>
////			{
////				bool ok;
////				return _uriState.TryGetValue(u, out ok) && ok;
////			});

////			// 先在單一路徑 ready 時嘗試設 Keep-Alive
////			//if (evt == ConnectionEvent.ConnectionEstablished && !_kaSet)
////			//{
////			//	for (int i = 0; i < 2 && !_kaSet; i++)
////			//	{
////			//		var sock = ExtractSocket(endpoint);
////			//		if (sock != null)
////			//		{
////			//			try
////			//			{
////			//				KeepAliveHelper.EnableKeepAlive(sock, 30, 10);
////			//				AppLogger.Info("[AMQP] TCP KeepAlive 已啟用：Idle=30s, Interval=10s");
////			//				_kaSet = true;
////			//			}
////			//			catch (Exception e)
////			//			{
////			//				AppLogger.Warn($"[AMQP] 設定 KeepAlive 失敗：{e.Message}");
////			//			}
////			//		}
////			//		else if (i == 0)
////			//		{
////			//			await Task.Delay(100);          // 第一輪抓不到 → 稍候再試
////			//		}
////			//		else
////			//		{
////			//			AppLogger.Warn("[AMQP] 取得 Socket 失敗，無法設定 KeepAlive");
////			//		}
////			//	}
////			//}
////			// === 修改開始：簡化為呼叫公用方法 ---------------------------------------------
////			if (evt == ConnectionEvent.ConnectionEstablished)
////			{
////				//_ = TryEnableKeepAliveAsync();   // 背景嘗試，不阻塞事件執行緒
////				// === 修改開始 --------------------------------------------------------------
////				_ = TryEnableKeepAliveAsync(attempts: 60, delayMs: 1000, verbose: true);
////				// === 修改結束 --------------------------------------------------------------


////			}
////			// === 修改結束 ------------------------------------------------------------------





////			// 所有通道 OK → 標記 Connected, 啟動 PublishLoop
////			if (allOk && !_amqpConnected)
////			{
////				_reconnectedAtUtc = DateTime.UtcNow;
////				_amqpConnected = true;

////				// === 修改開始：通道全開後再補一次 Keep-Alive -------------------------------
////				//_ = TryEnableKeepAliveAsync();

////				// === 修改開始 --------------------------------------------------------------
////				_ = TryEnableKeepAliveAsync(
////						attempts: 12,          // <-- 共掃描 12 次
////						delayMs: 500,         // <-- 每 500 ms 一次，總等 6 秒
////						verbose: true);       // <-- 把為何失敗寫進日誌
////											  // === 修改結束 -------------------------------------------
////											  // === 修改結束 --------------------------------------------------------------

////				AppLogger.Info("[AMQP] ✓ 所有通道握手完成：\n  └ " +
////							   string.Join("\n  └ ", _expectedUris));
////				PublishSafe(new EndpointConnected());
////				TryStartPublishLoop();
////			}
////			// 有任一通道掉線 → 關閉端點，重試
////			else if (!allOk && _amqpConnected &&
////					 (evt == ConnectionEvent.ConnectionInterrupted ||
////					  evt == ConnectionEvent.ConnectionClosed ||
////					  evt == ConnectionEvent.ConnectionFailed))
////			{
////				_amqpConnected = false;
////				_publishLoopStarted = false;
////				AppLogger.Warn("[AMQP] ✘ 通道中斷：" + evt + " → " + key + "，準備重試…");
////				try { endpoint?.Close(); } catch { }

////				if (_modbusHealthy)
////					_ = EnsureConnectedAsync(endpoint.CFXHandle);
////			}
////		}
////		#endregion

////		#region Publish-loop Gate
////		private void TryStartPublishLoop()
////		{
////			if (!_amqpConnected || !_modbusHealthy) return;
////			if (Interlocked.CompareExchange(ref _publishLoopGate, 1, 0) != 0) return;
////			_ = Task.Run(StartPublishLoop);
////		}
////		#endregion

////		#region Spin-wait until Connected
////		private async Task<bool> SpinWaitUntilConnectedAsync(TimeSpan timeout, CancellationToken ct)
////		{
////			var start = DateTime.UtcNow;
////			while (DateTime.UtcNow - start < timeout &&
////				   !_amqpConnected &&
////				   !ct.IsCancellationRequested)
////			{
////				await Task.Delay(200, ct);
////			}
////			return _amqpConnected;
////		}
////		#endregion

////		#region Publish-loop
////		private async Task StartPublishLoop()
////		{
////			var loopId = Interlocked.Increment(ref _loopIdSeed);
////			var energyPeriod = TimeSpan.FromSeconds(60);
////			var nextEnergyDue = DateTime.UtcNow;

////			AppLogger.Info($"[CFX] PublishLoop 啟動 (LoopId={loopId})");

////			try
////			{
////				var lastFault = new Dictionary<byte, int>();

////				while (_amqpConnected)
////				{
////					if (!_modbusHealthy)
////					{
////						await Task.Delay(500);
////						continue;
////					}

////					try
////					{
////						// 1) Fault（僅變化才送）
////						foreach (var stn in slaveData.Keys.ToList())
////						{
////							object obj;
////							if (slaveData[stn].TryGetValue("Fault_WarningCode", out obj))
////							{
////								int cur = Convert.ToInt32(obj);
////								if (!lastFault.TryGetValue(stn, out var prev) || prev != cur)
////								{
////									await PublishFaultOccurredMessages(stn, endpoint);
////									lastFault[stn] = cur;
////								}
////							}
////						}

////						// 2) Energy / Parameters 每 60s
////						if (DateTime.UtcNow >= nextEnergyDue)
////						{
////							foreach (var stn in slaveData.Keys.ToList())
////							{
////								PublishStationParametersModifiedMessages(stn, endpoint);
////								PublishEnergyConsumedMessages(stn, endpoint, _reconnectedAtUtc);
////							}
////							nextEnergyDue = DateTime.UtcNow + energyPeriod;
////						}
////					}
////					catch (Exception ex)
////					{
////						AppLogger.Error(ex, "[CFX] 發布循環錯誤");
////					}

////					await Task.Delay(1000);
////				}

////				AppLogger.Warn("[CFX] PublishLoop 結束（AMQP 未連線）");
////			}
////			finally
////			{
////				Interlocked.Exchange(ref _publishLoopGate, 0);
////			}
////		}
////		#endregion

////		#region Request-handler
////		private CFXEnvelope OnRequestReceivedHandler(CFXEnvelope request)
////		{
////			try
////			{
////				if (!(request.MessageBody is ModifyStationParametersRequest modifyRequest))
////					return CreateErrorResponse(request.RequestID, "Unsupported request type.");

////				AppLogger.Info("[CFX] 收到 ModifyStationParametersRequest: " +
////							   JsonConvert.SerializeObject(modifyRequest, Formatting.None));

////				// 1) 解析站號
////				var stationNumbers = modifyRequest.NewParameters
////					.OfType<GenericParameter>()
////					.Where(g => g.Name == "站號")
////					.Select(g =>
////					{
////						byte v;
////						return byte.TryParse(g.Value?.ToString(), out v) ? (byte?)v : null;
////					})
////					.Where(p => p.HasValue)
////					.Select(p => p.Value)
////					.ToList();

////				if (stationNumbers.Count == 0)
////					return CreateErrorResponse(request.RequestID, "No valid station numbers provided.");

////				bool hasProcessed = false;
////				bool hasError = false;
////				bool shouldPublishStateChanged = false;

////				foreach (var param in modifyRequest.NewParameters.OfType<GenericParameter>())
////				{
////					switch (param.Name)
////					{
////						case "TemperatureSV":
////							ushort tempVal;
////							if (ushort.TryParse(param.Value?.ToString(), out tempVal))
////							{
////								foreach (var stn in stationNumbers)
////								{
////									if (!slaveData.ContainsKey(stn)) { hasError = true; continue; }
////									try { modbusViewer.ExecuteSetTemperature(stn, tempVal); hasProcessed = true; }
////									catch (Exception ex) { hasError = true; AppLogger.Error(ex, $"[CFX] 設置溫度失敗，站號 {stn}"); }
////								}
////							}
////							break;

////						case "EnergyMode":
////							int mode;
////							if (int.TryParse(param.Value?.ToString(), out mode) && mode >= 0 && mode <= 2)
////							{
////								foreach (var stn in stationNumbers)
////								{
////									if (!slaveData.ContainsKey(stn)) { hasError = true; continue; }
////									try
////									{
////										slaveData[stn]["EnergyMode"] = mode;
////										hasProcessed = true;
////										if (mode != 0) shouldPublishStateChanged = true;
////									}
////									catch (Exception ex) { hasError = true; AppLogger.Error(ex, $"[CFX] 設置節能模式失敗，站號 {stn}"); }
////								}
////							}
////							break;

////						case "PowerSwitch":
////							var str = param.Value?.ToString();
////							if (str == "1" || str == "2")
////							{
////								bool isOn = str == "1";
////								foreach (var stn in stationNumbers)
////								{
////									if (!slaveData.ContainsKey(stn)) { hasError = true; continue; }
////									try
////									{
////										if (isOn)
////										{
////											modbusViewer.ExecuteSwitchCommand(ModbusHelper.SwitchON, new List<byte> { stn });
////											shouldPublishStateChanged = true;
////										}
////										else
////										{
////											modbusViewer.ExecuteSwitchCommand(ModbusHelper.SwitchOFF, new List<byte> { stn });
////										}
////										hasProcessed = true;
////									}
////									catch (Exception ex) { hasError = true; AppLogger.Error(ex, $"[CFX] 開關操作失敗，站號 {stn}"); }
////								}
////							}
////							break;
////					}
////				}

////				if (!hasProcessed)
////					return CreateErrorResponse(request.RequestID,
////						hasError ? "No parameters were processed successfully." :
////								   "No valid actions were performed.");

////				if (shouldPublishStateChanged)
////					PublishSafe(new StationStateChanged { NewState = ResourceState.NST_ShutdownAndStartup });

////				return CreateSuccessResponse(request.RequestID, "Parameters updated successfully.");
////			}
////			catch (Exception ex)
////			{
////				AppLogger.Error(ex, "[CFX] Request 處理錯誤");
////				return CreateErrorResponse(request.RequestID, ex.Message);
////			}
////		}
////		#endregion

////		#region Response-helpers
////		private static CFXEnvelope CreateSuccessResponse(string requestId, string msg)
////		{
////			var rsp = new ModifyStationParametersResponse
////			{
////				Result = new RequestResult
////				{
////					Result = StatusResult.Success,
////					ResultCode = 1,
////					Message = msg
////				}
////			};
////			var env = CFXEnvelope.FromCFXMessage(rsp);
////			env.RequestID = requestId;
////			return env;
////		}
////		private static CFXEnvelope CreateErrorResponse(string requestId, string error)
////		{
////			var rsp = new NotSupportedResponse
////			{
////				RequestResult = new RequestResult
////				{
////					Result = StatusResult.Failed,
////					ResultCode = 2,
////					Message = error
////				}
////			};
////			var env = CFXEnvelope.FromCFXMessage(rsp);
////			env.RequestID = requestId;
////			return env;
////		}
////		#endregion

////		#region Fault / Energy / Parameter 發布
////		private async Task PublishFaultOccurredMessages(byte stn, AmqpCFXEndpoint ep)
////		{
////			Dictionary<string, object> data;
////			if (!slaveData.TryGetValue(stn, out data) || !data.ContainsKey("Fault_WarningCode"))
////				return;

////			int statusRegister = Convert.ToInt32(data["Fault_WarningCode"]);
////			for (int bit = 0; bit <= 27; bit++)
////			{
////				if (((statusRegister >> bit) & 1) == 0) continue;
////				if (!faultDictionary.ContainsKey(bit)) continue;

////				var tuple = faultDictionary[bit];
////				var msg = new FaultOccurred
////				{
////					Fault = new Fault
////					{
////						FaultCode = tuple.Code,
////						FaultOccurrenceId = Guid.NewGuid(),
////						Description = tuple.ErrorDescription,
////						OccurredAt = DateTime.Now,
////						Severity = FaultSeverity.Error
////					}
////				};
////				await Task.Run(() => PublishSafe(msg));
////			}
////		}

////		private void PublishStationParametersModifiedMessages(byte stn, AmqpCFXEndpoint ep)
////		{
////			Dictionary<string, object> data;
////			if (!slaveData.TryGetValue(stn, out data) || data == null || data.Count == 0) return;

////			bool anyNonZero = data.Values.Any(v =>
////			{
////				if (v == null) return false;
////				if (v is int i) return i != 0;
////				if (v is double d) return Math.Abs(d) > 0.000001;
////				return false;
////			});
////			if (!anyNonZero) return;

////			var parameters = new List<Parameter>
////			{
////				new GenericParameter
////				{
////					Name  = "PowerSwitch",
////					Value = ConvertPowerSwitchToState(
////								data.ContainsKey("PowerSwitch") ? Convert.ToInt32(data["PowerSwitch"]) : 0).ToString()
////				},
////				new GenericParameter { Name = "EnergyMode",        Value = data.ContainsKey("EnergyMode")         ? data["EnergyMode"].ToString()         : "0" },
////				new GenericParameter { Name = "PowerTemperatureA", Value = data.ContainsKey("PowerTemperature_A") ? data["PowerTemperature_A"].ToString() : "0" },
////				new GenericParameter { Name = "PowerTemperatureB", Value = data.ContainsKey("PowerTemperature_B") ? data["PowerTemperature_B"].ToString() : "0" },
////				new GenericParameter { Name = "PowerTemperatureC", Value = data.ContainsKey("PowerTemperature_C") ? data["PowerTemperature_C"].ToString() : "0" }
////			};

////			PublishSafe(new StationParametersModified { ModifiedParameters = parameters });
////		}

////		private void PublishEnergyConsumedMessages(byte stn, AmqpCFXEndpoint ep, DateTime notBeforeUtc)
////		{
////			Dictionary<string, object> data;
////			if (!slaveData.TryGetValue(stn, out data) || data == null || data.Count == 0) return;

////			bool anyNonZero = data.Values.Any(v =>
////			{
////				if (v == null) return false;
////				if (v is int i) return i != 0;
////				if (v is double d) return Math.Abs(d) > 0.000001;
////				return false;
////			});
////			if (!anyNonZero) return;

////			double currentA = ConvertToDouble(data, "CurrentRYB_A");
////			double currentB = ConvertToDouble(data, "CurrentRYB_B");
////			double currentC = ConvertToDouble(data, "CurrentRYB_C");

////			double powerA = ConvertToDouble(data, "PowerRYB_A");
////			double powerB = ConvertToDouble(data, "PowerRYB_B");
////			double powerC = ConvertToDouble(data, "PowerRYB_C");

////			double energyUsed = ConvertToDouble(data, "EnergyUsed");
////			var nowUtc = DateTime.UtcNow;

////			var msg = new EnergyConsumed
////			{
////				EnergyUsed = energyUsed,
////				StartTime = nowUtc,
////				EndTime = nowUtc,
////				CurrentNowRYB = new List<double> { currentA, currentB, currentC },
////				PowerNowRYB = new List<double> { powerA, powerB, powerC }
////			};

////			var msgTimeUtc = msg.EndTime.ToUniversalTime();
////			if (msgTimeUtc >= notBeforeUtc)
////			{
////				PublishSafe(msg);
////				AppLogger.Info($"[CFX] Publish st.{stn} EnergyConsumed: {energyUsed:F2} kWh → LM");
////			}
////		}
////		#endregion

////		#region Utilities / helpers
////		private static double ConvertToDouble(Dictionary<string, object> data, string key)
////		{
////			if (!data.ContainsKey(key)) return 0.0;
////			var obj = data[key];
////			if (obj == null) return 0.0;
////			if (obj is double d) return d;
////			if (obj is int i) return i;
////			double v;
////			return double.TryParse(obj.ToString(), out v) ? v : 0.0;
////		}

////		public static int ConvertPowerSwitchToState(int v) => v == 1 ? 1 : (v == 0 ? 2 : -1);
////		#endregion

////		#region IDisposable
////		public void Dispose()
////		{
////			try { _amqpRetryCts?.Cancel(); } catch { }
////			_amqpRetryCts?.Dispose();
////			_watchdogTimer?.Dispose();
////			try { endpoint?.Close(); } catch { }
////		}
////		#endregion

////		#region 反射抓 Socket
////		// 完整取 Socket：取代原本的 ExtractSocket(...) 全部內容
////		private static Socket ExtractSocket(AmqpCFXEndpoint ep)
////		{
////			if (ep == null) return null;

////			// 1) 抓 channel 字典（欄位名可能是 _channels / channels / _cfxChannels / _channelsByUri）
////			var chFld = ep.GetType().GetField("_channels", BF) ??
////						ep.GetType().GetField("_cfxChannels", BF) ??
////						ep.GetType().GetField("_channelsByUri", BF) ??
////						ep.GetType().GetField("channels", BF);          // ★ 新增
////			if (chFld == null) return null;

////			var dict = chFld.GetValue(ep) as System.Collections.IDictionary;
////			if (dict == null || dict.Count == 0) return null;

////			foreach (var ch in dict.Values)
////			{
////				if (ch == null) continue;

////				// 2) 透過屬性或欄位拿 Amqp.Connection
////				var conn = ch.GetType().GetProperty("Connection", BF)?.GetValue(ch) ??
////						   ch.GetType().GetField("_connection", BF)?.GetValue(ch) ??
////						   ch.GetType().GetField("connection", BF)?.GetValue(ch);
////				if (conn == null) continue;

////				// --- 先檢 socket 本身 ---
////				var sock = TryGetSocketFromObject(conn);
////				if (sock != null) return sock;

////				// 3) 從 Connection 轉 Transport → InnerTransport... 一路往裡鑽
////				var transport = conn.GetType().GetProperty("Transport", BF)?.GetValue(conn) ??
////								conn.GetType().GetField("_transport", BF)?.GetValue(conn) ??
////								conn.GetType().GetField("transport", BF)?.GetValue(conn);

////				while (transport != null)
////				{
////					sock = TryGetSocketFromObject(transport);
////					if (sock != null) return sock;

////					// 再往下一層
////					transport = transport.GetType().GetProperty("InnerTransport", BF)?.GetValue(transport) ??
////								transport.GetType().GetField("_innerTransport", BF)?.GetValue(transport) ??
////								transport.GetType().GetField("innerStream", BF)?.GetValue(transport);
////				}
////			}
////			return null;    // 仍找不到
////		}



////		// === 修改結束 ==================================================================

////		#endregion

////		// === 修改開始：新增 TryEnableKeepAliveAsync 方法 -------------------------------
////		private async Task TryEnableKeepAliveAsync(int attempts, int delayMs, bool verbose = false)
////		{
////			for (int i = 0; i < attempts && !_kaSet; i++)
////			{
////				var sock = ExtractSocket(endpoint);
////				if (sock != null)
////				{
////					try
////					{
////						KeepAliveHelper.EnableKeepAlive(sock, 30, 10);
////						AppLogger.Info("[AMQP] TCP KeepAlive 已啟用：Idle=30s, Interval=10s");
////						_kaSet = true;
////						return;
////					}
////					catch (Exception ex)
////					{
////						AppLogger.Warn($"[AMQP] 設定 KeepAlive 失敗：{ex.Message}");
////						return;                     // 有抓到但設失敗，直接離開
////					}
////				}

////				if (verbose)
////					AppLogger.Info($"[AMQP] (KA) still no socket, pass {i + 1}/{attempts}");

////				await Task.Delay(delayMs);
////			}

////			if (!_kaSet)
////				AppLogger.Warn("[AMQP] 無法在限制時間內取得 Socket，未設定 Keep-Alive");
////		}

////		// === 修改結束 -----------------------------------------------------------------





////	}
////}


//// AmqpEndpointManager.cs   （C# 8 相容版）
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
//	public class AmqpEndpointManager : IDisposable
//	{
//		private readonly string endpointUri;
//		private readonly string publishChannelUri;
//		private readonly string subscribeChannelUri;

//		private readonly ModbusViewer modbusViewer;
//		private readonly SerialPort serialPort;               // 目前未用
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

//		// ───── 連線監測 ─────
//		private volatile bool _publishLoopStarted = false;
//		private Timer _watchdogTimer;            // 目前僅保留，日後如需額外 timer 可沿用
//		private DateTime _reconnectedAtUtc;
//		private readonly CancellationTokenSource _idleWatchdogCts = new CancellationTokenSource();
//		private DateTime _lastSendUtc = DateTime.UtcNow;
//		private volatile bool _kaSet = false;
//		// 共用 BindingFlags
//		private const BindingFlags BF = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;
//		// ────────────────────

//		// gate／seed
//		private int _publishLoopGate = 0;
//		private int _connectLoopGate = 0;
//		private long _loopIdSeed = 0;

//		// ───── 故障對照表 ─────
//		private static readonly Dictionary<int, (string Code, string ErrorDescription)> faultDictionary =
//			new Dictionary<int, (string Code, string ErrorDescription)>
//		{
//			{ 0 , ("EGY_1_WARN1" , "A相過壓") },
//			{ 1 , ("EGY_1_WARN2" , "B相過壓") },
//			{ 2 , ("EGY_1_WARN3" , "C相過壓") },
//			{ 3 , ("EGY_1_WARN4" , "A相欠壓") },
//			{ 4 , ("EGY_1_WARN5" , "B相欠壓") },
//			{ 5 , ("EGY_1_WARN6" , "C相過流") },
//			{ 6 , ("EGY_1_WARN7" , "A相過流") },
//			{ 7 , ("EGY_1_WARN8" , "B相過流") },
//			{ 8 , ("EGY_1_WARN9" , "C相過流") },
//			{ 9 , ("EGY_1_WARN10", "漏電異常") },
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
//		// ────────────────────

//		// ---------- 發布包裝 ----------
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
//					_lastSendUtc = DateTime.UtcNow;   // 更新 idle 水位
//				}
//				catch (InvalidOperationException ex)
//				{
//					LogThrottler.Every("cfx_pub_enum", 5,
//						() => AppLogger.Warn("[CFX] 發布循環內部列舉衝突（已略過一次）： " + ex.Message));
//				}
//				catch (Exception ex)
//				{
//					AppLogger.Error(ex, "[CFX] Publish 例外");
//				}
//			}
//		}
//		// --------------------------------

//		public AmqpEndpointManager(
//			string endpointUri,
//			string publishChannelUri,
//			string subscribeChannelUri,

//			ModbusViewer modbusViewer,
//			SerialPort serialPort,
//			Dictionary<byte, Dictionary<string, object>> slaveData
//			){

//			this.endpointUri = endpointUri;
//			this.publishChannelUri = publishChannelUri;
//			this.subscribeChannelUri = string.IsNullOrWhiteSpace(subscribeChannelUri) ? null : subscribeChannelUri;

//			this.modbusViewer = modbusViewer;
//			this.serialPort = serialPort;
//			this.slaveData = slaveData;

//			AddExpectedUri(publishChannelUri);
//			if (!string.IsNullOrWhiteSpace(this.subscribeChannelUri))
//				AddExpectedUri(this.subscribeChannelUri);
//		}

//		// ───── URI 轉正規化/收集 ─────
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
//		// ──────────────────────────────

//		#region 對外：Modbus 健康狀態切換
//		public void SetModbusHealthy(bool healthy)
//		{
//			if (_modbusHealthy == healthy) return;
//			_modbusHealthy = healthy;

//			AppLogger.Info(healthy ? "[MODBUS] 恢復連線，恢復 CFX 發布。" :
//									 "[MODBUS] 中斷，暫停 CFX 發布。");

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
//		#endregion

//		#region AMQP 連線確保
//		public async Task EnsureConnectedAsync(string endpointName, CancellationToken ct = default)
//		{
//			if (_amqpConnected) return;

//			if (Interlocked.CompareExchange(ref _connectLoopGate, 1, 0) != 0) return;   // gate

//			_amqpRetryCts?.Cancel();
//			_amqpRetryCts = CancellationTokenSource.CreateLinkedTokenSource(ct);

//			_ = Task.Run(async () =>
//			{
//				bool aborted = false;
//				try
//				{
//					while (!_amqpConnected &&
//						   _retryCount < _maxAttempts &&
//						   !_amqpRetryCts.IsCancellationRequested)
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
//							_kaSet = false;                    // 每次新起端點先清旗標
//							var ok = await SpinWaitUntilConnectedAsync(TimeSpan.FromSeconds(60), _amqpRetryCts.Token);
//							if (!ok) throw new Exception("handshake timeout");
//							AppLogger.Info($"[AMQP] 連線成功（第 {_retryCount}/{_maxAttempts} 次）");
//						}
//						catch (Exception ex)
//						{
//							_amqpConnected = false;
//							AppLogger.Warn($"[AMQP] 連線失敗（第 {_retryCount}/{_maxAttempts} 次，{_retryInterval.TotalSeconds:N0}s 後再試）：{ex.Message}");
//							try { await Task.Delay(_retryInterval, _amqpRetryCts.Token); } catch { }
//						}
//					}
//				}
//				catch (OperationCanceledException) { aborted = true; }
//				finally
//				{
//					Interlocked.Exchange(ref _connectLoopGate, 0);
//					if (!aborted && !_amqpConnected && _retryCount >= _maxAttempts)
//						AppLogger.Warn("[AMQP] 重試已達上限，請檢查設定（PublishAddress / MyRequestUri / SubscribeAddress）");
//				}
//			}, _amqpRetryCts.Token);
//		}
//		#endregion

//		#region 啟動 Endpoint
//		public void StartAmqpEndpoint(string endpointName)
//		{
//			if (string.IsNullOrWhiteSpace(endpointName))
//				throw new ArgumentException("endpointName 不能為空白", nameof(endpointName));

//			try { endpoint?.Close(); } catch { }

//			endpoint = new AmqpCFXEndpoint { HeartbeatFrequency = TimeSpan.FromSeconds(60) };
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

//			// Idle-watchdog（單一實例即可）
//			_ = Task.Run(IdleWatchdogLoop, _idleWatchdogCts.Token);
//		}
//		#endregion

//		#region Idle-watchdog
//		private async Task IdleWatchdogLoop()
//		{
//			while (!_idleWatchdogCts.IsCancellationRequested)
//			{
//				try
//				{
//					if (_amqpConnected &&
//						(DateTime.UtcNow - _lastSendUtc) > TimeSpan.FromSeconds(90))
//					{
//						AppLogger.Warn("[AMQP] 消息發布 idle >90s, force reconnect");
//						try { endpoint?.Close(); } catch { }
//						_amqpConnected = false;
//						_publishLoopStarted = false;
//						if (_modbusHealthy)
//							_ = EnsureConnectedAsync(endpoint?.CFXHandle ?? "Unknown");
//					}
//				}
//				catch { /* swallow */ }

//				await Task.Delay(30_000, _idleWatchdogCts.Token);
//			}
//		}
//		#endregion

//		#region 連線事件處理
//		// --- HandleConnectionEvent 替換整個方法 ---
//		private async void HandleConnectionEvent(
//			ConnectionEvent evt, Uri uri, int spool, string info, Exception ex)
//		{
//			string key = Normalize(uri.ToString());
//			bool isUp = evt == ConnectionEvent.ConnectionEstablished;
//			_uriState[key] = isUp;

//			bool allOk = _expectedUris.All(u =>
//			{
//				bool ok; return _uriState.TryGetValue(u, out ok) && ok;
//			});

//			// ── 所有通道 OK → 標記 Connected, 啟動 PublishLoop ──
//			if (allOk && !_amqpConnected)
//			{
//				_reconnectedAtUtc = DateTime.UtcNow;
//				_amqpConnected = true;

//				// 只在這裡試一次 Keep-Alive（最多 60 回，安靜模式）
//				//_ = TryEnableKeepAliveAsync(attempts: 60, delayMs: 1000, verbose: false);
//				_ = TryEnableKeepAliveAsync(attempts: 60, delayMs: 1000, verbose: true);


//				AppLogger.Info("[AMQP] ✓ 所有通道握手完成：\n  └ " +
//							   string.Join("\n  └ ", _expectedUris));
//				PublishSafe(new EndpointConnected());
//				TryStartPublishLoop();
//			}
//			// 任一通道掉線 → 關閉端點，重試
//			else if (!allOk && _amqpConnected &&
//					 (evt == ConnectionEvent.ConnectionInterrupted ||
//					  evt == ConnectionEvent.ConnectionClosed ||
//					  evt == ConnectionEvent.ConnectionFailed))
//			{
//				_amqpConnected = false;
//				_publishLoopStarted = false;
//				AppLogger.Warn("[AMQP] ✘ 通道中斷：" + evt + " → " + key + "，準備重試…");
//				try { endpoint?.Close(); } catch { }

//				if (_modbusHealthy)
//					_ = EnsureConnectedAsync(endpoint.CFXHandle);
//			}
//		}

//		#endregion

//		#region Publish-loop Gate
//		private void TryStartPublishLoop()
//		{
//			if (!_amqpConnected || !_modbusHealthy) return;
//			if (Interlocked.CompareExchange(ref _publishLoopGate, 1, 0) != 0) return;
//			_ = Task.Run(StartPublishLoop);
//		}
//		#endregion

//		#region Spin-wait until Connected
//		private async Task<bool> SpinWaitUntilConnectedAsync(TimeSpan timeout, CancellationToken ct)
//		{
//			var start = DateTime.UtcNow;
//			while (DateTime.UtcNow - start < timeout &&
//				   !_amqpConnected &&
//				   !ct.IsCancellationRequested)
//			{
//				await Task.Delay(200, ct);
//			}
//			return _amqpConnected;
//		}
//		#endregion

//		#region Publish-loop
//		private async Task StartPublishLoop()
//		{
//			var loopId = Interlocked.Increment(ref _loopIdSeed);
//			var energyPeriod = TimeSpan.FromSeconds(60);
//			var nextEnergyDue = DateTime.UtcNow;

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
//						// 1) Fault（僅變化才送）
//						foreach (var stn in slaveData.Keys.ToList())
//						{
//							object obj;
//							if (slaveData[stn].TryGetValue("Fault_WarningCode", out obj))
//							{
//								int cur = Convert.ToInt32(obj);
//								if (!lastFault.TryGetValue(stn, out var prev) || prev != cur)
//								{
//									await PublishFaultOccurredMessages(stn, endpoint);
//									lastFault[stn] = cur;
//								}
//							}
//						}

//						// 2) Energy / Parameters 每 60s
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
//				Interlocked.Exchange(ref _publishLoopGate, 0);
//			}
//		}
//		#endregion

//		#region Request-handler
//		private CFXEnvelope OnRequestReceivedHandler(CFXEnvelope request)
//		{
//			try
//			{
//				if (!(request.MessageBody is ModifyStationParametersRequest modifyRequest))
//					return CreateErrorResponse(request.RequestID, "Unsupported request type.");

//				AppLogger.Info("[CFX] 收到 ModifyStationParametersRequest: " +
//							   JsonConvert.SerializeObject(modifyRequest, Formatting.None));

//				// 1) 解析站號
//				var stationNumbers = modifyRequest.NewParameters
//					.OfType<GenericParameter>()
//					.Where(g => g.Name == "站號")
//					.Select(g =>
//					{
//						byte v;
//						return byte.TryParse(g.Value?.ToString(), out v) ? (byte?)v : null;
//					})
//					.Where(p => p.HasValue)
//					.Select(p => p.Value)
//					.ToList();

//				if (stationNumbers.Count == 0)
//					return CreateErrorResponse(request.RequestID, "No valid station numbers provided.");

//				bool hasProcessed = false;
//				bool hasError = false;
//				bool shouldPublishStateChanged = false;

//				foreach (var param in modifyRequest.NewParameters.OfType<GenericParameter>())
//				{
//					switch (param.Name)
//					{
//						case "TemperatureSV":
//							ushort tempVal;
//							if (ushort.TryParse(param.Value?.ToString(), out tempVal))
//							{
//								foreach (var stn in stationNumbers)
//								{
//									if (!slaveData.ContainsKey(stn)) { hasError = true; continue; }
//									try { modbusViewer.ExecuteSetTemperature(stn, tempVal); hasProcessed = true; }
//									catch (Exception ex) { hasError = true; AppLogger.Error(ex, $"[CFX] 設置溫度失敗，站號 {stn}"); }
//								}
//							}
//							break;

//						case "EnergyMode":
//							int mode;
//							if (int.TryParse(param.Value?.ToString(), out mode) && mode >= 0 && mode <= 2)
//							{
//								foreach (var stn in stationNumbers)
//								{
//									if (!slaveData.ContainsKey(stn)) { hasError = true; continue; }
//									try
//									{
//										slaveData[stn]["EnergyMode"] = mode;
//										hasProcessed = true;
//										if (mode != 0) shouldPublishStateChanged = true;
//									}
//									catch (Exception ex) { hasError = true; AppLogger.Error(ex, $"[CFX] 設置節能模式失敗，站號 {stn}"); }
//								}
//							}
//							break;

//						case "PowerSwitch":
//							var str = param.Value?.ToString();
//							if (str == "1" || str == "2")
//							{
//								bool isOn = str == "1";
//								foreach (var stn in stationNumbers)
//								{
//									if (!slaveData.ContainsKey(stn)) { hasError = true; continue; }
//									try
//									{
//										if (isOn)
//										{
//											modbusViewer.ExecuteSwitchCommand(ModbusHelper.SwitchON, new List<byte> { stn });
//											shouldPublishStateChanged = true;
//										}
//										else
//										{
//											modbusViewer.ExecuteSwitchCommand(ModbusHelper.SwitchOFF, new List<byte> { stn });
//										}
//										hasProcessed = true;
//									}
//									catch (Exception ex) { hasError = true; AppLogger.Error(ex, $"[CFX] 開關操作失敗，站號 {stn}"); }
//								}
//							}
//							break;
//					}
//				}

//				if (!hasProcessed)
//					return CreateErrorResponse(request.RequestID,
//						hasError ? "No parameters were processed successfully." :
//								   "No valid actions were performed.");

//				if (shouldPublishStateChanged)
//					PublishSafe(new StationStateChanged { NewState = ResourceState.NST_ShutdownAndStartup });

//				return CreateSuccessResponse(request.RequestID, "Parameters updated successfully.");
//			}
//			catch (Exception ex)
//			{
//				AppLogger.Error(ex, "[CFX] Request 處理錯誤");
//				return CreateErrorResponse(request.RequestID, ex.Message);
//			}
//		}
//		#endregion

//		#region Response-helpers
//		private static CFXEnvelope CreateSuccessResponse(string requestId, string msg)
//		{
//			var rsp = new ModifyStationParametersResponse
//			{
//				Result = new RequestResult
//				{
//					Result = StatusResult.Success,
//					ResultCode = 1,
//					Message = msg
//				}
//			};
//			var env = CFXEnvelope.FromCFXMessage(rsp);
//			env.RequestID = requestId;
//			return env;
//		}
//		private static CFXEnvelope CreateErrorResponse(string requestId, string error)
//		{
//			var rsp = new NotSupportedResponse
//			{
//				RequestResult = new RequestResult
//				{
//					Result = StatusResult.Failed,
//					ResultCode = 2,
//					Message = error
//				}
//			};
//			var env = CFXEnvelope.FromCFXMessage(rsp);
//			env.RequestID = requestId;
//			return env;
//		}
//		#endregion

//		#region Fault / Energy / Parameter 發布
//		private async Task PublishFaultOccurredMessages(byte stn, AmqpCFXEndpoint ep)
//		{
//			Dictionary<string, object> data;
//			if (!slaveData.TryGetValue(stn, out data) || !data.ContainsKey("Fault_WarningCode"))
//				return;

//			int statusRegister = Convert.ToInt32(data["Fault_WarningCode"]);
//			for (int bit = 0; bit <= 27; bit++)
//			{
//				if (((statusRegister >> bit) & 1) == 0) continue;
//				if (!faultDictionary.ContainsKey(bit)) continue;

//				var tuple = faultDictionary[bit];
//				var msg = new FaultOccurred
//				{
//					Fault = new Fault
//					{
//						FaultCode = tuple.Code,
//						FaultOccurrenceId = Guid.NewGuid(),
//						Description = tuple.ErrorDescription,
//						OccurredAt = DateTime.Now,
//						Severity = FaultSeverity.Error
//					}
//				};
//				await Task.Run(() => PublishSafe(msg));
//			}
//		}

//		private void PublishStationParametersModifiedMessages(byte stn, AmqpCFXEndpoint ep)
//		{
//			Dictionary<string, object> data;
//			if (!slaveData.TryGetValue(stn, out data) || data == null || data.Count == 0) return;

//			//bool anyNonZero = data.Values.Any(v =>
//			//{
//			//	if (v == null) return false;
//			//	if (v is int i) return i != 0;
//			//	if (v is double d) return Math.Abs(d) > 0.000001;
//			//	return false;
//			//});
//			//if (!anyNonZero) return;

//			var parameters = new List<Parameter>
//			{
//				new GenericParameter
//				{
//					Name  = "PowerSwitch",
//					Value = ConvertPowerSwitchToState(
//								data.ContainsKey("PowerSwitch") ? Convert.ToInt32(data["PowerSwitch"]) : 0).ToString()
//				},
//				new GenericParameter { Name = "EnergyMode",        Value = data.ContainsKey("EnergyMode")         ? data["EnergyMode"].ToString()         : "0" },
//				new GenericParameter { Name = "PowerTemperatureA", Value = data.ContainsKey("PowerTemperature_A") ? data["PowerTemperature_A"].ToString() : "0" },
//				new GenericParameter { Name = "PowerTemperatureB", Value = data.ContainsKey("PowerTemperature_B") ? data["PowerTemperature_B"].ToString() : "0" },
//				new GenericParameter { Name = "PowerTemperatureC", Value = data.ContainsKey("PowerTemperature_C") ? data["PowerTemperature_C"].ToString() : "0" }
//			};

//			PublishSafe(new StationParametersModified { ModifiedParameters = parameters });
//		}

//		private void PublishEnergyConsumedMessages(byte stn, AmqpCFXEndpoint ep, DateTime notBeforeUtc)
//		{
//			Dictionary<string, object> data;
//			if (!slaveData.TryGetValue(stn, out data) || data == null || data.Count == 0) return;

//			//bool anyNonZero = data.Values.Any(v =>
//			//{
//			//	if (v == null) return false;
//			//	if (v is int i) return i != 0;
//			//	if (v is double d) return Math.Abs(d) > 0.000001;
//			//	return false;
//			//});
//			//if (!anyNonZero) return;

//			double currentA = ConvertToDouble(data, "CurrentRYB_A");
//			double currentB = ConvertToDouble(data, "CurrentRYB_B");
//			double currentC = ConvertToDouble(data, "CurrentRYB_C");

//			double powerA = ConvertToDouble(data, "PowerRYB_A");
//			double powerB = ConvertToDouble(data, "PowerRYB_B");
//			double powerC = ConvertToDouble(data, "PowerRYB_C");

//			double energyUsed = ConvertToDouble(data, "EnergyUsed");
//			var nowUtc = DateTime.UtcNow;

//			var msg = new EnergyConsumed
//			{
//				EnergyUsed = energyUsed,
//				StartTime = nowUtc,
//				EndTime = nowUtc,
//				CurrentNowRYB = new List<double> { currentA, currentB, currentC },
//				PowerNowRYB = new List<double> { powerA, powerB, powerC }
//			};

//			var msgTimeUtc = msg.EndTime.ToUniversalTime();
//			if (msgTimeUtc >= notBeforeUtc)
//			{
//				PublishSafe(msg);
//				AppLogger.Info($"[CFX] Publish st.{stn} EnergyConsumed: {energyUsed:F2} kWh → LM");
//			}
//		}
//		#endregion

//		#region Utilities / helpers
//		private static double ConvertToDouble(Dictionary<string, object> data, string key)
//		{
//			if (!data.ContainsKey(key)) return 0.0;
//			var obj = data[key];
//			if (obj == null) return 0.0;
//			if (obj is double d) return d;
//			if (obj is int i) return i;
//			double v;
//			return double.TryParse(obj.ToString(), out v) ? v : 0.0;
//		}

//		public static int ConvertPowerSwitchToState(int v) => v == 1 ? 1 : (v == 0 ? 2 : -1);
//		#endregion

//		#region IDisposable
//		public void Dispose()
//		{
//			try { _amqpRetryCts?.Cancel(); } catch { }
//			_amqpRetryCts?.Dispose();
//			_watchdogTimer?.Dispose();
//			try { endpoint?.Close(); } catch { }
//		}
//		#endregion

//		#region 反射抓 Socket
//		// 取 Socket 的工具函式
//		private static Socket TryGetSocketFromObject(object obj)
//		{
//			if (obj == null) return null;

//			foreach (var name in new[] { "_socket", "socket", "tcp", "streamSocket" })
//			{
//				var f = obj.GetType().GetField(name, BF);
//				if (f != null && f.GetValue(obj) is Socket s) return s;
//			}

//			var any = obj.GetType()
//						 .GetFields(BF)
//						 .FirstOrDefault(f => typeof(Socket).IsAssignableFrom(f.FieldType));
//			return any?.GetValue(obj) as Socket;
//		}

//		// 完整 ExtractSocket
//		private static Socket ExtractSocket(AmqpCFXEndpoint ep)
//		{
//			if (ep == null) return null;

//			var chFld = ep.GetType().GetField("_channels", BF) ??
//						ep.GetType().GetField("_cfxChannels", BF) ??
//						ep.GetType().GetField("_channelsByUri", BF) ??
//						ep.GetType().GetField("channels", BF);
//			if (chFld == null) return null;

//			var dict = chFld.GetValue(ep) as System.Collections.IDictionary;
//			if (dict == null || dict.Count == 0) return null;

//			foreach (var ch in dict.Values)
//			{
//				if (ch == null) continue;

//				var conn = ch.GetType().GetProperty("Connection", BF)?.GetValue(ch) ??
//						   ch.GetType().GetField("_connection", BF)?.GetValue(ch) ??
//						   ch.GetType().GetField("connection", BF)?.GetValue(ch);
//				if (conn == null) continue;

//				var sock = TryGetSocketFromObject(conn);
//				if (sock != null) return sock;

//				var transport = conn.GetType().GetProperty("Transport", BF)?.GetValue(conn) ??
//								conn.GetType().GetField("_transport", BF)?.GetValue(conn) ??
//								conn.GetType().GetField("transport", BF)?.GetValue(conn);

//				while (transport != null)
//				{
//					sock = TryGetSocketFromObject(transport);
//					if (sock != null) return sock;

//					transport = transport.GetType().GetProperty("InnerTransport", BF)?.GetValue(transport) ??
//								transport.GetType().GetField("_innerTransport", BF)?.GetValue(transport) ??
//								transport.GetType().GetField("innerStream", BF)?.GetValue(transport);
//				}
//			}
//			return null;
//		}
//		#endregion

//		#region TryEnableKeepAliveAsync
//		// --- TryEnableKeepAliveAsync 全部替換 ---
//		private async Task TryEnableKeepAliveAsync(int attempts = 60, int delayMs = 1000, bool verbose = false)
//		{
//			for (int i = 0; i < attempts && !_kaSet; i++)
//			{
//				var sock = ExtractSocket(endpoint);
//				if (sock != null)
//				{
//					try
//					{
//						KeepAliveHelper.EnableKeepAlive(sock, 30, 10);
//						AppLogger.Info("[AMQP] TCP KeepAlive 已啟用：Idle=30s, Interval=10s");
//						_kaSet = true;
//						return;
//					}
//					catch (Exception ex)
//					{
//						AppLogger.Warn($"[AMQP] 設定 KeepAlive 失敗：{ex.Message}");
//						return;        // 抓到但設失敗，直接離開
//					}
//				}

//				if (verbose)
//				{
//					// 每 5 次才寫一次日誌，避免洗版
//					if (i % 5 == 0)
//						AppLogger.Info($"[AMQP] (KA) still no socket, pass {i + 1}/{attempts}");
//				}

//				await Task.Delay(delayMs);
//			}

//			if (!_kaSet && verbose)
//				AppLogger.Warn("[AMQP] 無法在限制時間內取得 Socket，未設定 Keep-Alive");
//		}

//		#endregion
//	}
//}

