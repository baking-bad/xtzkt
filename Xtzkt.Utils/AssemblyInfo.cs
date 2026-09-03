using System.Reflection;

namespace Xtzkt.Utils;

public static class AssemblyInfo
{
    public static string Name { get; }
    public static string Version { get; }

    static AssemblyInfo()
    {
        var assembly = Assembly.GetExecutingAssembly().GetName();
        Name = assembly.Name ?? string.Empty;
        Version = assembly.Version?.ToString() ?? string.Empty;
    }
}
