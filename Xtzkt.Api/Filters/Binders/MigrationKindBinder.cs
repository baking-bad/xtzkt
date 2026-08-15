using Microsoft.AspNetCore.Mvc.ModelBinding;
using Xtzkt.Api.Extensions;
using Xtzkt.Api.Filters.Parameters;
using Xtzkt.Api.Models.Enums;

namespace Xtzkt.Api.Filters.Binders;

public class MigrationKindBinder : IModelBinder
{
    public Task BindModelAsync(ModelBindingContext bindingContext)
    {
        var param = bindingContext.ModelName;
        var hasValue = false;

        if (!bindingContext.TryGetEnum(MigrationKinds.Mapping, $"{param}", ref hasValue, out var value))
            return Task.CompletedTask;

        if (!bindingContext.TryGetEnum(MigrationKinds.Mapping, $"{param}.eq", ref hasValue, out var eq))
            return Task.CompletedTask;

        if (!bindingContext.TryGetEnum(MigrationKinds.Mapping, $"{param}.ne", ref hasValue, out var ne))
            return Task.CompletedTask;

        if (!bindingContext.TryGetEnumList(MigrationKinds.Mapping, $"{param}.in", ref hasValue, out var @in))
            return Task.CompletedTask;

        if (!bindingContext.TryGetEnumList(MigrationKinds.Mapping, $"{param}.ni", ref hasValue, out var ni))
            return Task.CompletedTask;

        bindingContext.Result = ModelBindingResult.Success(!hasValue ? null : new MigrationKindParameter
        {
            Eq = value ?? eq,
            Ne = ne,
            In = @in,
            Ni = ni
        });

        return Task.CompletedTask;
    }
}
