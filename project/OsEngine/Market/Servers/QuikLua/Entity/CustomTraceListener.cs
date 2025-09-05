using System;
using System.Diagnostics;

namespace OsEngine.Market.Servers.QuikLua.Entity
{
    public class CustomTraceListener : TraceListener
    {
        public override void Write(string message) { }

        public override void WriteLine(string message) { }

        public override void TraceEvent(TraceEventCache eventCache, string source, TraceEventType eventType, int id, string message)
        {
            if (eventType == TraceEventType.Error)
            {
                OnTraceMessageReceived?.Invoke(message);
            }
        }

        public static event Action<string> OnTraceMessageReceived;
    }
}
