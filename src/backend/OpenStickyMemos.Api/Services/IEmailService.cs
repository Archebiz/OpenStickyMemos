namespace OpenStickyMemos.Api.Services;

/// <summary>
/// Interfaz para envío de emails.
/// Cualquier fork puede implementar su propio servicio (SendGrid, Mailgun, SMTP, etc.)
/// y registrarlo en Program.cs en lugar de la implementación por defecto.
/// </summary>
public interface IEmailService
{
    /// <summary>
    /// Envía un email con un link para resetear la contraseña.
    /// </summary>
    /// <param name="to">Email del destinatario</param>
    /// <param name="resetLink">URL completa con el token de reset</param>
    /// <param name="displayName">Nombre del usuario (opcional)</param>
    Task SendPasswordResetEmailAsync(string to, string resetLink, string? displayName);
}

/// <summary>
/// Implementación que solo escribe en log (Serilog).
/// Ideal para desarrollo local o cuando no se configura un proveedor de email.
/// El link de reset se puede copiar directamente desde la consola.
/// </summary>
public class LogEmailService : IEmailService
{
    private readonly ILogger<LogEmailService> _logger;

    public LogEmailService(ILogger<LogEmailService> logger)
    {
        _logger = logger;
    }

    public Task SendPasswordResetEmailAsync(string to, string resetLink, string? displayName)
    {
        _logger.LogInformation(
            "=== PASSWORD RESET EMAIL (no delivery) ===\n" +
            "To: {Email}\n" +
            "Name: {Name}\n" +
            "Reset link: {ResetLink}\n" +
            "===========================================",
            to, displayName ?? "(sin nombre)", resetLink);

        return Task.CompletedTask;
    }
}
