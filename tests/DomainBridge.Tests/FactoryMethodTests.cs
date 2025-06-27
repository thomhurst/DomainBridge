using System;
using System.Threading.Tasks;
using DomainBridge;
using TUnit.Assertions;
using TUnit.Core;

namespace DomainBridge.Tests
{
    // Test class that requires constructor parameters
    public class ServiceWithConstructorArgs
    {
        public string ConnectionString { get; }
        public int Timeout { get; }
        
        public ServiceWithConstructorArgs(string connectionString, int timeout)
        {
            ConnectionString = connectionString ?? throw new ArgumentNullException(nameof(connectionString));
            Timeout = timeout;
        }
        
        public string GetInfo()
        {
            return $"Connection: {ConnectionString}, Timeout: {Timeout}";
        }
    }
    
    // Bridge class with factory method
    [DomainBridge(typeof(ServiceWithConstructorArgs), FactoryMethod = nameof(CreateService))]
    public partial class ServiceWithConstructorArgsBridge
    {
        // Factory method to create instances with custom constructor arguments
        private static ServiceWithConstructorArgs CreateService()
        {
            return new ServiceWithConstructorArgs("Server=localhost;Database=Test", 30);
        }
    }
    
    public class FactoryMethodTests
    {
        [Test]
        public async Task CanCreateInstanceUsingFactoryMethod()
        {
            // Act
            var bridge = ServiceWithConstructorArgsBridge.CreateIsolated();
            
            // Assert
            await Assert.That(bridge).IsNotNull();
            var info = bridge.GetInfo();
            await Assert.That(info).IsEqualTo("Connection: Server=localhost;Database=Test, Timeout: 30");
        }
        
        [Test]
        public async Task FactoryMethodWorksWithComplexInitialization()
        {
            // Create a more complex service that requires initialization
            var bridge = ComplexServiceBridge.CreateIsolated();
            
            // Assert
            await Assert.That(bridge).IsNotNull();
            await Assert.That(bridge.IsInitialized).IsTrue();
            await Assert.That(bridge.GetConfiguration()).Contains("Initialized");
        }

        [Test]
        [DependsOn(nameof(CanCreateInstanceUsingFactoryMethod))]
        [DependsOn(nameof(FactoryMethodWorksWithComplexInitialization))]
        public void Cleanup_UnloadDomains()
        {
            // Unload all domains used in this test class
            ServiceWithConstructorArgsBridge.UnloadDomain();
            ComplexServiceBridge.UnloadDomain();
        }
    }
    
    // Complex service requiring initialization
    public class ComplexService
    {
        public bool IsInitialized { get; private set; }
        public string Configuration { get; private set; }
        
        private ComplexService()
        {
            Configuration = "Not initialized";
        }
        
        public static ComplexService CreateAndInitialize()
        {
            var service = new ComplexService();
            service.Initialize();
            return service;
        }
        
        private void Initialize()
        {
            IsInitialized = true;
            Configuration = "Initialized with complex setup";
        }
        
        public string GetConfiguration() => Configuration;
    }
    
    [DomainBridge(typeof(ComplexService), FactoryMethod = nameof(CreateComplexService))]
    public partial class ComplexServiceBridge
    {
        private static ComplexService CreateComplexService()
        {
            return ComplexService.CreateAndInitialize();
        }
    }
}