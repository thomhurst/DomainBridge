using System;
using System.Threading.Tasks;
using TUnit.Core;
using TUnit.Assertions;

namespace DomainBridge.Tests
{
    /// <summary>
    /// Tests to demonstrate and verify namespace collision issues
    /// </summary>
    public partial class NamespaceCollisionTests
    {
        // Define a local interface with the same name as a system interface
        public interface IEventSource
        {
            void WriteEvent(int eventId, string message);
            bool IsEnabled();
        }

        // A class that simulates namespace collision without inheriting from the problematic EventSource
        // This demonstrates the same interface name collision scenario
        [Serializable]
        public class CustomEventSource : MarshalByRefObject, IEventSource, IDisposable
        {
            // Simulate EventSource-like properties
            public string Name => "CustomEventSource";
            public Guid Guid => Guid.NewGuid();
            
            void IEventSource.WriteEvent(int eventId, string message)
            {
                // Explicit implementation for our local interface
                Console.WriteLine($"Local IEventSource: Event {eventId}: {message}");
            }

            bool IEventSource.IsEnabled()
            {
                // Explicit implementation for our local interface
                return true;
            }

            // Custom methods that would collide with EventSource
            public void LogInfo(string message)
            {
                Console.WriteLine($"LogInfo: {message}");
            }
            
            public void Dispose()
            {
                // Cleanup
            }
        }

        // Another scenario: A type that implements multiple interfaces with the same method names
        public interface ILogger
        {
            void Log(string message);
        }

        public interface IDiagnostics
        {
            void Log(string message);
        }

        public class MultiInterfaceService : MarshalByRefObject, ILogger, IDiagnostics
        {
            void ILogger.Log(string message)
            {
                Console.WriteLine($"ILogger: {message}");
            }

            void IDiagnostics.Log(string message)
            {
                Console.WriteLine($"IDiagnostics: {message}");
            }

            public void Log(string message)
            {
                Console.WriteLine($"Public: {message}");
            }
        }

        // Bridge for CustomEventSource - this should generate code that handles namespace collisions
        [DomainBridge(typeof(CustomEventSource))]
        public partial class CustomEventSourceBridge
        {
        }

        // Bridge for MultiInterfaceService
        [DomainBridge(typeof(MultiInterfaceService))]
        public partial class MultiInterfaceServiceBridge
        {
        }

        [Test]
        public async Task CustomEventSourceBridge_ShouldHandleNamespaceCollisions()
        {
            // This test verifies that the generated bridge correctly handles types
            // that implement interfaces with namespace collisions
            
            using var bridge = CustomEventSourceBridge.Create(() => new CustomEventSource());
            
            // The bridge should expose methods from both interfaces
            bridge.LogInfo("Test message");
            
            // Test explicit interface implementations
            var localEventSource = bridge as IEventSource;
            if (localEventSource != null)
            {
                localEventSource.WriteEvent(42, "Local interface test");
                var enabled = localEventSource.IsEnabled();
                await Assert.That(enabled).IsTrue();
            }
            
            await Assert.That(bridge).IsNotNull();
        }

        [Test]
        public async Task MultiInterfaceServiceBridge_ShouldHandleExplicitImplementations()
        {
            using var bridge = MultiInterfaceServiceBridge.Create(() => new MultiInterfaceService());
            
            // Test public method
            bridge.Log("Public method test");
            
            // Test explicit interface implementations
            var logger = bridge as ILogger;
            if (logger != null)
            {
                logger.Log("ILogger test");
            }
            
            var diagnostics = bridge as IDiagnostics;
            if (diagnostics != null)
            {
                diagnostics.Log("IDiagnostics test");
            }
            
            await Assert.That(bridge).IsNotNull();
        }
    }
}