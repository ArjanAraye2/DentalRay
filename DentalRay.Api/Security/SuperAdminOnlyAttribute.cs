using DentalRay.Api.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace DentalRay.Api.Security
{
    /// <summary>
    /// Restricts administrative lookup maintenance endpoints to DentalRay SuperAdmin.
    /// Authentication is already required globally; this filter verifies the SuperAdmin claim.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
    public sealed class SuperAdminOnlyAttribute : Attribute, IAuthorizationFilter
    {
        public void OnAuthorization(AuthorizationFilterContext context)
        {
            if (!StudyAccessService.IsSuperAdmin(context.HttpContext.User))
            {
                context.Result = new ObjectResult(new
                {
                    success = false,
                    message = "این عملیات فقط برای مدیر سیستم مجاز است.",
                    messageEn = "This operation is restricted to SuperAdmin."
                })
                {
                    StatusCode = StatusCodes.Status403Forbidden
                };
            }
        }
    }
}
