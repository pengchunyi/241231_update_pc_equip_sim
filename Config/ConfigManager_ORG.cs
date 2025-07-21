//using System;
//using System.IO;

//namespace AmqpModbusIntegration
//{
//	public class ConfigData
//	{
//		public string Factory { get; set; }
//		public string Line { get; set; }
//		public string Station { get; set; }
//		public string MachineSN { get; set; }
//		public string MC_IP { get; set; }
//		public int MC_Port { get; set; }
//		public string Remote_IP { get; set; }
//		public int Remote_Port { get; set; }
//		public string PublishAddress { get; set; }
//		public string MyrequestUri { get; set; }
//		public string ModelName { get; set; }
//		public string MC_Name { get; set; }
//		public bool UseCFX { get; set; }
//	}

//	public class SwitchDeviceConfig
//	{
//		public string COM { get; set; }
//		public int StationNumber { get; set; }
//		public float fTemperature { get; set; }
//		public bool bEnergyConsumption { get; set; }
//	}

//	/// <summary>
//	/// 單例工具 – 於程式啟動載入 CFX.ini
//	/// </summary>
//	public static class ConfigManager
//	{
//		public static ConfigData Configuration { get; private set; }
//		public static SwitchDeviceConfig SwitchDevice { get; private set; }



//		public static void Load(string iniPath)
//		{
//			if (!File.Exists(iniPath))
//				throw new FileNotFoundException($"設定檔不存在: {iniPath}");

//			Configuration = new ConfigData();
//			SwitchDevice = new SwitchDeviceConfig();
//			string section = string.Empty;

//			foreach (string raw in File.ReadAllLines(iniPath))
//			{

//				string line = raw.Trim();
//				if (line.Length == 0 || line.StartsWith(";")) continue;
//				if (line.StartsWith("[") && line.EndsWith("]"))
//				{ section = line[1..^1]; continue; }
//				int idx = line.IndexOf('='); if (idx < 0) continue;
//				string key = line[..idx].Trim();
//				string val = line[(idx + 1)..].Trim();

//				if (section.Equals("Configuration", StringComparison.OrdinalIgnoreCase))
//				{

//					switch (key)
//					{
//						case "Factory": Configuration.Factory = val; break;
//						case "Line": Configuration.Line = val; break;
//						case "Station": Configuration.Station = val; break;
//						case "MachineSN": Configuration.MachineSN = val; break;
//						case "MC_IP": Configuration.MC_IP = val; break;
//						case "MC_Port": Configuration.MC_Port = int.Parse(val); break;
//						case "Remote_IP": Configuration.Remote_IP = val; break;
//						case "Remote_Port": Configuration.Remote_Port = int.Parse(val); break;
//						case "PublishAddress": Configuration.PublishAddress = val; break;
//						case "MyrequestUri": Configuration.MyrequestUri = val; break;
//						case "ModelName": Configuration.ModelName = val; break;
//						case "MC_Name": Configuration.MC_Name = val; break;
//						case "UseCFX": Configuration.UseCFX = bool.Parse(val); break;
//					}

//				}
//				else if (section.Equals("SwitchDevice", StringComparison.OrdinalIgnoreCase))
//				{

//					switch (key)
//					{
//						case "COM": SwitchDevice.COM = val; break;
//						case "StationNumber": SwitchDevice.StationNumber = int.Parse(val); break;
//						case "fTemperature": SwitchDevice.fTemperature = float.Parse(val); break;
//						case "bEnergyConsumption": SwitchDevice.bEnergyConsumption = bool.Parse(val); break;
//					}

//				}

//			}

//		}




//	}
//}
