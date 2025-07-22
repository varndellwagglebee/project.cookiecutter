using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Serilog;
using Serilog.Events;

namespace {{cookiecutter.assembly_name}}.ServiceDefaults;

public static class SerilogExtensions
{
    /// <summary>
    /// Configure Serilog to write to the console and OpenTelemetry for Aspire structured logs.
    /// </summary>
    /// <remarks>
    /// ⚠ This method MUST be called before the <see cref="OpenTelemetryLoggingExtensions.AddOpenTelemetry(ILoggingBuilder)"/> method to still send structured logs via OpenTelemetry. ⚠
    /// </remarks>
    internal static IHostApplicationBuilder ConfigureSerilog(this IHostApplicationBuilder builder)
    {
        var otlpExporter = builder.Configuration["OTEL_EXPORTER_OTLP_ENDPOINT"];
        var serviceName = builder.Configuration["OTEL_SERVICE_NAME"] ?? "Unknown";
        Log.Logger.Information("App Service name {Name}", serviceName);
        builder.Services.AddSerilog((_, loggerConfiguration) =>
        {
            // Configure Serilog as desired here for every project (or use IConfiguration for configuration variations between projects)
            loggerConfiguration
                .ReadFrom.Configuration(builder.Configuration)
                .Enrich.FromLogContext()
                .WriteTo.Console();

            if (!string.IsNullOrEmpty(otlpExporter))
            {
                loggerConfiguration
                    .WriteTo.OpenTelemetry(options =>
                    {
                        options.Endpoint = otlpExporter;
                        options.ResourceAttributes.Add("service.name", serviceName);
                    });
            }
        });

        // Removes the built-in logging providers
        builder.Logging.ClearProviders().AddSerilog();
        return builder;
    }

    /// <summary>
    /// Sets up a initial logger to catch and report exceptions thrown during set-up of the ASP.NET Core host.
    /// This follows the Two-stage initialization process documented <a href="https://github.com/serilog/serilog-aspnetcore?tab=readme-ov-file#two-stage-initialization">here</a>.
    /// </summary>
    /// <param name="logger">Only used for the extension method base type</param>
    public static Serilog.ILogger ConfigureSerilogBootstrapLogger(this Serilog.ILogger logger)
    {
        var serviceName = Environment.GetEnvironmentVariable("OTEL_SERVICE_NAME") ?? "app";
        var logFile = Path.Combine("logs", $"{serviceName}-{DateTime.UtcNow:yyyyMMdd}.txt");

        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Override("Microsoft", LogEventLevel.Information)
            .Enrich.FromLogContext()
            .WriteTo.Console()
            .WriteTo.File(logFile, rollingInterval: RollingInterval.Day)
            .CreateBootstrapLogger();

        return logger;
    }
}