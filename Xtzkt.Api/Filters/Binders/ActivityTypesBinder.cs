using Microsoft.AspNetCore.Mvc.ModelBinding;
using Xtzkt.Api.Extensions;
using Xtzkt.Api.Filters.Parameters;
using Xtzkt.Api.Models.Enums;

namespace Xtzkt.Api.Filters.Binders;

public class ActivityTypesBinder : IModelBinder
{
    public Task BindModelAsync(ModelBindingContext bindingContext)
    {
        var hasValue = false;

        if (!bindingContext.TryGetStringList(bindingContext.ModelName, ref hasValue, out var value))
            return Task.CompletedTask;

        if (!hasValue)
        {
            bindingContext.Result = ModelBindingResult.Success(null);
            return Task.CompletedTask;
        }

        var types = new HashSet<string>();
        foreach (var type in value!)
        {
            if (!ActivityTypes.IsValid(type))
            {
                bindingContext.ModelState.TryAddModelError(bindingContext.ModelName, $"Invalid type '{type}'.");
                return Task.CompletedTask;
            }
            types.Add(type);
        }

        bindingContext.Result = ModelBindingResult.Success(new ActivityTypesParameter { Types = types });
        return Task.CompletedTask;
    }
}
