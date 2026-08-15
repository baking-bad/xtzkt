using Microsoft.AspNetCore.Mvc.ModelBinding;
using Xtzkt.Api.Extensions;
using Xtzkt.Api.Filters.Parameters;

namespace Xtzkt.Api.Filters.Binders;

public class ChainIdBinder : IModelBinder
{
    public Task BindModelAsync(ModelBindingContext bindingContext)
    {
        var param = bindingContext.ModelName;
        var hasValue = false;

        if (!bindingContext.TryGetHexOrBase58($"{param}", ref hasValue, out var value, "Net", 15))
            return Task.CompletedTask;

        if (!bindingContext.TryGetHexOrBase58($"{param}.eq", ref hasValue, out var eq, "Net", 15))
            return Task.CompletedTask;

        if (!bindingContext.TryGetHexOrBase58($"{param}.ne", ref hasValue, out var ne, "Net", 15))
            return Task.CompletedTask;

        if (!bindingContext.TryGetHexOrBase58List($"{param}.in", ref hasValue, out var @in, "Net", 15))
            return Task.CompletedTask;

        if (!bindingContext.TryGetHexOrBase58List($"{param}.ni", ref hasValue, out var ni, "Net", 15))
            return Task.CompletedTask;

        var _eq = value ?? eq;
        if (_eq != null)
        {
            if (ne != null)
            {
                if (ne == _eq)
                {
                    bindingContext.ModelState.TryAddModelError($"{param}.ne", "Conflicts with `.eq`.");
                    return Task.CompletedTask;
                }
                ne = null;
            }

            if (@in != null)
            {
                if (!@in.Contains(_eq))
                {
                    bindingContext.ModelState.TryAddModelError($"{param}.in", "Conflicts with `.eq`.");
                    return Task.CompletedTask;
                }
                @in = null;
            }

            if (ni != null)
            {
                if (ni.Contains(_eq))
                {
                    bindingContext.ModelState.TryAddModelError($"{param}.ni", "Conflicts with `.eq`.");
                    return Task.CompletedTask;
                }
                ni = null;
            }
        }

        if (ne != null)
        {
            if (@in != null)
            {
                if (@in.Contains(ne))
                {
                    bindingContext.ModelState.TryAddModelError($"{param}.in", "Conflicts with `.ne`.");
                    return Task.CompletedTask;
                }
                ne = null;
            }
            else if (ni != null)
            {
                if (!ni.Contains(ne))
                    ni.Add(ne);
                ne = null;
            }
        }

        if (@in != null)
        {
            if (ni != null)
            {
                if (@in.Any(x => ni.Contains(x)))
                {
                    bindingContext.ModelState.TryAddModelError($"{param}.ni", "Conflicts with `.in`.");
                    return Task.CompletedTask;
                }
                ni = null;
            }

            if (@in.Count == 1)
            {
                _eq = @in[0];
                @in = null;
            }
        }

        if (ni != null)
        {
            if (ni.Count == 1)
            {
                ne = ni[0];
                ni = null;
            }
        }

        bindingContext.Result = ModelBindingResult.Success(!hasValue ? null : new ChainIdParameter
        {
            Eq = _eq,
            Ne = ne,
            In = @in,
            Ni = ni
        });

        return Task.CompletedTask;
    }
}
