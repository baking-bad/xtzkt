using Microsoft.AspNetCore.Mvc.ModelBinding;
using Xtzkt.Api.Extensions;
using Xtzkt.Api.Filters.Parameters;

namespace Xtzkt.Api.Filters.Binders;

public class AddressInfoNullBinder(IModelMetadataProvider _metadata, IModelBinderFactory _factory) : IModelBinder
{
    public async Task BindModelAsync(ModelBindingContext bindingContext)
    {
        var param = bindingContext.ModelName;

        var id = await bindingContext.BindChild<Int32NullParameter>(_metadata, _factory, $"{param}.{nameof(AddressInfoNullParameter.Id)}");
        var hash = await bindingContext.BindChild<AddressHashNullParameter>(_metadata, _factory, $"{param}.{nameof(AddressInfoNullParameter.Hash)}");
        
        id ??= await bindingContext.BindChild<Int32NullParameter>(_metadata, _factory, param);

        if (bindingContext.ModelState.ErrorCount != 0)
            return;

        bindingContext.Result = ModelBindingResult.Success(id == null && hash == null ? null : new AddressInfoNullParameter { Id = id, Hash = hash });
    }
}
