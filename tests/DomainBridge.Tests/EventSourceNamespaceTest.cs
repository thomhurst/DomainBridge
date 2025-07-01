using System;
using System.Diagnostics.Tracing;

// This file intentionally creates a namespace collision scenario
// to test the DomainBridge source generator's handling of ambiguous type references

namespace System.Diagnostics.Tracing
{
    // Define IEventSource in the same namespace as EventSource
    public interface IEventSource
    {
        bool IsEnabled();
        void WriteEvent(int eventId);
    }
}

namespace DomainBridge.Tests
{
    // Now EventSource will implement the IEventSource from its own namespace
    public class ProblematicEventSource : EventSource, System.Diagnostics.Tracing.IEventSource
    {
        public static readonly ProblematicEventSource Log = new ProblematicEventSource();

        private ProblematicEventSource() : base() { }

        [Event(1, Level = EventLevel.Informational)]
        public void LogInfo(string message)
        {
            if (IsEnabled())
            {
                WriteEvent(1, message);
            }
        }

        // Explicit implementation of IEventSource.WriteEvent
        void System.Diagnostics.Tracing.IEventSource.WriteEvent(int eventId)
        {
            base.WriteEvent(eventId);
        }
    }

    // This bridge should cause issues if the generator doesn't handle 
    // interface resolution properly
    [DomainBridge(typeof(ProblematicEventSource))]
    public partial class ProblematicEventSourceBridge
    {
    }
}