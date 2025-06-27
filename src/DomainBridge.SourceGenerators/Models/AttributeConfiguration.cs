namespace DomainBridge.SourceGenerators.Models
{
    internal class AttributeConfiguration
    {
        public string? PrivateBinPath { get; set; }
        public string? ApplicationBase { get; set; }
        public string? ConfigurationFile { get; set; }
        public bool EnableShadowCopy { get; set; }
        public string? AssemblySearchPaths { get; set; }
        public string? FactoryMethod { get; set; }
    }
}