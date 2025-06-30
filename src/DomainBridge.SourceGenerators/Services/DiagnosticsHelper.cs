using System;
using Microsoft.CodeAnalysis;

namespace DomainBridge.SourceGenerators.Services
{
    /// <summary>
    /// Helper class for creating diagnostics for the DomainBridge source generator
    /// </summary>
    internal static class DiagnosticsHelper
    {
        // Diagnostic descriptors for various scenarios
        
        /// <summary>
        /// DBG200: Type cannot be bridged - sealed class
        /// </summary>
        public static readonly DiagnosticDescriptor SealedClassCannotBeBridged = new(
            "DBG200",
            "Sealed classes cannot be bridged",
            "Type '{0}' is sealed and cannot be bridged across AppDomain boundaries. Consider using an interface or unsealed base class.",
            "DomainBridge",
            DiagnosticSeverity.Error,
            true,
            "Sealed classes cannot be proxied because the bridge needs to inherit from MarshalByRefObject. Consider extracting an interface or using an unsealed base class.");
            
        /// <summary>
        /// DBG201: Type cannot be bridged - value type
        /// </summary>
        public static readonly DiagnosticDescriptor ValueTypeCannotBeBridged = new(
            "DBG201",
            "Value types cannot be bridged",
            "Type '{0}' is a value type (struct/enum) and cannot be bridged. Value types are passed by value across AppDomain boundaries.",
            "DomainBridge",
            DiagnosticSeverity.Info,
            true,
            "Value types (structs and enums) are automatically copied across AppDomain boundaries and don't need bridging.");
            
        /// <summary>
        /// DBG202: Type cannot be bridged - ref struct
        /// </summary>
        public static readonly DiagnosticDescriptor RefStructCannotBeBridged = new(
            "DBG202",
            "Ref structs cannot be bridged",
            "Type '{0}' is a ref struct and cannot be bridged or marshaled across AppDomain boundaries.",
            "DomainBridge",
            DiagnosticSeverity.Error,
            true,
            "Ref structs are stack-only types that cannot be marshaled across AppDomain boundaries. Consider using a regular struct or class instead.");
            
        /// <summary>
        /// DBG203: Type cannot be bridged - pointer type
        /// </summary>
        public static readonly DiagnosticDescriptor PointerTypeCannotBeBridged = new(
            "DBG203",
            "Pointer types cannot be bridged",
            "Type '{0}' is a pointer type and cannot be bridged across AppDomain boundaries.",
            "DomainBridge",
            DiagnosticSeverity.Error,
            true,
            "Pointer types represent direct memory addresses that are not valid across AppDomain boundaries. Consider using arrays or managed types instead.");
            
        /// <summary>
        /// DBG204: Type cannot be bridged - already inherits from MarshalByRefObject
        /// </summary>
        public static readonly DiagnosticDescriptor AlreadyMarshalByRefObject = new(
            "DBG204",
            "Type already inherits from MarshalByRefObject",
            "Type '{0}' already inherits from MarshalByRefObject and doesn't need a bridge.",
            "DomainBridge",
            DiagnosticSeverity.Info,
            true,
            "Types that already inherit from MarshalByRefObject can be used directly across AppDomain boundaries without a bridge.");
            
        /// <summary>
        /// DBG205: Missing partial keyword
        /// </summary>
        public static readonly DiagnosticDescriptor MissingPartialKeyword = new(
            "DBG205",
            "Bridge class must be declared as partial",
            "Class '{0}' marked with [DomainBridge] must be declared as 'partial' for source generation to work.",
            "DomainBridge",
            DiagnosticSeverity.Error,
            true,
            "The source generator needs to add implementation to your bridge class, which requires it to be declared as partial.");
            
        /// <summary>
        /// DBG206: Type not found
        /// </summary>
        public static readonly DiagnosticDescriptor TypeNotFound = new(
            "DBG206",
            "Target type not found",
            "Could not find type '{0}' specified in [DomainBridge] attribute. Ensure the type is accessible and the assembly is referenced.",
            "DomainBridge",
            DiagnosticSeverity.Error,
            true,
            "The type specified in the DomainBridge attribute could not be resolved. Check that the type name is correct and the containing assembly is referenced.");
            
        /// <summary>
        /// DBG207: Circular type reference
        /// </summary>
        public static readonly DiagnosticDescriptor CircularTypeReference = new(
            "DBG207",
            "Circular type reference detected",
            "Type '{0}' has a circular reference that may cause issues during bridge generation. Consider using interfaces to break the cycle.",
            "DomainBridge",
            DiagnosticSeverity.Warning,
            true,
            "Circular type references can cause infinite recursion during bridge generation. Consider using interfaces or lazy loading to break the circular dependency.");
            
        /// <summary>
        /// DBG208: Generic constraints incompatible
        /// </summary>
        public static readonly DiagnosticDescriptor IncompatibleGenericConstraints = new(
            "DBG208",
            "Generic constraints may be incompatible with bridging",
            "Type '{0}' has generic constraints that may conflict with MarshalByRefObject requirements. Bridge generation will proceed but runtime errors may occur.",
            "DomainBridge",
            DiagnosticSeverity.Warning,
            true,
            "Generic type constraints (such as 'new()' or specific base class requirements) may conflict with the bridge's need to inherit from MarshalByRefObject.");
            
        /// <summary>
        /// DBG209: Interface implementation missing
        /// </summary>
        public static readonly DiagnosticDescriptor InterfaceImplementationMissing = new(
            "DBG209",
            "Interface member not implemented in bridge",
            "Bridge class '{0}' does not implement interface member '{1}' from interface '{2}'.",
            "DomainBridge",
            DiagnosticSeverity.Error,
            true,
            "All interface members must be implemented in the bridge class to maintain compatibility with the original type.");

        /// <summary>
        /// Creates a diagnostic for a type that cannot be bridged
        /// </summary>
        public static Diagnostic CreateUnbridgeableTypeDiagnostic(ITypeSymbol type, Location location)
        {
            if (type.IsValueType)
            {
                if (type.IsRefLikeType)
                {
                    return Diagnostic.Create(RefStructCannotBeBridged, location, type.ToDisplayString());
                }
                return Diagnostic.Create(ValueTypeCannotBeBridged, location, type.ToDisplayString());
            }
            
            if (type.TypeKind == TypeKind.Pointer)
            {
                return Diagnostic.Create(PointerTypeCannotBeBridged, location, type.ToDisplayString());
            }
            
            if (type.IsSealed)
            {
                return Diagnostic.Create(SealedClassCannotBeBridged, location, type.ToDisplayString());
            }
            
            if (InheritsFromMarshalByRefObject(type))
            {
                return Diagnostic.Create(AlreadyMarshalByRefObject, location, type.ToDisplayString());
            }
            
            // Generic fallback - should not reach here
            return Diagnostic.Create(
                new DiagnosticDescriptor(
                    "DBG299",
                    "Type cannot be bridged",
                    "Type '{0}' cannot be bridged for an unknown reason.",
                    "DomainBridge",
                    DiagnosticSeverity.Error,
                    true),
                location,
                type.ToDisplayString());
        }
        
        private static bool InheritsFromMarshalByRefObject(ITypeSymbol type)
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
    }
}