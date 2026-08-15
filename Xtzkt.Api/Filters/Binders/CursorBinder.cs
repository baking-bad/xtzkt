using Microsoft.AspNetCore.Mvc.ModelBinding;
using Xtzkt.Api.Extensions;
using Xtzkt.Api.Filters.Parameters;

namespace Xtzkt.Api.Filters.Binders;

public class CursorBinder : IModelBinder
{
    public Task BindModelAsync(ModelBindingContext bindingContext)
    {
        var hasValue = false;
        if (!bindingContext.TryGetStringList(bindingContext.ModelName, ref hasValue, out var list))
            return Task.CompletedTask;

        if (!hasValue)
        {
            bindingContext.Result = ModelBindingResult.Success(null);
            return Task.CompletedTask;
        }

        bindingContext.Result = ModelBindingResult.Success(new CursorParameter
        {
            Cols = list,
        });

        return Task.CompletedTask;
    }
}
