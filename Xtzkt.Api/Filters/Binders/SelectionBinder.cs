using Microsoft.AspNetCore.Mvc.ModelBinding;
using Xtzkt.Api.Extensions;
using Xtzkt.Api.Filters.Parameters;

namespace Xtzkt.Api.Filters.Binders;

public class SelectionBinder : IModelBinder
{
    public Task BindModelAsync(ModelBindingContext bindingContext)
    {
        var param = bindingContext.ModelName;
        var hasValue = false;

        if (!bindingContext.TryGetSelectionFields($"{param}", ref hasValue, out var value))
            return Task.CompletedTask;

        if (!bindingContext.TryGetSelectionFields($"{param}.fields", ref hasValue, out var fields))
            return Task.CompletedTask;

        if (!bindingContext.TryGetSelectionFields($"{param}.values", ref hasValue, out var values))
            return Task.CompletedTask;

        bindingContext.Result = ModelBindingResult.Success(!hasValue ? null : new SelectionParameter
        {
            Fields = value ?? fields,
            Values = values
        });

        return Task.CompletedTask;
    }
}
