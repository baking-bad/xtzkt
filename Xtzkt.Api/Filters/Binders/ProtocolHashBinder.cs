using Microsoft.AspNetCore.Mvc.ModelBinding;
using Xtzkt.Api.Extensions;
using Xtzkt.Api.Filters.Parameters;

namespace Xtzkt.Api.Filters.Binders;

public class ProtocolHashBinder : IModelBinder
{
    internal const string Prefix = "P";
    internal const int Base58Len = 51;
    internal const int HexLen = 66;

    public Task BindModelAsync(ModelBindingContext bindingContext)
    {
        var param = bindingContext.ModelName;
        var hasValue = false;

        if (!bindingContext.TryGetHexOrBase58($"{param}", ref hasValue, out var value, Prefix, Base58Len, HexLen))
            return Task.CompletedTask;

        if (!bindingContext.TryGetHexOrBase58($"{param}.eq", ref hasValue, out var eq, Prefix, Base58Len, HexLen))
            return Task.CompletedTask;

        if (!bindingContext.TryGetHexOrBase58($"{param}.ne", ref hasValue, out var ne, Prefix, Base58Len, HexLen))
            return Task.CompletedTask;

        if (!bindingContext.TryGetHexOrBase58List($"{param}.in", ref hasValue, out var @in, Prefix, Base58Len, HexLen))
            return Task.CompletedTask;

        if (!bindingContext.TryGetHexOrBase58List($"{param}.ni", ref hasValue, out var ni, Prefix, Base58Len, HexLen))
            return Task.CompletedTask;

        bindingContext.Result = ModelBindingResult.Success(!hasValue ? null : new ProtocolHashParameter
        {
            Eq = value ?? eq,
            Ne = ne,
            In = @in,
            Ni = ni
        });

        return Task.CompletedTask;
    }
}
