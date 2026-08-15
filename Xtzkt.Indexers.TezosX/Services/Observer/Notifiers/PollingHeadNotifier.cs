using Xtzkt.Indexers.Common.Extensions;
using Xtzkt.Indexers.TezosX.Utils;

namespace Xtzkt.Indexers.TezosX.Services.Observer.Notifiers
{
    class PollingHeadNotifier(int _lag, int _period, EvmNode _node, ILogger _logger) : HeadNotifier(_logger)
    {
        protected override string Parameters => $"method: polling, period: {_period}ms, lag: {_lag}";

        protected override async Task OnTick(CancellationToken cancellationToken)
        {
            var result = await _node.GetHead();
            
            var head = new Header
            {
                Predecessor = result.RequiredString("parentHash"),
                Hash = result.RequiredString("hash"),
                Level = HexNumber.GetInt32(result.RequiredString("number")),
                Timestamp = HexNumber.GetTimestamp(result.RequiredString("timestamp"))
            };
            Notify(head);

            await Task.Delay(_period, cancellationToken);
        }
    }
}
