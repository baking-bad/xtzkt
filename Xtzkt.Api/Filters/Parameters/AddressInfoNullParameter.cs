using Xtzkt.Api.Filters.Base;
using Xtzkt.Api.Filters.Binders;
using Xtzkt.Api.Services.ResponseCache;
using Microsoft.AspNetCore.Mvc;

namespace Xtzkt.Api.Filters.Parameters
{
    [ModelBinder(BinderType = typeof(AddressInfoNullBinder))]
    public class AddressInfoNullParameter : INormalizable
    {
        /// <summary>
        /// Filters by internal unique address id (default).
        /// Click on the parameter to expand more details.
        /// </summary>
        public Int32NullParameter? Id { get; set; }

        /// <summary>
        /// Filters by address hash.
        /// Click on the parameter to expand more details.
        /// </summary>
        public AddressHashNullParameter? Hash { get; set; }

        public virtual bool IsEmpty() =>
            Id == null &&
            Hash == null;

        public virtual string Normalize(string name) => ResponseCacheService.BuildKey("",
            ($"{name}.id", Id),
            ($"{name}.hash", Hash));
    }
}
