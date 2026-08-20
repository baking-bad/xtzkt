using System.Text.Json;
using System.Text.Json.Serialization;
using Xtzkt.Indexers.Common.Extensions;
using Xtzkt.Indexers.TezosX.Services.Observer.Notifiers;
using Xtzkt.Indexers.TezosX.Utils;

namespace Xtzkt.Indexers.TezosX.Services.Observer
{
    abstract class HeadNotifier(ILogger _logger)
    {
        public event OnHeadEventHandler? OnHead;

        protected abstract string Parameters { get; }

        public async Task RunAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("Head notifier started ({params})", Parameters);

            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    await OnTick(cancellationToken);
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to fetch head from node");
                    try { await Task.Delay(5000, cancellationToken); }
                    catch (OperationCanceledException) { }
                }
            }

            _logger.LogInformation("Head notifier stopped");
        }

        protected abstract Task OnTick(CancellationToken cancellationToken);

        protected void Notify(Header head)
        {
            OnHead?.Invoke(head);
        }

        public static HeadNotifier Create(ObserverConfig config, EvmNode node, ILogger logger)
        {
            return config.Method == "streaming"
                ? new StreamingHeadNotifier(node, logger)
                : new PollingHeadNotifier(config.Period, node, logger);
        }
    }

    delegate void OnHeadEventHandler(Header head);

    class Header
    {
        [JsonPropertyName("predecessor")]
        public required string Predecessor { get; set; }

        [JsonPropertyName("hash")]
        public required string Hash { get; set; }

        [JsonPropertyName("level")]
        public required int Level { get; set; }

        [JsonPropertyName("timestamp")]
        public required DateTime Timestamp { get; set; }

        public static Header Parse(JsonElement block) => new()
        {
            Predecessor = block.RequiredString("parentHash"),
            Hash = block.RequiredString("hash"),
            Level = HexNumber.GetInt32(block.RequiredString("number")),
            Timestamp = HexNumber.GetTimestamp(block.RequiredString("timestamp"))
        };

        public static Header Empty() => new()
        {
            Predecessor = string.Empty,
            Hash = string.Empty,
            Level = -1,
            Timestamp = DateTime.MinValue,
        };
    }
}
