using Microsoft.AspNetCore.Mvc.ModelBinding;
using Xtzkt.Api.Extensions;
using Xtzkt.Api.Filters.Parameters;

namespace Xtzkt.Api.Filters.Binders;

public class ExpressionBinder : IModelBinder
{
    public Task BindModelAsync(ModelBindingContext bindingContext)
    {
        var param = bindingContext.ModelName;
        var hasValue = false;

        if (!bindingContext.TryGetBase58($"{param}", ref hasValue, out var value, "expr", 54))
            return Task.CompletedTask;

        if (!bindingContext.TryGetBase58($"{param}.eq", ref hasValue, out var eq, "expr", 54))
            return Task.CompletedTask;

        if (!bindingContext.TryGetBase58($"{param}.ne", ref hasValue, out var ne, "expr", 54))
            return Task.CompletedTask;

        if (!bindingContext.TryGetBase58List($"{param}.in", ref hasValue, out var @in, "expr", 54))
            return Task.CompletedTask;

        if (!bindingContext.TryGetBase58List($"{param}.ni", ref hasValue, out var ni, "expr", 54))
            return Task.CompletedTask;

        bindingContext.Result = ModelBindingResult.Success(!hasValue ? null : new ExpressionParameter
        {
            Eq = value ?? eq,
            Ne = ne,
            In = @in,
            Ni = ni
        });

        return Task.CompletedTask;
    }
}
