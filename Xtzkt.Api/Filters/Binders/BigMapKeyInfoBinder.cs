using Microsoft.AspNetCore.Mvc.ModelBinding;
using Xtzkt.Api.Extensions;
using Xtzkt.Api.Filters.Parameters;

namespace Xtzkt.Api.Filters.Binders;

public class BigMapKeyInfoBinder(IModelMetadataProvider _metadata, IModelBinderFactory _factory) : IModelBinder
{
    public async Task BindModelAsync(ModelBindingContext bindingContext)
    {
        var param = bindingContext.ModelName;

        var id = await bindingContext.BindChild<Int64NullParameter>(_metadata, _factory, $"{param}.{nameof(BigMapKeyInfoParameter.Id)}");
        var keyHash = await bindingContext.BindChild<ExpressionBytesParameter>(_metadata, _factory, $"{param}.{nameof(BigMapKeyInfoParameter.KeyHash)}");
        var rawKey = await bindingContext.BindChild<MichelineParameter>(_metadata, _factory, $"{param}.{nameof(BigMapKeyInfoParameter.RawKey)}");
        var key = await bindingContext.BindChild<JsonParameter>(_metadata, _factory, $"{param}.{nameof(BigMapKeyInfoParameter.Key)}");

        id ??= await bindingContext.BindChild<Int64NullParameter>(_metadata, _factory, param);

        if (bindingContext.ModelState.ErrorCount != 0)
            return;

        bindingContext.Result = ModelBindingResult.Success(
            id == null && keyHash == null && rawKey == null && key == null
                ? null
                : new BigMapKeyInfoParameter { Id = id, KeyHash = keyHash, RawKey = rawKey, Key = key });
    }
}
