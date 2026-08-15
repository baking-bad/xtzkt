using Microsoft.AspNetCore.Mvc.ModelBinding;
using Xtzkt.Api.Extensions;
using Xtzkt.Api.Filters.Parameters;
using Xtzkt.Api.Models.Enums;

namespace Xtzkt.Api.Filters.Binders;

public class BigMapTagsBinder : IModelBinder
{
    public Task BindModelAsync(ModelBindingContext bindingContext)
    {
        var param = bindingContext.ModelName;
        var hasValue = false;

        if (!bindingContext.TryGetEnum(BigMapTags.Mapping, $"{param}", ref hasValue, out var value))
            return Task.CompletedTask;

        if (!bindingContext.TryGetEnum(BigMapTags.Mapping, $"{param}.eq", ref hasValue, out var eq))
            return Task.CompletedTask;

        if (!bindingContext.TryGetEnum(BigMapTags.Mapping, $"{param}.ne", ref hasValue, out var ne))
            return Task.CompletedTask;

        if (!bindingContext.TryGetEnumList(BigMapTags.Mapping, $"{param}.any", ref hasValue, out var any))
            return Task.CompletedTask;

        if (!bindingContext.TryGetEnumList(BigMapTags.Mapping, $"{param}.all", ref hasValue, out var all))
            return Task.CompletedTask;

        bindingContext.Result = ModelBindingResult.Success(!hasValue ? null : new BigMapTagsParameter
        {
            Eq = value ?? eq,
            Ne = ne,
            Any = any?.Aggregate(0, (res, x) => res | x),
            All = all?.Aggregate(0, (res, x) => res | x),
        });

        return Task.CompletedTask;
    }
}
