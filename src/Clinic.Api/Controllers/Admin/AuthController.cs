using Clinic.Api.Common;
using Clinic.Api.Data;
using Clinic.Api.Dtos;
using Clinic.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;

namespace Clinic.Api.Controllers.Admin;

[ApiController]
[Route("api/admin/auth")]
public class AuthController(AppDbContext db, ITokenService tokens) : ControllerBase
{
    // Generated once, then reused: verifying against this when the username is
    // unknown equalizes the ~100 ms BCrypt cost so response timing cannot
    // reveal which usernames exist.
    private static readonly Lazy<string> DummyHash =
        new(() => BCrypt.Net.BCrypt.HashPassword(Guid.NewGuid().ToString("N"), workFactor: 11));

    [AllowAnonymous]
    [EnableRateLimiting("auth-login")]
    [HttpPost("login")]
    public async Task<ActionResult<LoginResponse>> Login(LoginRequest request, CancellationToken ct)
    {
        var user = await db.AdminUsers.FirstOrDefaultAsync(u => u.Username == request.Username, ct);
        var passwordHash = user?.PasswordHash ?? DummyHash.Value;
        var valid = BCrypt.Net.BCrypt.Verify(request.Password, passwordHash);

        if (user is null || !valid)
            throw new UnauthorizedException("Invalid username or password.");

        return new LoginResponse(tokens.CreateToken(user));
    }
}
