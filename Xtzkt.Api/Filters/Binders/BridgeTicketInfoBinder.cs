using Microsoft.AspNetCore.Mvc.ModelBinding;
using Xtzkt.Api.Extensions;
using Xtzkt.Api.Filters.Parameters;

namespace Xtzkt.Api.Filters.Binders;

public class BridgeTicketInfoBinder(IModelMetadataProvider _metadata, IModelBinderFactory _factory) : IModelBinder
{
    public async Task BindModelAsync(ModelBindingContext bindingContext)
    {
        var param = bindingContext.ModelName;

        var id = await bindingContext.BindChild<Int64Parameter>(_metadata, _factory, $"{param}.{nameof(BridgeTicketInfoParameter.Id)}");
        var weakHash = await bindingContext.BindChild<HexBytesParameter>(_metadata, _factory, $"{param}.{nameof(BridgeTicketInfoParameter.WeakHash)}");

        id ??= await bindingContext.BindChild<Int64Parameter>(_metadata, _factory, param);

        if (bindingContext.ModelState.ErrorCount != 0)
            return;

        bindingContext.Result = ModelBindingResult.Success(
            id == null && weakHash == null
                ? null
                : new BridgeTicketInfoParameter
                {
                    Id = id,
                    WeakHash = weakHash,
                });
    }
}
