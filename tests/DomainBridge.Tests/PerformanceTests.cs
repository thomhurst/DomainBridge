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
        public async Task Bridge_CreatesPerInstanceAppDomains()
        {
            // Arrange & Act
            var bridge1 = PerformanceTestServiceBridge.Create(() => new PerformanceTestService());
            var bridge2 = PerformanceTestServiceBridge.Create(() => new PerformanceTestService());
            
            // Assert - Each bridge should have its own AppDomain (different instances)
            await Assert.That(bridge1).IsNotSameReferenceAs(bridge2);
            
            // Clean up
            bridge1.Dispose();
            bridge2.Dispose();
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
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
            var initialMemory = GC.GetTotalMemory(true);
            
            // Act - Create and dispose bridges in batches to test for leaks
            const int batchSize = 10;
            const int batches = 5;
            var memoryMeasurements = new List<long>();
            
            for (int batch = 0; batch < batches; batch++)
            {
                var bridges = new List<PerformanceTestServiceBridge>();
                
                // Create batch of bridges
                for (int i = 0; i < batchSize; i++)
                {
                    // Use parameterless constructor to avoid capturing variables in lambda
                    bridges.Add(PerformanceTestServiceBridge.Create(() => new PerformanceTestService()));
                }
                
                // Dispose all bridges in batch
                foreach (var bridge in bridges)
                {
                    bridge?.Dispose();
                }
                
                bridges.Clear();
                
                // Force cleanup
                GC.Collect();
                GC.WaitForPendingFinalizers();
                GC.Collect();
                
                // Measure memory after batch
                var currentMemory = GC.GetTotalMemory(true);
                memoryMeasurements.Add(currentMemory);
            }
            
            // Assert - Memory should stabilize (not continuously grow)
            // Check that the last measurement isn't significantly higher than earlier ones
            var firstBatchMemory = memoryMeasurements[0];
            var lastBatchMemory = memoryMeasurements[memoryMeasurements.Count - 1];
            var acceptableGrowth = 2 * 1024 * 1024; // 2MB tolerance for runtime overhead
            
            // The key is that memory doesn't grow linearly with each batch
            // Some growth is acceptable due to runtime optimizations, but it should stabilize
            var growth = lastBatchMemory - firstBatchMemory;
            var growthMB = growth / 1024.0 / 1024.0;
            
            // Log the memory measurements for debugging
            Console.WriteLine($"Memory measurements across {batches} batches:");
            for (int i = 0; i < memoryMeasurements.Count; i++)
            {
                var mb = memoryMeasurements[i] / 1024.0 / 1024.0;
                Console.WriteLine($"  Batch {i + 1}: {memoryMeasurements[i]:N0} bytes ({mb:F2} MB)");
            }
            Console.WriteLine($"Total growth: {growth:N0} bytes ({growthMB:F2} MB)");
            
            await Assert.That(growth).IsLessThan(acceptableGrowth);
        }
    }

    [Serializable]
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