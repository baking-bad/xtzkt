using Microsoft.AspNetCore.Mvc.ModelBinding;
using Xtzkt.Api.Extensions;
using Xtzkt.Api.Filters.Parameters;

namespace Xtzkt.Api.Filters.Binders;

public class TokenInfoBinder(IModelMetadataProvider _metadata, IModelBinderFactory _factory) : IModelBinder
{
    public async Task BindModelAsync(ModelBindingContext bindingContext)
    {
        var param = bindingContext.ModelName;

        var id = await bindingContext.BindChild<Int64Parameter>(_metadata, _factory, $"{param}.{nameof(TokenInfoParameter.Id)}");
        var contract = await bindingContext.BindChild<AddressInfoParameter>(_metadata, _factory, $"{param}.{nameof(TokenInfoParameter.Contract)}");
        var tokenId = await bindingContext.BindChild<BigIntegerParameter>(_metadata, _factory, $"{param}.{nameof(TokenInfoParameter.TokenId)}");
        var standard = await bindingContext.BindChild<TokenStandardParameter>(_metadata, _factory, $"{param}.{nameof(TokenInfoParameter.Standard)}");
        var metadata = await bindingContext.BindChild<JsonParameter>(_metadata, _factory, $"{param}.{nameof(TokenInfoParameter.Metadata)}");

        id ??= await bindingContext.BindChild<Int64Parameter>(_metadata, _factory, param);

        if (bindingContext.ModelState.ErrorCount != 0)
            return;

        bindingContext.Result = ModelBindingResult.Success(
            id == null && contract == null && tokenId == null && standard == null && metadata == null
                ? null
                : new TokenInfoParameter { Id = id, Contract = contract, TokenId = tokenId, Standard = standard, Metadata = metadata });
    }
}
