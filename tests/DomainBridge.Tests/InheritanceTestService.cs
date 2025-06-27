using System;
using DomainBridge;

namespace DomainBridge.Tests
{
    /// <summary>
    /// Base class with methods and properties that should be inherited
    /// </summary>
    [Serializable]
    public class BaseService
    {
        public virtual string BaseProperty { get; set; } = "Base";
        
        public virtual string GetBaseMessage()
        {
            return "Message from base class";
        }
        
        public virtual void SetBaseData(string data)
        {
            BaseProperty = data;
        }
        
        protected virtual string GetProtectedData()
        {
            return "Protected data";
        }
    }
    
    /// <summary>
    /// Derived class that should expose base class members through bridge
    /// </summary>
    [Serializable]
    public class DerivedService : BaseService
    {
        public string DerivedProperty { get; set; } = "Derived";
        
        public override string GetBaseMessage()
        {
            return "Overridden: " + base.GetBaseMessage();
        }
        
        public string GetDerivedMessage()
        {
            return "Message from derived class";
        }
        
        // This should expose the protected method through a public method
        public string GetProtectedDataPublic()
        {
            return GetProtectedData();
        }
    }
    
    /// <summary>
    /// Abstract base class test
    /// </summary>
    [Serializable]
    public abstract class AbstractService
    {
        public string AbstractProperty { get; set; } = "Abstract";
        
        public abstract string GetAbstractMessage();
        
        public virtual string GetVirtualMessage()
        {
            return "Virtual message from abstract base";
        }
    }
    
    /// <summary>
    /// Concrete implementation of abstract class
    /// </summary>
    [Serializable]
    public class ConcreteService : AbstractService
    {
        public override string GetAbstractMessage()
        {
            return "Implemented abstract method";
        }
        
        public override string GetVirtualMessage()
        {
            return "Overridden: " + base.GetVirtualMessage();
        }
        
        public string GetConcreteMessage()
        {
            return "Message from concrete class";
        }
    }
    
    [DomainBridge(typeof(DerivedService))]
    public partial class DerivedServiceBridge { }
    
    [DomainBridge(typeof(ConcreteService))]
    public partial class ConcreteServiceBridge { }
}