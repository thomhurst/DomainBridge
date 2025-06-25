using System;

namespace DomainBridge
{
    /// <summary>
    /// Marks a class for automatic proxy generation to enable AppDomain isolation.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, Inherited = false)]
    public sealed class DomainBridgeAttribute : Attribute
    {
        /// <summary>
        /// Gets or sets whether to automatically process nested types.
        /// </summary>
        public bool IncludeNestedTypes { get; set; } = true;

        /// <summary>
        /// Gets or sets the namespace for generated proxy types.
        /// </summary>
        public string? GeneratedNamespace { get; set; }
    }
}