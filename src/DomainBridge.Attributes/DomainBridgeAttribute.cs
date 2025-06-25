using System;

namespace DomainBridge
{
    /// <summary>
    /// Marks a partial class to generate a bridge for the specified target type, enabling AppDomain isolation.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, Inherited = false)]
    public sealed class DomainBridgeAttribute : Attribute
    {
        /// <summary>
        /// Gets the target type to create a bridge for.
        /// </summary>
        public Type TargetType { get; }

        /// <summary>
        /// Gets or sets whether to automatically process nested types.
        /// </summary>
        public bool IncludeNestedTypes { get; set; } = true;

        /// <summary>
        /// Gets or sets the private bin path for assembly resolution in the isolated AppDomain.
        /// </summary>
        public string? PrivateBinPath { get; set; }

        /// <summary>
        /// Gets or sets the application base directory for the isolated AppDomain.
        /// </summary>
        public string? ApplicationBase { get; set; }

        /// <summary>
        /// Gets or sets the configuration file path for the isolated AppDomain.
        /// </summary>
        public string? ConfigurationFile { get; set; }

        /// <summary>
        /// Gets or sets whether to enable shadow copying of assemblies.
        /// </summary>
        public bool EnableShadowCopy { get; set; }

        /// <summary>
        /// Gets or sets additional assembly search paths (semicolon-separated).
        /// </summary>
        public string? AssemblySearchPaths { get; set; }

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