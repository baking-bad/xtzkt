using System.Text.Json;

namespace Xtzkt.Indexers.L1.Protocols.Genesis
{
    class Diagnostics : IDiagnostics
    {
        public void TrackChanges() { }
        public Task Run(JsonElement block) => Task.CompletedTask;
        public Task Run(int level) => Task.CompletedTask;
    }
}
