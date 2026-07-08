using System.Text;
using FluentValidation;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using OpenStickyMemos.Api.Data;
using OpenStickyMemos.Api.Services;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

// ── Forwarded Headers (Railway proxy HTTPS → HTTP interno) ──
builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
});

// ── Serilog ──
builder.Host.UseSerilog((ctx, cfg) =>
    cfg.ReadFrom.Configuration(ctx.Configuration));

// ── Database ──
var connectionString = Environment.GetEnvironmentVariable("DATABASE_URL")
    ?? builder.Configuration.GetConnectionString("DefaultConnection");

if (connectionString is not null)
{
    // Railway provides DATABASE_URL as postgresql://...
    if (connectionString.StartsWith("postgresql://", StringComparison.OrdinalIgnoreCase))
    {
        var uri = new Uri(connectionString);
        var db = uri.AbsolutePath.TrimStart('/');
        var userInfo = uri.UserInfo.Split(':');
        connectionString = $"Host={uri.Host};Port={uri.Port};Database={db};Username={userInfo[0]};Password={userInfo[1]}";
    }

    builder.Services.AddDbContext<AppDbContext>(options =>
        options.UseNpgsql(connectionString));
}

// ── Services ──
builder.Services.AddScoped<IJwtService, JwtService>();
builder.Services.AddScoped<IRefreshTokenService, RefreshTokenService>();
builder.Services.AddScoped<IProjectService, ProjectService>();
builder.Services.AddScoped<INoteService, NoteService>();

// ── Email Service ──
// Por defecto: LogEmailService (solo escribe en consola, ideal para desarrollo).
// Si se configura EMAIL_API_KEY (variable de entorno) o Email:ApiKey (appsettings),
// se usa ResendEmailService automáticamente.
var emailApiKey = Environment.GetEnvironmentVariable("EMAIL_API_KEY")
                  ?? builder.Configuration["Email:ApiKey"];

if (!string.IsNullOrEmpty(emailApiKey))
{
    builder.Services.AddHttpClient<IEmailService, ResendEmailService>();
    Log.Information("Email service: Resend (configurado con API key)");
}
else
{
    builder.Services.AddSingleton<IEmailService, LogEmailService>();
    Log.Information("Email service: Log (modo desarrollo - sin email real)");
}

// ── FluentValidation ──
builder.Services.AddValidatorsFromAssemblyContaining<Program>();

// ── JWT Authentication ──
var jwtSection = builder.Configuration.GetSection("Jwt");
var jwtKey = Encoding.UTF8.GetBytes(jwtSection["Key"]!);

builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true,
        ValidateAudience = true,
        ValidateLifetime = true,
        ValidateIssuerSigningKey = true,
        ValidIssuer = jwtSection["Issuer"],
        ValidAudience = jwtSection["Audience"],
        IssuerSigningKey = new SymmetricSecurityKey(jwtKey)
    };

    // Allow SignalR to receive JWT from query string
    options.Events = new JwtBearerEvents
    {
        OnMessageReceived = context =>
        {
            var accessToken = context.Request.Query["access_token"];
            var path = context.HttpContext.Request.Path;
            if (!string.IsNullOrEmpty(accessToken) && path.StartsWithSegments("/api/hubs"))
            {
                context.Token = accessToken;
            }
            return Task.CompletedTask;
        }
    };
});

builder.Services.AddAuthorization();

// ── SignalR ──
builder.Services.AddSignalR();

// ── Controllers + OpenAPI ──
builder.Services.AddControllers();
builder.Services.AddOpenApi();

// ── CORS ──
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyMethod()
              .AllowAnyHeader();
    });

    options.AddPolicy("SignalR", policy =>
    {
        policy.SetIsOriginAllowed(_ => true)
              .AllowAnyMethod()
              .AllowAnyHeader()
              .AllowCredentials();
    });
});

var app = builder.Build();

// ── Startup diagnostics ──
var logger = app.Services.GetRequiredService<ILogger<Program>>();
logger.LogInformation("=== OpenStickyMemos Backend Starting ===");
logger.LogInformation("ASPNETCORE_URLS: {Urls}", Environment.GetEnvironmentVariable("ASPNETCORE_URLS") ?? "(not set)");
logger.LogInformation("ASPNETCORE_ENVIRONMENT: {Env}", Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "(not set)");
logger.LogInformation("DATABASE_URL present: {Db}", !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("DATABASE_URL")));
logger.LogInformation("WEB_BASE_URL: {Url}", Environment.GetEnvironmentVariable("WEB_BASE_URL") ?? "(not set - se usará appsettings o localhost)");
logger.LogInformation("EMAIL_API_KEY present: {Key}", !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("EMAIL_API_KEY")));
logger.LogInformation("SignalR Hub mapped at: /api/hubs/notes");
logger.LogInformation("OpenAPI JSON at: /openapi/v1.json");
logger.LogInformation("Swagger UI at: /swagger");

// ── Pipeline ──
app.UseForwardedHeaders();
app.UseSerilogRequestLogging();

// OpenAPI disponible en todos los entornos
app.MapOpenApi();

// Middleware que agrega esquema Bearer al spec OpenAPI y corrige URLs a HTTPS
app.Use(async (ctx, next) =>
{
    if (ctx.Request.Path == "/openapi/v1.json")
    {
        var body = ctx.Response.Body;
        using var buffer = new MemoryStream();
        ctx.Response.Body = buffer;
        await next();
        buffer.Position = 0;
        var json = await new StreamReader(buffer).ReadToEndAsync();
        var doc = System.Text.Json.Nodes.JsonNode.Parse(json)!.AsObject();

        // Forzar scheme HTTPS en servers
        if (doc["servers"] is System.Text.Json.Nodes.JsonArray servers)
        {
            foreach (var srv in servers.OfType<System.Text.Json.Nodes.JsonObject>())
            {
                if (srv["url"]?.GetValue<string>() is string url && url.StartsWith("http://"))
                {
                    srv["url"] = "https://" + url[7..];
                }
            }
        }

        // Agregar esquema Bearer
        doc["components"] ??= new System.Text.Json.Nodes.JsonObject();
        doc["components"]!["securitySchemes"] = new System.Text.Json.Nodes.JsonObject
        {
            ["Bearer"] = new System.Text.Json.Nodes.JsonObject
            {
                ["type"] = "http",
                ["scheme"] = "bearer",
                ["bearerFormat"] = "JWT",
                ["description"] = "Token JWT de POST /api/auth/login"
            }
        };
        doc["security"] = new System.Text.Json.Nodes.JsonArray
        {
            new System.Text.Json.Nodes.JsonObject { ["Bearer"] = new System.Text.Json.Nodes.JsonArray() }
        };

        ctx.Response.Body = body;
        ctx.Response.ContentType = "application/json;charset=utf-8";
        await ctx.Response.WriteAsync(doc.ToJsonString());
        return;
    }
    await next();
});

// Swagger UI via CDN — https://localhost:5000/swagger
app.MapGet("/swagger", () => Results.Content("""
<!DOCTYPE html>
<html>
<head><title>OpenStickyMemos API</title>
<link rel="stylesheet" href="https://unpkg.com/swagger-ui-dist@5/swagger-ui.css">
</head>
<body>
<div id="swagger-ui"></div>
<script src="https://unpkg.com/swagger-ui-dist@5/swagger-ui-bundle.js"></script>
<script>
const ui = SwaggerUIBundle({
  url:'/openapi/v1.json',
  dom_id:'#swagger-ui',
  presets:[SwaggerUIBundle.presets.apis],
  requestInterceptor: (req) => {
    const token = localStorage.getItem('osm_swagger_token');
    if (token) req.headers['Authorization'] = 'Bearer ' + token;
    return req;
  }
});

window.authorizeSwagger = function(token) {
  ui.preauthorizeApiKey('Bearer', token);
  localStorage.setItem('osm_swagger_token', token);
};
window.logoutSwagger = function() {
  ui.authActions.logout();
  localStorage.removeItem('osm_swagger_token');
};
</script>
</body>
</html>
""", "text/html"));

// Favicon (evita 404)
app.MapGet("/favicon.ico", () => Results.NoContent());

app.UseCors("AllowAll");
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.MapHub<OpenStickyMemos.Api.Hubs.NotesHub>("/api/hubs/notes").RequireCors("SignalR");

// ── Auto-migrate database ──
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    db.Database.Migrate();
}

app.Run();
