using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using DomainBridge;
using TUnit.Assertions;
using TUnit.Core;

namespace DomainBridge.Tests
{
    /// <summary>
    /// Tests for indexer support across AppDomain boundaries
    /// </summary>
    public class IndexerTests
    {
        [Test]
        public async Task BasicIndexer_WorksAcrossAppDomains()
        {
            // Arrange
            using var bridge = IndexerServiceBridge.Create(() => new IndexerService());
            
            // Act - Write values using indexer
            bridge[0] = "First";
            bridge[1] = "Second";
            bridge[2] = "Third";
            
            // Assert - Read values using indexer
            await Assert.That(bridge[0]).IsEqualTo("First");
            await Assert.That(bridge[1]).IsEqualTo("Second");
            await Assert.That(bridge[2]).IsEqualTo("Third");
        }
        
        [Test]
        public async Task IndexerWithMultipleParameters_WorksCorrectly()
        {
            // Arrange
            using var bridge = GridServiceBridge.Create(() => new GridService());
            
            // Act - Set values in 2D grid
            bridge[0, 0] = "TopLeft";
            bridge[1, 1] = "Center";
            bridge[2, 2] = "BottomRight";
            
            // Assert
            await Assert.That(bridge[0, 0]).IsEqualTo("TopLeft");
            await Assert.That(bridge[1, 1]).IsEqualTo("Center");
            await Assert.That(bridge[2, 2]).IsEqualTo("BottomRight");
        }
        
        [Test]
        public async Task IndexerWithDifferentTypes_HandlesCorrectly()
        {
            // Arrange
            using var bridge = TypedIndexerServiceBridge.Create(() => new TypedIndexerService());
            
            // Act & Assert - String key indexer
            bridge["key1"] = 100;
            bridge["key2"] = 200;
            await Assert.That(bridge["key1"]).IsEqualTo(100);
            await Assert.That(bridge["key2"]).IsEqualTo(200);
        }
        
        [Test]
        public async Task ReadOnlyIndexer_WorksCorrectly()
        {
            // Arrange
            using var bridge = ReadOnlyIndexerServiceBridge.Create(() => new ReadOnlyIndexerService());
            
            // Act & Assert - Can read from read-only indexer
            await Assert.That(bridge[0]).IsEqualTo("Item0");
            await Assert.That(bridge[5]).IsEqualTo("Item5");
        }
    }
    
    [Serializable]
    public class IndexerService
    {
        private string[] items = new string[10];
        
        public string this[int index]
        {
            get { return items[index] ?? $"Empty{index}"; }
            set { items[index] = value; }
        }
    }
    
    [DomainBridge(typeof(IndexerService))]
    public partial class IndexerServiceBridge { }
    
    [Serializable]
    public class GridService
    {
        private string[,] grid = new string[5, 5];
        
        public string this[int x, int y]
        {
            get { return grid[x, y] ?? $"Empty[{x},{y}]"; }
            set { grid[x, y] = value; }
        }
    }
    
    [DomainBridge(typeof(GridService))]
    public partial class GridServiceBridge { }
    
    [Serializable]
    public class TypedIndexerService
    {
        private Dictionary<string, int> data = new Dictionary<string, int>();
        
        public int this[string key]
        {
            get { return data.ContainsKey(key) ? data[key] : 0; }
            set { data[key] = value; }
        }
    }
    
    [DomainBridge(typeof(TypedIndexerService))]
    public partial class TypedIndexerServiceBridge { }
    
    [Serializable]
    public class ReadOnlyIndexerService
    {
        public string this[int index]
        {
            get { return $"Item{index}"; }
        }
    }
    
    [DomainBridge(typeof(ReadOnlyIndexerService))]
    public partial class ReadOnlyIndexerServiceBridge { }
}