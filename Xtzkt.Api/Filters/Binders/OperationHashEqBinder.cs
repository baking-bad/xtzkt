using Microsoft.AspNetCore.Mvc.ModelBinding;
using Xtzkt.Api.Extensions;
using Xtzkt.Api.Filters.Parameters;

namespace Xtzkt.Api.Filters.Binders;

public class OperationHashEqBinder : IModelBinder
{
    const string Prefix = OperationHashBinder.Prefix;
    const int Base58Len = OperationHashBinder.Base58Len;
    const int HexLen = OperationHashBinder.HexLen;

    public Task BindModelAsync(ModelBindingContext bindingContext)
    {
        var param = bindingContext.ModelName;
        var hasValue = false;

        if (!bindingContext.TryGetHexOrBase58($"{param}", ref hasValue, out var value, Prefix, Base58Len, HexLen))
            return Task.CompletedTask;

        if (!bindingContext.TryGetHexOrBase58($"{param}.eq", ref hasValue, out var eq, Prefix, Base58Len, HexLen))
            return Task.CompletedTask;

        if (!bindingContext.TryGetHexOrBase58List($"{param}.in", ref hasValue, out var @in, Prefix, Base58Len, HexLen))
            return Task.CompletedTask;

        var _eq = value ?? eq;
        if (_eq != null)
        {
            if (@in != null)
            {
                if (!@in.Contains(_eq))
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
