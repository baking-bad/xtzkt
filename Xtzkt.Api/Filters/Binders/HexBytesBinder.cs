using Microsoft.AspNetCore.Mvc.ModelBinding;
using Xtzkt.Api.Extensions;
using Xtzkt.Api.Filters.Parameters;

namespace Xtzkt.Api.Filters.Binders;

public class HexBytesBinder : IModelBinder
{
    public Task BindModelAsync(ModelBindingContext bindingContext)
    {
        var param = bindingContext.ModelName;
        var hasValue = false;

        if (!bindingContext.TryGetHexBytes($"{param}", ref hasValue, out var value))
            return Task.CompletedTask;

        if (!bindingContext.TryGetHexBytes($"{param}.eq", ref hasValue, out var eq))
            return Task.CompletedTask;

        if (!bindingContext.TryGetHexBytes($"{param}.ne", ref hasValue, out var ne))
            return Task.CompletedTask;

        if (!bindingContext.TryGetHexBytesList($"{param}.in", ref hasValue, out var @in))
            return Task.CompletedTask;

        if (!bindingContext.TryGetHexBytesList($"{param}.ni", ref hasValue, out var ni))
            return Task.CompletedTask;

        bindingContext.Result = ModelBindingResult.Success(!hasValue ? null : new HexBytesParameter
        {
            Eq = value ?? eq,
            Ne = ne,
            In = @in,
            Ni = ni
        });

        return Task.CompletedTask;
    }
}
