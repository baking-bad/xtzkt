using Microsoft.AspNetCore.Mvc.Filters;
using Xtzkt.Api.Responses;

namespace Xtzkt.Api.Exceptions;

public class BadRequestExceptionFilter : IExceptionFilter
{
    public void OnException(ExceptionContext context)
    {
        if (context.Exception is BadRequestException ex)
        {
            context.Result = new BadRequest(ex.Field, ex.Message);
            context.ExceptionHandled = true;
        }
    }
}
