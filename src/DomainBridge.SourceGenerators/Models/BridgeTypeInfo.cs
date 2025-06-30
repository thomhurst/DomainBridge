using System;
using Microsoft.CodeAnalysis;

namespace DomainBridge.SourceGenerators.Models
{
    /// <summary>
    /// Represents metadata about a type that needs a bridge
    /// </summary>
    internal sealed class BridgeTypeInfo : IEquatable<BridgeTypeInfo>
    {
        public string OriginalFullName { get; }
        public string BridgeFullName { get; }
        public string BridgeNamespace { get; }
        public string BridgeClassName { get; }
        public string FileName { get; }
        public bool IsExplicitlyMarked { get; }
        
        public BridgeTypeInfo(
            INamedTypeSymbol originalType, 
            bool isExplicitlyMarked = false,
            string? explicitBridgeClassName = null,
            string? explicitBridgeNamespace = null)
        {
            if (originalType == null)
            {
                throw new ArgumentNullException(nameof(originalType));
            }

            OriginalFullName = originalType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
            
            // Generate bridge namespace
            var originalNamespace = originalType.ContainingNamespace?.IsGlobalNamespace == true
                ? ""
                : originalType.ContainingNamespace?.ToDisplayString() ?? "";
                
            // For explicitly marked types, use the provided namespace or original namespace
            // For auto-generated types, use the DomainBridge.Generated pattern
            if (isExplicitlyMarked)
            {
                BridgeNamespace = explicitBridgeNamespace ?? originalNamespace;
            }
            else
            {
                // Keep auto-generated bridges in the original namespace
                BridgeNamespace = originalNamespace;
            }
                
            // Use explicit bridge class name if provided, otherwise generate one
            if (!string.IsNullOrEmpty(explicitBridgeClassName))
            {
                BridgeClassName = explicitBridgeClassName!;
            }
            else
            {
                // Handle nested types properly
                var typeName = GetTypeNameWithContainingTypes(originalType);
                BridgeClassName = $"{typeName}Bridge";
            }
            
            BridgeFullName = string.IsNullOrEmpty(BridgeNamespace) ? BridgeClassName : $"{BridgeNamespace}.{BridgeClassName}";
            
            // Generate unique filename based on full type name
            // Replace invalid filename characters with underscores
            var safeFileName = BridgeFullName
                .Replace(".", "_")
                .Replace("<", "_")
                .Replace(">", "_")
                .Replace(":", "_")
                .Replace("/", "_")
                .Replace("\\", "_")
                .Replace("|", "_")
                .Replace("?", "_")
                .Replace("*", "_")
                .Replace("\"", "_")
                .Replace("[", "_")
                .Replace("]", "_")
                .Replace(",", "_")
                .Replace(" ", "_");
            FileName = $"{safeFileName}.g.cs";
            
            IsExplicitlyMarked = isExplicitlyMarked;
        }
        
        public bool Equals(BridgeTypeInfo? other)
        {
            if (other is null)
            {
                return false;
            }
            if (ReferenceEquals(this, other))
            {
                return true;
            }
            return OriginalFullName == other.OriginalFullName;
        }
        
        public override bool Equals(object? obj) => Equals(obj as BridgeTypeInfo);
        
        public override int GetHashCode() => OriginalFullName.GetHashCode();
        
        private static string GetTypeNameWithContainingTypes(INamedTypeSymbol type)
        {
            var parts = new System.Collections.Generic.List<string>();
            var current = type;
            
            while (current != null)
            {
                parts.Insert(0, current.Name);
                current = current.ContainingType;
            }
            
            return string.Join("_", parts);
        }
    }
}