using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;

namespace DomainBridge
{
    /// <summary>
    /// Factory for creating isolated proxies across AppDomain boundaries.
    /// </summary>
    public static class DomainBridgeFactory
    {
        private static readonly Dictionary<string, IsolatedDomain> _domains = new Dictionary<string, IsolatedDomain>();
        private static readonly object _lock = new object();

        /// <summary>
        /// Creates an isolated proxy for the specified interface type.
        /// </summary>
        /// <typeparam name="T">The interface type to create a proxy for.</typeparam>
        /// <param name="config">Optional configuration for the isolated domain.</param>
        /// <returns>A proxy implementing the specified interface.</returns>
        public static T Create<T>(DomainConfiguration? config = null) where T : class
        {
            var interfaceType = typeof(T);
            if (!interfaceType.IsInterface)
            {
                throw new ArgumentException($"Type {interfaceType.Name} must be an interface.", nameof(T));
            }

            config = config ?? DomainConfiguration.Default;
            
            lock (_lock)
            {
                var domainKey = config.DomainName ?? interfaceType.Assembly.FullName ?? "DefaultDomain";
                
                if (!_domains.TryGetValue(domainKey, out var isolatedDomain))
                {
                    isolatedDomain = new IsolatedDomain(domainKey, config);
                    _domains[domainKey] = isolatedDomain;
                }

                return isolatedDomain.CreateProxy<T>();
            }
        }

        /// <summary>
        /// Unloads all isolated domains.
        /// </summary>
        public static void UnloadAll()
        {
            lock (_lock)
            {
                foreach (var domain in _domains.Values)
                {
                    domain.Dispose();
                }
                _domains.Clear();
            }
        }

        /// <summary>
        /// Unloads a specific isolated domain.
        /// </summary>
        /// <param name="domainName">The name of the domain to unload.</param>
        public static void Unload(string domainName)
        {
            lock (_lock)
            {
                if (_domains.TryGetValue(domainName, out var domain))
                {
                    domain.Dispose();
                    _domains.Remove(domainName);
                }
            }
        }
    }

    internal class IsolatedDomain : IDisposable
    {
        private readonly AppDomain _appDomain;
        private readonly DomainBootstrap _bootstrap;
        private bool _disposed;

        public IsolatedDomain(string name, DomainConfiguration config)
        {
            var setup = new AppDomainSetup
            {
                ApplicationBase = config.ApplicationBase ?? AppDomain.CurrentDomain.BaseDirectory,
                PrivateBinPath = config.PrivateBinPath,
                ConfigurationFile = config.ConfigurationFile,
                ShadowCopyFiles = config.EnableShadowCopy ? "true" : "false"
            };

            _appDomain = AppDomain.CreateDomain(name, null, setup);
            
            _bootstrap = (DomainBootstrap)_appDomain.CreateInstanceAndUnwrap(
                Assembly.GetExecutingAssembly().FullName,
                typeof(DomainBootstrap).FullName);
            
            _bootstrap.Initialize(config);
        }

        public T CreateProxy<T>() where T : class
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(IsolatedDomain));
                
            return _bootstrap.CreateProxy<T>();
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                try
                {
                    AppDomain.Unload(_appDomain);
                }
                catch (Exception ex)
                {
                    // Log error
                    System.Diagnostics.Debug.WriteLine($"Failed to unload AppDomain: {ex.Message}");
                }
                _disposed = true;
            }
        }
    }
}