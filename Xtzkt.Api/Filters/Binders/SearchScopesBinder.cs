using Microsoft.AspNetCore.Mvc.ModelBinding;
using Xtzkt.Api.Extensions;
using Xtzkt.Api.Filters.Parameters;
using Xtzkt.Api.Models.Enums;

namespace Xtzkt.Api.Filters.Binders;

public class SearchScopesBinder : IModelBinder
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

        var scopes = new HashSet<string>();
        foreach (var scope in value!)
        {
            if (!SearchScopes.IsValid(scope))
            {
                bindingContext.ModelState.TryAddModelError(bindingContext.ModelName, $"Invalid scope '{scope}'.");
                return Task.CompletedTask;
            }
            scopes.Add(scope);
        }

        bindingContext.Result = ModelBindingResult.Success(new SearchScopesParameter { Scopes = scopes });
        return Task.CompletedTask;
    }
}
