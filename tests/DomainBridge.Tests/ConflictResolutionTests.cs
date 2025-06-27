using System;
using System.Threading.Tasks;
using DomainBridge;
using TUnit.Assertions;
using TUnit.Core;

namespace DomainBridge.Tests.ConflictScenarios
{
    // These tests demonstrate the global conflict resolution in the source generator.
    // When multiple bridge classes reference types with the same names (even from 
    // different namespaces), the generator automatically resolves naming conflicts
    // by appending namespace prefixes or counters to ensure unique file names.
    // Scenario 1: Same type names in different namespaces
    namespace ScenarioA
    {
        public class Result
        {
            public string Value { get; set; } = "Result from A";
        }
        
        public class Service
        {
            public Result GetResult() => new Result();
        }
    }
    
    namespace ScenarioB
    {
        public class Result
        {
            public string Value { get; set; } = "Result from B";
        }
        
        public class Service
        {
            public Result GetResult() => new Result();
        }
    }
    
    // Bridge for Service A
    [DomainBridge(typeof(ScenarioA.Service))]
    public partial class ServiceABridge { }
    
    // Bridge for Service B
    [DomainBridge(typeof(ScenarioB.Service))]
    public partial class ServiceBBridge { }
}

namespace DomainBridge.Tests
{
    public class ConflictResolutionTests
    {
        [Test]
        public async Task HandlesConflictingTypeNamesFromDifferentNamespaces()
        {
            // Both services return a type called "Result" but from different namespaces
            // The generator should create unique bridge names for each
            
            var serviceA = ConflictScenarios.ServiceABridge.CreateIsolated();
            var resultA = serviceA.GetResult();
            await Assert.That(resultA.Value).IsEqualTo("Result from A");
            
            var serviceB = ConflictScenarios.ServiceBBridge.CreateIsolated();
            var resultB = serviceB.GetResult();
            await Assert.That(resultB.Value).IsEqualTo("Result from B");
            
            // Clean up
            ConflictScenarios.ServiceABridge.UnloadDomain();
            ConflictScenarios.ServiceBBridge.UnloadDomain();
        }
    }
    
    // Scenario 2: Nested classes with same names
    public class Order
    {
        public class Item
        {
            public string Name { get; set; } = "OrderItem";
        }
        
        public Item GetItem() => new Item();
    }
    
    public class Invoice
    {
        public class Item
        {
            public string Name { get; set; } = "InvoiceItem";
        }
        
        public Item GetItem() => new Item();
    }
    
    [DomainBridge(typeof(Order))]
    public partial class OrderBridge { }
    
    [DomainBridge(typeof(Invoice))]
    public partial class InvoiceBridge { }
    
    public class NestedTypeConflictTests
    {
        [Test]
        public async Task HandlesNestedTypesWithSameNames()
        {
            var order = OrderBridge.CreateIsolated();
            var orderItem = order.GetItem();
            await Assert.That(orderItem.Name).IsEqualTo("OrderItem");
            
            var invoice = InvoiceBridge.CreateIsolated();
            var invoiceItem = invoice.GetItem();
            await Assert.That(invoiceItem.Name).IsEqualTo("InvoiceItem");
            
            // Clean up
            OrderBridge.UnloadDomain();
            InvoiceBridge.UnloadDomain();
        }
    }
}