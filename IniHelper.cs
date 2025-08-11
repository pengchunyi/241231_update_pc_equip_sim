using System.Runtime.InteropServices;
using System.Text;

public class IniHelper
{
	private string iniPath;

	public IniHelper(string path)
	{
		iniPath = path;
	}

	[DllImport("kernel32", CharSet = CharSet.Auto)]
	private static extern int GetPrivateProfileString(
		string section, string key, string defaultValue,
		StringBuilder returnValue, int size, string filePath);

	public string Read(string section, string key, string defaultValue = "")
	{
		StringBuilder result = new StringBuilder(255);
		GetPrivateProfileString(section, key, defaultValue, result, 255, iniPath);
		return result.ToString();
	}
}
