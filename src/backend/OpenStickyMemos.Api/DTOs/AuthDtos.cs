using System.ComponentModel.DataAnnotations;

namespace OpenStickyMemos.Api.DTOs;

// ── Request ──

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
