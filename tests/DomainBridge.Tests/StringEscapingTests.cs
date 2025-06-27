using System;
using System.Threading.Tasks;
using DomainBridge;
using TUnit.Assertions;
using TUnit.Core;

namespace DomainBridge.Tests
{
    // Test service with string parameters containing special characters
    public class StringService
    {
        // Method with string default parameter that contains quotes
        public string ProcessString(string input = "Hello \"World\"")
        {
            return $"Processed: {input}";
        }
        
        // Method with path-like default parameter
        public string ProcessPath(string path = @"Some\Path\With\Backslashes")
        {
            return $"Path: {path}";
        }
    }
    
    [DomainBridge(typeof(StringService))]
    public partial class StringServiceBridge { }
    
    public class StringEscapingTests
    {
        [Test]
        public async Task BridgeHandlesStringDefaultsWithQuotes()
        {
            // Arrange
            var service = StringServiceBridge.CreateIsolated();
            
            // Act - call with default parameter
            var resultDefault = service.ProcessString();
            var resultCustom = service.ProcessString("Custom \"Value\"");
            
            // Assert
            await Assert.That(resultDefault).IsEqualTo("Processed: Hello \"World\"");
            await Assert.That(resultCustom).IsEqualTo("Processed: Custom \"Value\"");
            
            // Cleanup
            StringServiceBridge.UnloadDomain();
        }
        
        [Test]
        [DependsOn(nameof(BridgeHandlesStringDefaultsWithQuotes))]
        public async Task BridgeHandlesStringDefaultsWithBackslashes()
        {
            // Arrange
            var service = StringServiceBridge.CreateIsolated();
            
            // Act - call with default parameter
            var resultDefault = service.ProcessPath();
            var resultCustom = service.ProcessPath(@"Another\Path");
            
            // Assert
            await Assert.That(resultDefault).IsEqualTo(@"Path: Some\Path\With\Backslashes");
            await Assert.That(resultCustom).IsEqualTo(@"Path: Another\Path");
            
            // Cleanup
            StringServiceBridge.UnloadDomain();
        }
    }
}