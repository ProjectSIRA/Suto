using Mono.Cecil;
using System.ComponentModel;
using System.Reflection;
using System.Runtime.InteropServices;

namespace Suto;

internal class Loader : BaseAssemblyResolver
{
    public override AssemblyDefinition Resolve(AssemblyNameReference name, ReaderParameters parameters)
    {
        LibLoader.SetupAssemblyFilenames();

        if (LibLoader.Locations.TryGetValue($"{name.Name}.{name.Version}.dll", out var path))
        {
            if (File.Exists(path))
            {
                return AssemblyDefinition.ReadAssembly(path, parameters);
            }
        }
        else if (LibLoader.Locations.TryGetValue($"{name.Name}.dll", out path))
        {
            if (File.Exists(path))
            {
                return AssemblyDefinition.ReadAssembly(path, parameters);
            }
        }

        return base.Resolve(name, parameters);
    }
}

internal static class LibLoader
{
    internal static Dictionary<string, string> Locations { get; private set; } = null!;
    private static string _path = null!;

    internal static void Configure(string path)
    {
        _path = path;
        AppDomain.CurrentDomain.AssemblyResolve -= AssemblyLibLoader;
        AppDomain.CurrentDomain.AssemblyResolve += AssemblyLibLoader;
        SetupAssemblyFilenames(true);
    }

    internal static void SetupAssemblyFilenames(bool force = false)
    {
        string LibraryPath = Path.Combine(_path, "Libs");
        string NativeLibraryPath = Path.Combine(LibraryPath, "Native");

        if (Locations == null || force)
        {
            Locations = new Dictionary<string, string>();

            foreach (var fn in TraverseTree(LibraryPath, s => s != NativeLibraryPath))
            {
                if (Locations.ContainsKey(fn.Name) == false)
                {
                    Locations.Add(fn.Name, fn.FullName);
                }
            }

            void AddDir(string path)
            {
                var retPtr = AddDllDirectory(path);
                if (retPtr == IntPtr.Zero)
                {
                    var err = new Win32Exception();
                }
            }

            if (Directory.Exists(NativeLibraryPath))
            {
                AddDir(NativeLibraryPath);
                TraverseTree(NativeLibraryPath, dir =>
                { // this is a terrible hack for iterating directories
                    AddDir(dir); return true;
                }).All(f => true); // force it to iterate all
            }

            var unityData = Directory.EnumerateDirectories(_path, "*_Data").First();
            AddDir(Path.Combine(unityData, "Plugins"));

            foreach (string? dir in Environment.GetEnvironmentVariable("path")?.Split(Path.PathSeparator)!)
            {
                AddDir(dir);
            }
        }
    }

    public static Assembly? AssemblyLibLoader(object? source, ResolveEventArgs e)
    {
        var asmName = new AssemblyName(e.Name);
        return LoadLibrary(asmName);
    }

    internal static Assembly? LoadLibrary(AssemblyName asmName)
    {
        SetupAssemblyFilenames();

        var testFile = $"{asmName.Name}.{asmName.Version}.dll";

        if (Locations.TryGetValue(testFile, out var path))
        {
            if (File.Exists(path))
                return Assembly.LoadFrom(path);
        }
        else if (Locations.TryGetValue(_ = $"{asmName.Name}.dll", out path))
        {
            if (File.Exists(path))
                return Assembly.LoadFrom(path);
        }

        return null;
    }

    private static IEnumerable<FileInfo> TraverseTree(string root, Func<string, bool>? dirValidator = null)
    {
        if (dirValidator == null)
            dirValidator = s => true;

        Stack<string> dirs = new(32);

        if (!Directory.Exists(root))
            throw new ArgumentException(root);

        dirs.Push(root);

        while (dirs.Count > 0)
        {
            string currentDir = dirs.Pop();
            string[] subDirs;
            try
            {
                subDirs = Directory.GetDirectories(currentDir);
            }
            catch (UnauthorizedAccessException)
            { continue; }
            catch (DirectoryNotFoundException)
            { continue; }

            string[] files;
            try
            {
                files = Directory.GetFiles(currentDir);
            }
            catch (UnauthorizedAccessException)
            { continue; }
            catch (DirectoryNotFoundException)
            { continue; }

            foreach (string str in subDirs)
                if (dirValidator(str)) dirs.Push(str);

            foreach (string file in files)
            {
                FileInfo nextValue;
                try
                {
                    nextValue = new FileInfo(file);
                }
                catch (FileNotFoundException)
                { continue; }

                yield return nextValue;
            }
        }
    }

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern IntPtr AddDllDirectory(string lpPathName);
}