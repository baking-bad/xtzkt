using Microsoft.AspNetCore.Mvc.ModelBinding;
using Xtzkt.Api.Extensions;
using Xtzkt.Api.Filters.Parameters;

namespace Xtzkt.Api.Filters.Binders;

public class TicketInfoBinder(IModelMetadataProvider _metadata, IModelBinderFactory _factory) : IModelBinder
{
    public async Task BindModelAsync(ModelBindingContext bindingContext)
    {
        var param = bindingContext.ModelName;

        var id = await bindingContext.BindChild<Int64Parameter>(_metadata, _factory, $"{param}.{nameof(TicketInfoParameter.Id)}");
        var ticketer = await bindingContext.BindChild<AddressInfoParameter>(_metadata, _factory, $"{param}.{nameof(TicketInfoParameter.Ticketer)}");
        var rawType = await bindingContext.BindChild<MichelineParameter>(_metadata, _factory, $"{param}.{nameof(TicketInfoParameter.RawType)}");
        var rawContent = await bindingContext.BindChild<MichelineParameter>(_metadata, _factory, $"{param}.{nameof(TicketInfoParameter.RawContent)}");
        var content = await bindingContext.BindChild<JsonParameter>(_metadata, _factory, $"{param}.{nameof(TicketInfoParameter.Content)}");
        var typeHash = await bindingContext.BindChild<Int32Parameter>(_metadata, _factory, $"{param}.{nameof(TicketInfoParameter.TypeHash)}");
        var contentHash = await bindingContext.BindChild<Int32Parameter>(_metadata, _factory, $"{param}.{nameof(TicketInfoParameter.ContentHash)}");

        id ??= await bindingContext.BindChild<Int64Parameter>(_metadata, _factory, param);

        if (bindingContext.ModelState.ErrorCount != 0)
            return;

        bindingContext.Result = ModelBindingResult.Success(
            id == null && ticketer == null && rawType == null && rawContent == null && content == null && typeHash == null && contentHash == null
                ? null
                : new TicketInfoParameter
                {
                    Id = id,
                    Ticketer = ticketer,
                    RawType = rawType,
                    RawContent = rawContent,
                    Content = content,
                    TypeHash = typeHash,
                    ContentHash = contentHash,
                });
    }
}
