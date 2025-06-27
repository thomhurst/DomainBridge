using System;
using System.Threading.Tasks;
using DomainBridge;

namespace DomainBridge.Tests
{
    public interface IExampleService
    {
        string GetMessage();
        int Calculate(int a, int b);
        event EventHandler<string> MessageChanged;
    }

    public interface IDisposableService : IDisposable
    {
        bool IsDisposed { get; }
    }

    public class ExampleService : IExampleService, IDisposableService
    {
        public bool IsDisposed { get; private set; }
        
        public event EventHandler<string>? MessageChanged;

        public string GetMessage()
        {
            return "Hello from ExampleService";
        }

        public int Calculate(int a, int b)
        {
            return a + b;
        }

        public void Dispose()
        {
            IsDisposed = true;
            MessageChanged?.Invoke(this, "Disposed");
        }
    }

    [DomainBridge(typeof(ExampleService))]
    public partial class ExampleServiceBridge
    {
    }

    public class InterfaceImplementationTests
    {
        [Test]
        public async Task Bridge_Should_Implement_Target_Interfaces()
        {
            // Arrange & Act
            var bridge = new ExampleServiceBridge();

            // Assert - Check if bridge implements the interfaces
            await Assert.That(bridge is IExampleService).IsTrue();
            await Assert.That(bridge is IDisposableService).IsTrue();
        }

        [Test]
        public async Task Bridge_Should_Delegate_Interface_Methods()
        {
            // Arrange
            var bridge = new ExampleServiceBridge();
            
            // Act - Cast to interface and call methods
            var service = (IExampleService)bridge;
            var message = service.GetMessage();
            var result = service.Calculate(5, 3);

            // Assert
            await Assert.That(message).IsEqualTo("Hello from ExampleService");
            await Assert.That(result).IsEqualTo(8);
        }

        [Test]
        public async Task Bridge_Should_Handle_Interface_Events()
        {
            // Arrange
            var bridge = new ExampleServiceBridge();
            var service = (IExampleService)bridge;
            string? receivedMessage = null;
            
            // Act
            service.MessageChanged += (sender, msg) => receivedMessage = msg;
            ((IDisposableService)bridge).Dispose();

            // Assert
            await Assert.That(receivedMessage).IsEqualTo("Disposed");
        }

        [Test]
        public async Task Bridge_Should_Work_With_Multiple_Interfaces()
        {
            // Arrange
            var bridge = new ExampleServiceBridge();
            
            // Act - Use methods from both interfaces
            var service = (IExampleService)bridge;
            var disposable = (IDisposableService)bridge;
            
            var message = service.GetMessage();
            disposable.Dispose();
            
            // Assert
            await Assert.That(message).IsEqualTo("Hello from ExampleService");
            await Assert.That(disposable.IsDisposed).IsTrue();
        }

        [Test]
        public async Task Isolated_Bridge_Should_Implement_Interfaces()
        {
            // Arrange & Act
            using (var bridge = ExampleServiceBridge.CreateIsolated())
            {
                // Assert - Check if isolated bridge implements the interfaces
                await Assert.That(bridge is IExampleService).IsTrue();
                await Assert.That(bridge is IDisposableService).IsTrue();
                
                // Use through interface
                var service = (IExampleService)bridge;
                var result = service.Calculate(10, 20);
                await Assert.That(result).IsEqualTo(30);
            }
        }

    }
}