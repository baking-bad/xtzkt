using Microsoft.AspNetCore.Mvc.ModelBinding;
using Xtzkt.Api.Extensions;
using Xtzkt.Api.Filters.Parameters;
using Xtzkt.Api.Models.Abstract;
using Xtzkt.Api.Models.Enums;

namespace Xtzkt.Api.Filters.Binders;

public class ActivityRolesBinder : IModelBinder
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

        var roles = ActivityRole.None;
        foreach (var role in value!)
        {
            switch (role)
            {
                case ActivityRoles.Sender:
                    roles |= ActivityRole.Sender;
                    break;
                case ActivityRoles.Target:
                    roles |= ActivityRole.Target;
                    break;
                case ActivityRoles.Initiator:
                    roles |= ActivityRole.Initiator;
                    break;
                case ActivityRoles.Mention:
                    roles |= ActivityRole.Mention;
                    break;
                case ActivityRoles.All:
                    roles |= ActivityRole.All;
                    break;
                default:
                    bindingContext.ModelState.TryAddModelError(bindingContext.ModelName, $"Invalid role '{role}'.");
                    return Task.CompletedTask;
            }
        }

        bindingContext.Result = ModelBindingResult.Success(new ActivityRolesParameter { Roles = roles });
        return Task.CompletedTask;
    }
}
