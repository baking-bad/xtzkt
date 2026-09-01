using Microsoft.AspNetCore.Mvc.ModelBinding;
using Netezos;
using Xtzkt.Api.Extensions;
using Xtzkt.Api.Filters.Parameters;

namespace Xtzkt.Api.Filters.Binders;

public class ExpressionBytesBinder : IModelBinder
{
    public Task BindModelAsync(ModelBindingContext bindingContext)
    {
        var param = bindingContext.ModelName;
        var hasValue = false;

        if (!bindingContext.TryGetBase58Bytes($"{param}", ref hasValue, out var value, Prefixes.expr))
            return Task.CompletedTask;

        if (!bindingContext.TryGetBase58Bytes($"{param}.eq", ref hasValue, out var eq, Prefixes.expr))
            return Task.CompletedTask;

        if (!bindingContext.TryGetBase58Bytes($"{param}.ne", ref hasValue, out var ne, Prefixes.expr))
            return Task.CompletedTask;

        if (!bindingContext.TryGetBase58BytesList($"{param}.in", ref hasValue, out var @in, Prefixes.expr))
            return Task.CompletedTask;

        if (!bindingContext.TryGetBase58BytesList($"{param}.ni", ref hasValue, out var ni, Prefixes.expr))
            return Task.CompletedTask;

        bindingContext.Result = ModelBindingResult.Success(!hasValue ? null : new ExpressionBytesParameter
        {
            Eq = value ?? eq,
            Ne = ne,
            In = @in,
            Ni = ni
        });

        return Task.CompletedTask;
    }
}
