using Akka.Event;
using NLog;
using NLogLevel = global::NLog.LogLevel;

namespace Akka.Logger.NLog
{
    /// <inheritdoc />
    /// <summary>
    /// This class contains methods used to convert MessageTemplated messages
    /// into normal text messages.
    /// </summary>
    public class NLogMessageFormatter : ILogMessageFormatter
    {
        /// <summary>
        /// Converts the specified template string to a text string using the specified
        /// token array to match replacements.
        /// </summary>
        /// <param name="format">The template string used in the conversion.</param>
        /// <param name="args">The array that contains values to replace in the template.</param>
        /// <returns>
        /// A text string where the template placeholders have been replaced with
        /// their corresponding values.
        /// </returns>
        public string Format(string format, params object[] args)
        {
            if (args?.Length > 0)
            {
                return LogEventInfo.Create(NLogLevel.Info, string.Empty, null, format, args).FormattedMessage;
            }
            return format;
        }
    }
}
