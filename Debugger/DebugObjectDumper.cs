////using System;
////using System.Collections;
////using System.Collections.Generic;
////using System.Linq;
////using System.Reflection;

////namespace AmqpModbusIntegration
////{
////	/// <summary>
////	/// 以遞迴方式 dump 出任意物件的欄位 / 屬性結構，用於偵錯反射。
////	/// 呼叫範例：DebugObjectDumper.Dump(endpoint, "endpoint");
////	/// </summary>
////	internal static class DebugObjectDumper
////	{
////		private const BindingFlags BF = BindingFlags.Instance | BindingFlags.NonPublic |
////										BindingFlags.Public | BindingFlags.FlattenHierarchy;

////		/// <summary>
////		/// 把 obj 的內部結構列印到 Console / Logger。
////		/// </summary>
////		public static void Dump(object obj, string rootLabel = "root",
////								int maxDepth = 5, int maxEnumerable = 10)
////		{
////			var visited = new HashSet<object>();
////			DumpRec(obj, rootLabel, 0);

////			// ── 内部遞迴 ─────────────────────────────────────────────────────────────
////			void DumpRec(object target, string lbl, int depth)
////			{
////				if (target == null)
////				{
////					Print(lbl, "null");
////					return;
////				}

////				var type = target.GetType();
////				Print(lbl, $"[{type.FullName}]");

////				if (depth >= maxDepth) return;
////				if (!visited.Add(target)) return;   // 避免循環參考

////				// 先列欄位，再列屬性
////				foreach (var fld in type.GetFields(BF))
////				{
////					PrintFieldOrProp(fld, fld.FieldType, fld.GetValue(target), depth, $".{fld.Name}");
////				}
////				foreach (var prop in type.GetProperties(BF))
////				{
////					if (!prop.CanRead) continue;
////					object val;
////					try { val = prop.GetValue(target); }
////					catch { continue; }              // 有些 getter 可能丟例外
////					PrintFieldOrProp(prop, prop.PropertyType, val, depth, $".{prop.Name}");
////				}
////			}

////			void PrintFieldOrProp(MemberInfo mi, Type memberType, object val,
////								  int depth, string suffix)
////			{
////				string indent = new string(' ', (depth + 1) * 2);
////				string valueStr = GetShortValue(val);

////				Console.WriteLine($"{indent}{suffix} : {memberType.Name} = {valueStr}");
////				AppLogger.Info($"{lbl}{suffix} : {memberType.Name} = {valueStr}");

////				// 遞迴往下，只對參考型別 (非字串) 做
////				if (val != null && !(val is string) && !memberType.IsPrimitive)
////				{
////					// 若是 IEnumerable，最多展開幾個元素
////					if (val is IEnumerable en && !(val is byte[]))
////					{
////						int i = 0;
////						foreach (var item in en)
////						{
////							if (i >= maxEnumerable) { Print($"{suffix}[...] (more)", "…"); break; }
////							DumpRec(item, $"{lbl}{suffix}[{i}]", depth + 1);
////							i++;
////						}
////					}
////					else
////					{
////						DumpRec(val, $"{lbl}{suffix}", depth + 1);
////					}
////				}
////			}

////			void Print(string name, string txt)
////			{
////				string indent = new string(' ', name.Count(c => c == '.') * 2);
////				Console.WriteLine($"{indent}{name} : {txt}");
////				AppLogger.Info($"{name} : {txt}");
////			}

////			static string GetShortValue(object v)
////			{
////				if (v == null) return "null";
////				try
////				{
////					string s = v.ToString();
////					return s.Length > 60 ? s.Substring(0, 57) + "..." : s;
////				}
////				catch { return "{?}"; }
////			}
////		}
////	}
////}

//using System;
//using System.Collections;
//using System.Collections.Generic;
//using System.Linq;
//using System.Reflection;

//namespace AmqpModbusIntegration
//{
//	/// <summary>
//	/// 以遞迴方式 dump 出任意物件的欄位 / 屬性結構，用於偵錯反射。
//	/// 呼叫範例：DebugObjectDumper.Dump(endpoint, "endpoint");
//	/// </summary>
//	internal static class DebugObjectDumper
//	{
//		private const BindingFlags BF = BindingFlags.Instance | BindingFlags.NonPublic |
//										BindingFlags.Public | BindingFlags.FlattenHierarchy;

//		/// <summary>
//		/// 把 obj 的內部結構列印到 Console / Logger。
//		/// </summary>
//		public static void Dump(object obj, string rootLabel = "root",
//								int maxDepth = 5, int maxEnumerable = 10)
//		{
//			var visited = new HashSet<object>();
//			DumpRec(obj, rootLabel, 0);

//			//─────────────────────────────────────────────────────────────────────
//			void DumpRec(object target, string lbl, int depth)
//			{
//				if (target == null)
//				{
//					Print(lbl, "null");
//					return;
//				}

//				var type = target.GetType();
//				Print(lbl, $"[{type.FullName}]");

//				if (depth >= maxDepth) return;
//				if (!visited.Add(target)) return;   // 避免循環參考

//				// 先列欄位，再列屬性
//				foreach (var fld in type.GetFields(BF))
//				{
//					PrintFieldOrProp(fld, fld.FieldType, fld.GetValue(target),
//									 depth, $".{fld.Name}", lbl);
//				}
//				foreach (var prop in type.GetProperties(BF))
//				{
//					if (!prop.CanRead) continue;
//					object val;
//					try { val = prop.GetValue(target); }
//					catch { continue; }              // 有些 getter 可能丟例外
//					PrintFieldOrProp(prop, prop.PropertyType, val,
//									 depth, $".{prop.Name}", lbl);
//				}
//			}

//			//─────────────────────────────────────────────────────────────────────
//			void PrintFieldOrProp(MemberInfo mi, Type memberType, object val,
//								  int depth, string suffix, string parentLabel)
//			{
//				string fullLabel = parentLabel + suffix;
//				string indent = new string(' ', (depth + 1) * 2);
//				string valueStr = GetShortValue(val);

//				Console.WriteLine($"{indent}{fullLabel} : {memberType.Name} = {valueStr}");
//				AppLogger.Info($"{fullLabel} : {memberType.Name} = {valueStr}");

//				// 遞迴往下，只對參考型別 (非字串) 做
//				if (val != null && !(val is string) && !memberType.IsPrimitive)
//				{
//					// 若是 IEnumerable，最多展開幾個元素
//					if (val is IEnumerable en && !(val is byte[]))
//					{
//						int i = 0;
//						foreach (var item in en)
//						{
//							if (i >= maxEnumerable)
//							{
//								Print($"{fullLabel}[...] (more)", "…");
//								break;
//							}
//							DumpRec(item, $"{fullLabel}[{i}]", depth + 1);
//							i++;
//						}
//					}
//					else
//					{
//						DumpRec(val, fullLabel, depth + 1);
//					}
//				}
//			}

//			//─────────────────────────────────────────────────────────────────────
//			void Print(string name, string txt)
//			{
//				string indent = new string(' ', name.Count(c => c == '.') * 2);
//				Console.WriteLine($"{indent}{name} : {txt}");
//				AppLogger.Info($"{name} : {txt}");
//			}

//			static string GetShortValue(object v)
//			{
//				if (v == null) return "null";
//				try
//				{
//					string s = v.ToString();
//					return s.Length > 60 ? s.Substring(0, 57) + "..." : s;
//				}
//				catch { return "{?}"; }
//			}
//		}
//	}
//}


