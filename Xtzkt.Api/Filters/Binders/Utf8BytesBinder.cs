using Microsoft.AspNetCore.Mvc.ModelBinding;
using Xtzkt.Api.Extensions;
using Xtzkt.Api.Filters.Parameters;

namespace Xtzkt.Api.Filters.Binders;

public class Utf8BytesBinder : IModelBinder
{
    public Task BindModelAsync(ModelBindingContext bindingContext)
    {
        var param = bindingContext.ModelName;
        var hasValue = false;

        if (!bindingContext.TryGetUtf8Bytes($"{param}", ref hasValue, out var value))
            return Task.CompletedTask;

        if (!bindingContext.TryGetUtf8Bytes($"{param}.eq", ref hasValue, out var eq))
            return Task.CompletedTask;

        if (!bindingContext.TryGetUtf8Bytes($"{param}.ne", ref hasValue, out var ne))
            return Task.CompletedTask;

        if (!bindingContext.TryGetUtf8BytesList($"{param}.in", ref hasValue, out var @in))
            return Task.CompletedTask;

        if (!bindingContext.TryGetUtf8BytesList($"{param}.ni", ref hasValue, out var ni))
            return Task.CompletedTask;

        bindingContext.Result = ModelBindingResult.Success(!hasValue ? null : new Utf8BytesParameter
        {
            Eq = value ?? eq,
            Ne = ne,
            In = @in,
            Ni = ni,
        });

        return Task.CompletedTask;
    }
}
