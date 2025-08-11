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
//using System.Threading;
//using System.Threading.Tasks;

//namespace AmqpModbusIntegration
//{
//	public class AmqpEndpointManager
//	{
//		private readonly string endpointUri;
//		private readonly string publishChannelUri;
//		private readonly string subscribeChannelUri;

//		private readonly ModbusViewer modbusViewer;
//		private readonly SerialPort serialPort;
//		private readonly Dictionary<byte, Dictionary<string, object>> slaveData;

//		// === 重連 LM 機制 ===
//		private bool _amqpConnected = false;
//		private int _retryCount = 0;
//		private const int _maxAttempts = 9999;
//		private readonly TimeSpan _retryInterval = TimeSpan.FromMinutes(1);
//		private CancellationTokenSource _amqpRetryCts;

//		// Endpoint / 狀態
//		private AmqpCFXEndpoint endpoint;
//		private readonly HashSet<string> _expectedUris = new HashSet<string>();
//		private readonly ConcurrentDictionary<string, bool> _uriState = new ConcurrentDictionary<string, bool>();
//		private bool _publishLoopStarted = false;   // ★ 防止重複啟動 PublishLoop

//		// 用於串口操作的鎖
//		public static readonly object serialPortLock = new object();

//		private static readonly Dictionary<int, (string Code, string ErrorDescription)> faultDictionary = new Dictionary<int, (string Code, string ErrorDescription)>
//		{
//			{ 0,  ("EGY_1_WARN1",  "A相過壓") },
//			{ 1,  ("EGY_1_WARN2",  "B相過壓") },
//			{ 2,  ("EGY_1_WARN3",  "C相過壓") },
//			{ 3,  ("EGY_1_WARN4",  "A相欠壓") },
//			{ 4,  ("EGY_1_WARN5",  "B相欠壓") },
//			{ 5,  ("EGY_1_WARN6",  "C相欠壓") },
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

//		// --------- ctor ---------
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
//			this.subscribeChannelUri = subscribeChannelUri;

//			this.modbusViewer = modbusViewer;
//			this.serialPort = serialPort;
//			this.slaveData = slaveData;

//			// 先把可能會用到的 URI 全部正規化後放進 expected（後面 StartAmqpEndpoint 會再清一次）
//			AddExpectedUri(endpointUri);
//			AddExpectedUri(publishChannelUri);
//			AddExpectedUri(subscribeChannelUri);
//		}

//		// 統一 URI 字串（避免 / 差異）
//		private static string Normalize(string u)
//		{
//			if (string.IsNullOrWhiteSpace(u)) return null;
//			return new Uri(u).ToString();   // 會補上結尾 '/'
//		}

//		private void AddExpectedUri(string u)
//		{
//			var n = Normalize(u);
//			if (!string.IsNullOrWhiteSpace(n))
//				_expectedUris.Add(n);
//		}

//		private volatile bool _modbusHealthy = false;
//		public void SetModbusHealthy(bool healthy)
//		{
//			if (_modbusHealthy != healthy)
//			_modbusHealthy = healthy;
//		}



//		/// <summary>
//		/// 若 AMQP 尚未連線則自動重試，直到成功或達到上限。
//		/// </summary>
//		public async Task EnsureConnectedAsync(string endpointName, CancellationToken ct = default)
//		{
//			if (_amqpConnected) return;

//			_amqpRetryCts?.Cancel();
//			_amqpRetryCts = CancellationTokenSource.CreateLinkedTokenSource(ct);

//			_ = Task.Run(async () =>
//			{
//				while (!_amqpConnected &&
//					   _retryCount < _maxAttempts &&
//					   !_amqpRetryCts.IsCancellationRequested)
//				{
//					_retryCount++;
//					try
//					{
//						StartAmqpEndpoint(endpointName);     // 啟端點，真正握手後會在事件裡把 _amqpConnected 設為 true

//						// 等待握手真正完成
//						bool ok = await SpinWaitUntilConnectedAsync(TimeSpan.FromSeconds(60), _amqpRetryCts.Token);
//						if (!ok) throw new Exception("handshake timeout");

//						modbusViewer.AppendLog($"AMQP 連線成功（第 {_retryCount}/{_maxAttempts} 次）");
//					}
//					catch (Exception ex)
//					{
//						_amqpConnected = false;              // 確保失敗時為 false
//						modbusViewer.AppendLog($"AMQP 連線失敗（第 {_retryCount}/{_maxAttempts} 次，1分鐘後再試）: {ex.Message}");
//						await Task.Delay(_retryInterval, _amqpRetryCts.Token);
//					}
//				}

//				if (!_amqpConnected)
//					modbusViewer.AppendLog("AMQP 重試已達上限，請檢查 PublishAddress / MyRequestUri");
//			},
//			_amqpRetryCts.Token);
//		}

//		/// <summary>
//		/// 初始化並啟動 AMQP 端點。
//		/// </summary>
//		public void StartAmqpEndpoint(string endpointName)
//		{
//			if (string.IsNullOrWhiteSpace(endpointName))
//				throw new ArgumentException("endpointName 不能為空白", nameof(endpointName));

//			try { endpoint?.Close(); } catch { }


//			//這邊是設定心跳頻率
//			endpoint = new AmqpCFXEndpoint
//			{
//				HeartbeatFrequency = TimeSpan.FromSeconds(60)
//			};

//			// 統一使用單一事件處理
//			endpoint.OnConnectionEvent += HandleConnectionEvent;
//			endpoint.OnRequestReceived += OnRequestReceivedHandler;

//			// 重新整理 _expectedUris（只保留這次真的會用到的）
//			_expectedUris.Clear();
//			//AddExpectedUri(endpointUri);
//			AddExpectedUri(publishChannelUri);
//			if (!string.IsNullOrWhiteSpace(subscribeChannelUri))
//				AddExpectedUri(subscribeChannelUri);

//			// 先都標 false
//			foreach (var u in _expectedUris)
//				_uriState[u] = false;

//			// 開 RequestUri
//			endpoint.Open(endpointName, new Uri(endpointUri));

//			// Publish channel
//			endpoint.AddPublishChannel(new Uri(publishChannelUri), "event");

//			// Subscribe（如果需要）
//			if (!string.IsNullOrWhiteSpace(subscribeChannelUri))
//				endpoint.AddSubscribeChannel(new Uri(subscribeChannelUri), "subscribe");

//			modbusViewer.AppendLog("AMQP 端點已啟動，等待通道握手…");
//		}

//		// 統一事件處理
//		private void HandleConnectionEvent(ConnectionEvent evt, Uri uri, int spool, string info, Exception ex)
//		{
//			string key = Normalize(uri.ToString());
//			bool isUp = evt == ConnectionEvent.ConnectionEstablished;

//			_uriState[key] = isUp;

//			bool allOk = _expectedUris.All(u => _uriState.TryGetValue(u, out var ok) && ok);

//			if (allOk && !_amqpConnected)
//			{
//				_amqpConnected = true;
//				modbusViewer.AppendLog($"✓ AMQP 所有通道握手完成：\n  └ {string.Join("\n  └ ", _expectedUris)}");

//				endpoint.Publish(new EndpointConnected());

//				if (!_publishLoopStarted)
//				{
//					_publishLoopStarted = true;
//					_ = Task.Run(StartPublishLoop);
//				}
//			}
//			else if (!allOk && _amqpConnected &&
//					(evt == ConnectionEvent.ConnectionInterrupted || evt == ConnectionEvent.ConnectionClosed || evt == ConnectionEvent.ConnectionFailed))
//			{
//				_amqpConnected = false;
//				_publishLoopStarted = false;   // 讓下次連回來能再啟 PublishLoop
//				modbusViewer.AppendLog($"✘ AMQP 通道中斷：{evt} → {key}，準備重試…");

//				// 再次進入重連
//				_ = EnsureConnectedAsync(endpoint.CFXHandle);
//			}
//		}

//		private async Task<bool> SpinWaitUntilConnectedAsync(TimeSpan timeout, CancellationToken ct)
//		{
//			var start = DateTime.UtcNow;
//			while (DateTime.UtcNow - start < timeout && !_amqpConnected && !ct.IsCancellationRequested)
//				await Task.Delay(500, ct);
//			return _amqpConnected;
//		}

//		// --------------------------------------------------------
//		//  背景發佈循環
//		// --------------------------------------------------------
//		private async Task StartPublishLoop()
//		{
//			var lastFault = new Dictionary<byte, int>();
//			var lastEnergy = DateTime.MinValue;

//			modbusViewer.AppendLog("CFX PublishLoop 啟動");

//			while (_amqpConnected)
//			{


//				//這邊不注解掉就沒辦法發布錯誤和能源消息
//				if (!_modbusHealthy)
//				{
//					await Task.Delay(1000);     // Modbus 掛了就暫停
//					continue;
//				}

//				try
//				{
//					// 1) Fault
//					foreach (var stn in slaveData.Keys)
//					{
//						if (slaveData[stn].TryGetValue("Fault_WarningCode", out var obj))
//						{
//							int cur = Convert.ToInt32(obj);
//							if (lastFault.TryGetValue(stn, out var prev) && prev != cur)
//								await PublishFaultOccurredMessages(stn, endpoint);
//							lastFault[stn] = cur;
//						}
//					}

//					// 2) 每 60 秒能源 / 參數
//					if ((DateTime.Now - lastEnergy).TotalSeconds >= 60)
//					{
//						foreach (var stn in slaveData.Keys)
//						{
//							PublishStationParametersModifiedMessages(stn, endpoint);
//							PublishEnergyConsumedMessages(stn, endpoint);
//						}
//						lastEnergy = DateTime.Now;
//					}
//				}
//				catch (Exception ex)
//				{
//					modbusViewer.AppendLog($"發布循環錯誤：{ex.Message}");
//				}

//				await Task.Delay(1000);
//			}

//			modbusViewer.AppendLog("PublishLoop 結束");
//		}

//		// --------------------------------------------------------
//		//  Request Handler
//		// --------------------------------------------------------
//		private CFXEnvelope OnRequestReceivedHandler(CFXEnvelope request)
//		{
//			try
//			{
//				if (request.MessageBody is ModifyStationParametersRequest modifyRequest)
//				{
//					Console.WriteLine($"收到 ModifyStationParametersRequest: {JsonConvert.SerializeObject(modifyRequest, Formatting.Indented)}");

//					var stationNumbers = modifyRequest.NewParameters
//						.OfType<GenericParameter>()
//						.Where(g => g.Name == "站號")
//						.Select(g =>
//						{
//							if (byte.TryParse(g.Value.ToString(), out var stn))
//								return (byte?)stn;
//							return null;
//						})
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
//								if (ushort.TryParse(param.Value.ToString(), out var tempVal))
//								{
//									foreach (var stn in stationNumbers)
//									{
//										if (!slaveData.ContainsKey(stn)) { hasError = true; continue; }
//										try
//										{
//											modbusViewer.ExecuteSetTemperature(stn, tempVal);
//											hasProcessed = true;
//										}
//										catch (Exception ex)
//										{
//											hasError = true;
//											Console.WriteLine($"設置溫度失敗，站號 {stn}：{ex.Message}");
//										}
//									}
//								}
//								break;

//							case "EnergyMode":
//								if (param.Value.ToString() == "0" ||
//									param.Value.ToString() == "1" ||
//									param.Value.ToString() == "2")
//								{
//									int mode = int.Parse(param.Value.ToString());
//									foreach (var stn in stationNumbers)
//									{
//										if (!slaveData.ContainsKey(stn)) { hasError = true; continue; }
//										try
//										{
//											slaveData[stn]["EnergyMode"] = mode;
//											hasProcessed = true;
//											if (mode != 0) shouldPublishStateChanged = true;
//										}
//										catch (Exception ex)
//										{
//											hasError = true;
//											Console.WriteLine($"設置節能模式失敗，站號 {stn}：{ex.Message}");
//										}
//									}
//								}
//								break;

//							case "PowerSwitch":
//								if (param.Value.ToString() == "1" || param.Value.ToString() == "2")
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
//										catch (Exception ex)
//										{
//											hasError = true;
//											Console.WriteLine($"開關操作失敗，站號 {stn}：{ex.Message}");
//										}
//									}
//								}
//								break;

//							default:
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
//						var stateChangedMessage = new StationStateChanged
//						{
//							NewState = ResourceState.NST_ShutdownAndStartup,
//						};
//						endpoint.Publish(stateChangedMessage);
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
//				Console.WriteLine($"Error processing request: {ex.Message}");
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

//			var responseEnvelope = CFXEnvelope.FromCFXMessage(response);
//			responseEnvelope.RequestID = requestId;

//			return responseEnvelope;
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

//			var responseEnvelope = CFXEnvelope.FromCFXMessage(response);
//			responseEnvelope.RequestID = requestId;

//			return responseEnvelope;
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
//					await Task.Run(() => endpoint.Publish(faultOccurred));
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
//				new GenericParameter
//				{
//					Name = "EnergyMode",
//					Value = data.ContainsKey("EnergyMode") ? data["EnergyMode"].ToString() : "0"
//				},
//				new GenericParameter
//				{
//					Name = "PowerTemperatureA",
//					Value = data.ContainsKey("PowerTemperature_A") ? data["PowerTemperature_A"].ToString() : "0"
//				},
//				new GenericParameter
//				{
//					Name = "PowerTemperatureB",
//					Value = data.ContainsKey("PowerTemperature_B") ? data["PowerTemperature_B"].ToString() : "0"
//				},
//				new GenericParameter
//				{
//					Name = "PowerTemperatureC",
//					Value = data.ContainsKey("PowerTemperature_C") ? data["PowerTemperature_C"].ToString() : "0"
//				}
//			};

//			endpoint.Publish(new StationParametersModified { ModifiedParameters = parameters });
//		}

//		private void PublishEnergyConsumedMessages(byte stationNumber, AmqpCFXEndpoint endpoint)
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

//			var energyConsumed = new EnergyConsumed
//			{
//				EnergyUsed = energyUsed,
//				StartTime = DateTime.Now,
//				EndTime = DateTime.Now,
//				CurrentNowRYB = currentRYB,
//				PowerNowRYB = powerRYB,
//			};

//			endpoint.Publish(energyConsumed);
//			modbusViewer.AppendLog($"Publish st.{stationNumber} CFX EnergyConsumed Msgs: {energyUsed:F2}kWh to LM");
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
//			if (powerSwitchValue == 1)
//				return 1;
//			if (powerSwitchValue == 0)
//				return 2;

//			return -1;
//		}
//	}
//}



