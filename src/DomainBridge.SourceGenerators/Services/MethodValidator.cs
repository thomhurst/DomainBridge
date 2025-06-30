using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;
using DomainBridge.SourceGenerators.Models;

namespace DomainBridge.SourceGenerators.Services
{
    /// <summary>
    /// Validates methods for AppDomain bridging compatibility and generates diagnostics
    /// </summary>
    internal sealed class MethodValidator
    {
        private static readonly string[] UnsupportedSpanTypes = 
        {
            "System.Span<T>",
            "System.ReadOnlySpan<T>", 
            "System.Memory<T>",
            "System.ReadOnlyMemory<T>"
        };

        /// <summary>
        /// Validates a property type for AppDomain bridging compatibility
        /// </summary>
        public IEnumerable<Diagnostic> ValidatePropertyType(ITypeSymbol propertyType, Location location, string context)
        {
            return ValidateTypeForAppDomainMarshaling(propertyType, location, context);
        }
        
        /// <summary>
        /// Validates a method for AppDomain bridging compatibility
        /// </summary>
        public IEnumerable<Diagnostic> ValidateMethod(MethodModel method, IMethodSymbol methodSymbol, Location location)
        {
            var diagnostics = new List<Diagnostic>();
            
            // Check return type for unsupported types
            diagnostics.AddRange(ValidateTypeForAppDomainMarshaling(method.ReturnType, location, 
                $"Return type of method '{method.Name}'"));
            
            // Check parameters for unsupported types and ref/out issues
            for (int i = 0; i < method.Parameters.Count && i < methodSymbol.Parameters.Length; i++)
            {
                var parameter = method.Parameters[i];
                var parameterSymbol = methodSymbol.Parameters[i];
                
                diagnostics.AddRange(ValidateTypeForAppDomainMarshaling(parameter.Type, location,
                    $"Parameter '{parameter.Name}' of method '{method.Name}'"));
                    
                diagnostics.AddRange(ValidateRefOutParameter(parameter, parameterSymbol, method.Name, location));
            }
            
            return diagnostics;
        }
        
        /// <summary>
        /// Validates a type for compatibility with AppDomain marshaling
        /// </summary>
        private IEnumerable<Diagnostic> ValidateTypeForAppDomainMarshaling(ITypeSymbol type, Location location, string context)
        {
            var diagnostics = new List<Diagnostic>();
            
            // Check for Span<T> and Memory<T> types
            if (IsUnsupportedSpanOrMemoryType(type))
            {
                diagnostics.Add(CreateSpanMemoryError(location, context, type.ToDisplayString()));
            }
            
            
            // Recursively check generic type arguments
            if (type is INamedTypeSymbol namedTypeSymbol && namedTypeSymbol.IsGenericType)
            {
                foreach (var typeArg in namedTypeSymbol.TypeArguments)
                {
                    diagnostics.AddRange(ValidateTypeForAppDomainMarshaling(typeArg, location, 
                        $"{context} (generic argument {typeArg.ToDisplayString()})"));
                }
            }
            
            // Check array element types
            if (type is IArrayTypeSymbol arrayType)
            {
                diagnostics.AddRange(ValidateTypeForAppDomainMarshaling(arrayType.ElementType, location,
                    $"{context} (array element type)"));
            }
            
            return diagnostics;
        }
        
        /// <summary>
        /// Validates ref/out parameters for serialization compatibility
        /// </summary>
        private IEnumerable<Diagnostic> ValidateRefOutParameter(ParameterModel parameter, IParameterSymbol parameterSymbol, string methodName, Location location)
        {
            var diagnostics = new List<Diagnostic>();
            
            if (parameterSymbol.RefKind == RefKind.Ref || parameterSymbol.RefKind == RefKind.Out)
            {
                // Always warn about ref/out parameters as they behave differently across AppDomain boundaries
                // Even serializable types have different semantics when passed by reference across domains
                diagnostics.Add(CreateRefOutWarning(location, parameter.Name, methodName, 
                    parameter.Type.ToDisplayString(), parameterSymbol.RefKind.ToString().ToLowerInvariant()));
            }
            
            return diagnostics;
        }
        
        /// <summary>
        /// Checks if a type is one of the unsupported Span/Memory types
        /// </summary>
        private bool IsUnsupportedSpanOrMemoryType(ITypeSymbol type)
        {
            if (type is INamedTypeSymbol namedType && namedType.IsGenericType)
            {
                var genericDefinition = namedType.ConstructedFrom?.ToDisplayString();
                return UnsupportedSpanTypes.Any(unsupported => 
                    genericDefinition == unsupported || 
                    genericDefinition?.StartsWith(unsupported.Replace("<T>", "<")) == true);
            }
            
            return false;
        }
        
        /// <summary>
        /// Determines if a type is compatible for ref/out parameters across AppDomains
        /// </summary>
        private bool IsRefOutCompatibleType(ITypeSymbol type)
        {
            // Primitive types are fine
            if (type.SpecialType != SpecialType.None)
            {
                return true;
            }

            // String is fine
            if (type.SpecialType == SpecialType.System_String)
            {
                return true;
            }

            // MarshalByRefObject types are fine
            if (InheritsFromMarshalByRefObject(type))
            {
                return true;
            }

            // Types explicitly marked [Serializable] are fine
            if (HasSerializableAttribute(type))
            {
                return true;
            }

            // Everything else is potentially problematic
            return false;
        }
        
        /// <summary>
        /// Checks if a type inherits from MarshalByRefObject
        /// </summary>
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
        
        /// <summary>
        /// Checks if a type has the [Serializable] attribute
        /// </summary>
        private bool HasSerializableAttribute(ITypeSymbol type)
        {
            return type.GetAttributes().Any(attr => 
                attr.AttributeClass?.ToDisplayString() == "System.SerializableAttribute");
        }
        
        /// <summary>
        /// Gets the RefKind from a pointer type (simplified - in real implementation would need more robust detection)
        /// </summary>
        private RefKind GetRefKind(IPointerTypeSymbol pointerType)
        {
            // This is a simplified implementation - in practice you'd need to check the actual 
            // parameter's RefKind from the original method symbol
            return RefKind.Ref; // Placeholder
        }
        
        /// <summary>
        /// Creates a diagnostic error for Span/Memory types
        /// </summary>
        private Diagnostic CreateSpanMemoryError(Location location, string context, string typeName)
        {
            return Diagnostic.Create(
                new DiagnosticDescriptor(
                    "DBG100",
                    "Span and Memory types cannot be marshaled across AppDomains",
                    "{0} uses type '{1}' which cannot be marshaled across AppDomain boundaries. Consider using arrays (T[]) or collections instead.",
                    "DomainBridge",
                    DiagnosticSeverity.Error,
                    true,
                    "Span<T>, ReadOnlySpan<T>, Memory<T>, and ReadOnlyMemory<T> are stack-allocated or direct memory references that cannot cross AppDomain boundaries. Use T[] arrays or List<T> collections for data transfer."),
                location,
                context,
                typeName);
        }
        
        /// <summary>
        /// Creates a diagnostic warning for problematic ref/out parameters
        /// </summary>
        private Diagnostic CreateRefOutWarning(Location location, string parameterName, string methodName, 
            string typeName, string refKind)
        {
            return Diagnostic.Create(
                new DiagnosticDescriptor(
                    "DBG101", 
                    "ref/out parameters may not work as expected across AppDomains",
                    "Parameter '{0}' in method '{1}' is {2} {3}. ref/out parameters are converted to normal parameters across AppDomain boundaries, losing reference semantics.",
                    "DomainBridge",
                    DiagnosticSeverity.Warning,
                    true,
                    "ref/out parameters cannot maintain reference semantics across AppDomain boundaries. They are marshaled by value, so modifications will not be reflected in the original variable."),
                location,
                parameterName,
                methodName, 
                refKind,
                typeName);
        }
    }
}