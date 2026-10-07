using System.Linq;
using System.Reflection;
using System;

namespace FirstPersonView;

// shared reflection lookups for the soft dependency compats.
internal static class Reflection
{
    public static Assembly? FindAssembly(string name) =>
        AppDomain.CurrentDomain.GetAssemblies().FirstOrDefault(a => a.GetName().Name == name);
}
