using System;
using System.Threading.Tasks;
using DomainBridge;
using TUnit.Assertions;
using TUnit.Core;

namespace DomainBridge.Tests
{
    // Test class with reserved keywords as parameter names
    [Serializable]
    public class EventService
    {
        public string LogEvent(string @event, int @class, bool @checked = false)
        {
            return $"Event: {@event}, Class: {@class}, Checked: {@checked}";
        }
    }
    
    [DomainBridge(typeof(EventService))]
    public partial class EventServiceBridge { }
    
    public class KeywordParameterTests
    {
        [Test]
        public async Task BridgeHandlesReservedKeywordParameters()
        {
            // Arrange
            var service = EventServiceBridge.Create();
            
            // Act
            var result = service.LogEvent("TestEvent", 123, true);
            
            // Assert
            await Assert.That(result).IsEqualTo("Event: TestEvent, Class: 123, Checked: True");
        }
        
        // Note: Delegates cannot be passed across AppDomain boundaries unless they are
        // static methods or the target object is MarshalByRefObject. This is a fundamental
        // limitation of .NET AppDomains, not specific to DomainBridge.

    }
}