using Microsoft.AspNetCore.Mvc.ModelBinding;
using Netezos;
using Xtzkt.Api.Extensions;
using Xtzkt.Api.Filters.Parameters;

namespace Xtzkt.Api.Filters.Binders;

public class MichelsonBlockHashBinder : IModelBinder
{
    public Task BindModelAsync(ModelBindingContext bindingContext)
    {
        var param = bindingContext.ModelName;
        var hasValue = false;

        if (!bindingContext.TryGetBase58Bytes($"{param}", ref hasValue, out var value, Prefixes.B))
            return Task.CompletedTask;

        if (!bindingContext.TryGetBase58Bytes($"{param}.eq", ref hasValue, out var eq, Prefixes.B))
            return Task.CompletedTask;

        if (!bindingContext.TryGetBase58Bytes($"{param}.ne", ref hasValue, out var ne, Prefixes.B))
            return Task.CompletedTask;

        if (!bindingContext.TryGetBase58BytesList($"{param}.in", ref hasValue, out var @in, Prefixes.B))
            return Task.CompletedTask;

        if (!bindingContext.TryGetBase58BytesList($"{param}.ni", ref hasValue, out var ni, Prefixes.B))
            return Task.CompletedTask;

        bindingContext.Result = ModelBindingResult.Success(!hasValue ? null : new MichelsonBlockHashParameter
        {
            Eq = value ?? eq,
            Ne = ne,
            In = @in,
            Ni = ni
        });

        return Task.CompletedTask;
    }
}
