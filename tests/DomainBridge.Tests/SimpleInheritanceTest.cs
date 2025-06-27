using System;
using System.Threading.Tasks;
using TUnit.Assertions;
using TUnit.Core;

namespace DomainBridge.Tests
{
    /// <summary>
    /// Simple test to verify inheritance functionality is working
    /// </summary>
    public class SimpleInheritanceTest
    {
        [Test]
        public async Task TestBasicInheritance()
        {
            // Test DerivedService which inherits from BaseService
            var derived = DerivedServiceBridge.CreateIsolated();
            
            // Test inherited property
            await Assert.That(derived.BaseProperty).IsEqualTo("Base");
            
            // Test inherited method  
            derived.SetBaseData("Inherited Test");
            await Assert.That(derived.BaseProperty).IsEqualTo("Inherited Test");
            
            // Test overridden method
            var message = derived.GetBaseMessage();
            await Assert.That(message).Contains("Overridden:");
            
            // Test derived property
            await Assert.That(derived.DerivedProperty).IsEqualTo("Derived");
        }
        
        [Test]
        public async Task TestAbstractInheritance()
        {
            // Test ConcreteService which inherits from AbstractService
            var concrete = ConcreteServiceBridge.CreateIsolated();
            
            // Test inherited property from abstract base
            await Assert.That(concrete.AbstractProperty).IsEqualTo("Abstract");
            
            // Test implemented abstract method
            var abstractMessage = concrete.GetAbstractMessage();
            await Assert.That(abstractMessage).IsEqualTo("Implemented abstract method");
            
            // Test overridden virtual method
            var virtualMessage = concrete.GetVirtualMessage();
            await Assert.That(virtualMessage).Contains("Overridden:");
        }
        
        [Test]
        public async Task TestDeepInheritance()
        {
            // Test GrandChildService which has 3 levels of inheritance
            var grandChild = GrandChildServiceBridge.CreateIsolated();
            
            // Test properties from all inheritance levels
            await Assert.That(grandChild.GrandChildProperty).IsEqualTo("GrandChild"); // Direct
            await Assert.That(grandChild.ChildProperty).IsEqualTo("Child"); // Parent
            await Assert.That(grandChild.BaseProperty).IsEqualTo("Base"); // Grandparent
            
            // Test method inheritance and overriding
            var message = grandChild.GetBaseMessage();
            await Assert.That(message).IsEqualTo("GrandChild override");
            
            // Test inherited method from middle class
            var childMessage = grandChild.GetChildMessage();
            await Assert.That(childMessage).IsEqualTo("Child message");
        }
    }
}