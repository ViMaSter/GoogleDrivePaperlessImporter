using Serilog;
using Serilog.Configuration;

namespace GoogleDrivePaperlessImporter.Logging;

internal static class LoggerCallerEnrichmentConfiguration
{
    public static LoggerConfiguration WithCaller(this LoggerEnrichmentConfiguration enrichmentConfiguration)
    {
        return enrichmentConfiguration.With<CallerEnricher>();
    }
}