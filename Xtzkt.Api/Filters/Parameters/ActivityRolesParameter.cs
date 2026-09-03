using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Mvc;
using Xtzkt.Api.Filters.Base;
using Xtzkt.Api.Filters.Binders;
using Xtzkt.Api.Models.Abstract;

namespace Xtzkt.Api.Filters.Parameters;

[ModelBinder(BinderType = typeof(ActivityRolesBinder))]
public class ActivityRolesParameter : INormalizable
{
    [JsonIgnore]
    public ActivityRole? Roles { get; set; }

    public string Normalize(string name)
    {
        return Roles != null ? $"{name}={Roles}&" : string.Empty;
    }
}
