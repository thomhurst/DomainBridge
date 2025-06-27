using System;
using System.Reflection;

namespace DomainBridge.Runtime
{
    /// <summary>
    /// Factory that runs in the isolated AppDomain to create proxies.
    /// </summary>
    public class ProxyFactory : MarshalByRefObject
    {
        private AssemblyResolver? _assemblyResolver;
        public object CreateProxy(string assemblyName, string typeName)
        {
            // Load the assembly and resolve the type in this AppDomain
            var assembly = Assembly.Load(assemblyName);
            var targetType = assembly.GetType(typeName);
            
            if (targetType == null)
            {
                throw new InvalidOperationException(
                    $"Could not find type '{typeName}' in assembly '{assemblyName}'.");
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
                    $"Could not create instance of type '{targetType}'. " +
                    $"Type must have a public parameterless constructor or a static Instance property.", ex);
            }
        }
        
        /// <summary>
        /// Configures the assembly resolver with additional search paths
        /// </summary>
        public void ConfigureAssemblyResolver(string[] searchPaths)
        {
            if (_assemblyResolver == null)
            {
                _assemblyResolver = new AssemblyResolver();
                // AssemblyResolver constructor already sets up the event handler
            }
            
            _assemblyResolver.AddSearchPaths(searchPaths);
        }
        
        /// <summary>
        /// Creates an instance using a factory function
        /// </summary>
        public T CreateInstance<T>(Func<T> factory) where T : class
        {
            if (factory == null)
                throw new ArgumentNullException(nameof(factory));
                
            return factory();
        }
        
        public override object? InitializeLifetimeService()
        {
            return null; // Infinite lifetime
        }
    }
}