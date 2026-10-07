using EasyVetClinic.Api.Data;
using Microsoft.AspNetCore.Mvc.Filters;

namespace EasyVetClinic.Api.Authentication;

[AttributeUsage(AttributeTargets.Class)]
public sealed class RequireClinicAccessAttribute : Attribute, IAuthorizationFilter
{
    public void OnAuthorization(AuthorizationFilterContext context)
    {
        // Resolve access before EF evaluates controller query parameters.
        _ = context.HttpContext.RequestServices.GetRequiredService<CurrentClinic>().Id;
    }
}
