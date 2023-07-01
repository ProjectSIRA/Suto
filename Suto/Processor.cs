using Mono.Cecil;

namespace Suto;

internal class Processor : IDisposable
{
    public static Processor Load(string fileName, params string[] resolverDirs)
    {
        return new Processor(fileName, resolverDirs);
    }

    private readonly FileInfo _file;
    private ModuleDefinition _module = null!;

    internal Processor(string fileName, params string[] resolverDirs)
    {
        _file = new FileInfo(fileName);
        LoadModules(resolverDirs);
    }

    private void LoadModules(string[] directories)
    {
        var resolver = new Loader();
        resolver.AddSearchDirectory(_file.Directory?.FullName);

        foreach (string dir in directories)
        {
            if (Directory.Exists(dir))
                resolver.AddSearchDirectory(dir);
        }

        ReaderParameters parameters = new()
        {
            AssemblyResolver = resolver,
            ReadWrite = false,
            ReadingMode = ReadingMode.Immediate,
            InMemory = true
        };

        _module = ModuleDefinition.ReadModule(_file.FullName, parameters);
    }

    public void Strip()
    {
        foreach (TypeDefinition type in _module.Types)
        {
            StripType(type);
        }
    }

    private static void StripType(TypeDefinition type)
    {
        foreach (var m in type.Methods)
        {
            if (m.Body != null)
            {
                m.Body.Instructions.Clear();
                m.Body.InitLocals = false;
                m.Body.Variables.Clear();
            }
        }

        foreach (var subType in type.NestedTypes)
        {
            StripType(subType);
        }
    }

    public void Write(string outFile)
    {
        _module.Write(outFile);
    }

    public void Dispose()
    {
        _module.Dispose();
    }
}