using System;
using System.Threading.Tasks;
using TUnit.Assertions;
using TUnit.Core;

namespace DomainBridge.Tests
{
    public class InheritanceTests
    {
        [Test]
        public async Task TestDerivedServiceInheritsBaseMembers()
        {
            var service = DerivedServiceBridge.CreateIsolated();
            
            // Reset state to ensure test isolation
            service.BaseProperty = "Base";
            service.DerivedProperty = "Derived";
            
            // Test inherited property from base class
            await Assert.That(service.BaseProperty).IsEqualTo("Base");
            service.BaseProperty = "Modified Base";
            await Assert.That(service.BaseProperty).IsEqualTo("Modified Base");
            
            // Test derived property
            await Assert.That(service.DerivedProperty).IsEqualTo("Derived");
            service.DerivedProperty = "Modified Derived";
            await Assert.That(service.DerivedProperty).IsEqualTo("Modified Derived");
            
            // Test inherited method from base class
            service.SetBaseData("Test Data");
            await Assert.That(service.BaseProperty).IsEqualTo("Test Data");
            
            // Test overridden method (should use derived implementation)
            var message = service.GetBaseMessage();
            await Assert.That(message).IsEqualTo("Overridden: Message from base class");
            
            // Test derived method
            var derivedMessage = service.GetDerivedMessage();
            await Assert.That(derivedMessage).IsEqualTo("Message from derived class");
            
            // Test method that exposes protected functionality
            var protectedData = service.GetProtectedDataPublic();
            await Assert.That(protectedData).IsEqualTo("Protected data");
        }
        
        [Test]
        public async Task TestConcreteServiceInheritsAbstractMembers()
        {
            var service = ConcreteServiceBridge.CreateIsolated();
            
            // Test inherited property from abstract base class
            await Assert.That(service.AbstractProperty).IsEqualTo("Abstract");
            service.AbstractProperty = "Modified Abstract";
            await Assert.That(service.AbstractProperty).IsEqualTo("Modified Abstract");
            
            // Test implemented abstract method
            var abstractMessage = service.GetAbstractMessage();
            await Assert.That(abstractMessage).IsEqualTo("Implemented abstract method");
            
            // Test overridden virtual method
            var virtualMessage = service.GetVirtualMessage();
            await Assert.That(virtualMessage).IsEqualTo("Overridden: Virtual message from abstract base");
            
            // Test concrete method
            var concreteMessage = service.GetConcreteMessage();
            await Assert.That(concreteMessage).IsEqualTo("Message from concrete class");
        }
        
        [Test]
        public async Task TestInheritanceChainWithMultipleLevels()
        {
            // Test a deeper inheritance chain
            var grandChild = GrandChildServiceBridge.CreateIsolated();
            
            // Test properties from all levels of inheritance
            await Assert.That(grandChild.BaseProperty).IsEqualTo("Base");
            await Assert.That(grandChild.ChildProperty).IsEqualTo("Child");
            await Assert.That(grandChild.GrandChildProperty).IsEqualTo("GrandChild");
            
            // Test methods from all levels
            var baseMessage = grandChild.GetBaseMessage();
            await Assert.That(baseMessage).IsEqualTo("GrandChild override");
            
            var childMessage = grandChild.GetChildMessage();
            await Assert.That(childMessage).IsEqualTo("Child message");
            
            var grandChildMessage = grandChild.GetGrandChildMessage();
            await Assert.That(grandChildMessage).IsEqualTo("GrandChild message");
            
            // Test method that uses base functionality
            grandChild.SetBaseData("Deep inheritance test");
            await Assert.That(grandChild.BaseProperty).IsEqualTo("Deep inheritance test");
        }
        
        [Test] 
        public async Task TestVirtualMethodOverrideChain()
        {
            var service = DerivedServiceBridge.CreateIsolated();
            
            // Test that overridden virtual methods work correctly
            var message = service.GetBaseMessage();
            
            // Should get the derived implementation, not the base implementation
            await Assert.That(message).Contains("Overridden:");
            await Assert.That(message).Contains("Message from base class");
            
            // Verify the complete override chain works
            await Assert.That(message).IsEqualTo("Overridden: Message from base class");
        }
        
        [Test]
        public async Task TestInheritedMemberAccessibility()
        {
            var service = DerivedServiceBridge.CreateIsolated();
            
            // Test that only public members are accessible
            // This is a compile-time test - protected members should not be generated
            
            // Ensure we can access public inherited members
            await Assert.That(service.BaseProperty).IsNotNull();
            
            // Ensure we can call inherited public methods
            service.SetBaseData("Accessibility test");
            await Assert.That(service.BaseProperty).IsEqualTo("Accessibility test");
            
            // Protected members should only be accessible through public wrappers
            var protectedData = service.GetProtectedDataPublic();
            await Assert.That(protectedData).IsEqualTo("Protected data");
        }

    }
    
    // Additional test types for deeper inheritance testing
    [Serializable]
    public class ChildService : BaseService
    {
        public string ChildProperty { get; set; } = "Child";
        
        public string GetChildMessage()
        {
            return "Child message";
        }
    }
    
    [Serializable]
    public class GrandChildService : ChildService
    {
        public string GrandChildProperty { get; set; } = "GrandChild";
        
        public override string GetBaseMessage()
        {
            return "GrandChild override";
        }
        
        public string GetGrandChildMessage()
        {
            return "GrandChild message";
        }
    }
    
    [DomainBridge(typeof(GrandChildService))]
    public partial class GrandChildServiceBridge { }
}