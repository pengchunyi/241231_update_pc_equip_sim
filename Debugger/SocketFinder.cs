using AmqpModbusIntegration;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Net.Sockets;
using System.Reflection;

static class SocketFinder
{
	private const BindingFlags BF = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;

	public static void DumpSockets(object root, string label = "root", int maxDepth = 6)
	{
		var visited = new HashSet<object>();
		Find(root, label, 0);

		void Find(object obj, string path, int depth)
		{
			if (obj == null || depth > maxDepth) return;
			if (!visited.Add(obj)) return;                 // 避免循環

			if (obj is Socket s)
			{
				Console.WriteLine($"[SOCKET] {path}  ←  {s.RemoteEndPoint}");
				AppLogger.Info($"[SOCKET] {path}  ←  {s.RemoteEndPoint}");
				return;                                    // Socket 不再往下爬
			}

			// 遞迴子成員
			foreach (var f in obj.GetType().GetFields(BF))
			{
				var val = f.GetValue(obj);
				if (val == null) continue;
				if (val is IEnumerable en && !(val is string))
				{
					int idx = 0;
					foreach (var item in en)
					{
						Find(item, $"{path}.{f.Name}[{idx}]", depth + 1);
						idx++;
					}
				}
				else
				{
					Find(val, $"{path}.{f.Name}", depth + 1);
				}
			}

			foreach (var p in obj.GetType().GetProperties(BF))
			{
				//if (!p.CanRead) continue;
				if (!p.CanRead || p.GetIndexParameters().Length != 0) continue; // 跳過索引屬性
				object val;
				try { val = p.GetValue(obj); }
				catch { continue; }
				Find(val, $"{path}.{p.Name}", depth + 1);
			}
		}
	}
}
