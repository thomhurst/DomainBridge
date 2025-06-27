using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;
using DomainBridge;
using TUnit.Assertions;
using TUnit.Core;

namespace DomainBridge.Tests
{
    /// <summary>
    /// Performance and scalability tests for DomainBridge
    /// </summary>
    public class PerformanceTests
    {
        [Test]
        public async Task BridgeInstanceCache_ReusesInstances()
        {
            // Arrange
            var service = new PerformanceTestService();
            
            // Act
            var bridge1 = new PerformanceTestServiceBridge(service);
            var bridge2 = new PerformanceTestServiceBridge(service);
            
            // Assert - Should reuse cached instances
            await Assert.That(bridge1).IsSameReferenceAs(bridge2);
        }

        [Test]
        public async Task MethodCalls_PerformWithinReasonableTime()
        {
            // Arrange
            var bridge = PerformanceTestServiceBridge.Instance;
            var stopwatch = Stopwatch.StartNew();
            
            // Act - Make 1000 method calls
            for (int i = 0; i < 1000; i++)
            {
                bridge.FastMethod();
            }
            
            stopwatch.Stop();
            
            // Assert - Should complete within reasonable time (adjust threshold as needed)
            await Assert.That(stopwatch.ElapsedMilliseconds).IsLessThan(1000);
        }

        [Test]
        public async Task LargeDataTransfer_HandlesCorrectly()
        {
            // Arrange
            var bridge = PerformanceTestServiceBridge.Instance;
            var largeData = new string('X', 10000); // 10KB string
            
            // Act
            var result = bridge.ProcessLargeData(largeData);
            
            // Assert
            await Assert.That(result.Length).IsEqualTo(10000);
            await Assert.That(result).IsEqualTo(largeData);
        }

        [Test]
        public async Task ConcurrentAccess_HandlesMultipleThreads()
        {
            // Arrange
            var bridge = PerformanceTestServiceBridge.Instance;
            var tasks = new List<Task<int>>();
            
            // Act - Create 10 concurrent tasks
            for (int i = 0; i < 10; i++)
            {
                int taskId = i;
                tasks.Add(Task.Run(() => bridge.GetThreadSafeValue(taskId)));
            }
            
            var results = await Task.WhenAll(tasks);
            
            // Assert - All tasks should complete successfully
            await Assert.That(results.Length).IsEqualTo(10);
            for (int i = 0; i < 10; i++)
            {
                await Assert.That(results[i]).IsEqualTo(i * 2);
            }
        }

        [Test]
        public async Task MemoryUsage_DoesNotLeakWithManyInstances()
        {
            // Arrange
            var initialMemory = GC.GetTotalMemory(true);
            var bridges = new List<PerformanceTestServiceBridge>();
            
            // Act - Create many bridge instances
            for (int i = 0; i < 1000; i++)
            {
                var service = new PerformanceTestService { Id = i };
                bridges.Add(new PerformanceTestServiceBridge(service));
            }
            
            // Force garbage collection
            bridges.Clear();
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
            
            var finalMemory = GC.GetTotalMemory(true);
            var memoryIncrease = finalMemory - initialMemory;
            
            // Assert - Memory increase should be reasonable (less than 10MB)
            await Assert.That(memoryIncrease).IsLessThan(10 * 1024 * 1024);
        }
    }

    public class PerformanceTestService
    {
        public static PerformanceTestService Instance { get; } = new PerformanceTestService();
        
        public int Id { get; set; }
        private readonly object _lock = new object();
        
        public void FastMethod()
        {
            // Simple operation for performance testing
            var result = DateTime.Now.Ticks % 1000;
        }
        
        public string ProcessLargeData(string data)
        {
            // Echo the data back
            return data;
        }
        
        public int GetThreadSafeValue(int input)
        {
            lock (_lock)
            {
                // Simulate some thread-safe processing
                System.Threading.Thread.Sleep(1);
                return input * 2;
            }
        }
    }

    [DomainBridge(typeof(PerformanceTestService))]
    public partial class PerformanceTestServiceBridge { }
}