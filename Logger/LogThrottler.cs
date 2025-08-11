// LogThrottler.cs （C# 8 版本）
using System;
using System.Collections.Concurrent;

public static class LogThrottler
{
	private static readonly ConcurrentDictionary<string, DateTime> _last =
		new ConcurrentDictionary<string, DateTime>();

	/// <summary>同一 key，5 秒內只執行一次 action</summary>
	public static void Every(string key, int seconds, Action action)
	{
		var now = DateTime.UtcNow;
		var last = _last.GetOrAdd(key, DateTime.MinValue);
		if ((now - last).TotalSeconds >= seconds)
		{
			_last[key] = now;
			action?.Invoke();
		}
	}

	public static void Reset(string key)
	{
		DateTime _;
		_last.TryRemove(key, out _);
	}
}
