using System;
using System.Diagnostics.Tracing;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using TUnit.Core;
using TUnit.Assertions;

namespace DomainBridge.Tests
{
    public class EventSourceInterfaceTest
    {
        [Test]
        public async Task EventSource_InterfaceAnalysis()
        {
            // Check what interfaces EventSource implements
            var eventSourceType = typeof(EventSource);
            var interfaces = eventSourceType.GetInterfaces();
            
            Console.WriteLine($"EventSource implements {interfaces.Length} interfaces:");
            foreach (var iface in interfaces)
            {
                Console.WriteLine($"  - {iface.FullName}");
            }
            
            // Check if there's an IEventSource interface in System.Diagnostics.Tracing
            var tracingAssembly = typeof(EventSource).Assembly;
            var typesInNamespace = tracingAssembly.GetTypes()
                .Where(t => t.Namespace == "System.Diagnostics.Tracing" && t.IsInterface)
                .ToList();
                
            Console.WriteLine($"\nInterfaces in System.Diagnostics.Tracing namespace:");
            foreach (var type in typesInNamespace)
            {
                Console.WriteLine($"  - {type.Name}");
            }
            
            // Check if ProblematicEventSource's interfaces include both our custom and system ones
            var problematicType = typeof(ProblematicEventSource);
            var problematicInterfaces = problematicType.GetInterfaces();
            
            Console.WriteLine($"\nProblematicEventSource implements {problematicInterfaces.Length} interfaces:");
            foreach (var iface in problematicInterfaces)
            {
                Console.WriteLine($"  - {iface.FullName} (Assembly: {iface.Assembly.GetName().Name})");
            }
            
            await Assert.That(interfaces).IsNotNull();
        }
    }
}