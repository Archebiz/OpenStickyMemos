using System.Net.Http.Json;
using System.Text.Json.Serialization;

namespace OpenStickyMemos.Api.Services;

/// <summary>
/// Implementación de IEmailService usando la API REST de Resend (https://resend.com).
/// 
/// Configuración requerida en appsettings.json o variable de entorno:
///   "Email": {
///     "Provider": "Resend",
///     "ApiKey": "re_xxxxxxxxxxxx",
///     "FromEmail": "onboarding@resend.dev",  // En desarrollo usar el dominio de Resend
///     "FromName": "OpenStickyMemos"
///   }
///
/// Variable de entorno alternativa: EMAIL_API_KEY (para Railway)
/// </summary>
public class ResendEmailService : IEmailService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<ResendEmailService> _logger;
    private readonly string _apiKey;
    private readonly string _fromEmail;
    private readonly string _fromName;

    public ResendEmailService(
        HttpClient httpClient,
        IConfiguration configuration,
        ILogger<ResendEmailService> logger)
    {
        _httpClient = httpClient;
        _logger = logger;

        // Soporte para variable de entorno (Railway) o appsettings
        _apiKey = Environment.GetEnvironmentVariable("EMAIL_API_KEY")
                  ?? configuration["Email:ApiKey"]
                  ?? string.Empty;

        _fromEmail = configuration["Email:FromEmail"] ?? "onboarding@resend.dev";
        _fromName = configuration["Email:FromName"] ?? "OpenStickyMemos";

        // Configurar el header Authorization con Bearer token para Resend API
        if (!string.IsNullOrEmpty(_apiKey))
        {
            _httpClient.DefaultRequestHeaders.Authorization =
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _apiKey);
        }
    }

    public async Task SendPasswordResetEmailAsync(string to, string resetLink, string? displayName)
    {
        if (string.IsNullOrEmpty(_apiKey))
        {
            _logger.LogWarning(
                "ResendEmailService: No API key configurada. " +
                "Establece EMAIL_API_KEY o Email:ApiKey en la configuración. " +
                "El reset link sería: {ResetLink}", resetLink);
            return;
        }

        var salutation = string.IsNullOrEmpty(displayName) ? "Usuario" : displayName;

        var from = $"{_fromName} <{_fromEmail}>";
        var html = $$"""
            <!DOCTYPE html>
            <html>
            <head>
                <meta charset="utf-8">
                <style>
                    body { font-family: 'Segoe UI', Arial, sans-serif; background: #f5f5f5; margin: 0; padding: 0; }
                    .container { max-width: 480px; margin: 40px auto; background: white; border-radius: 12px; padding: 32px; box-shadow: 0 2px 8px rgba(0,0,0,0.1); }
                    .logo { font-size: 24px; font-weight: bold; color: #FFD700; margin-bottom: 16px; }
                    h1 { font-size: 20px; color: #333; margin: 0 0 8px 0; }
                    p { color: #555; line-height: 1.6; margin: 12px 0; }
                    .button { display: inline-block; background: #FFD700; color: #333 !important; text-decoration: none; padding: 12px 28px; border-radius: 8px; font-weight: 600; margin: 16px 0; }
                    .footer { font-size: 12px; color: #999; margin-top: 24px; border-top: 1px solid #eee; padding-top: 16px; }
                </style>
            </head>
            <body>
                <div class="container">
                    <div class="logo">📝 OpenStickyMemos</div>
                    <h1>Hola, {{salutation}}</h1>
                    <p>Recibimos una solicitud para restablecer la contraseña de tu cuenta.</p>
                    <p>Haz clic en el siguiente botón para crear una nueva contraseña. Este enlace expira en 1 hora.</p>
                    <p style="text-align: center;">
                        <a href="{{resetLink}}" class="button">Restablecer contraseña</a>
                    </p>
                    <p style="font-size: 14px; color: #888;">
                        Si no solicitaste este cambio, puedes ignorar este email. Tu cuenta está segura.
                    </p>
                    <p style="font-size: 14px; color: #888;">
                        Si el botón no funciona, copia y pega este enlace en tu navegador:<br>
                        <a href="{{resetLink}}" style="color: #666; word-break: break-all;">{{resetLink}}</a>
                    </p>
                    <div class="footer">
                        OpenStickyMemos — App colaborativa de notas adhesivas
                    </div>
                </div>
            </body>
            </html>
            """;

        var requestBody = new
        {
            from,
            to = new[] { to },
            subject = "Restablece tu contraseña de OpenStickyMemos",
            html
        };

        var response = await _httpClient.PostAsJsonAsync(
            "https://api.resend.com/emails", requestBody);

        if (!response.IsSuccessStatusCode)
        {
            var errorBody = await response.Content.ReadAsStringAsync();
            _logger.LogError(
                "Error al enviar email por Resend. Status: {Status}, Body: {Body}",
                response.StatusCode, errorBody);
            throw new InvalidOperationException(
                $"Error al enviar el email: {response.StatusCode}");
        }

        _logger.LogInformation(
            "Email de reset enviado exitosamente a {Email}", to);
    }
}
