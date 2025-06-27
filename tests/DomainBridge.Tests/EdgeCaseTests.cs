using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using DomainBridge;
using DomainBridge.Runtime;
using TUnit.Assertions;
using TUnit.Core;

namespace DomainBridge.Tests
{
    // Test types for edge cases
    [Serializable]
    public class AsyncService
    {
        public async Task<string> GetDataAsync()
        {
            await Task.Delay(10);
            return "Async result";
        }
        
        public async Task DoWorkAsync()
        {
            await Task.Delay(10);
        }
        
        public Task<ComplexData> GetComplexDataAsync()
        {
            return Task.FromResult(new ComplexData { Value = "Complex async" });
        }
    }
    
    [Serializable]
    public class ComplexData
    {
        public string Value { get; set; } = "";
    }
    
    public interface IDataProvider
    {
        string GetData();
        ComplexData GetComplexData();
    }
    
    [Serializable]
    public class DataProvider : IDataProvider
    {
        public string GetData() => "Interface data";
        public ComplexData GetComplexData() => new ComplexData { Value = "Interface complex" };
    }
    
    public class ServiceWithInterfaces
    {
        public IDataProvider GetProvider() => new DataProvider();
    }
    
    [DomainBridge(typeof(AsyncService))]
    public partial class AsyncServiceBridge { }
    
    [DomainBridge(typeof(ServiceWithInterfaces))]
    public partial class ServiceWithInterfacesBridge { }
    
    public class EdgeCaseTests
    {
        [Test]
        public async Task TestThreadSafeCaching()
        {
            // Test that ConditionalWeakTable caching is thread-safe
            var testObject = new ComplexData { Value = "Thread safety test" };
            var bridgeInstances = new ConcurrentBag<object>();
            var factoryCallCount = 0;
            
            // Run multiple threads trying to create bridges for the same instance
            var tasks = Enumerable.Range(0, 10).Select(_ => Task.Run(() =>
            {
                for (int i = 0; i < 100; i++)
                {
                    var bridge = BridgeInstanceCache.GetOrCreate<TestBridge, ComplexData>(
                        testObject, 
                        _ => 
                        {
                            Interlocked.Increment(ref factoryCallCount);
                            return new TestBridge();
                        });
                    bridgeInstances.Add(bridge);
                }
            })).ToArray();
            
            await Task.WhenAll(tasks);
            
            // Verify only one instance was created
            var uniqueInstances = bridgeInstances.Distinct().Count();
            await Assert.That(uniqueInstances).IsEqualTo(1);
            await Assert.That(factoryCallCount).IsEqualTo(1);
        }
        
        [Test]
        public async Task TestAsyncMethodSupport()
        {
            var service = AsyncServiceBridge.CreateIsolated();
            
            // Test async method returning Task<string>
            var result = await service.GetDataAsync();
            await Assert.That(result).IsEqualTo("Async result");
            
            // Test async method returning Task
            await service.DoWorkAsync();
            
            // Test async method returning Task<ComplexType>
            var complexResult = await service.GetComplexDataAsync();
            await Assert.That(complexResult.Value).IsEqualTo("Complex async");
            
            AsyncServiceBridge.UnloadDomain();
        }
        
        [Test, Skip("Interface proxy support not yet implemented")]
        public async Task TestInterfaceReturnTypes()
        {
            var service = ServiceWithInterfacesBridge.CreateIsolated();
            
            // This should return a proxy that implements IDataProvider
            var provider = service.GetProvider();
            await Assert.That(provider).IsNotNull();
            await Assert.That(provider).IsAssignableTo<IDataProvider>();
            
            // Should be able to call interface methods
            var data = provider.GetData();
            await Assert.That(data).IsEqualTo("Interface data");
            
            var complexData = provider.GetComplexData();
            await Assert.That(complexData.Value).IsEqualTo("Interface complex");
            
            ServiceWithInterfacesBridge.UnloadDomain();
        }
        
        [Test]
        public void TestLargeObjectGraphs()
        {
            // Create a deeply nested structure
            var depth = 100;
            NestedData? current = null;
            for (int i = 0; i < depth; i++)
            {
                current = new NestedData { Value = $"Level {i}", Inner = current };
            }
            
            // This should not cause stack overflow during serialization  
            var service = new NestedDataServiceBridge(new NestedDataService());
            var nestedDataBridge = new global::DomainBridge.Generated.DomainBridge.Tests.NestedDataBridge(current!);
            service.ProcessNestedData(nestedDataBridge);
            
            // If we get here, serialization succeeded
            // No explicit assertion needed - test passes if no exception
            
            NestedDataServiceBridge.UnloadDomain();
        }
        
        [Test]
        public async Task TestStaticFieldsDoNotPreventUnloading()
        {
            // Create and use a bridge
            var service1 = StaticFieldTestBridge.CreateIsolated();
            var data1 = service1.GetData();
            await Assert.That((object)data1).IsEqualTo("Static test");
            
            // Get the AppDomain reference before unloading
            var domainId1 = service1.GetAppDomainId();
            
            // Unload the domain
            StaticFieldTestBridge.UnloadDomain();
            
            // Wait a bit for cleanup
            await Task.Delay(100);
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
            
            // Create a new instance - should create a new AppDomain
            var service2 = StaticFieldTestBridge.CreateIsolated();
            var domainId2 = service2.GetAppDomainId();
            
            // Verify we got a new AppDomain (different ID)
            await Assert.That(domainId2).IsNotEqualTo(domainId1);
            
            StaticFieldTestBridge.UnloadDomain();
        }
    }
    
    // Helper types for testing
    internal class TestBridge : MarshalByRefObject
    {
        public Guid Id { get; } = Guid.NewGuid();
    }
    
    [Serializable]
    public class NestedData
    {
        public string Value { get; set; } = "";
        public NestedData? Inner { get; set; }
    }
    
    public class NestedDataService
    {
        public void ProcessNestedData(NestedData data)
        {
            // Just accept the data - if serialization works, we're good
        }
    }
    
    [DomainBridge(typeof(NestedDataService))]
    public partial class NestedDataServiceBridge { }
    
    public class StaticFieldTest
    {
        public string GetData() => "Static test";
        public int GetAppDomainId() => AppDomain.CurrentDomain.Id;
    }
    
    [DomainBridge(typeof(StaticFieldTest))]
    public partial class StaticFieldTestBridge { }
}