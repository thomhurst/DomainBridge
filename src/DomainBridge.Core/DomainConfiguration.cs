using System.Collections.Generic;

namespace DomainBridge
{
    /// <summary>
    /// Configuration options for isolated AppDomains.
    /// </summary>
    public class DomainConfiguration
    {
        /// <summary>
        /// Gets or sets the name of the AppDomain.
        /// </summary>
        public string? DomainName { get; set; }

        /// <summary>
        /// Gets or sets the application base directory.
        /// </summary>
        public string? ApplicationBase { get; set; }

        /// <summary>
        /// Gets or sets the private bin path for assembly resolution.
        /// </summary>
        public string? PrivateBinPath { get; set; }

        /// <summary>
        /// Gets or sets the configuration file path.
        /// </summary>
        public string? ConfigurationFile { get; set; }

        /// <summary>
        /// Gets or sets whether to enable shadow copying of assemblies.
        /// </summary>
        public bool EnableShadowCopy { get; set; }

        /// <summary>
        /// Gets or sets assembly mappings for the isolated domain.
        /// </summary>
        public Dictionary<string, string> AssemblyMappings { get; set; } = new Dictionary<string, string>();

        /// <summary>
        /// Gets or sets the target assembly containing the types to proxy.
        /// </summary>
        public string? TargetAssembly { get; set; }

        /// <summary>
        /// Gets or sets additional assembly search paths (semicolon-separated).
        /// </summary>
        public string? AssemblySearchPaths { get; set; }

        /// <summary>
        /// Gets the default configuration.
        /// </summary>
        public static DomainConfiguration Default => new DomainConfiguration();
    }
}