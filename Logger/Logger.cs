// AppLogger.cs
using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace AmqpModbusIntegration
{
	public static class AppLogger
	{
		private sealed class LogItem
		{
			public long Seq { get; }
			public DateTime Ts { get; }
			public string Level { get; }
			public string Message { get; }
			public LogItem(long seq, DateTime ts, string level, string message)
			{
				Seq = seq; Ts = ts; Level = level; Message = message;
			}
		}

		private static readonly BlockingCollection<LogItem> _queue =
			new BlockingCollection<LogItem>(new ConcurrentQueue<LogItem>());

		private static Control _uiControl;           // 讓 logger 可以回到 UI 執行緒
		private static Action<string> _uiSink;
		private static string _logFilePath;
		private static Task _flushTask;
		private static CancellationTokenSource _cts;

		private static readonly object _initLock = new object();
		private static bool _initialized = false;
		private static long _seq = 0;

		// 去重控制（避免相同訊息在極短時間內刷屏）
		private static string _lastMsg;
		private static DateTime _lastTs;
		private static readonly TimeSpan _dedupWindow = TimeSpan.FromMilliseconds(400);

		public static void Init(string appName = "CFX", string logDirName = "logs")
		{
			lock (_initLock)
			{
				if (_initialized) return;

				var baseDir = AppDomain.CurrentDomain.BaseDirectory;
				var logDir = Path.Combine(baseDir, logDirName);
				Directory.CreateDirectory(logDir);

				var stamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
				_logFilePath = Path.Combine(logDir, $"{appName}_{stamp}.log");

				_cts = new CancellationTokenSource();
				_flushTask = Task.Run(() => FlushLoop(_cts.Token), _cts.Token);
				_initialized = true;

				Info($"=== {appName} started at {DateTime.Now:yyyy-MM-dd HH:mm:ss} ===");
				Info($"Log file: {_logFilePath}");
			}
		}

		/// <summary>在 UI 準備好（Handle 建立）後附掛，logger 會自動 BeginInvoke 回 UI。</summary>
		public static void AttachUi(Control uiControl, Action<string> appendToUi)
		{
			_uiControl = uiControl;
			_uiSink = appendToUi;
		}

		public static void AttachUiSink(Action<string> appendToUi) => _uiSink = appendToUi;

		public static void Shutdown()
		{
			try
			{
				_cts?.Cancel();
				_queue.CompleteAdding();
				_flushTask?.Wait(3000);
			}
			catch { /* ignore */ }
			finally
			{
				_cts?.Dispose();
				_cts = null;
			}
		}

		// 外部 API
		public static void Info(string message) => Enqueue("INFO", message);
		public static void Warn(string message) => Enqueue("WARN", message);
		public static void Debug(string message) => Enqueue("DEBUG", message);
		public static void Error(string message) => Enqueue("ERROR", message);

		public static void Error(Exception ex, string message = null)
		{
			var head = string.IsNullOrEmpty(message) ? "" : (message + " | ");
			Enqueue("ERROR", head + ex.GetType().Name + ": " + ex.Message);
			if (!string.IsNullOrEmpty(ex.StackTrace)) Enqueue("ERROR", ex.StackTrace);
		}

		private static void Enqueue(string level, string message)
		{
			if (!_initialized) Init();

			var now = DateTime.Now;

			// 在佇列端做去重（完全相同訊息且極短時間內）
			if (_lastMsg == message && (now - _lastTs) <= _dedupWindow) return;
			_lastMsg = message; _lastTs = now;

			var item = new LogItem(Interlocked.Increment(ref _seq), now, level, message);
			if (!_queue.IsAddingCompleted)
			{
				try { _queue.Add(item); } catch { /* ignore on shutdown */ }
			}
		}

		private static void FlushLoop(CancellationToken ct)
		{
			try
			{
				using var fs = new FileStream(_logFilePath, FileMode.Append, FileAccess.Write, FileShare.Read);
				using var sw = new StreamWriter(fs, new UTF8Encoding(false)) { AutoFlush = true };

				foreach (var it in _queue.GetConsumingEnumerable(ct))
				{
					// 1) 寫檔（保序）
					string fileLine = $"[{it.Ts:yyyy-MM-dd HH:mm:ss.fff}] [{it.Level}] {it.Message}";
					sw.WriteLine(fileLine);

					// 2) 回 UI（在 flush 執行緒 → 回 UI 執行緒）
					if (_uiSink != null)
					{
						string uiLine = $"[{it.Ts:HH:mm:ss}] {it.Message}";
						try
						{
							// 只有 UI Control 的 Handle 建立後才回 UI，避免跨執行緒例外
							if (_uiControl != null && !_uiControl.IsDisposed && _uiControl.IsHandleCreated)
							{
								_uiControl.BeginInvoke(new Action(() => _uiSink?.Invoke(uiLine + "\r\n")));
							}
							// 否則略過（仍寫檔，不丟訊息）
						}
						catch
						{
							// 忽略 UI 例外，避免影響檔案日誌
						}
					}
				}
			}
			catch (OperationCanceledException) { }
			catch (Exception ex)
			{
				try
				{
					// 注意：此處不能再遞迴進入 Enqueue 太多次，僅做簡單輸出
					Debug("Logger flush loop error: " + ex);
				}
				catch { }
			}
		}
	}
}
