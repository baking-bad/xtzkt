using System.Text.Json;

namespace Xtzkt.Indexers.L1.Protocols
{
    public interface IDiagnostics
    {
        void TrackChanges();
        Task Run(JsonElement block);
        Task Run(int level);
    }
}
