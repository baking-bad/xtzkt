using Xtzkt.Indexers.Common.Services;

namespace Xtzkt.Indexers.L1.Services.Observer.Notifiers
{
    class StreamingHeadNotifier(TezosNode _node, ILogger _logger) : HeadNotifier(_logger)
    {
        protected override string Parameters => $"method: streaming";

        protected override async Task OnTick(CancellationToken cancellationToken)
        {
            await foreach (var head in _node.MonitorAsync<Header>("monitor/heads/main", cancellationToken))
            {
                Notify(head);
            }
        }
    }
}
