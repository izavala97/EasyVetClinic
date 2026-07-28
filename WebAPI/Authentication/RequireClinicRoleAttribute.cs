using EasyVetClinic.Api.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace EasyVetClinic.Api.Authentication;

[AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
public sealed class RequireClinicRoleAttribute(params string[] roles) : Attribute, IAsyncAuthorizationFilter
{
    public Task OnAuthorizationAsync(AuthorizationFilterContext context)
    {
        var currentClinic = context.HttpContext.RequestServices.GetRequiredService<CurrentClinic>();
        if (!roles.Contains(currentClinic.Role, StringComparer.Ordinal))
        {
            context.Result = new ForbidResult();
        }

        return Task.CompletedTask;
    }
}