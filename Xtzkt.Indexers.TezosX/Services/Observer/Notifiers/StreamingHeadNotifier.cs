namespace Xtzkt.Indexers.TezosX.Services.Observer.Notifiers;

class StreamingHeadNotifier(EvmNode _node, ILogger _logger) : HeadNotifier(_logger)
{
    protected override string Parameters => "method: streaming";

    protected override async Task OnTick(CancellationToken cancellationToken)
    {
        Notify(Header.Parse(await _node.GetHead()));

        await foreach (var block in _node.MonitorHeads(cancellationToken))
        {
            Notify(Header.Parse(block));
        }
    }
}
