using Microsoft.AspNetCore.Mvc.ModelBinding;
using Xtzkt.Api.Extensions;
using Xtzkt.Api.Filters.Parameters;

namespace Xtzkt.Api.Filters.Binders;

public class BigIntegerBinder : IModelBinder
{
    public Task BindModelAsync(ModelBindingContext bindingContext)
    {
        var param = bindingContext.ModelName;
        var hasValue = false;

        if (!bindingContext.TryGetBigInteger($"{param}", ref hasValue, out var value))
            return Task.CompletedTask;

        if (!bindingContext.TryGetBigInteger($"{param}.eq", ref hasValue, out var eq))
            return Task.CompletedTask;

        if (!bindingContext.TryGetBigInteger($"{param}.ne", ref hasValue, out var ne))
            return Task.CompletedTask;

        if (!bindingContext.TryGetBigInteger($"{param}.gt", ref hasValue, out var gt))
            return Task.CompletedTask;

        if (!bindingContext.TryGetBigInteger($"{param}.ge", ref hasValue, out var ge))
            return Task.CompletedTask;

        if (!bindingContext.TryGetBigInteger($"{param}.lt", ref hasValue, out var lt))
            return Task.CompletedTask;

        if (!bindingContext.TryGetBigInteger($"{param}.le", ref hasValue, out var le))
            return Task.CompletedTask;

        if (!bindingContext.TryGetBigIntegerList($"{param}.in", ref hasValue, out var @in))
            return Task.CompletedTask;

        if (!bindingContext.TryGetBigIntegerList($"{param}.ni", ref hasValue, out var ni))
            return Task.CompletedTask;

        bindingContext.Result = ModelBindingResult.Success(!hasValue ? null : new BigIntegerParameter
        {
            Eq = value ?? eq,
            Ne = ne,
            Gt = gt,
            Ge = ge,
            Lt = lt,
            Le = le,
            In = @in,
            Ni = ni
        });

        return Task.CompletedTask;
    }
}
