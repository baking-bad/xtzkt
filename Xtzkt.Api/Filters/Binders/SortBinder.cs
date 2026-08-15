using Microsoft.AspNetCore.Mvc.ModelBinding;
using Xtzkt.Api.Extensions;
using Xtzkt.Api.Filters.Parameters;

namespace Xtzkt.Api.Filters.Binders;

public class SortBinder : IModelBinder
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

        bindingContext.Result = ModelBindingResult.Success(new SortParameter
        {
            Cols = [..list!.Select(x => x.EndsWith(".desc") ? (x[..^5], false) : x.EndsWith(".asc") ? (x[..^4], true) : (x, true))],
        });

        return Task.CompletedTask;
    }
}
