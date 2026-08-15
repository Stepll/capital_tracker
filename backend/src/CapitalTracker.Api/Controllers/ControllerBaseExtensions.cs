using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;

namespace CapitalTracker.Api.Controllers;

public static class ControllerBaseExtensions
{
    /// <summary>Id of the authenticated user, taken from the JWT "sub" claim.</summary>
    public static Guid GetUserId(this ControllerBase controller) =>
        Guid.Parse(controller.User.FindFirstValue(JwtRegisteredClaimNames.Sub)!);
}
