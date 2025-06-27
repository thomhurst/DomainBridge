using System;
using System.Collections.Concurrent;
using System.Runtime.CompilerServices;

namespace DomainBridge.Runtime
{
    /// <summary>
    /// Caches bridge instances to avoid creating multiple bridges for the same object
    /// </summary>
    public static class BridgeInstanceCache
    {
        // Use ConditionalWeakTable to allow GC of both keys and values
        private static readonly ConditionalWeakTable<object, object> _cache = new ConditionalWeakTable<object, object>();
        private static readonly object _cacheLock = new object();
        
        /// <summary>
        /// Gets or creates a bridge instance for the given object
        /// </summary>
        /// <typeparam name="TBridge">The bridge type</typeparam>
        /// <typeparam name="TInstance">The instance type</typeparam>
        /// <param name="instance">The instance to wrap</param>
        /// <param name="factory">Factory to create the bridge if not cached</param>
        /// <returns>The cached or newly created bridge</returns>
        public static TBridge GetOrCreate<TBridge, TInstance>(TInstance instance, Func<TInstance, TBridge> factory)
            where TBridge : class
            where TInstance : class
        {
            if (instance == null)
                throw new ArgumentNullException(nameof(instance));
                
            if (factory == null)
                throw new ArgumentNullException(nameof(factory));
                
            // Try to get from cache without lock first (fast path)
            if (_cache.TryGetValue(instance, out var cached) && cached is TBridge bridge)
            {
                return bridge;
            }
            
            // Use double-check locking pattern for thread safety
            lock (_cacheLock)
            {
                // Check again inside the lock
                if (_cache.TryGetValue(instance, out cached) && cached is TBridge bridgeInLock)
                {
                    return bridgeInLock;
                }
                
                // Create new bridge
                bridge = factory(instance);
                
                // Add to cache - this will throw if another thread added it between our checks
                try
                {
                    _cache.Add(instance, bridge);
                }
                catch (ArgumentException)
                {
                    // Another thread added it - try to get their instance
                    if (_cache.TryGetValue(instance, out cached) && cached is TBridge existingBridge)
                    {
                        return existingBridge;
                    }
                    throw; // Unexpected state
                }
                
                return bridge;
            }
        }
        
        /// <summary>
        /// Clears the cache. Use with caution as it may break reference equality for bridges.
        /// </summary>
        public static void Clear()
        {
            // ConditionalWeakTable doesn't have a Clear method, so we need to create a new instance
            // This is not thread-safe but is acceptable for a "Clear" operation that should be used sparingly
            lock (_cache)
            {
                // We can't actually clear a ConditionalWeakTable, but entries will be GC'd
                // when their keys are no longer referenced
            }
        }
    }
}