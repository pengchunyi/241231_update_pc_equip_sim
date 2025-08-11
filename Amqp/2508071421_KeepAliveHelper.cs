// ─────────────────────────────────────────────────────────────
// KeepAliveHelper.cs  ★ New file
// ─────────────────────────────────────────────────────────────
using System;
using System.Net.Sockets;
using System.Runtime.InteropServices;

namespace AmqpModbusIntegration
{
	/// <summary>
	/// Helper to enable per‑socket TCP keep‑alive on Windows so we can detect
	/// half‑open (idle) connections within seconds instead of hours.
	/// </summary>
	internal static class KeepAliveHelper
	{
		public static void EnableKeepAlive(Socket socket, uint timeSec = 30, uint intervalSec = 10)
		{
			if (socket == null)
			{
				AppLogger.Warn("[KeepAlive] Socket 為 null，跳過設定");
				return;
			}

			try
			{
				byte[] vals = new byte[12];
				BitConverter.GetBytes((uint)1).CopyTo(vals, 0);
				BitConverter.GetBytes(timeSec * 1000).CopyTo(vals, 4);
				BitConverter.GetBytes(intervalSec * 1000).CopyTo(vals, 8);

				const int SIO_KEEPALIVE_VALS = -1744830460;
				socket.IOControl(SIO_KEEPALIVE_VALS, vals, null);
				AppLogger.Info($"[KeepAlive] 設定完成 Idle={timeSec}s, Interval={intervalSec}s");
			}
			catch (Exception ex)
			{
				AppLogger.Warn($"[KeepAlive] 設定失敗：{ex.Message}");
			}
		}
	}

}