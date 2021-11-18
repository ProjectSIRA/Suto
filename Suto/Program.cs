using Serilog;
using Suto;
using System.Diagnostics;
using System.IO.Compression;

Log.Logger = new LoggerConfiguration().MinimumLevel.Debug().WriteTo.Console().CreateLogger();


string? installDirectory = Utilities.GetInstallDirectory();
if (installDirectory is null)
{
    Log.Logger.Fatal("Failed at [1] Could not find Beat Saber installation folder. Make sure it's installed!");
    return;
}

Log.Logger.Information("Beat Saber location determined. {Path}", installDirectory);
LibLoader.Configure(installDirectory);

string libsDir = Path.Combine(installDirectory, @"Libs");
string managedDir = Path.Combine(installDirectory, @"Beat Saber_Data\Managed");

string? beatSaberVersion = Utilities.GetGameVersion(installDirectory);
if (beatSaberVersion is null)
{
    Log.Logger.Fatal("Failed at [2]. Unable to determine the game version.");
    return;
}


Log.Logger.Information("Game version determined. v{Version}", beatSaberVersion);

string outDirectory = Path.Combine(Directory.GetCurrentDirectory(), "Stripped", beatSaberVersion);
if (Directory.Exists(outDirectory))
    Directory.Delete(outDirectory, true);
Directory.CreateDirectory(outDirectory);

DirectoryInfo outLibs = Directory.CreateDirectory(Path.Combine(outDirectory, "Libs"));
DirectoryInfo outManaged = Directory.CreateDirectory(Path.Combine(outDirectory, "Beat Saber_Data", "Managed"));
outLibs.Create();
outManaged.Create();

string[] ignorables = new string[]
{
    //"BouncyCastle.Crypto.dll",
    //"Microsoft.",
    //"System.",
    //"mscorlib"
};

string[] libInclude = new string[]
{
    "0Harmony",
    "Hive.Versioning",
    "Ionic",
    "Mono",
    "Newtonsoft",
    "SemVer"
};

try
{
    foreach (string f in Resolver.DLLs(managedDir, ignorables, true))
        Stripper.DLL(f, outManaged.FullName, libsDir, managedDir);

    foreach (string f in Resolver.DLLs(libsDir, libInclude))
        Stripper.DLL(f, outLibs.FullName, libsDir, managedDir);
}

catch (Exception ex)
{
    Log.Logger.Fatal("Failed at [3]. Unable to resolve and strip the installation.\n{Exception}", ex);
    return;
}

Log.Logger.Information("Successfully stripped Beat Saber.");
Log.Logger.Information("Zipping stripped libraries into a single file.");

string strippedFolder = Path.Combine(Directory.GetCurrentDirectory(), "Stripped");

string zipSavePath = Path.Combine(strippedFolder, $"{beatSaberVersion}-Stripped.zip");

if (File.Exists(zipSavePath))
    File.Delete(zipSavePath);

ZipFile.CreateFromDirectory(outDirectory, zipSavePath);

ProcessStartInfo info = new();
info.FileName = "explorer";
info.Arguments = string.Format("/select, \"{0}\"", zipSavePath);
Process.Start(info);

Log.Logger.Information("Completed. Enjoy your files!");