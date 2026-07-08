using System.ComponentModel.DataAnnotations;

namespace OpenStickyMemos.Api.DTOs;

/// <summary>
/// Request para solicitar un reset de contraseña (POST /api/auth/forgot-password)
/// </summary>
public class ForgotPasswordRequest
{
    [Required, EmailAddress]
    public string Email { get; set; } = string.Empty;
}

/// <summary>
/// Request para ejecutar el reset de contraseña (POST /api/auth/reset-password)
/// </summary>
public class ResetPasswordRequest
{
    [Required]
    public string Token { get; set; } = string.Empty;

    [Required, MinLength(6), MaxLength(128)]
    public string NewPassword { get; set; } = string.Empty;
}

/// <summary>
/// Respuesta del endpoint forgot-password
/// </summary>
public class ForgotPasswordResponse
{
    public string Message { get; set; } = string.Empty;

    /// <summary>
    /// Solo se incluye en desarrollo (cuando se usa LogEmailService).
    /// En producción con un proveedor real, este campo es null.
    /// </summary>
    public string? DebugResetLink { get; set; }
}
