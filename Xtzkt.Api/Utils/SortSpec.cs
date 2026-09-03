namespace Xtzkt.Api.Utils
{
    public class SortSpec(string pk) : Dictionary<string, (string column, string type)>
    {
        public string PrimaryKey { get; } = pk;
    }
}
