namespace Xtzkt.Indexers.TezosX.Services.Observer.Notifiers
{
    class PollingHeadNotifier(int _period, EvmNode _node, ILogger _logger) : HeadNotifier(_logger)
    {
        protected override string Parameters => $"method: polling, period: {_period}ms";

        protected override async Task OnTick(CancellationToken cancellationToken)
        {
            Notify(Header.Parse(await _node.GetHead()));

            await Task.Delay(_period, cancellationToken);
        }
    }
}
