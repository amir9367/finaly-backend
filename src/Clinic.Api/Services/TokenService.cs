using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Clinic.Api.Domain;
using Microsoft.IdentityModel.Tokens;

namespace Clinic.Api.Services;

public interface ITokenService
{
    string CreateToken(AdminUser user);
}

public class TokenService(IConfiguration config) : ITokenService
{
    public string CreateToken(AdminUser user)
    {
        var secret = config["Jwt:Secret"];
        if (string.IsNullOrWhiteSpace(secret) || secret.Length < 32)
            throw new InvalidOperationException("Jwt:Secret is missing or shorter than 32 characters.");

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secret));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
        // Short-lived tokens: there is no revocation mechanism, so a stolen
        // token must age out quickly. Default 2 h.
        var expireHours = double.TryParse(config["Jwt:ExpireHours"], out var h) && h > 0 ? h : 2;

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, user.Username),
            new(ClaimTypes.Name, user.Username),
            new(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new(ClaimTypes.Role, "admin"),
        };

        var token = new JwtSecurityToken(
            issuer: config["Jwt:Issuer"],
            audience: config["Jwt:Audience"],
            claims: claims,
            expires: DateTime.UtcNow.AddHours(expireHours),
            signingCredentials: credentials);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
