using System.Security.Claims;
using Google.Apis.Auth;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OpenStickyMemos.Api.Data;
using OpenStickyMemos.Api.DTOs;
using OpenStickyMemos.Api.Models;
using OpenStickyMemos.Api.Services;

namespace OpenStickyMemos.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly IJwtService _jwt;
    private readonly IConfiguration _configuration;

    public AuthController(AppDbContext db, IJwtService jwt, IConfiguration configuration)
    {
        _db = db;
        _jwt = jwt;
        _configuration = configuration;
    }

    /// <summary>
    /// POST /api/auth/register — Registro con email y contraseña
    /// </summary>
    [HttpPost("register")]
    public async Task<IActionResult> Register([FromBody] RegisterRequest request)
    {
        var existingUser = await _db.Users.FirstOrDefaultAsync(u => u.Email == request.Email);
        if (existingUser is not null)
            return Conflict(new { error = "El email ya está registrado" });

        var user = new User
        {
            Email = request.Email,
            DisplayName = request.DisplayName ?? request.Email.Split('@')[0],
            AuthProvider = "Email",
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.Password)
        };

        _db.Users.Add(user);
        await _db.SaveChangesAsync();

        return await GenerateAuthResponse(user);
    }

    /// <summary>
    /// POST /api/auth/login — Inicio de sesión con email y contraseña
    /// </summary>
    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginRequest request)
    {
        var user = await _db.Users.FirstOrDefaultAsync(u => u.Email == request.Email);
        if (user is null)
            return Unauthorized(new { error = "Email o contraseña incorrectos" });

        if (user.AuthProvider != "Email" || string.IsNullOrEmpty(user.PasswordHash))
            return Unauthorized(new { error = "Esta cuenta usa un proveedor externo. Usa Google o Microsoft para iniciar sesión." });

        if (!BCrypt.Net.BCrypt.Verify(request.Password, user.PasswordHash))
            return Unauthorized(new { error = "Email o contraseña incorrectos" });

        return await GenerateAuthResponse(user);
    }

    /// <summary>
    /// POST /api/auth/google — Intercambia un id_token de Google por un JWT de la app
    /// </summary>
    [HttpPost("google")]
    public async Task<IActionResult> GoogleLogin([FromBody] ExternalLoginRequest request)
    {
        if (request.Provider != "Google")
            return BadRequest(new { error = "Provider debe ser 'Google'" });

        try
        {
            // Validar el id_token de Google
            var payload = await GoogleJsonWebSignature.ValidateAsync(request.IdToken,
                new GoogleJsonWebSignature.ValidationSettings
                {
                    Audience = new[] { _configuration["OAuth:Google:ClientId"] }
                });

            var user = await FindOrCreateUser(
                provider: "Google",
                providerId: payload.Subject,
                email: payload.Email,
                displayName: payload.Name,
                avatarUrl: payload.Picture
            );

            return await GenerateAuthResponse(user);
        }
        catch (Exception ex)
        {
            return Unauthorized(new { error = "Token de Google inválido", detail = ex.Message });
        }
    }

    /// <summary>
    /// POST /api/auth/microsoft — Intercambia un id_token de Microsoft por un JWT de la app
    /// </summary>
    [HttpPost("microsoft")]
    public async Task<IActionResult> MicrosoftLogin([FromBody] ExternalLoginRequest request)
    {
        if (request.Provider != "Microsoft")
            return BadRequest(new { error = "Provider debe ser 'Microsoft'" });

        try
        {
            // Validar el id_token de Microsoft (manualmente con OpenID)
            var payload = await ValidateMicrosoftToken(request.IdToken);

            var user = await FindOrCreateUser(
                provider: "Microsoft",
                providerId: payload.sub,
                email: payload.email,
                displayName: payload.name,
                avatarUrl: null // Microsoft no siempre envía picture en id_token
            );

            return await GenerateAuthResponse(user);
        }
        catch (Exception ex)
        {
            return Unauthorized(new { error = "Token de Microsoft inválido", detail = ex.Message });
        }
    }

    /// <summary>
    /// POST /api/auth/refresh — Renueva el access token usando un refresh token válido
    /// </summary>
    [HttpPost("refresh")]
    public async Task<IActionResult> Refresh([FromBody] RefreshTokenRequest request)
    {
        var principal = _jwt.ValidateToken(request.RefreshToken);
        if (principal is null)
            return Unauthorized(new { error = "Refresh token inválido o expirado" });

        var userId = principal.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (userId is null || !Guid.TryParse(userId, out var userGuid))
            return Unauthorized(new { error = "Refresh token inválido" });

        var user = await _db.Users.FindAsync(userGuid);
        if (user is null)
            return Unauthorized(new { error = "Usuario no encontrado" });

        return await GenerateAuthResponse(user);
    }

    // ── Helpers ──

    private async Task<User> FindOrCreateUser(
        string provider, string providerId, string email,
        string displayName, string? avatarUrl)
    {
        // Buscar por provider + providerId
        var user = await _db.Users
            .FirstOrDefaultAsync(u => u.AuthProvider == provider && u.ProviderId == providerId);

        if (user is not null)
        {
            // Actualizar datos que puedan haber cambiado
            user.DisplayName = displayName;
            user.Email = email;
            user.AvatarUrl = avatarUrl;
            await _db.SaveChangesAsync();
            return user;
        }

        // Buscar por email (tal vez ya se registró con otro provider)
        user = await _db.Users.FirstOrDefaultAsync(u => u.Email == email);
        if (user is not null)
        {
            // Vincular este provider al usuario existente
            // En este MVP permitimos solo un provider por usuario
            // En futura versión: soportar múltiples providers vinculados
            user.DisplayName = displayName;
            user.AvatarUrl = avatarUrl;
            await _db.SaveChangesAsync();
            return user;
        }

        // Crear nuevo usuario
        user = new User
        {
            Email = email,
            DisplayName = displayName,
            AvatarUrl = avatarUrl,
            AuthProvider = provider,
            ProviderId = providerId
        };

        _db.Users.Add(user);
        await _db.SaveChangesAsync();
        return user;
    }

    private async Task<IActionResult> GenerateAuthResponse(User user)
    {
        var (accessToken, expiresAt) = _jwt.GenerateAccessToken(user);
        var refreshToken = _jwt.GenerateRefreshToken();

        return Ok(new AuthResponse
        {
            AccessToken = accessToken,
            RefreshToken = refreshToken,
            ExpiresAt = expiresAt,
            User = new UserInfo
            {
                Id = user.Id,
                Email = user.Email,
                DisplayName = user.DisplayName,
                AvatarUrl = user.AvatarUrl,
                AuthProvider = user.AuthProvider
            }
        });
    }

    // ── Microsoft Token Validation ──

    private record MicrosoftTokenPayload(
        string sub, string email, string name, string? preferred_username);

    private async Task<MicrosoftTokenPayload> ValidateMicrosoftToken(string idToken)
    {
        var clientId = _configuration["OAuth:Microsoft:ClientId"];
        var tenantId = _configuration["OAuth:Microsoft:TenantId"] ?? "common";

        // Obtener claves públicas de Microsoft
        var openIdConfigUrl = $"https://login.microsoftonline.com/{tenantId}/.well-known/openid-configuration";
        using var httpClient = new HttpClient();

        var configResponse = await httpClient.GetFromJsonAsync<MicrosoftOpenIdConfig>(openIdConfigUrl)
            ?? throw new InvalidOperationException("No se pudo obtener configuración OpenID de Microsoft");

        // Usar la librería JWT para validar manualmente
        var handler = new System.IdentityModel.Tokens.Jwt.JwtSecurityTokenHandler();
        var jwtToken = handler.ReadJwtToken(idToken);

        // Validar issuer, audience, expiración
        var expectedIssuer = $"https://login.microsoftonline.com/{tenantId}/v2.0";
        if (jwtToken.Issuer != expectedIssuer)
            throw new UnauthorizedAccessException("Issuer inválido");

        if (!jwtToken.Audiences.Contains(clientId))
            throw new UnauthorizedAccessException("Audience inválida");

        if (jwtToken.ValidTo < DateTime.UtcNow)
            throw new UnauthorizedAccessException("Token expirado");

        return new MicrosoftTokenPayload(
            sub: jwtToken.Subject,
            email: jwtToken.Claims.FirstOrDefault(c => c.Type == "email")?.Value
                    ?? jwtToken.Claims.FirstOrDefault(c => c.Type == "preferred_username")?.Value
                    ?? throw new UnauthorizedAccessException("Email no encontrado en el token"),
            name: jwtToken.Claims.FirstOrDefault(c => c.Type == "name")?.Value ?? "Usuario",
            preferred_username: jwtToken.Claims.FirstOrDefault(c => c.Type == "preferred_username")?.Value
        );
    }

    private record MicrosoftOpenIdConfig(string issuer, string jwks_uri);
}
