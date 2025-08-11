//using CFX.Structures;
//using System.Collections.Generic;
//using System;

//public class ParameterProcessor
//{
//	public List<byte> ExtractStationNumbers(List<Parameter> parameters)
//	{
//		return parameters
//			.OfType<GenericParameter>()
//			.Where(p => p.Name == "站號")
//			.Select(p =>
//			{
//				if (byte.TryParse(p.Value.ToString(), out var station))
//				{
//					return (byte?)station;
//				}
//				return null;
//			})
//			.Where(station => station.HasValue)
//			.Select(station => station.Value)
//			.ToList();
//	}

//	public bool ProcessTemperature(GenericParameter parameter, List<byte> stationNumbers, ModbusService modbusService)
//	{
//		if (!ushort.TryParse(parameter.Value.ToString(), out var temperature))
//		{
//			Console.WriteLine($"無效的保護溫度值: {parameter.Value}");
//			return false;
//		}

//		foreach (var station in stationNumbers)
//		{
//			modbusService.SetTemperature(station, temperature);
//		}

//		return true;
//	}

//	public bool ProcessSwitch(GenericParameter parameter, List<byte> stationNumbers, ModbusService modbusService)
//	{
//		bool isSwitchOn = parameter.Value.ToString() == "開";

//		foreach (var station in stationNumbers)
//		{
//			modbusService.SwitchCommand(station, isSwitchOn);
//		}

//		return true;
//	}
//}
