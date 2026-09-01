using Microsoft.AspNetCore.Mvc.ModelBinding;
using Netezos;
using Xtzkt.Api.Extensions;
using Xtzkt.Api.Filters.Parameters;

namespace Xtzkt.Api.Filters.Binders;

public class OperationHashBinder : IModelBinder
{
    internal const int HexLen = 64;

    public Task BindModelAsync(ModelBindingContext bindingContext)
    {
        var param = bindingContext.ModelName;
        var hasValue = false;

        if (!bindingContext.TryGetHexOrBase58Bytes($"{param}", ref hasValue, out var value, Prefixes.o, HexLen))
            return Task.CompletedTask;

        if (!bindingContext.TryGetHexOrBase58Bytes($"{param}.eq", ref hasValue, out var eq, Prefixes.o, HexLen))
            return Task.CompletedTask;

        if (!bindingContext.TryGetHexOrBase58Bytes($"{param}.ne", ref hasValue, out var ne, Prefixes.o, HexLen))
            return Task.CompletedTask;

        if (!bindingContext.TryGetHexOrBase58BytesList($"{param}.in", ref hasValue, out var @in, Prefixes.o, HexLen))
            return Task.CompletedTask;

        if (!bindingContext.TryGetHexOrBase58BytesList($"{param}.ni", ref hasValue, out var ni, Prefixes.o, HexLen))
            return Task.CompletedTask;

        bindingContext.Result = ModelBindingResult.Success(!hasValue ? null : new OperationHashParameter
        {
            Eq = value ?? eq,
            Ne = ne,
            In = @in,
            Ni = ni
        });

        return Task.CompletedTask;
    }
}
