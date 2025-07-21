//using System;
//using System.IO;

//namespace AmqpModbusIntegration
//{
//	public static class ConfigHelper
//	{
//		private static readonly string IniPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "CFX.ini");

//		public static void LoadConfiguration()
//		{
//			if (!File.Exists(IniPath))
//				throw new FileNotFoundException("找不到配置檔: " + IniPath);

//			string section = string.Empty;
//			foreach (var raw in File.ReadAllLines(IniPath))
//			{
//				var line = raw.Trim();
//				if (string.IsNullOrEmpty(line) || line.StartsWith(";")) continue;

//				if (line.StartsWith("[") && line.EndsWith("]"))
//				{
//					section = line.Substring(1, line.Length - 2);  // ⬅️ 改回 Substring
//					continue;
//				}

//				int idx = line.IndexOf('=');
//				if (idx < 0) continue;

//				string key = line.Substring(0, idx).Trim();                // ⬅️ 改回 Substring
//				string val = line.Substring(idx + 1).Trim();               // ⬅️ 改回 Substring

//				if (section.Equals("Configuration", StringComparison.OrdinalIgnoreCase))
//					ApplyConfiguration(key, val);
//				else if (section.Equals("SwitchDevice", StringComparison.OrdinalIgnoreCase))
//					ApplySwitchDevice(key, val);
//			}
//		}

//		private static void ApplyConfiguration(string key, string value)
//		{
//			switch (key)
//			{
//				case "Factory": SystemConfig.Factory = value; break;
//				case "Line": SystemConfig.Line = value; break;
//				case "Station": SystemConfig.Station = value; break;
//				case "MachineSN": SystemConfig.MachineSN = value; break;
//				case "MC_IP": SystemConfig.IpAddress = value; break;
//				case "MC_Port": int port; if (int.TryParse(value, out port)) SystemConfig.Port = port; break;
//				case "Remote_IP": SystemConfig.RemoteIp = value; break;
//				case "Remote_Port": int rport; if (int.TryParse(value, out rport)) SystemConfig.RemotePort = rport; break;
//				case "PublishAddress": SystemConfig.PublishAddress = value; break;
//				case "MyrequestUri": SystemConfig.MyRequestUri = value; break;
//				case "ModelName": SystemConfig.ModelName = value; break;
//				case "MC_Name": SystemConfig.MachineName = value; break;
//				case "UseCFX": bool cfx; if (bool.TryParse(value, out cfx)) SystemConfig.UseCfx = cfx; break;
//			}
//		}

//		private static void ApplySwitchDevice(string key, string value)
//		{
//			switch (key)
//			{
//				case "COM": SwitchDeviceConfig.ComPort = value; break;
//				case "StationNumber": byte stn; if (byte.TryParse(value, out stn)) SwitchDeviceConfig.StationNumber = stn; break;
//				case "fTemperature": float ft; if (float.TryParse(value, out ft)) SwitchDeviceConfig.FTemperature = ft; break;
//				case "bEnergyConsumption": bool be; if (bool.TryParse(value, out be)) SwitchDeviceConfig.BEnergyConsumption = be; break;
//			}
//		}
//	}

//	public static class SystemConfig
//	{
//		public static string Factory { get; set; }
//		public static string Line { get; set; }
//		public static string Station { get; set; }
//		public static string MachineSN { get; set; }
//		public static string IpAddress { get; set; }
//		public static int Port { get; set; }
//		public static string RemoteIp { get; set; }
//		public static int RemotePort { get; set; }
//		public static string PublishAddress { get; set; }
//		public static string MyRequestUri { get; set; }
//		public static string ModelName { get; set; }
//		public static string MachineName { get; set; }
//		public static bool UseCfx { get; set; }
//	}

//	public static class SwitchDeviceConfig
//	{
//		public static string ComPort { get; set; }
//		public static byte StationNumber { get; set; }
//		public static float FTemperature { get; set; }
//		public static bool BEnergyConsumption { get; set; }
//	}
//}
