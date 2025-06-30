using System;
using System.Threading.Tasks;
using DomainBridge;
using TUnit.Core;

namespace DomainBridge.Tests
{
    public class SealedTypeTests
    {
        [Test]
        public async Task Can_Bridge_Sealed_Type()
        {
            // Arrange & Act
            using var bridge = SealedServiceBridge.Create();
            
            // Assert
            var result = bridge.GetValue();
            await Assert.That(result).IsEqualTo("Hello from sealed service!");
            
            bridge.SetValue("Updated value");
            var updatedResult = bridge.GetValue();
            await Assert.That(updatedResult).IsEqualTo("Updated value");
        }
        
        [Test]
        public async Task Can_Bridge_Type_That_Inherits_From_MarshalByRefObject()
        {
            // Arrange & Act
            using var bridge = MarshalByRefServiceBridge.Create();
            
            // Assert
            var result = bridge.Process("test");
            await Assert.That(result).IsEqualTo("Processed: test");
        }
    }
    
    // Test sealed class
    public sealed class SealedService
    {
        private string _value = "Hello from sealed service!";
        
        public string GetValue() => _value;
        public void SetValue(string value) => _value = value;
    }
    
    // Test class that already inherits from MarshalByRefObject
    public class MarshalByRefService : MarshalByRefObject
    {
        public string Process(string input)
        {
            return $"Processed: {input}";
        }
    }
    
    // Bridge for sealed type
    [DomainBridge(typeof(SealedService))]
    public partial class SealedServiceBridge
    {
    }
    
    // Bridge for MarshalByRefObject-derived type
    [DomainBridge(typeof(MarshalByRefService))]
    public partial class MarshalByRefServiceBridge
    {
    }
}