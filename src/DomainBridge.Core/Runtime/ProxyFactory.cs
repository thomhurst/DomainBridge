using System;
using System.Reflection;

namespace DomainBridge.Runtime
{
    /// <summary>
    /// Factory that runs in the isolated AppDomain to create proxies.
    /// </summary>
    public class ProxyFactory : MarshalByRefObject
    {
        public object CreateProxy(string typeName)
        {
            Type? targetType = null;
            
            // Search for the type in all loaded assemblies
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                targetType = assembly.GetType(typeName);
                if (targetType != null)
                    break;
            }
            
            if (targetType == null)
            {
                // Try to load by assembly-qualified name
                targetType = Type.GetType(typeName);
            }
            
            if (targetType == null)
            {
                throw new TypeLoadException($"Could not find type '{typeName}'");
            }
            
            // Check for static Instance property first
            var instanceProperty = targetType.GetProperty("Instance", 
                BindingFlags.Public | BindingFlags.Static);
                
            if (instanceProperty != null && instanceProperty.CanRead)
            {
                var instance = instanceProperty.GetValue(null);
                if (instance != null)
                {
                    return instance;
                }
            }
            
            // Try to create an instance
            try
            {
                return Activator.CreateInstance(targetType);
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException(
                    $"Could not create instance of type '{typeName}'. " +
                    $"Type must have a public parameterless constructor or a static Instance property.", ex);
            }
        }
        
        public override object? InitializeLifetimeService()
        {
            return null; // Infinite lifetime
        }
    }
}