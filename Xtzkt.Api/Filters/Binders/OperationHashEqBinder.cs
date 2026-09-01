using Microsoft.AspNetCore.Mvc.ModelBinding;
using Netezos;
using Xtzkt.Api.Extensions;
using Xtzkt.Api.Filters.Parameters;

namespace Xtzkt.Api.Filters.Binders;

public class OperationHashEqBinder : IModelBinder
{
    const int HexLen = OperationHashBinder.HexLen;

    public Task BindModelAsync(ModelBindingContext bindingContext)
    {
        var param = bindingContext.ModelName;
        var hasValue = false;

        if (!bindingContext.TryGetHexOrBase58Bytes($"{param}", ref hasValue, out var value, Prefixes.o, HexLen))
            return Task.CompletedTask;

        if (!bindingContext.TryGetHexOrBase58Bytes($"{param}.eq", ref hasValue, out var eq, Prefixes.o, HexLen))
            return Task.CompletedTask;

        if (!bindingContext.TryGetHexOrBase58BytesList($"{param}.in", ref hasValue, out var @in, Prefixes.o, HexLen))
            return Task.CompletedTask;

        var _eq = value ?? eq;
        if (_eq != null)
        {
            if (@in != null)
            {
                if (!@in.Any(x => x.SequenceEqual(_eq)))
                {
                    bindingContext.ModelState.TryAddModelError($"{param}.in", "Conflicts with `.eq`.");
                    return Task.CompletedTask;
                }
                @in = null;
            }
        }

        if (@in != null)
        {
            if (@in.Count == 1)
            {
                _eq = @in[0];
                @in = null;
            }
        }

        bindingContext.Result = ModelBindingResult.Success(!hasValue ? null : new OperationHashEqParameter
        {
            Eq = _eq,
            In = @in,
        });

        return Task.CompletedTask;
    }
}
