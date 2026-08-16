using Microsoft.AspNetCore.Mvc.Filters;
using Npgsql;
using Xtzkt.Api.Responses;

namespace Xtzkt.Api.Exceptions;

public class TimeoutExceptionFilter(ILogger<TimeoutExceptionFilter> logger) : IExceptionFilter
{
    public void OnException(ExceptionContext context)
    {
        if (context.Exception is not (
            PostgresException { SqlState: PostgresErrorCodes.QueryCanceled } or
            NpgsqlException { InnerException: TimeoutException } or
            TimeoutException))
            return;

        logger.LogWarning("Query timed out: {path}{query}",
            context.HttpContext.Request.Path,
            context.HttpContext.Request.QueryString);

        context.Result = new GatewayTimeout();
        context.ExceptionHandled = true;
    }
}
