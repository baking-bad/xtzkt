using Microsoft.AspNetCore.Mvc.ModelBinding;
using Xtzkt.Api.Extensions;
using Xtzkt.Api.Filters.Parameters;

namespace Xtzkt.Api.Filters.Binders;

public class AddressInfoBinder(IModelMetadataProvider _metadata, IModelBinderFactory _factory) : IModelBinder
{
    public async Task BindModelAsync(ModelBindingContext bindingContext)
    {
        var param = bindingContext.ModelName;

        var id = await bindingContext.BindChild<Int32Parameter>(_metadata, _factory, $"{param}.{nameof(AddressInfoParameter.Id)}");
        var hash = await bindingContext.BindChild<AddressHashParameter>(_metadata, _factory, $"{param}.{nameof(AddressInfoParameter.Hash)}");
        
        id ??= await bindingContext.BindChild<Int32Parameter>(_metadata, _factory, param);

        if (bindingContext.ModelState.ErrorCount != 0)
            return;

        bindingContext.Result = ModelBindingResult.Success(id == null && hash == null ? null : new AddressInfoParameter { Id = id, Hash = hash });
    }
}
