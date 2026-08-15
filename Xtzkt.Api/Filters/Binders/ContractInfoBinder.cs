using Microsoft.AspNetCore.Mvc.ModelBinding;
using Xtzkt.Api.Extensions;
using Xtzkt.Api.Filters.Parameters;

namespace Xtzkt.Api.Filters.Binders;

public class ContractInfoBinder(IModelMetadataProvider _metadata, IModelBinderFactory _factory) : IModelBinder
{
    public async Task BindModelAsync(ModelBindingContext bindingContext)
    {
        var param = bindingContext.ModelName;

        var id = await bindingContext.BindChild<Int32Parameter>(_metadata, _factory, $"{param}.{nameof(ContractInfoParameter.Id)}");
        var hash = await bindingContext.BindChild<AddressHashParameter>(_metadata, _factory, $"{param}.{nameof(ContractInfoParameter.Hash)}");
        var typeHash = await bindingContext.BindChild<Int32Parameter>(_metadata, _factory, $"{param}.{nameof(ContractInfoParameter.TypeHash)}");
        var codeHash = await bindingContext.BindChild<Int32Parameter>(_metadata, _factory, $"{param}.{nameof(ContractInfoParameter.CodeHash)}");
        var creator = await bindingContext.BindChild<AddressInfoParameter>(_metadata, _factory, $"{param}.{nameof(ContractInfoParameter.Creator)}");

        id ??= await bindingContext.BindChild<Int32Parameter>(_metadata, _factory, param);

        if (bindingContext.ModelState.ErrorCount != 0)
            return;

        bindingContext.Result = ModelBindingResult.Success(
            id == null && hash == null && typeHash == null && codeHash == null && creator == null
                ? null
                : new ContractInfoParameter { Id = id, Hash = hash, TypeHash = typeHash, CodeHash = codeHash, Creator = creator });
    }
}
