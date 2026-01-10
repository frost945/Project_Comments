using Microsoft.IdentityModel.Logging;
using Serilog.Context;

namespace Comments.Infrastructure.Logging
{
    public static class AuditLoggerExtensions
    {
        public static void LogAuditUser(this ILogger logger, string message, params object[] args)
        {
            using (LogContext.PushProperty("AuditUser", true))
            {
                logger.LogInformation(message, args);
            }
        }
    }
}
