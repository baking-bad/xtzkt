namespace Xtzkt.Indexers.Common.Extensions;

public static class ListExtension
{
    public static bool StartsWith<T>(this List<T> src, List<T> data) where T : notnull
    {
        if (src.Count < data.Count)
            return false;

        for (int i = 0; i < data.Count; i++)
            if (!src[i].Equals(data[i]))
                return false;

        return true;
    }
}
