using Microsoft.AspNetCore.Mvc.ModelBinding;
using Xtzkt.Api.Extensions;
using Xtzkt.Api.Filters.Parameters;

namespace Xtzkt.Api.Filters.Binders;

public class BigMapInfoBinder(IModelMetadataProvider _metadata, IModelBinderFactory _factory) : IModelBinder
{
    public async Task BindModelAsync(ModelBindingContext bindingContext)
    {
        var param = bindingContext.ModelName;

        var id = await bindingContext.BindChild<Int32Parameter>(_metadata, _factory, $"{param}.{nameof(BigMapInfoParameter.Id)}");
        var ptr = await bindingContext.BindChild<Int32Parameter>(_metadata, _factory, $"{param}.{nameof(BigMapInfoParameter.Ptr)}");
        var contract = await bindingContext.BindChild<ContractInfoParameter>(_metadata, _factory, $"{param}.{nameof(BigMapInfoParameter.Contract)}");
        var storagePath = await bindingContext.BindChild<StringParameter>(_metadata, _factory, $"{param}.{nameof(BigMapInfoParameter.StoragePath)}");

        id ??= await bindingContext.BindChild<Int32Parameter>(_metadata, _factory, param);

        if (bindingContext.ModelState.ErrorCount != 0)
            return;

        bindingContext.Result = ModelBindingResult.Success(
            id == null && ptr == null && contract == null && storagePath == null
                ? null
                : new BigMapInfoParameter { Id = id, Ptr = ptr, Contract = contract, StoragePath = storagePath });
    }
}
