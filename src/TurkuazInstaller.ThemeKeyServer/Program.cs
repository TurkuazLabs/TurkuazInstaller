// Dosya Yolu: src/TurkuazInstaller.ThemeKeyServer/Program.cs
// Amac: OIDC JWT middleware ile sifreli tema anahtar API'si olusturmak
// Modul - CSharp Web Host
// Version: 0.2.0-qa
// Aciklama: Gecersiz IdP/yetki ve HTTP deny; server-side entitlement/KMS olmadan key cikmaz
// Bagimli Oldugu Katman: ASP.NET Core | Infrastructure | Identity

using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.IdentityModel.Tokens;
using TurkuazInstaller.Infrastructure.Branding;
using TurkuazInstaller.ThemeKeyServer;

var builder = WebApplication.CreateBuilder(args);

// This service is not deployable by default. Its authority/audience are
// trusted deployment configuration, NEVER supplied by a .tpe or end user.
// A missing identity configuration is a startup error, not anonymous mode.
builder.Services.AddOptions<ThemeKeyIssuerOptions>()
    .BindConfiguration("ThemeKeyIssuer")
    .Validate(settings => settings.IsValid(),
        "A trusted HTTPS ThemeKeyIssuer Authority and Audience are required.")
    .ValidateOnStart();

builder.WebHost.ConfigureKestrel(options =>
    options.Limits.MaxRequestBodySize = 1024);

builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        var settings = builder.Configuration.GetSection("ThemeKeyIssuer")
            .Get<ThemeKeyIssuerOptions>() ??
                throw new InvalidOperationException("ThemeKeyIssuer settings are required.");
        var trustedIssuer = settings.GetTrustedIssuer();

        options.Authority = trustedIssuer;
        options.Audience = settings.Audience;
        options.RequireHttpsMetadata = true;
        options.MapInboundClaims = false;
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = trustedIssuer,
            ValidateAudience = true,
            ValidAudience = settings.Audience,
            ValidateLifetime = true,
            RequireExpirationTime = true,
            RequireSignedTokens = true,
            ValidateIssuerSigningKey = true,
            ClockSkew = TimeSpan.FromSeconds(30),
            ValidTypes = ["at+jwt"]
        };
    });

// Only authenticated JWT identities may access any current/future endpoint.
builder.Services.AddAuthorization(options =>
{
    options.FallbackPolicy = new AuthorizationPolicyBuilder(
        JwtBearerDefaults.AuthenticationScheme).RequireAuthenticatedUser().Build();
});

// DENY-BY-DEFAULT PLACEHOLDERS: a real, reviewed KMS + entitlement adapter
// must explicitly replace these via publisher-managed service registration.
// Merely starting the API never enables a valid key response.
builder.Services.AddSingleton<IThemeKeyEntitlementStore, DenyAllThemeEntitlements>();
builder.Services.AddSingleton<IThemeKeySecretStore, UnavailableThemeKeyVault>();
builder.Services.AddScoped<ThemeKeyIssueService>();

var app = builder.Build();
app.Use(async (context, next) =>
{
    context.Response.Headers["Cache-Control"] = "no-store";
    context.Response.Headers["Pragma"] = "no-cache";
    // Require direct HTTPS; no untrusted Forwarded/X-Forwarded-* bypass.
    if (!context.Request.IsHttps)
    {
        context.Response.StatusCode = StatusCodes.Status400BadRequest;
        return;
    }
    await next();
});
app.UseAuthentication();
app.UseAuthorization();

app.MapPost("/v1/theme-key", ThemeKeyApiEndpoint.HandleAsync)
    .RequireAuthorization();

app.Run();

// Exposes the host to isolated TestServer-based integration tests only.
public partial class Program { }
