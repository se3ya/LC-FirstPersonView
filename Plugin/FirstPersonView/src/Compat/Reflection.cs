using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System;

namespace FirstPersonView.Compat;

// shared reflection lookups for the soft dependency compats.
internal static class Reflection
{
    public static Assembly? FindAssembly(string name) =>
        AppDomain.CurrentDomain.GetAssemblies().FirstOrDefault(a => a.GetName().Name == name);

    public static IEnumerable<Type> GetLoadableTypes(this Assembly assembly)
    {
        if (assembly == null)
            return Enumerable.Empty<Type>();

        try
        {
            return assembly.GetTypes();
        }
        catch (ReflectionTypeLoadException ex)
        {
            return ex.Types.Where(type => type != null)!;
        }
    }
}