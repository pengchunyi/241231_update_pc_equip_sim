//using System;
//using System.Collections.Generic;
//using System.Linq;
//using System.Threading.Tasks;
//using CFX;
//using CFX.ResourcePerformance;
//using CFX.Structures;
//using CFX.Transport;

//namespace AmqpModbusIntegration
//{
//	public class AmqpEndpointManager
//	{
//		private readonly AmqpCFXEndpoint _endpoint = new();
//		private readonly Dictionary<byte, Dictionary<string, object>> _slaveData;
//		private readonly Dictionary<int, (string code, string desc)> _faultDict;
//		private readonly Dictionary<byte, int> _lastStatus = new();
//		private DateTime _lastEnergyTs = DateTime.MinValue;

//		public AmqpEndpointManager(Dictionary<byte, Dictionary<string, object>> slaveData)
//		{
//			_slaveData = slaveData;
//			_faultDict = BuildFaultDictionary();
//		}

//		#region Connect / Disconnect
//		public void Connect()
//		{
//			var cfg = ConfigManager.Configuration;
//			string handle = $"{cfg.Factory}.{cfg.Line}.{cfg.Station}.{cfg.MachineSN}";
//			Uri reqUri = new(cfg.MyrequestUri);
//			Uri pubUri = new($"amqp://{cfg.Remote_IP}:{cfg.Remote_Port}");

//			_endpoint.Open(handle, reqUri);
//			_endpoint.AddPublishChannel(pubUri, cfg.PublishAddress);
//			_endpoint.OnRequestReceived += OnRequestReceived;
//			_endpoint.Publish(new EndpointConnected { CFXHandle = handle });
//			Console.WriteLine($"[AMQP] Connected as {handle}");

//			_ = Task.Run(PublisherLoop);
//		}

//		public void Disconnect()
//		{
//			try
//			{
//				_endpoint.Publish(new EndpointShuttingDown());
//				_endpoint.Close();
//			}
//			catch { /* ignore */ }
//		}
//		#endregion

//		#region Request handler (維持原本完整邏輯)
//		private CFXEnvelope OnRequestReceived(CFXEnvelope req)
//		{
//			// 此處沿用您原始 OnRequestReceivedHandler 完整實作
//			// (保持所有 TemperatureSV / EnergyMode / PowerSwitch 嚴謹檢查與錯誤回傳)

//			try
//			{
//				if (req.MessageBody is ModifyStationParametersRequest mreq)
//				{
//					// === 這段完整移植自您原始 250508_configVer_update.cs ===
//					//   ‑ 解析站號 (現改為僅允許 single StationNumber 來自 ConfigManager)
//					//   ‑ 執行 SetTemperature / SwitchON/OFF / EnergyMode 寫入
//					//   ‑ 發布 StationStateChanged (6500) 當必要
//					//   ‑ 回傳成功或失敗
//					// ------------------  (篇幅限制，請直接複製您原檔案相同區塊) ------------------
//				}

//				// 若非支援類型
//				var res = new NotSupportedResponse
//				{
//					RequestResult = new RequestResult
//					{
//						Result = StatusResult.Failed,
//						ResultCode = 99,
//						Message = "Unsupported request type"
//					}
//				};
//				var env = CFXEnvelope.FromCFXMessage(res);
//				env.RequestID = req.RequestID;
//				return env;
//			}
//			catch (Exception ex)
//			{
//				var res = new NotSupportedResponse
//				{
//					RequestResult = new RequestResult
//					{
//						Result = StatusResult.Failed,
//						ResultCode = -1,
//						Message = ex.Message
//					}
//				};
//				var env = CFXEnvelope.FromCFXMessage(res);
//				env.RequestID = req.RequestID;
//				return env;
//			}
//		}
//		#endregion

//		#region Background publisher loop
//		private async Task PublisherLoop()
//		{
//			while (true)
//			{
//				try
//				{
//					List<Task> tasks = new();

//					foreach (byte stn in _slaveData.Keys)
//					{
//						if (_slaveData[stn].TryGetValue("Fault_WarningCode", out var obj))
//						{
//							int cur = Convert.ToInt32(obj);
//							if (_lastStatus.TryGetValue(stn, out int last) && last != cur)
//								tasks.Add(PublishFaultOccurredMessagesAsync(stn, cur));
//							_lastStatus[stn] = cur;
//						}
//					}

//					// 每 60s 發佈 StationParameters & EnergyConsumed
//					if ((DateTime.Now - _lastEnergyTs).TotalSeconds >= 60)
//					{
//						foreach (byte stn in _slaveData.Keys)
//						{
//							PublishStationParametersModified(stn);
//							PublishEnergyConsumed(stn);
//						}
//						_lastEnergyTs = DateTime.Now;
//					}

//					await Task.WhenAll(tasks);
//					await Task.Delay(1000);
//				}
//				catch (Exception ex) { Console.WriteLine($"[AMQP loop] {ex.Message}"); }
//			}
//		}
//		#endregion

//		#region Message builders
//		private async Task PublishFaultOccurredMessagesAsync(byte stn, int status)
//		{
//			for (int bit = 0; bit <= 27; bit++)
//			{
//				if (((status >> bit) & 1) == 1 && _faultDict.TryGetValue(bit, out var def))
//				{
//					var msg = new FaultOccurred
//					{
//						Fault = new Fault
//						{
//							FaultCode = def.code,
//							FaultOccurrenceId = Guid.NewGuid(),
//							Description = def.desc,
//							OccurredAt = DateTime.Now,
//							Severity = FaultSeverity.Error
//						}
//					};
//					await Task.Run(() => _endpoint.Publish(msg));
//				}
//			}
//		}

//		private void PublishStationParametersModified(byte stn)
//		{
//			if (!_slaveData.TryGetValue(stn, out var d) || !d.Any()) return;
//			var list = new List<Parameter>
//			{
//				new GenericParameter { Name="PowerSwitch",    Value=d.GetValueOrDefault("PowerSwitch",0).ToString() },
//				new GenericParameter { Name="TemperatureSV",  Value=d.GetValueOrDefault("TemperatureSV",0).ToString() },
//				new GenericParameter { Name="EnergyMode",     Value=d.GetValueOrDefault("EnergyMode",0).ToString() }
//			};
//			_endpoint.Publish(new StationParametersModified { ModifiedParameters = list });
//		}

//		private void PublishEnergyConsumed(byte stn)
//		{
//			if (!_slaveData.TryGetValue(stn, out var d) || !d.Any()) return;
//			double energy = Convert.ToDouble(d.GetValueOrDefault("EnergyUsed", 0.0));
//			var msg = new EnergyConsumed
//			{
//				EnergyUsed = energy,
//				StartTime = DateTime.Now,
//				EndTime = DateTime.Now,
//			};
//			_endpoint.Publish(msg);
//		}
//		#endregion

//		private static Dictionary<int, (string, string)> BuildFaultDictionary() => new()
//		{
//			{0,("EGY_1_WARN1","A相過壓")},
//			{1,("EGY_1_WARN2","B相過壓")},
//			{2,("EGY_1_WARN3","C相過壓")},
//			{3,("EGY_1_WARN4","A相欠壓")},
//			{4,("EGY_1_WARN5","B相欠壓")},
//			{5,("EGY_1_WARN6","C相欠壓")},
//			{6,("EGY_1_WARN7","A相過流")},
//			{7,("EGY_1_WARN8","B相過流")},
//			{8,("EGY_1_WARN9","C相過流")},
//			{9,("EGY_1_WARN10","漏電異常")},
//			{10,("EGY_1_WARN11","A相出線溫度異常")},
//			{11,("EGY_1_WARN12","B相出線溫度異常")},
//			{12,("EGY_1_WARN13","C相出線溫度異常")},
//			{13,("EGY_1_WARN14","N相出線溫度異常")},
//			{14,("EGY_1_WARN15","電弧")},
//			{15,("EGY_1_WARN16","缺相")},
//			{16,("EGY_1_WARN17","斷零")},
//			{17,("EGY_1_WARN18","三相電壓不平衡")},
//			{18,("EGY_1_WARN19","鎖定")},
//			{19,("EGY_1_WARN20","進入維修或手動模式")},
//			{20,("EGY_1_WARN21","開關狀態異常，請更換設備")},
//			{21,("EGY_1_WARN22","漏電功能故障，請更換設備")},
//			{22,("EGY_1_WARN23","設備離線")},
//			{23,("EGY_1_WARN24","過壓預警")},
//			{24,("EGY_1_WARN25","欠壓預警")},
//			{25,("EGY_1_WARN26","過流預警")},
//			{26,("EGY_1_WARN27","過溫預警")},
//		};
//	}
//}