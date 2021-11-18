using Serilog;

namespace Suto;

internal class Stripper
{
    internal static void DLL(string f, string outDir, params string[] resolverDirs)
    {
        if (!File.Exists(f))
            return;

        FileInfo file = new(f);
        Log.Logger.Debug($"Stripping {file.Name}");

        Processor mod = Processor.Load(file.FullName, resolverDirs);
        mod.Virtualize();
        mod.Strip();

        string outFile = Path.Combine(outDir, file.Name);
        mod.Write(outFile);
    }
}