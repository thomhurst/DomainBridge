using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;

namespace DomainBridge.Runtime
{
    /// <summary>
    /// Handles custom assembly resolution in isolated AppDomains.
    /// </summary>
    public class AssemblyResolver : MarshalByRefObject
    {
        private readonly List<string> _searchPaths = new List<string>();

        public AssemblyResolver()
        {
            AppDomain.CurrentDomain.AssemblyResolve += OnAssemblyResolve;
        }

        /// <summary>
        /// Adds search paths for assembly resolution.
        /// </summary>
        public void AddSearchPaths(params string[] paths)
        {
            if (paths == null) return;
            
            foreach (var path in paths.Where(p => !string.IsNullOrWhiteSpace(p)))
            {
                var fullPath = Path.GetFullPath(path);
                if (Directory.Exists(fullPath) && !_searchPaths.Contains(fullPath))
                {
                    _searchPaths.Add(fullPath);
                }
            }
        }

        private Assembly? OnAssemblyResolve(object? sender, ResolveEventArgs args)
        {
            var assemblyName = new AssemblyName(args.Name);
            
            // Try to load from search paths
            foreach (var searchPath in _searchPaths)
            {
                var assemblyPath = Path.Combine(searchPath, assemblyName.Name + ".dll");
                if (File.Exists(assemblyPath))
                {
                    try
                    {
                        return Assembly.LoadFrom(assemblyPath);
                    }
                    catch
                    {
                        // Continue to next path
                    }
                }
            }
            
            return null;
        }
    }
}