// Program.cs
using AmqpModbusIntegration.Services;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace AmqpModbusIntegration
{
	internal class Program
	{
		private static ModbusViewer viewer;
		private static ConnectionManager connMgr;
		private static AmqpEndpointManager amqpManager;

		[STAThread]
		static void Main()
		{
			AppLogger.Init("CFX");  // 初始化 Logger

			// ── PATCH-5：全域崩潰 → Crash.log + 寫進 Logger ──────────
			var crashFile = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Crash.log");

			AppDomain.CurrentDomain.UnhandledException += (s, e) =>
			{
				var ex = e.ExceptionObject as Exception;
				File.AppendAllText(crashFile,
					$"[UE] {DateTime.Now:yyyy-MM-dd HH:mm:ss} {ex}\r\n{ex?.StackTrace}\r\n");
				AppLogger.Error(ex, "[APP] UnhandledException");
			};

			TaskScheduler.UnobservedTaskException += (s, e) =>
			{
				File.AppendAllText(crashFile,
					$"[Task] {DateTime.Now:yyyy-MM-dd HH:mm:ss} {e.Exception}\r\n{e.Exception.StackTrace}\r\n");
				AppLogger.Error(e.Exception, "[TASK] UnobservedTaskException");
				e.SetObserved();                       // 避免 .NET 強制中止
			};

			Application.ThreadException += (s, e) =>
			{
				File.AppendAllText(crashFile,
					$"[UI] {DateTime.Now:yyyy-MM-dd HH:mm:ss} {e.Exception}\r\n{e.Exception.StackTrace}\r\n");
				AppLogger.Error(e.Exception, "[UI] ThreadException");
			};
			// ─────────────────────────────────────────────

			bool newInstance;
			using (var mutex = new Mutex(true, @"Global\CFX_SmartBreaker_Upload_Mutex", out newInstance))
			{
				if (!newInstance)
				{
					MessageBox.Show("已有一個 CFX 智慧空開上傳程式正在執行。", "單一執行", MessageBoxButtons.OK, MessageBoxIcon.Information);
					return;
				}

				Application.EnableVisualStyles();
				Application.SetCompatibleTextRenderingDefault(false);

				// 讀設定（先讀完設定，等視窗建立後再開始連線）
				try
				{
					ConfigHelper.LoadConfiguration();
				}
				catch (Exception ex)
				{
					MessageBox.Show("載入設定檔失敗: " + ex.Message);
					return;
				}

				// 建立 UI（此時尚未開始任何連線）
				viewer = new ModbusViewer();

				// 等視窗顯示且 Handle 建立後，才把 Logger 附到 UI，並且再建立 AMQP/ConnectionManager
				viewer.Shown += (_, __) =>
				{
					AppLogger.AttachUi(viewer, viewer.AppendLog);

					try
					{
						// 僅建立「一組」AMQP + ConnectionManager
						amqpManager = new AmqpEndpointManager(
							// endpointUri 是本端 Endpoint 所連到的 Broker 節點位址（CFX 的 Open 會用它）
							//SystemConfig.MyRequestUri ?? "amqp://127.0.0.1:6666",
							SystemConfig.PublishAddress ?? "amqp://127.0.0.1:8888",
							SystemConfig.MyRequestUri ?? "amqp://127.0.0.1:6666",
							// publishChannelUri 是對外發布事件的 Channel（LM Broker）
							// subscribeChannelUri 可選，若不用請給 null 或空字串
							//string.IsNullOrWhiteSpace(SystemConfig.SubscribeAddress) ? null : SystemConfig.SubscribeAddress,
							//SystemConfig.MyRequestUri ?? "amqp://127.0.0.1:6666",
							null,
							viewer,
							viewer.GetSerialPort(),
							viewer.slaveData
						);


						connMgr = new ConnectionManager(viewer, amqpManager);
						connMgr.Start(); // 注意：這個 Start 只會先去確保 Modbus；Modbus 首次就緒後才會啟動 AMQP
						AppLogger.Info("背景連線管理已啟動（將先確認COM/Modbus就緒，才會啟動AMQP）。");
					}
					catch (Exception ex)
					{
						AppLogger.Error(ex, "背景連線初始化失敗");
					}
				};

				viewer.RefreshIniView();
				viewer.SetComPortUI();

				// 不主動在 Main 開 COM，統一交由 ConnectionManager 管理
				if (string.IsNullOrWhiteSpace(SwitchDeviceConfig.ComPort))
					AppLogger.Info("配置檔未賦予 COM 口，請確認配置或手動選擇。");

				Application.Run(viewer);

				// 收尾
				try { connMgr?.Dispose(); } catch { }
				try { amqpManager?.Dispose(); } catch { }

				mutex.ReleaseMutex();
				AppLogger.Shutdown();
			}
		}
	}




	// ====== 設定讀取 ======
	public static class ConfigHelper
	{
		private static readonly string IniPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "CFX.ini");

		public static void LoadConfiguration()
		{
			if (!File.Exists(IniPath))
				throw new FileNotFoundException("找不到配置檔: " + IniPath);

			string section = string.Empty;
			foreach (var raw in File.ReadAllLines(IniPath))
			{
				var line = raw.Trim();
				if (string.IsNullOrEmpty(line) || line.StartsWith(";")) continue;

				if (line.StartsWith("[") && line.EndsWith("]"))
				{
					section = line.Substring(1, line.Length - 2);
					continue;
				}

				int idx = line.IndexOf('=');
				if (idx < 0) continue;

				string key = line.Substring(0, idx).Trim();
				string val = line.Substring(idx + 1).Trim();

				if (section.Equals("Configuration", StringComparison.OrdinalIgnoreCase))
					ApplyConfiguration(key, val);

				else if (section.Equals("SwitchDevice", StringComparison.OrdinalIgnoreCase))
					ApplySwitchDevice(key, val);

			}
		}

		private static void ApplyConfiguration(string key, string value)
		{
			switch (key)
			{
				case "EquipmentName": SystemConfig.EquipmentName = value; break;
				case "MachineSN": SystemConfig.MachineSN = value; break;
				case "PublishAddress": SystemConfig.PublishAddress = value; break;

				// 同時接受兩種大小寫
				case "MyRequestUri":
				case "MyrequestUri":
					SystemConfig.MyRequestUri = value;
					break;

				// 新增可選 Subscribe 通道；若不用可不填
				case "SubscribeAddress":
					SystemConfig.SubscribeAddress = value;
					break;

				case "AutoPickNewCOM":
					if (bool.TryParse(value, out var b)) SystemConfig.AutoPickNewCOM = b;
					break;
			}
		}

		private static void ApplySwitchDevice(string key, string value)
		{
			switch (key)
			{
				case "COM":
					SwitchDeviceConfig.ComPort = value;
					break;

				case "StationNumber":
					var stationList = value.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
					SwitchDeviceConfig.StationNumber = new List<byte>();
					foreach (var s in stationList)
					{
						if (byte.TryParse(s.Trim(), out var stn))
							SwitchDeviceConfig.StationNumber.Add(stn);
					}
					break;
			}
		}
	}

	public static class SystemConfig
	{
		public static string EquipmentName { get; set; }
		public static string MachineSN { get; set; }
		public static string PublishAddress { get; set; }
		public static string MyRequestUri { get; set; }
		public static string SubscribeAddress { get; set; }   // 可選
		public static bool AutoPickNewCOM { get; set; } = false; // 目前未使用；保持關閉



		// ★ 新增：手動覆蓋 COM 模式（開發者模式時啟用）
		public static bool ManualComOverride { get; set; } = false;
		public static string ManualComPort { get; set; } = null;
	}

	public static class SwitchDeviceConfig
	{
		public static string ComPort { get; set; }
		public static List<byte> StationNumber { get; set; } = new List<byte>();
	}
}
