using System;
using System.Threading.Tasks;
using DomainBridge;
using TUnit.Assertions;
using TUnit.Core;

namespace DomainBridge.Tests
{
    // Simple interface with just one method
    public interface IMinimalService
    {
        string GetData();
    }

    // Implementation class
    [Serializable]
    public class MinimalServiceImpl : IMinimalService
    {
        public string GetData()
        {
            return "Test Data";
        }
    }

    // Bridge class with DomainBridge attribute
    [DomainBridge(typeof(MinimalServiceImpl))]
    public partial class MinimalServiceBridge
    {
    }

    /// <summary>
    /// Minimal test to reproduce interface implementation error
    /// </summary>
    public class MinimalInterfaceTest
    {
        [Test]
        public async Task MinimalTest_Shows_Interface_Implementation_Works()
        {
            // This test demonstrates that interface implementation works correctly
            // The generator now correctly uses the partial class name
            
            // Create the bridge using the correct class name:
            using var bridge = MinimalServiceBridge.Create();
            
            // Check if the generated bridge implements the interface
            await Assert.That(bridge is IMinimalService).IsTrue();
            
            // Cast to interface and call method
            var service = (IMinimalService)bridge;
            var data = service.GetData();
            
            await Assert.That(data).IsEqualTo("Test Data");
        }
    }
}