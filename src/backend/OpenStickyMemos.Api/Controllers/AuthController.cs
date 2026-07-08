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
    private readonly IRefreshTokenService _refreshTokenService;
    private readonly IEmailService _emailService;
    private readonly IConfiguration _configuration;

    public AuthController(
        AppDbContext db,
        IJwtService jwt,
        IRefreshTokenService refreshTokenService,
        IEmailService emailService,
        IConfiguration configuration)
    {
        _db = db;
        _jwt = jwt;
        _refreshTokenService = refreshTokenService;
        _emailService = emailService;
        _configuration = configuration;
    }

    /// <summary>
    /// POST /api/auth/register — Registro con email y contraseña
    /// </summary>
    [HttpPost("register")]
    public async Task<IActionResult> Register([FromBody] RegisterRequest request)
    {
        var normalizedEmail = request.Email.ToLowerInvariant();

        var existingUser = await _db.Users.FirstOrDefaultAsync(u => u.Email == normalizedEmail);
        if (existingUser is not null)
            return Conflict(new { error = "El email ya está registrado" });

        var user = new User
        {
            Email = normalizedEmail,
            DisplayName = request.DisplayName ?? normalizedEmail.Split('@')[0],
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
        var normalizedEmail = request.Email.ToLowerInvariant();
        var user = await _db.Users.FirstOrDefaultAsync(u => u.Email == normalizedEmail);
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
    /// El refresh token se almacena en BD y se rota (se revoca el anterior y se crea uno nuevo).
    /// </summary>
    [HttpPost("refresh")]
    public async Task<IActionResult> Refresh([FromBody] RefreshTokenRequest request)
    {
        // Extraer el userId del access token (aunque esté expirado, nos sirve para identificar al usuario)
        var principal = _jwt.ValidateToken(request.RefreshToken);

        // El refresh token NO es un JWT, es un string aleatorio.
        // Extraemos manualmente el userId del cuerpo de la petición.
        // Buscamos el refresh token en BD sin validar claims.
        var storedToken = await _db.RefreshTokens
            .Include(rt => rt.User)
            .FirstOrDefaultAsync(rt =>
                rt.Token == request.RefreshToken &&
                !rt.IsRevoked &&
                rt.ExpiresAt > DateTime.UtcNow);

        if (storedToken is null)
            return Unauthorized(new { error = "Refresh token inválido o expirado" });

        var user = storedToken.User;

        // Rotar el refresh token (revocar el actual y crear uno nuevo)
        var newRefreshToken = await _refreshTokenService.ValidateAndRotate(
            request.RefreshToken, user.Id);

        if (newRefreshToken is null)
            return Unauthorized(new { error = "Refresh token inválido o expirado" });

        return await GenerateAuthResponse(user, newRefreshToken.Token);
    }

    /// <summary>
    /// POST /api/auth/logout — Revoca el refresh token del usuario
    /// </summary>
    [HttpPost("logout")]
    public async Task<IActionResult> Logout([FromBody] RefreshTokenRequest request)
    {
        var storedToken = await _db.RefreshTokens
            .FirstOrDefaultAsync(rt =>
                rt.Token == request.RefreshToken && !rt.IsRevoked);

        if (storedToken is not null)
        {
            storedToken.IsRevoked = true;
            storedToken.RevokedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync();
        }

        return Ok(new { message = "Sesión cerrada exitosamente" });
    }

    /// <summary>
    /// POST /api/auth/forgot-password — Solicita reset de contraseña
    /// Genera un token temporal, lo guarda en BD y envía un email con el link de reset.
    /// </summary>
    [HttpPost("forgot-password")]
    public async Task<IActionResult> ForgotPassword([FromBody] ForgotPasswordRequest request)
    {
        var normalizedEmail = request.Email.ToLowerInvariant();
        var user = await _db.Users.FirstOrDefaultAsync(u => u.Email == normalizedEmail);

        // Siempre responder con éxito aunque el email no exista (seguridad: no revelar usuarios)
        if (user is null || user.AuthProvider != "Email")
        {
            return Ok(new ForgotPasswordResponse
            {
                Message = "Si el email está registrado, recibirás un enlace para restablecer tu contraseña."
            });
        }

        // Generar token único y seguro
        var tokenBytes = new byte[32];
        using var rng = System.Security.Cryptography.RandomNumberGenerator.Create();
        rng.GetBytes(tokenBytes);
        var token = Convert.ToHexString(tokenBytes).ToLowerInvariant();

        // Guardar token con expiración de 1 hora
        user.PasswordResetToken = token;
        user.PasswordResetTokenExpiresAt = DateTime.UtcNow.AddHours(1);
        await _db.SaveChangesAsync();

        // Construir link de reset → apunta al frontend (Angular)
        // Prioridad: WEB_BASE_URL (env) > Web__BaseUrl (env con doble underscore) > Web:BaseUrl (appsettings) > App:BaseUrl > default
        var baseUrl = Environment.GetEnvironmentVariable("WEB_BASE_URL")
                      ?? Environment.GetEnvironmentVariable("Web__BaseUrl")
                      ?? _configuration["Web:BaseUrl"]
                      ?? _configuration["App:BaseUrl"]
                      ?? "http://localhost:4200";
        var resetLink = $"{baseUrl}/forgot-password?token={token}";

        // Enviar email
        await _emailService.SendPasswordResetEmailAsync(
            user.Email, resetLink, user.DisplayName);

        var response = new ForgotPasswordResponse
        {
            Message = "Si el email está registrado, recibirás un enlace para restablecer tu contraseña."
        };

        // Solo en desarrollo (LogEmailService) incluimos el link en la respuesta para debug
        if (_emailService is LogEmailService)
        {
            response.DebugResetLink = resetLink;
        }

        return Ok(response);
    }

    /// <summary>
    /// POST /api/auth/reset-password — Ejecuta el reset de contraseña con el token
    /// </summary>
    [HttpPost("reset-password")]
    public async Task<IActionResult> ResetPassword([FromBody] ResetPasswordRequest request)
    {
        var user = await _db.Users.FirstOrDefaultAsync(u =>
            u.PasswordResetToken == request.Token &&
            u.PasswordResetTokenExpiresAt > DateTime.UtcNow);

        if (user is null)
            return BadRequest(new { error = "El token es inválido o ha expirado. Solicita un nuevo reset." });

        // Actualizar contraseña
        user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.NewPassword);

        // Limpiar token de reset
        user.PasswordResetToken = null;
        user.PasswordResetTokenExpiresAt = null;

        // Revocar todos los refresh tokens (seguridad: cerrar sesiones activas)
        await _refreshTokenService.RevokeAllUserTokens(user.Id);

        await _db.SaveChangesAsync();

        return Ok(new { message = "Contraseña actualizada exitosamente. Todas tus sesiones han sido cerradas." });
    }

    // ── Helpers ──

    private async Task<User> FindOrCreateUser(
        string provider, string providerId, string email,
        string displayName, string? avatarUrl)
    {
        var normalizedEmail = email.ToLowerInvariant();

        // Buscar por provider + providerId
        var user = await _db.Users
            .FirstOrDefaultAsync(u => u.AuthProvider == provider && u.ProviderId == providerId);

        if (user is not null)
        {
            // Actualizar datos que puedan haber cambiado
            user.DisplayName = displayName;
            user.Email = normalizedEmail;
            user.AvatarUrl = avatarUrl;
            await _db.SaveChangesAsync();
            return user;
        }

        // Buscar por email (tal vez ya se registró con otro provider)
        user = await _db.Users.FirstOrDefaultAsync(u => u.Email == normalizedEmail);
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
            Email = normalizedEmail,
            DisplayName = displayName,
            AvatarUrl = avatarUrl,
            AuthProvider = provider,
            ProviderId = providerId
        };

        _db.Users.Add(user);
        await _db.SaveChangesAsync();
        return user;
    }

    private async Task<IActionResult> GenerateAuthResponse(User user, string? existingRefreshToken = null)
    {
        var (accessToken, expiresAt) = _jwt.GenerateAccessToken(user);

        // Si ya tenemos un refresh token (rotación), lo usamos; si no, creamos uno nuevo
        string refreshToken;
        if (existingRefreshToken is not null)
        {
            refreshToken = existingRefreshToken;
        }
        else
        {
            var newToken = await _refreshTokenService.CreateRefreshToken(user);
            refreshToken = newToken.Token;
        }

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
