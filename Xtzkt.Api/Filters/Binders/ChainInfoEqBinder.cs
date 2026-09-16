using Microsoft.AspNetCore.Mvc.ModelBinding;
using Xtzkt.Api.Extensions;
using Xtzkt.Api.Filters.Parameters;

namespace Xtzkt.Api.Filters.Binders;

public class ChainInfoEqBinder(IModelMetadataProvider _metadata, IModelBinderFactory _factory) : IModelBinder
{
    public async Task BindModelAsync(ModelBindingContext bindingContext)
    {
        var param = bindingContext.ModelName;

        var id = await bindingContext.BindChild<Int32EqParameter>(_metadata, _factory, $"{param}.{nameof(ChainInfoEqParameter.Id)}");
        var chainId = await bindingContext.BindChild<ChainIdEqParameter>(_metadata, _factory, $"{param}.{nameof(ChainInfoEqParameter.ChainId)}");

        id ??= await bindingContext.BindChild<Int32EqParameter>(_metadata, _factory, param);

        if (bindingContext.ModelState.ErrorCount != 0)
            return;

        bindingContext.Result = ModelBindingResult.Success(id == null && chainId == null ? null : new ChainInfoEqParameter { Id = id, ChainId = chainId });
    }
}
