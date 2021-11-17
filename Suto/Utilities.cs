using Microsoft.Win32;
using System.Management;
using System.Text;
using System.Text.RegularExpressions;

#pragma warning disable CA1416 // Validate platform compatibility
namespace Suto;

/// <summary>
/// This entire class has essentially been yoinked from ModAssistant
/// https://github.com/Assistant/ModAssistant/blob/master/ModAssistant/Classes/Utils.cs
/// </summary>
internal class Utilities
{
    public static string? GetInstallDirectory()
    {
        string? installDir = @"C:\Program Files (x86)\Steam\steamapps\common\Beat Saber";

        if (!string.IsNullOrEmpty(installDir)
            && Directory.Exists(installDir)
            && Directory.Exists(Path.Combine(installDir, "Beat Saber_Data", "Plugins"))
            && File.Exists(Path.Combine(installDir, "Beat Saber.exe")))
        {
            return installDir;
        }

        try
        {
            installDir = GetSteamDirectory();
        }
        catch { }
        if (!string.IsNullOrEmpty(installDir))
        {
            return installDir;
        }

        try
        {
            installDir = GetOculusDirectory();
        }
        catch { }
        if (!string.IsNullOrEmpty(installDir))
        {
            return installDir;
        }

        return null;
    }

    public static string? GetSteamDirectory()
    {
        string? steamInstall = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64)?.OpenSubKey("SOFTWARE")?.OpenSubKey("WOW6432Node")?.OpenSubKey("Valve")?.OpenSubKey("Steam")?.GetValue("InstallPath")?.ToString();
        if (string.IsNullOrEmpty(steamInstall))
            steamInstall = Registry.LocalMachine.OpenSubKey("SOFTWARE")?.OpenSubKey("WOW6432Node")?.OpenSubKey("Valve")?.OpenSubKey("Steam")?.GetValue("InstallPath")?.ToString();

        if (string.IsNullOrEmpty(steamInstall))
            return null;

        string vdf = Path.Combine(steamInstall, @"steamapps\libraryfolders.vdf");
        if (!File.Exists(@vdf)) return null;

        Regex regex = new Regex("\\s\"(?:\\d|path)\"\\s+\"(.+)\"");
        List<string> SteamPaths = new() { Path.Combine(steamInstall, @"steamapps") };

        using (StreamReader reader = new(@vdf))
        {
            string? line;
            while ((line = reader.ReadLine()) != null)
            {
                Match match = regex.Match(line);
                if (match.Success)
                {
                    SteamPaths.Add(Path.Combine(match.Groups[1].Value.Replace(@"\\", @"\"), @"steamapps"));
                }
            }
        }

        regex = new Regex("\\s\"installdir\"\\s+\"(.+)\"");
        foreach (string path in SteamPaths)
        {
            if (File.Exists(Path.Combine(@path, @"appmanifest_" + "620980" + ".acf")))
            {
                using StreamReader reader = new(Path.Combine(@path, @"appmanifest_" + "620980" + ".acf"));
                string? line;
                while ((line = reader.ReadLine()) != null)
                {
                    Match match = regex.Match(line);
                    if (match.Success)
                    {
                        if (File.Exists(Path.Combine(@path, @"common", match.Groups[1].Value, "Beat Saber.exe")))
                            return Path.Combine(@path, @"common", match.Groups[1].Value);
                    }
                }
            }
        }
        return null;
    }

    public static string? GetOculusDirectory()
    {
        string? oculusInstall = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64)?.OpenSubKey("SOFTWARE")?.OpenSubKey("Wow6432Node")?.OpenSubKey("Oculus VR, LLC")?.OpenSubKey("Oculus")?.OpenSubKey("Config")?.GetValue("InitialAppLibrary")?.ToString();
        if (string.IsNullOrEmpty(oculusInstall))
            return null;

        if (!string.IsNullOrEmpty(oculusInstall))
        {
            if (File.Exists(Path.Combine(oculusInstall, "Software", "hyperbolic-magnetism-beat-saber", "Beat Saber.exe")))
            {
                return Path.Combine(oculusInstall, "Software", "hyperbolic-magnetism-beat-saber");
            }
        }

        // Yoinked this code from Umbranox's Mod Manager. Lot's of thanks and love for Umbra <3
        using (RegistryKey librariesKey = Registry.CurrentUser.OpenSubKey("Software")?.OpenSubKey("Oculus VR, LLC")?.OpenSubKey("Oculus")?.OpenSubKey("Libraries")!)
        {
            // Oculus libraries uses GUID volume paths like this "\\?\Volume{0fea75bf-8ad6-457c-9c24-cbe2396f1096}\Games\Oculus Apps", we need to transform these to "D:\Game"\Oculus Apps"
            WqlObjectQuery wqlQuery = new("SELECT * FROM Win32_Volume");
            using ManagementObjectSearcher searcher = new(wqlQuery);
            Dictionary<string, string> guidLetterVolumes = new();

            foreach (ManagementBaseObject disk in searcher.Get())
            {
                var diskId = ((string)disk.GetPropertyValue("DeviceID")).Substring(11, 36);
                var diskLetter = ((string)disk.GetPropertyValue("DriveLetter")) + @"\";

                if (!string.IsNullOrWhiteSpace(diskLetter))
                {
                    guidLetterVolumes.Add(diskId, diskLetter);
                }
            }

            // Search among the library folders
            foreach (string libraryKeyName in librariesKey.GetSubKeyNames())
            {
                using RegistryKey libraryKey = librariesKey.OpenSubKey(libraryKeyName)!;
                string libraryPath = (string)libraryKey.GetValue("Path")!;
                // Yoinked this code from Megalon's fix. <3
                string GUIDLetter = guidLetterVolumes.FirstOrDefault(x => libraryPath.Contains(x.Key)).Value;
                if (!string.IsNullOrEmpty(GUIDLetter))
                {
                    string finalPath = Path.Combine(GUIDLetter, libraryPath[49..], @"Software\hyperbolic-magnetism-beat-saber");
                    if (File.Exists(Path.Combine(finalPath, "Beat Saber.exe")))
                    {
                        return finalPath;
                    }
                }
            }
        }

        return null;
    }

    public static string? GetGameVersion(string path)
    {
        string filename = Path.Combine(path, "Beat Saber_Data", "globalgamemanagers");
        using FileStream? stream = File.OpenRead(filename);
        using var reader = new BinaryReader(stream, Encoding.UTF8);
        const string key = "public.app-category.games";
        int pos = 0;

        while (stream.Position < stream.Length && pos < key.Length)
        {
            if (reader.ReadByte() == key[pos]) pos++;
            else pos = 0;
        }

        if (stream.Position == stream.Length) // we went through the entire stream without finding the key
            return null;

        while (stream.Position < stream.Length)
        {
            var current = (char)reader.ReadByte();
            if (char.IsDigit(current))
                break;
        }

        var rewind = -sizeof(int) - sizeof(byte);
        stream.Seek(rewind, SeekOrigin.Current); // rewind to the string length

        var strlen = reader.ReadInt32();
        var strbytes = reader.ReadBytes(strlen);

        return Encoding.UTF8.GetString(strbytes);
    }
}