using System.Diagnostics;
using Serilog;
using Serilog.Core;
using Serilog.Events;

namespace GoogleDrivePaperlessImporter.Logging;

internal class CallerEnricher : ILogEventEnricher
{
    public void Enrich(LogEvent logEvent, ILogEventPropertyFactory propertyFactory)
    {
        var skip = 3;
        while (true)
        {
            var stack = new StackFrame(skip);
            if (!stack.HasMethod())
            {
                logEvent.AddPropertyIfAbsent(new LogEventProperty("CallerClass", new ScalarValue("<unknown class>")));
                return;
            }

            var method = stack.GetMethod();
            if (method.DeclaringType.Assembly != typeof(Log).Assembly)
            {
                var callerClass = method.DeclaringType.Name;
                logEvent.AddPropertyIfAbsent(new LogEventProperty("CallerClass", new ScalarValue(callerClass)));
                return;
            }

            skip++;
        }
    }
}