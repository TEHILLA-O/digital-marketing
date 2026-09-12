using System.Diagnostics;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using System.Threading.RateLimiting;

using LedgerX.SharedKernel;
using LedgerX.SharedKernel.Context;
using LedgerX.SharedKernel.Security;
using LedgerX.SharedKernel.Time;

using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;

using OpenTelemetry;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

using Scalar.AspNetCore;

namespace LedgerX.ServiceDefaults;

public static class Extensions
{
    public static IHostApplicationBuilder AddLedgerXDefaults(this IHostApplicationBuilder builder, string serviceName)
    {
        builder.Services.AddSingleton<IClock, SystemClock>();
        builder.Services.AddSingleton<ICorrelationAccessor, CorrelationAccessor>();
        builder.Services.AddProblemDetails();
        builder.Services.AddExceptionHandler<DomainExceptionHandler>();

        builder.Services.AddOpenApi();

        builder.Services.AddRequestTimeouts();
        builder.Services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            options.AddFixedWindowLimiter("fixed", limiter =>
            {
                limiter.PermitLimit = 120;
                limiter.Window = TimeSpan.FromMinutes(1);
                limiter.QueueLimit = 0;
            });
        });

        builder.Services.ConfigureHttpJsonOptions(o =>
        {
            o.SerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
        });

        builder.Services.AddCors(o =>
        {
            o.AddPolicy("LedgerX", policy =>
            {
                var origins = builder.Configuration.GetSection("Cors:Origins").Get<string[]>() ?? ["http://localhost:5100"];
                policy.WithOrigins(origins).AllowAnyHeader().AllowAnyMethod();
            });
        });

        builder.Services.AddHealthChecks()
            .AddCheck("self", () => HealthCheckResult.Healthy(), tags: ["live"]);

        builder.ConfigureOpenTelemetry(serviceName);
        builder.Services.AddServiceDiscovery();
        builder.Services.ConfigureHttpClientDefaults(http =>
        {
            http.AddStandardResilienceHandler();
            http.AddServiceDiscovery();
        });

        return builder;
    }

    public static IHostApplicationBuilder AddLedgerXJwtAuth(this IHostApplicationBuilder builder)
    {
        var jwt = builder.Configuration.GetSection(JwtOptions.SectionName).Get<JwtOptions>() ?? new JwtOptions();
        builder.Services.Configure<JwtOptions>(builder.Configuration.GetSection(JwtOptions.SectionName));

        builder.Services
            .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidateAudience = true,
                    ValidateLifetime = true,
                    ValidateIssuerSigningKey = true,
                    ValidIssuer = jwt.Issuer,
                    ValidAudience = jwt.Audience,
                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.SigningKey)),
                    ClockSkew = TimeSpan.FromSeconds(30),
                    RoleClaimType = System.Security.Claims.ClaimTypes.Role,
                    NameClaimType = System.Security.Claims.ClaimTypes.NameIdentifier
                };
            });

        builder.Services.AddAuthorizationBuilder()
            .AddPolicy(Policies.CustomerOnly, p => p.RequireRole(Roles.Customer))
            .AddPolicy(Policies.Staff, p => p.RequireRole(Roles.Staff.ToArray()))
            .AddPolicy(Policies.Administrator, p => p.RequireRole(Roles.Administrator))
            .AddPolicy(Policies.FinanceOperator, p => p.RequireRole(Roles.FinanceOperator, Roles.Administrator))
            .AddPolicy(Policies.AuditorReadOnly, p => p.RequireRole(Roles.Auditor, Roles.Administrator, Roles.FinanceOperator))
            .AddPolicy(Policies.CanFreezeAccounts, p => p.RequireRole(Roles.Administrator, Roles.SupportAgent, Roles.FinanceOperator))
            .AddPolicy(Policies.CanInspectLedger, p => p.RequireRole(Roles.Administrator, Roles.FinanceOperator, Roles.Auditor))
            .AddPolicy(Policies.CanReviewTransfers, p => p.RequireRole(Roles.Administrator, Roles.FinanceOperator, Roles.SupportAgent, Roles.Auditor))
            .AddPolicy(Policies.ServiceToService, p => p.RequireAssertion(_ => true));

        return builder;
    }

    public static WebApplication MapLedgerXDefaults(this WebApplication app, string serviceName)
    {
        app.UseExceptionHandler();
        app.UseMiddleware<CorrelationMiddleware>();
        app.UseMiddleware<SecurityHeadersMiddleware>();
        app.UseCors("LedgerX");
        app.UseRateLimiter();
        app.UseAuthentication();
        app.UseAuthorization();

        app.MapOpenApi().AllowAnonymous();
        app.MapScalarApiReference(options =>
        {
            options.Title = $"{serviceName} API";
        }).AllowAnonymous();

        app.MapHealthChecks("/health/live", new HealthCheckOptions
        {
            Predicate = r => r.Tags.Contains("live")
        }).AllowAnonymous();

        app.MapHealthChecks("/health/ready", new HealthCheckOptions
        {
            Predicate = r => r.Tags.Contains("ready") || r.Tags.Contains("live")
        }).AllowAnonymous();

        app.MapGet("/health", () => Results.Ok(new
        {
            service = serviceName,
            status = "ok",
            disclaimer = "LedgerX is an educational simulation, not a regulated bank."
        })).AllowAnonymous();

        return app;
    }

    private static IHostApplicationBuilder ConfigureOpenTelemetry(this IHostApplicationBuilder builder, string serviceName)
    {
        builder.Logging.AddOpenTelemetry(o =>
        {
            o.IncludeFormattedMessage = true;
            o.IncludeScopes = true;
        });

        builder.Services.AddOpenTelemetry()
            .ConfigureResource(r => r.AddService(serviceName))
            .WithMetrics(metrics =>
            {
                metrics.AddAspNetCoreInstrumentation()
                    .AddHttpClientInstrumentation()
                    .AddRuntimeInstrumentation()
                    .AddMeter("LedgerX");
            })
            .WithTracing(tracing =>
            {
                tracing.AddAspNetCoreInstrumentation()
                    .AddHttpClientInstrumentation()
                    .AddSource("LedgerX");
            });

        var endpoint = builder.Configuration["OTEL_EXPORTER_OTLP_ENDPOINT"];
        if (!string.IsNullOrWhiteSpace(endpoint))
        {
            builder.Services.AddOpenTelemetry().UseOtlpExporter();
        }

        return builder;
    }
}

public sealed class JwtOptions
{
    public const string SectionName = "Jwt";

    public string Issuer { get; set; } = "ledgerx";

    public string Audience { get; set; } = "ledgerx-clients";

    public string SigningKey { get; set; } = "DEV-ONLY-CHANGE-ME-32CHARS-MIN-KEY!!";

    public int AccessTokenMinutes { get; set; } = 60;
}

public sealed class DomainExceptionHandler(ILogger<DomainExceptionHandler> logger) : Microsoft.AspNetCore.Diagnostics.IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        var (status, code) = exception switch
        {
            NotFoundException => (StatusCodes.Status404NotFound, "not_found"),
            ForbiddenException => (StatusCodes.Status403Forbidden, "forbidden"),
            ConflictException => (StatusCodes.Status409Conflict, "conflict"),
            InsufficientFundsException => (StatusCodes.Status409Conflict, "insufficient_funds"),
            IllegalStateTransitionException => (StatusCodes.Status409Conflict, "illegal_state_transition"),
            ConcurrencyConflictException => (StatusCodes.Status409Conflict, "concurrency_conflict"),
            DomainException domain => (StatusCodes.Status400BadRequest, domain.Code),
            _ => (StatusCodes.Status500InternalServerError, "unexpected")
        };

        if (status >= 500)
        {
            logger.LogError(exception, "Unhandled exception. Correlation {CorrelationId}", httpContext.TraceIdentifier);
        }
        else
        {
            logger.LogWarning(exception, "Domain exception {Code}", code);
        }

        httpContext.Response.StatusCode = status;
        await httpContext.Response.WriteAsJsonAsync(new
        {
            type = $"https://httpstatuses.com/{status}",
            title = exception is DomainException ? exception.Message : "An unexpected error occurred.",
            status,
            code,
            traceId = Activity.Current?.Id ?? httpContext.TraceIdentifier
        }, cancellationToken).ConfigureAwait(false);

        return true;
    }
}

public sealed class CorrelationMiddleware(RequestDelegate next, ICorrelationAccessor accessor)
{
    public async Task InvokeAsync(HttpContext context)
    {
        var correlationId = context.Request.Headers[HeaderNames.CorrelationId].FirstOrDefault()
                            ?? context.TraceIdentifier;

        accessor.Current = new CorrelationContext
        {
            CorrelationId = correlationId,
            CausationId = context.Request.Headers[HeaderNames.CausationId].FirstOrDefault(),
            ActorId = context.User.FindFirstValue(System.Security.Claims.ClaimTypes.NameIdentifier),
            ActorRoles = string.Join(',', context.User.FindAll(System.Security.Claims.ClaimTypes.Role).Select(c => c.Value)),
            RemoteIp = context.Connection.RemoteIpAddress?.ToString(),
            UserAgent = context.Request.Headers.UserAgent.ToString()
        };

        context.Response.Headers[HeaderNames.CorrelationId] = correlationId;
        await next(context).ConfigureAwait(false);
    }
}

public sealed class SecurityHeadersMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext context)
    {
        context.Response.Headers["X-Content-Type-Options"] = "nosniff";
        context.Response.Headers["X-Frame-Options"] = "DENY";
        context.Response.Headers["Referrer-Policy"] = "no-referrer";
        context.Response.Headers["X-LedgerX-Disclaimer"] = "educational-simulation";
        await next(context).ConfigureAwait(false);
    }
}
