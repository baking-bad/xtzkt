using System.Reflection;
using System.Text.Json.Serialization.Metadata;

namespace Xtzkt.Api.Utils.Converters;

public static class TypeInfoResolverModifiers
{
    public static void BasePropsFirst(JsonTypeInfo typeInfo)
    {
        if (typeInfo.Kind != JsonTypeInfoKind.Object) return;

        var order = 0;
        foreach (var type in GetTypeHierarchy(typeInfo.Type))
        {
            foreach (var prop in typeInfo.Properties)
            {
                if (prop.AttributeProvider is MemberInfo mi && mi.DeclaringType == type)
                    prop.Order = order++;
            }
        }
    }

    static List<Type> GetTypeHierarchy(Type type)
    {
        var chain = new List<Type>();
        var t = type;
        while (t != null && t != typeof(object)) { chain.Add(t); t = t.BaseType; }
        chain.Reverse();
        return chain;
    }
}
