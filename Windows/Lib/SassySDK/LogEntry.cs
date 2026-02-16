using System;

namespace SassyMQ.SSOTME.Lib.RMQActors
{
    /// <summary>
    /// Represents a log entry from a transpiler/tool execution
    /// </summary>
    public class LogEntry
    {
        /// <summary>
        /// Log level: "message", "warning", "error", "info", "debug"
        /// </summary>
        public string Level { get; set; }

        /// <summary>
        /// The log message text
        /// </summary>
        public string Text { get; set; }

        /// <summary>
        /// Optional timestamp for the log entry
        /// </summary>
        public DateTime? Timestamp { get; set; }

        public LogEntry()
        {
        }

        public LogEntry(string level, string text)
        {
            Level = level;
            Text = text;
            Timestamp = DateTime.UtcNow;
        }
    }
}
