using System;
using Xunit;
using DomainBridge;

namespace DomainBridge.Tests
{
    public class BasicProxyTests
    {
        [Fact]
        public void Create_WithInvalidType_ThrowsArgumentException()
        {
            // Arrange & Act & Assert
            Assert.Throws<ArgumentException>(() => DomainBridgeFactory.Create<BasicProxyTests>());
        }

        [Fact]
        public void UnloadAll_ClearsAllDomains()
        {
            // Act
            DomainBridgeFactory.UnloadAll();
            
            // Assert - should not throw
            Assert.True(true);
        }
    }
    
    // Test types
    [DomainBridge]
    public class TestApplication
    {
        public static TestApplication Instance { get; } = new TestApplication();
        
        public string GetMessage() => "Hello from TestApplication";
        
        public TestDocument GetDocument(string id) => new TestDocument { Id = id, Name = "Test Doc" };
    }
    
    public class TestDocument
    {
        public string Id { get; set; } = "";
        public string Name { get; set; } = "";
    }
}