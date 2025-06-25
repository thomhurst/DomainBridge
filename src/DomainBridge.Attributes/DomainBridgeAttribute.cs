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
        /// Gets the target type to create a bridge for.
        /// </summary>
        public Type? TargetType { get; }

        /// <summary>
        /// Gets or sets whether to automatically process nested types.
        /// </summary>
        public bool IncludeNestedTypes { get; set; } = true;

        /// <summary>
        /// Gets or sets the namespace for generated proxy types.
        /// </summary>
        public string? GeneratedNamespace { get; set; }

        /// <summary>
        /// Initializes a new instance of the DomainBridgeAttribute class.
        /// </summary>
        public DomainBridgeAttribute()
        {
        }

        /// <summary>
        /// Initializes a new instance of the DomainBridgeAttribute class with a target type.
        /// </summary>
        /// <param name="targetType">The type to create a bridge for.</param>
        public DomainBridgeAttribute(Type targetType)
        {
            TargetType = targetType ?? throw new ArgumentNullException(nameof(targetType));
        }
    }
}