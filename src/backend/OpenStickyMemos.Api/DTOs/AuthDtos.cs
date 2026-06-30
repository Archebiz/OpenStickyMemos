using System.ComponentModel.DataAnnotations;

namespace OpenStickyMemos.Api.DTOs;

// ── Request ──

public class RegisterRequest
{
    [Required, EmailAddress, MaxLength(256)]
    public string Email { get; set; } = string.Empty;

    [Required, MinLength(6), MaxLength(128)]
    public string Password { get; set; } = string.Empty;

    [MaxLength(128)]
    public string? DisplayName { get; set; }
}

public class LoginRequest
{
    [Required, EmailAddress]
    public string Email { get; set; } = string.Empty;

    [Required]
    public string Password { get; set; } = string.Empty;
}

public class ExternalLoginRequest
{
    [Required]
    public string IdToken { get; set; } = string.Empty;

    [Required]
    public string Provider { get; set; } = string.Empty; // "Google" or "Microsoft"
}

public class RefreshTokenRequest
{
    [Required]
    public string RefreshToken { get; set; } = string.Empty;
}

// ── Response ──

public class AuthResponse
{
    public string AccessToken { get; set; } = string.Empty;
    public string RefreshToken { get; set; } = string.Empty;
    public DateTime ExpiresAt { get; set; }
    public UserInfo User { get; set; } = null!;
}

public class UserInfo
{
    public Guid Id { get; set; }
    public string Email { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string? AvatarUrl { get; set; }
    public string AuthProvider { get; set; } = string.Empty;
}
