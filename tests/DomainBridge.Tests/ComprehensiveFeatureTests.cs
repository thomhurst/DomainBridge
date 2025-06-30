using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using DomainBridge;
using TUnit.Assertions;
using TUnit.Core;

namespace DomainBridge.Tests
{
    /// <summary>
    /// Comprehensive tests covering all DomainBridge functionality
    /// </summary>
    public class ComprehensiveFeatureTests
    {
        [Test]
        public async Task BasicBridge_CreatesInstance()
        {
            // Act
            using var bridge = TestApplicationBridge.Create(() => new TestApplication());
            
            // Assert
            await Assert.That(bridge).IsNotNull();
        }

        [Test]
        public async Task StaticInstanceProperty_ReturnsValidBridge()
        {
            // Act
            var bridge = TestApplicationBridge.Instance;
            
            // Assert
            await Assert.That(bridge).IsNotNull();
            await Assert.That(bridge.GetMessage()).IsEqualTo("Hello from TestApplication");
        }

        [Test]
        public async Task MethodWrapping_WorksCorrectly()
        {
            // Arrange
            var bridge = TestApplicationBridge.Instance;
            
            // Act
            var message = bridge.GetMessage();
            
            // Assert
            await Assert.That(message).IsEqualTo("Hello from TestApplication");
        }

        [Test]
        public async Task NestedTypeWrapping_CreatesCorrectBridge()
        {
            // Arrange
            var bridge = TestApplicationBridge.Instance;
            
            // Act
            var document = bridge.GetDocument("test-doc");
            
            // Assert
            await Assert.That(document).IsNotNull();
            await Assert.That(document.Id).IsEqualTo("test-doc");
            await Assert.That(document.Name).IsEqualTo("Test Doc");
        }

        [Test]
        public async Task InheritanceBridge_IncludesBaseMembers()
        {
            // Act
            using var bridge = DerivedServiceBridge.Create(() => new DerivedService());
            
            // Act & Assert - Base class members
            bridge.BaseProperty = "test base";
            await Assert.That(bridge.BaseProperty).IsEqualTo("test base");
            
            bridge.SetBaseData("base data");
            var baseMessage = bridge.GetBaseMessage();
            await Assert.That(baseMessage).Contains("Overridden: Message from base class");
            
            // Act & Assert - Derived class members  
            bridge.DerivedProperty = "test derived";
            await Assert.That(bridge.DerivedProperty).IsEqualTo("test derived");
            
            var derivedMessage = bridge.GetDerivedMessage();
            await Assert.That(derivedMessage).Contains("Message from derived class");
        }

        [Test]
        public async Task AbstractImplementation_IncludesAbstractMembers()
        {
            // Act
            using var bridge = ConcreteServiceBridge.Create(() => new ConcreteService());
            
            // Act & Assert - Abstract property
            bridge.AbstractProperty = "abstract test";
            await Assert.That(bridge.AbstractProperty).IsEqualTo("abstract test");
            
            // Act & Assert - Abstract method
            var abstractMessage = bridge.GetAbstractMessage();
            await Assert.That(abstractMessage).Contains("Implemented abstract method");
            
            // Act & Assert - Virtual method (overridden)
            var virtualMessage = bridge.GetVirtualMessage();
            await Assert.That(virtualMessage).Contains("Overridden: Virtual message from abstract base");
            
            // Act & Assert - Concrete method
            var concreteMessage = bridge.GetConcreteMessage();
            await Assert.That(concreteMessage).Contains("Message from concrete class");
        }

        [Test]
        public async Task IsolatedDomain_CreatesSuccessfully()
        {
            // Act
            var isolatedBridge = TestApplicationBridge.Create();
            
            // Assert
            await Assert.That(isolatedBridge).IsNotNull();
            var message = isolatedBridge.GetMessage();
            await Assert.That(message).IsEqualTo("Hello from TestApplication");
        }

        [Test]
        public async Task IsolatedDomain_WithConfiguration_Works()
        {
            // Arrange
            var config = new DomainConfiguration
            {
                EnableShadowCopy = true
            };
            
            // Act
            var isolatedBridge = TestApplicationBridge.Create(config);
            
            // Assert
            await Assert.That(isolatedBridge).IsNotNull();
            var message = isolatedBridge.GetMessage();
            await Assert.That(message).IsEqualTo("Hello from TestApplication");
        }

        [Test]
        public async Task CollectionWrapping_HandlesListReturnTypes()
        {
            // Act
            using var bridge = CollectionTestServiceBridge.Create(() => new CollectionTestService());
            var items = bridge.GetItems();
            
            // Assert
            await Assert.That(items).IsNotNull();
            await Assert.That(items.Count).IsEqualTo(3);
            await Assert.That(items[0].Name).IsEqualTo("Item 1");
        }

        [Test]
        public async Task ExceptionWrapping_HandlesNonSerializableExceptions()
        {
            // Act & Assert
            using var bridge = ErrorTestServiceBridge.Create(() => new ErrorTestService());
            var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            {
                bridge.ThrowNonSerializableException();
                return Task.CompletedTask;
            });
            
            await Assert.That(exception?.Message).Contains("Exception in method ThrowNonSerializableException");
        }

        [Test]
        public async Task AsyncMethod_ReturnsTaskCorrectly()
        {
            // Act
            using var bridge = AsyncTestServiceBridge.Create(() => new AsyncTestService());
            var result = await bridge.GetDataAsync();
            
            // Assert
            await Assert.That(result).IsEqualTo("Async data");
        }

        [Test]
        public async Task AsyncMethod_WithBridgeReturnType_WrapsCorrectly()
        {
            // Act
            using var bridge = AsyncTestServiceBridge.Create(() => new AsyncTestService());
            var documentBridge = await bridge.GetDocumentAsync("async-doc");
            
            // Assert
            await Assert.That(documentBridge).IsNotNull();
            await Assert.That(documentBridge.Id).IsEqualTo("async-doc");
        }

    }

    // Test service classes
    [Serializable]
    public class CollectionTestService
    {
        public List<TestItem> GetItems()
        {
            return new List<TestItem>
            {
                new TestItem { Name = "Item 1" },
                new TestItem { Name = "Item 2" }, 
                new TestItem { Name = "Item 3" }
            };
        }
    }

    [DomainBridge(typeof(CollectionTestService))]
    public partial class CollectionTestServiceBridge { }

    [Serializable]
    public class TestItem
    {
        public string Name { get; set; } = "";
    }

    [Serializable]
    public class ErrorTestService
    {
        public void ThrowNonSerializableException()
        {
            throw new CustomNonSerializableException("This exception is not serializable");
        }
    }

    [DomainBridge(typeof(ErrorTestService))]
    public partial class ErrorTestServiceBridge { }

    // Non-serializable exception for testing
    public class CustomNonSerializableException : Exception
    {
        public CustomNonSerializableException(string message) : base(message) { }
    }

    [Serializable]
    public class AsyncTestService
    {
        public async Task<string> GetDataAsync()
        {
            await Task.Delay(1);
            return "Async data";
        }

        public async Task<TestDocument> GetDocumentAsync(string id)
        {
            await Task.Delay(1);
            return new TestDocument { Id = id, Name = "Async doc" };
        }
    }

    [DomainBridge(typeof(AsyncTestService))]
    public partial class AsyncTestServiceBridge { }
}