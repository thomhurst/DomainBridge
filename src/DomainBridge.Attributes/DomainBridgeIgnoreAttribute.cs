using System;

namespace DomainBridge
{
    /// <summary>
    /// Excludes a member from proxy generation.
    /// </summary>
    [AttributeUsage(AttributeTargets.Property | AttributeTargets.Method | AttributeTargets.Event)]
    public sealed class DomainBridgeIgnoreAttribute : Attribute
    {
    }
}