using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using OpenStickyMemos.Api.Data;
using OpenStickyMemos.Api.Models;

namespace OpenStickyMemos.Api.Services;

public interface IJwtService
{
    (string token, DateTime expiresAt) GenerateAccessToken(User user);
    string GenerateRefreshToken();
    ClaimsPrincipal? ValidateToken(string token);
}

public class JwtService : IJwtService
{
    private readonly IConfiguration _configuration;

    public JwtService(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    public (string token, DateTime expiresAt) GenerateAccessToken(User user)
    {
        var jwtSection = _configuration.GetSection("Jwt");
        var key = new SymmetricSecurityKey(
            Encoding.UTF8.GetBytes(jwtSection["Key"]!));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var expiresAt = DateTime.UtcNow.AddMinutes(
            double.Parse(jwtSection["ExpiresInMinutes"] ?? "60"));

        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new Claim(JwtRegisteredClaimNames.Email, user.Email),
            new Claim(JwtRegisteredClaimNames.Name, user.DisplayName),
            new Claim("avatar_url", user.AvatarUrl ?? ""),
            new Claim("auth_provider", user.AuthProvider),
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
        };

        var token = new JwtSecurityToken(
            issuer: jwtSection["Issuer"],
            audience: jwtSection["Audience"],
            claims: claims,
            expires: expiresAt,
            signingCredentials: credentials
        );

        return (new JwtSecurityTokenHandler().WriteToken(token), expiresAt);
    }

    public string GenerateRefreshToken()
    {
        var randomBytes = new byte[64];
        using var rng = RandomNumberGenerator.Create();
        rng.GetBytes(randomBytes);
        return Convert.ToBase64String(randomBytes);
    }

    public ClaimsPrincipal? ValidateToken(string token)
    {
        var jwtSection = _configuration.GetSection("Jwt");
        var key = new SymmetricSecurityKey(
            Encoding.UTF8.GetBytes(jwtSection["Key"]!));

        try
        {
            var handler = new JwtSecurityTokenHandler();
            return handler.ValidateToken(token, new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidateAudience = true,
                ValidateLifetime = true,
                ValidateIssuerSigningKey = true,
                ValidIssuer = jwtSection["Issuer"],
                ValidAudience = jwtSection["Audience"],
                IssuerSigningKey = key
            }, out _);
        }
        catch
        {
            return null;
        }
    }
}

/// <summary>
/// Servicio para gestionar el almacenamiento y validación de refresh tokens en BD.
/// </summary>
public interface IRefreshTokenService
{
    Task<RefreshToken> CreateRefreshToken(User user);
    Task<RefreshToken?> ValidateAndRotate(string currentToken, Guid userId);
    Task RevokeAllUserTokens(Guid userId);
}

public class RefreshTokenService : IRefreshTokenService
{
    private readonly AppDbContext _db;
    private readonly IJwtService _jwt;
    private readonly IConfiguration _configuration;

    public RefreshTokenService(AppDbContext db, IJwtService jwt, IConfiguration configuration)
    {
        _db = db;
        _jwt = jwt;
        _configuration = configuration;
    }

    public async Task<RefreshToken> CreateRefreshToken(User user)
    {
        var expiresInDays = double.Parse(
            _configuration.GetSection("Jwt")["RefreshExpiresInDays"] ?? "7");

        var refreshToken = new RefreshToken
        {
            Token = _jwt.GenerateRefreshToken(),
            UserId = user.Id,
            ExpiresAt = DateTime.UtcNow.AddDays(expiresInDays)
        };

        _db.RefreshTokens.Add(refreshToken);
        await _db.SaveChangesAsync();

        return refreshToken;
    }

    public async Task<RefreshToken?> ValidateAndRotate(string currentToken, Guid userId)
    {
        // Buscar el token activo y no expirado
        var storedToken = await _db.RefreshTokens
            .FirstOrDefaultAsync(rt =>
                rt.Token == currentToken &&
                rt.UserId == userId &&
                !rt.IsRevoked &&
                rt.ExpiresAt > DateTime.UtcNow);

        if (storedToken is null)
            return null;

        // Revocar el token actual
        storedToken.IsRevoked = true;
        storedToken.RevokedAt = DateTime.UtcNow;

        // Crear un nuevo token (rotación)
        var newToken = _jwt.GenerateRefreshToken();
        var expiresInDays = double.Parse(
            _configuration.GetSection("Jwt")["RefreshExpiresInDays"] ?? "7");

        var rotatedToken = new RefreshToken
        {
            Token = newToken,
            UserId = userId,
            ExpiresAt = DateTime.UtcNow.AddDays(expiresInDays)
        };

        _db.RefreshTokens.Add(rotatedToken);
        await _db.SaveChangesAsync();

        return rotatedToken;
    }

    public async Task RevokeAllUserTokens(Guid userId)
    {
        var activeTokens = await _db.RefreshTokens
            .Where(rt => rt.UserId == userId && !rt.IsRevoked)
            .ToListAsync();

        foreach (var token in activeTokens)
        {
            token.IsRevoked = true;
            token.RevokedAt = DateTime.UtcNow;
        }

        await _db.SaveChangesAsync();
    }
}
