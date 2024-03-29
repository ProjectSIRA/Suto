﻿using Mono.Cecil;
using Mono.Cecil.Rocks;
using System.Runtime.InteropServices;

namespace Suto;

internal class Processor
{
    public static Processor Load(string fileName, params string[] resolverDirs)
    {
        return new Processor(fileName, resolverDirs);
    }

    private readonly FileInfo _file;
    private TypeReference? _inModreqRef;
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

    public void Virtualize()
    {
        foreach (TypeDefinition type in _module.Types)
        {
            VirtualizeType(type);
        }
    }

    private void VirtualizeType(TypeDefinition type)
    {
        if (type.IsSealed) type.IsSealed = false;
        
        if (type.IsNestedPrivate)
        {
            type.IsNestedPrivate = false;
            type.IsNestedPublic = true;
        }

        if (type.IsInterface) return;
        if (type.IsAbstract) return;

        foreach (var subType in type.NestedTypes)
        {
            VirtualizeType(subType);
        }

        foreach (var m in type.Methods)
        {
            if (m.IsManaged
                && m.IsIL
                && !m.IsStatic
                && (!m.IsVirtual || m.IsFinal)
                && !m.IsAbstract
                && !m.IsAddOn
                && !m.IsConstructor
                && !m.IsSpecialName
                && !m.IsGenericInstance
                && !m.HasOverrides)
            {

                foreach (var param in m.Parameters)
                {
                    if (param.IsIn)
                    {
                        _inModreqRef ??= _module.ImportReference(typeof(InAttribute));
                        param.ParameterType = AddModreqIfNotExist(param.ParameterType, _inModreqRef);
                    }
                }

                m.IsVirtual = true;
                m.IsFinal = false;
                m.IsPublic = true;
                m.IsPrivate = false;
                m.IsNewSlot = true;
                m.IsHideBySig = true;
            }
        }

        foreach (var field in type.Fields)
        {
            if (field.IsPrivate) field.IsFamily = true;
        }
    }

    private TypeReference AddModreqIfNotExist(TypeReference type, TypeReference mod)
    {
        var (element, opt, req) = GetDecomposedModifiers(type);
        if (!req.Contains(mod))
        {
            req.Add(mod);
        }
        return BuildModifiedType(element, opt, req);
    }

    public void Strip()
    {
        foreach (TypeDefinition type in _module.Types)
        {
            StripType(type);
        }
    }

    private (TypeReference Element, List<TypeReference> ModOpt, List<TypeReference> ModReq) GetDecomposedModifiers(TypeReference type)
    {
        var opt = new List<TypeReference>();
        var req = new List<TypeReference>();

        while (type is IModifierType modif)
        {
            if (type.IsOptionalModifier)
                opt.Add(modif.ModifierType);
            if (type.IsRequiredModifier)
                req.Add(modif.ModifierType);

            type = modif.ElementType;
        }

        return (type, opt, req);
    }

    private TypeReference BuildModifiedType(TypeReference type, IEnumerable<TypeReference> opt, IEnumerable<TypeReference> req)
    {
        foreach (var mod in req)
        {
            type = type.MakeRequiredModifierType(mod);
        }

        foreach (var mod in opt)
        {
            type = type.MakeOptionalModifierType(mod);
        }

        return type;
    }

    private void StripType(TypeDefinition type)
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
}