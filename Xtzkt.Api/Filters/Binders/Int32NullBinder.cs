using Microsoft.AspNetCore.Mvc.ModelBinding;
using Xtzkt.Api.Extensions;
using Xtzkt.Api.Filters.Parameters;

namespace Xtzkt.Api.Filters.Binders;

public class Int32NullBinder : IModelBinder
{
    public Task BindModelAsync(ModelBindingContext bindingContext)
    {
        var param = bindingContext.ModelName;
        var hasValue = false;

        if (!bindingContext.TryGetInt32Null($"{param}", ref hasValue, out var value))
            return Task.CompletedTask;

        if (!bindingContext.TryGetInt32Null($"{param}.eq", ref hasValue, out var eq))
            return Task.CompletedTask;

        if (!bindingContext.TryGetInt32Null($"{param}.ne", ref hasValue, out var ne))
            return Task.CompletedTask;

        if (!bindingContext.TryGetInt32Null($"{param}.gt", ref hasValue, out var gt))
            return Task.CompletedTask;

        if (!bindingContext.TryGetInt32Null($"{param}.ge", ref hasValue, out var ge))
            return Task.CompletedTask;

        if (!bindingContext.TryGetInt32Null($"{param}.lt", ref hasValue, out var lt))
            return Task.CompletedTask;

        if (!bindingContext.TryGetInt32Null($"{param}.le", ref hasValue, out var le))
            return Task.CompletedTask;

        if (!bindingContext.TryGetInt32NullList($"{param}.in", ref hasValue, out var @in))
            return Task.CompletedTask;

        if (!bindingContext.TryGetInt32NullList($"{param}.ni", ref hasValue, out var ni))
            return Task.CompletedTask;

        bindingContext.Result = ModelBindingResult.Success(!hasValue ? null : new Int32NullParameter
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
