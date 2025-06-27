using System;
using DomainBridge;
using System.Threading.Tasks;

namespace DomainBridge.Tests
{
    public class BasicBridgeTests
    {
        [Test]
        public async Task BridgeInstance_ReturnsNotNull()
        {
            // Act
            var app = TestApplicationBridge.Instance;

            // Assert
            await Assert.That(app).IsNotNull();
        }

        [Test]
        public async Task BridgeInstance_ReturnsSameInstance()
        {
            // Act
            var app1 = TestApplicationBridge.Instance;
            var app2 = TestApplicationBridge.Instance;

            // Assert
            await Assert.That(app1).IsSameReferenceAs(app2);
        }

        [Test]
        public async Task BridgeMethod_ReturnsExpectedValue()
        {
            // Arrange
            var app = TestApplicationBridge.Instance;

            // Act
            var message = app.GetMessage();

            // Assert
            await Assert.That(message).IsEqualTo("Hello from TestApplication");
        }

        [Test]
        public async Task BridgeMethod_ReturnsNestedBridge()
        {
            // Arrange
            var app = TestApplicationBridge.Instance;

            // Act
            var doc = app.GetDocument("test-id");

            // Assert
            await Assert.That(doc).IsNotNull();
            await Assert.That(doc.Id).IsEqualTo("test-id");
            await Assert.That(doc.Name).IsEqualTo("Test Doc");
        }

        [Test]
        public async Task CreateIsolated_WithConfig_Works()
        {
            // Arrange
            var config = new DomainConfiguration
            {
                EnableShadowCopy = true
            };

            // Act
            var app = TestApplicationBridge.CreateIsolated(config);

            // Assert
            await Assert.That(app).IsNotNull();
            var message = app.GetMessage();
            await Assert.That(message).IsEqualTo("Hello from TestApplication");
        }

        [Test]
        [DependsOn(nameof(CreateIsolated_WithConfig_Works))]
        [DependsOn(nameof(BridgeInstance_ReturnsNotNull))]
        [DependsOn(nameof(BridgeInstance_ReturnsSameInstance))]
        [DependsOn(nameof(BridgeMethod_ReturnsExpectedValue))]
        [DependsOn(nameof(BridgeMethod_ReturnsNestedBridge))]
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
    
    [Serializable]
    public class TestDocument
    {
        public string Id { get; set; } = "";
        public string Name { get; set; } = "";
    }
}