using System;
using System.Linq;
using Microsoft.CodeAnalysis;

namespace DomainBridge.SourceGenerators.Services
{
    /// <summary>
    /// Determines which types should have bridges generated
    /// </summary>
    internal sealed class TypeFilter
    {
        private const string DomainBridgeIgnoreAttribute = "DomainBridge.DomainBridgeIgnoreAttribute";
        private static readonly string[] SystemAssemblyPrefixes = { "System", "Microsoft", "Windows", "mscorlib", "netstandard" };
        private readonly GeneratorExecutionContext? _context;
        
        public TypeFilter(GeneratorExecutionContext? context = null)
        {
            _context = context;
        }
        
        /// <summary>
        /// Determines if a bridge should be generated for the given type
        /// </summary>
        public bool ShouldGenerateBridge(ITypeSymbol type, Location? location = null)
        {
            // Only process named types (classes and interfaces)
            if (type is not INamedTypeSymbol namedType)
            {
                return false;
            }

            // Skip special types
            if (type.SpecialType != SpecialType.None)
            {
                return false;
            }

            // Skip value types (structs, enums) - report diagnostic if context available
            if (type.IsValueType)
            {
                if (_context.HasValue && location != null && type.IsRefLikeType)
                {
                    _context.Value.ReportDiagnostic(DiagnosticsHelper.CreateUnbridgeableTypeDiagnostic(type, location));
                }
                return false;
            }
                
            // Skip static classes
            if (type.IsStatic)
            {
                return false;
            }

            // Skip abstract classes and interfaces (can't be instantiated)
            if (type.IsAbstract || type.TypeKind == TypeKind.Interface)
            {
                return false;
            }

                
            // Skip types already inheriting from MarshalByRefObject - report diagnostic if context available
            if (InheritsFromMarshalByRefObject(type))
            {
                if (_context.HasValue && location != null)
                {
                    _context.Value.ReportDiagnostic(DiagnosticsHelper.CreateUnbridgeableTypeDiagnostic(type, location));
                }
                return false;
            }
                
            // Skip types marked with [Serializable] - they can cross AppDomain boundaries directly
            if (IsSerializable(type))
            {
                return false;
            }

            // Skip types with the ignore attribute
            if (HasIgnoreAttribute(type))
            {
                return false;
            }

            // Skip system types unless explicitly configured
            if (IsSystemType(type))
            {
                return false;
            }

            // Skip generic type definitions (we'll handle constructed types)
            if (namedType.IsUnboundGenericType)
            {
                return false;
            }

            return true;
        }
        
        private bool CanProxySealed(ITypeSymbol type)
        {
            // We now support bridging sealed types by inheriting from MarshalByRefObject directly
            // instead of inheriting from the target type
            return true;
        }
        
        private bool InheritsFromMarshalByRefObject(ITypeSymbol type)
        {
            var baseType = type.BaseType;
            while (baseType != null)
            {
                if (baseType.ToDisplayString() == "System.MarshalByRefObject")
                {
                    return true;
                }
                baseType = baseType.BaseType;
            }
            return false;
        }
        
        private bool HasIgnoreAttribute(ITypeSymbol type)
        {
            return type.GetAttributes().Any(attr =>
                attr.AttributeClass?.ToDisplayString() == DomainBridgeIgnoreAttribute);
        }
        
        private bool IsSerializable(ITypeSymbol type)
        {
            // Check if the type has the [Serializable] attribute
            return type.GetAttributes().Any(attr =>
                attr.AttributeClass?.ToDisplayString() == "System.SerializableAttribute");
        }
        
        private bool IsSystemType(ITypeSymbol type)
        {
            var assemblyName = type.ContainingAssembly?.Name;
            
            if (string.IsNullOrEmpty(assemblyName))
            {
                return false;
            }

            return SystemAssemblyPrefixes.Any(prefix => 
                assemblyName!.StartsWith(prefix, StringComparison.OrdinalIgnoreCase));
        }
    }
}