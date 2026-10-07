using Microsoft.AspNetCore.Mvc.ModelBinding;
using Xtzkt.Api.Extensions;
using Xtzkt.Api.Filters.Parameters;

namespace Xtzkt.Api.Filters.Binders;

public class AddressInfoEqBinder(IModelMetadataProvider _metadata, IModelBinderFactory _factory) : IModelBinder
{
    public async Task BindModelAsync(ModelBindingContext bindingContext)
    {
        var param = bindingContext.ModelName;

        var id = await bindingContext.BindChild<Int32EqInParameter>(_metadata, _factory, $"{param}.{nameof(AddressInfoEqParameter.Id)}");
        var hash = await bindingContext.BindChild<AddressHashEqParameter>(_metadata, _factory, $"{param}.{nameof(AddressInfoEqParameter.Hash)}");

        id ??= await bindingContext.BindChild<Int32EqInParameter>(_metadata, _factory, param);

        if (bindingContext.ModelState.ErrorCount != 0)
            return;

        bindingContext.Result = ModelBindingResult.Success(id == null && hash == null ? null : new AddressInfoEqParameter { Id = id, Hash = hash });
    }
}
