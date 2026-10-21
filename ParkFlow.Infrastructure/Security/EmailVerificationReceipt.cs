using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using ParkFlow.Application.Interfaces;

namespace ParkFlow.Infrastructure.Security;

public class EmailVerificationReceipt(IConfiguration configuration) : IEmailVerificationReceipt
{
    private const string Audience = "parkflow:email-verification";
    private SymmetricSecurityKey Key => new(Encoding.UTF8.GetBytes(configuration["Jwt:Key"] ?? throw new InvalidOperationException("JWT key is missing.")));
    public string Issue(string email) => new JwtSecurityTokenHandler().WriteToken(new JwtSecurityToken(
        issuer: configuration["Jwt:Issuer"], audience: Audience,
        claims: [new Claim(JwtRegisteredClaimNames.Email, email.Trim().ToLowerInvariant()), new Claim("purpose", "email_verification")],
        notBefore: DateTime.UtcNow, expires: DateTime.UtcNow.AddMinutes(10), signingCredentials: new SigningCredentials(Key, SecurityAlgorithms.HmacSha256)));

    public bool Validate(string email, string token)
    {
        try
        {
            new JwtSecurityTokenHandler().ValidateToken(token, new TokenValidationParameters
            {
                ValidateIssuer = true, ValidIssuer = configuration["Jwt:Issuer"], ValidateAudience = true, ValidAudience = Audience,
                ValidateLifetime = true, RequireExpirationTime = true, ClockSkew = TimeSpan.Zero,
                ValidateIssuerSigningKey = true, IssuerSigningKey = Key, ValidAlgorithms = [SecurityAlgorithms.HmacSha256]
            }, out var validated);
            var jwt = (JwtSecurityToken)validated;
            return jwt.Claims.Any(c => c.Type == "purpose" && c.Value == "email_verification") &&
                jwt.Claims.Any(c => c.Type == JwtRegisteredClaimNames.Email && c.Value.Equals(email.Trim(), StringComparison.OrdinalIgnoreCase));
        }
        catch { return false; }
    }
}
