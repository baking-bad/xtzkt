using Microsoft.AspNetCore.Mvc;

namespace Xtzkt.Api.Responses;

public class GatewayTimeout : ObjectResult
{
    public GatewayTimeout() : base(new
    {
        Code = 504,
        Errors = new Dictionary<string, string>
        {
            { "query", "The query took too long to complete. Narrow the filters down and avoid offset pagination." }
        }
    })
    {
        StatusCode = 504;
    }
}
