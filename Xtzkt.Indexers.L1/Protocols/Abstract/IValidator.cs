using System.Text.Json;

namespace Xtzkt.Indexers.L1.Protocols
{
    public interface IValidator
    {
        Task ValidateBlock(JsonElement block);
    }
}
