using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;

namespace DomainBridge
{
    /// <summary>
    /// Bootstrap class that runs in the isolated AppDomain.
    /// </summary>
    internal class DomainBootstrap : MarshalByRefObject
    {
        private DomainConfiguration _config = null!;
        private readonly Dictionary<string, Assembly> _loadedAssemblies = new Dictionary<string, Assembly>();
        private readonly Dictionary<Type, object> _singletonProxies = new Dictionary<Type, object>();

        public void Initialize(DomainConfiguration config)
        {
            _config = config;
            
            // Set up assembly resolution
            AppDomain.CurrentDomain.AssemblyResolve += OnAssemblyResolve;
        }

        public T CreateProxy<T>() where T : class
        {
            var interfaceType = typeof(T);
            
            // Check if we already have a singleton proxy
            if (_singletonProxies.TryGetValue(interfaceType, out var existing))
            {
                return (T)existing;
            }

            // Find the proxy type
            var proxyTypeName = $"DomainBridge.Generated.{interfaceType.Name.Substring(1)}Proxy";
            var proxyType = FindType(proxyTypeName);
            
            if (proxyType == null)
            {
                throw new InvalidOperationException($"Proxy type {proxyTypeName} not found. Ensure the source generator has run.");
            }

            // Find the target type
            var targetTypeName = interfaceType.Name.Substring(1); // Remove 'I' prefix
            var targetType = FindTargetType(targetTypeName);
            
            if (targetType == null)
            {
                throw new InvalidOperationException($"Target type {targetTypeName} not found.");
            }

            // Create instance of the target type
            object targetInstance;
            
            // Check for static Instance property
            var instanceProperty = targetType.GetProperty("Instance", BindingFlags.Public | BindingFlags.Static);
            if (instanceProperty != null)
            {
                targetInstance = instanceProperty.GetValue(null);
            }
            else
            {
                // Try to create instance
                targetInstance = Activator.CreateInstance(targetType);
            }

            // Create the proxy
            var proxy = Activator.CreateInstance(
                proxyType, 
                BindingFlags.NonPublic | BindingFlags.Instance, 
                null, 
                new[] { targetInstance }, 
                null);

            if (proxy == null)
            {
                throw new InvalidOperationException($"Failed to create proxy of type {proxyType.Name}");
            }

            _singletonProxies[interfaceType] = proxy;
            return (T)proxy;
        }

        private Type? FindType(string typeName)
        {
            // Search in all loaded assemblies
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                var type = assembly.GetType(typeName);
                if (type != null) return type;
            }

            // Try to load from generated assembly
            var generatedAssemblyName = "DomainBridge.Generated";
            if (!_loadedAssemblies.ContainsKey(generatedAssemblyName))
            {
                try
                {
                    var assembly = Assembly.Load(generatedAssemblyName);
                    _loadedAssemblies[generatedAssemblyName] = assembly;
                    
                    var type = assembly.GetType(typeName);
                    if (type != null) return type;
                }
                catch
                {
                    // Assembly might not exist
                }
            }

            return null;
        }

        private Type? FindTargetType(string typeName)
        {
            // If target assembly is specified, load it
            if (!string.IsNullOrEmpty(_config.TargetAssembly))
            {
                if (!_loadedAssemblies.TryGetValue(_config.TargetAssembly, out var targetAssembly))
                {
                    targetAssembly = Assembly.Load(_config.TargetAssembly);
                    _loadedAssemblies[_config.TargetAssembly] = targetAssembly;
                }

                // Search in target assembly namespace
                var types = targetAssembly.GetTypes().Where(t => t.Name == typeName);
                return types.FirstOrDefault();
            }

            // Search in all assemblies
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                var type = assembly.GetTypes().FirstOrDefault(t => t.Name == typeName);
                if (type != null) return type;
            }

            return null;
        }

        private Assembly? OnAssemblyResolve(object? sender, ResolveEventArgs args)
        {
            var assemblyName = new AssemblyName(args.Name);
            
            // Check assembly mappings
            if (_config.AssemblyMappings.TryGetValue(assemblyName.Name, out var mappedPath))
            {
                if (File.Exists(mappedPath))
                {
                    return Assembly.LoadFrom(mappedPath);
                }
            }

            // Check private bin path
            if (!string.IsNullOrEmpty(_config.PrivateBinPath))
            {
                var paths = _config.PrivateBinPath.Split(';');
                foreach (var path in paths)
                {
                    var fullPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, path, $"{assemblyName.Name}.dll");
                    if (File.Exists(fullPath))
                    {
                        return Assembly.LoadFrom(fullPath);
                    }
                }
            }

            return null;
        }

        public override object? InitializeLifetimeService()
        {
            return null; // Infinite lifetime
        }
    }
}