using System;
using Xunit;
using DomainBridge;

namespace DomainBridge.Tests
{
    public class BasicBridgeTests
    {
        [Fact]
        public void BridgeInstance_ReturnsNotNull()
        {
            // Act
            var app = TestApplicationBridge.Instance;
            
            // Assert
            Assert.NotNull(app);
        }
        
        [Fact]
        public void BridgeInstance_ReturnsSameInstance()
        {
            // Act
            var app1 = TestApplicationBridge.Instance;
            var app2 = TestApplicationBridge.Instance;
            
            // Assert
            Assert.Same(app1, app2);
        }
        
        [Fact]
        public void BridgeMethod_ReturnsExpectedValue()
        {
            // Arrange
            var app = TestApplicationBridge.Instance;
            
            // Act
            var message = app.GetMessage();
            
            // Assert
            Assert.Equal("Hello from TestApplication", message);
        }
        
        [Fact]
        public void BridgeMethod_ReturnsNestedBridge()
        {
            // Arrange
            var app = TestApplicationBridge.Instance;
            
            // Act
            var doc = app.GetDocument("test-id");
            
            // Assert
            Assert.NotNull(doc);
            Assert.Equal("test-id", doc.Id);
            Assert.Equal("Test Doc", doc.Name);
        }
        
        [Fact]
        public void CreateIsolated_WithConfig_Works()
        {
            // Arrange
            var config = new DomainConfiguration
            {
                EnableShadowCopy = true
            };
            
            // Act
            var app = TestApplicationBridge.CreateIsolated(config);
            
            // Assert
            Assert.NotNull(app);
            var message = app.GetMessage();
            Assert.Equal("Hello from TestApplication", message);
        }
        
        [Fact]
        public void UnloadDomain_DoesNotThrow()
        {
            // Arrange - ensure domain is loaded
            var app = TestApplicationBridge.Instance;
            
            // Act & Assert - should not throw
            TestApplicationBridge.UnloadDomain();
        }
    }
    
    // Bridge for testing
    [DomainBridge(typeof(TestApplication))]
    public partial class TestApplicationBridge
    {
        // Source generator will create all members
    }
    
    // Test target type
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