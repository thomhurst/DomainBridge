using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;
using DomainBridge.SourceGenerators.Models;

namespace DomainBridge.SourceGenerators.Services
{
    /// <summary>
    /// Resolves types to their bridge equivalents for code generation
    /// </summary>
    internal sealed class BridgeTypeResolver
    {
        private readonly IReadOnlyDictionary<INamedTypeSymbol, BridgeTypeInfo> _bridgeTypeMap;
        private static readonly SymbolDisplayFormat FullyQualifiedFormat = new SymbolDisplayFormat(
            typeQualificationStyle: SymbolDisplayTypeQualificationStyle.NameAndContainingTypesAndNamespaces,
            globalNamespaceStyle: SymbolDisplayGlobalNamespaceStyle.Included,
            genericsOptions: SymbolDisplayGenericsOptions.IncludeTypeParameters,
            miscellaneousOptions: SymbolDisplayMiscellaneousOptions.UseSpecialTypes | SymbolDisplayMiscellaneousOptions.IncludeNullableReferenceTypeModifier);
            
        private static readonly SymbolDisplayFormat FullyQualifiedFormatWithoutGlobal = new SymbolDisplayFormat(
            typeQualificationStyle: SymbolDisplayTypeQualificationStyle.NameAndContainingTypesAndNamespaces,
            globalNamespaceStyle: SymbolDisplayGlobalNamespaceStyle.Omitted,
            genericsOptions: SymbolDisplayGenericsOptions.IncludeTypeParameters,
            miscellaneousOptions: SymbolDisplayMiscellaneousOptions.UseSpecialTypes | SymbolDisplayMiscellaneousOptions.IncludeNullableReferenceTypeModifier);
        
        public BridgeTypeResolver(IReadOnlyDictionary<INamedTypeSymbol, BridgeTypeInfo> bridgeTypeMap)
        {
            _bridgeTypeMap = bridgeTypeMap ?? throw new ArgumentNullException(nameof(bridgeTypeMap));
        }
        
        /// <summary>
        /// Resolves a type to its bridge equivalent name for use in generated code
        /// </summary>
        public string ResolveType(ITypeSymbol type)
        {
            return ResolveTypeInternal(type, new HashSet<ITypeSymbol>(SymbolEqualityComparer.Default));
        }
        
        /// <summary>
        /// Determines if a type has a bridge and returns the bridge info
        /// </summary>
        public bool TryGetBridgeInfo(ITypeSymbol type, out BridgeTypeInfo? bridgeInfo)
        {
            bridgeInfo = null;
            
            if (type is INamedTypeSymbol namedType)
            {
                // Check both the type and its constructed form for generics
                var typeToCheck = namedType.ConstructedFrom ?? namedType;
                return _bridgeTypeMap.TryGetValue(typeToCheck, out bridgeInfo);
            }
            
            return false;
        }
        
        /// <summary>
        /// Checks if a type needs wrapping when returned from a method
        /// </summary>
        public bool NeedsWrapping(ITypeSymbol type)
        {
            // Only reference types can be wrapped
            if (!type.IsReferenceType)
            {
                return false;
            }

            return TryGetBridgeInfo(type, out _);
        }
        
        private string ResolveTypeInternal(ITypeSymbol type, HashSet<ITypeSymbol> visitedTypes)
        {
            // Prevent infinite recursion
            if (!visitedTypes.Add(type))
            {
                return type.ToDisplayString(FullyQualifiedFormat);
            }

            // Handle nullable reference types (but not nullable value types like int?)
            if (type.NullableAnnotation == NullableAnnotation.Annotated && type is not ITypeParameterSymbol)
            {
                // Check if this is already a nullable value type (e.g., int?, Nullable<int>)
                if (type.OriginalDefinition?.SpecialType == SpecialType.System_Nullable_T)
                {
                    // It's already nullable, don't add another ?
                    return type.ToDisplayString(FullyQualifiedFormat);
                }
                
                var underlyingType = type.WithNullableAnnotation(NullableAnnotation.None);
                return ResolveTypeInternal(underlyingType, visitedTypes) + "?";
            }
                
            // Handle special cases
            if (type.SpecialType != SpecialType.None)
            {
                return type.ToDisplayString(FullyQualifiedFormat);
            }

            // Handle type parameters
            if (type is ITypeParameterSymbol)
            {
                return type.Name;
            }

            // Handle pointers
            if (type is IPointerTypeSymbol pointerType)
            {
                var pointedType = ResolveTypeInternal(pointerType.PointedAtType, visitedTypes);
                return $"{pointedType}*";
            }
                
            // Handle arrays
            if (type is IArrayTypeSymbol arrayType)
            {
                var elementType = ResolveTypeInternal(arrayType.ElementType, visitedTypes);
                var rankSpecifier = arrayType.Rank == 1 ? "[]" : $"[{new string(',', arrayType.Rank - 1)}]";
                return $"{elementType}{rankSpecifier}";
            }
            
            // Handle named types
            if (type is INamedTypeSymbol namedType)
            {
                // Check if this type has a bridge
                if (TryGetBridgeInfo(namedType, out var bridgeInfo))
                {
                    // For generic types, we need to resolve type arguments
                    if (namedType.IsGenericType && namedType.TypeArguments.Length > 0)
                    {
                        var resolvedArgs = namedType.TypeArguments
                            .Select(arg => ResolveTypeInternal(arg, visitedTypes))
                            .ToList();
                            
                        // Construct the generic bridge type with proper escaping for nested types
                        var bridgeNamespace = EscapeNestedTypeName(bridgeInfo!.BridgeNamespace);
                        var bridgeClassName = EscapeNestedTypeName(bridgeInfo.BridgeClassName);
                        return $"global::{bridgeNamespace}.{bridgeClassName}<{string.Join(", ", resolvedArgs)}>";
                    }
                    
                    // Non-generic bridge type
                    return $"global::{EscapeNestedTypeName(bridgeInfo!.BridgeFullName)}";
                }
                
                // Handle nested types
                if (namedType.ContainingType != null)
                {
                    var containingType = ResolveTypeInternal(namedType.ContainingType, visitedTypes);
                    var typeName = namedType.Name;
                    
                    if (namedType.IsGenericType && namedType.TypeArguments.Length > 0)
                    {
                        var resolvedArgs = namedType.TypeArguments
                            .Select(arg => ResolveTypeInternal(arg, visitedTypes))
                            .ToList();
                        typeName = $"{typeName}<{string.Join(", ", resolvedArgs)}>";
                    }
                    
                    return $"{containingType}.{typeName}";
                }
                
                // Handle generic types without bridges
                if (namedType.IsGenericType && namedType.TypeArguments.Length > 0)
                {
                    var resolvedArgs = namedType.TypeArguments
                        .Select(arg => ResolveTypeInternal(arg, visitedTypes))
                        .ToList();
                        
                    var baseTypeName = GetGenericTypeBaseName(namedType);
                    return $"{baseTypeName}<{string.Join(", ", resolvedArgs)}>";
                }
            }
            
            // Default: return the fully qualified name
            return type.ToDisplayString(FullyQualifiedFormat);
        }
        
        private string GetGenericTypeBaseName(INamedTypeSymbol namedType)
        {
            // For generic types, we need to get the name without type parameters
            var fullName = namedType.ConstructedFrom?.ToDisplayString(FullyQualifiedFormat) 
                          ?? namedType.ToDisplayString(FullyQualifiedFormat);
                          
            var genericIndex = fullName.IndexOf('<');
            if (genericIndex > 0)
            {
                return fullName.Substring(0, genericIndex);
            }
            
            return fullName;
        }
        
        private string EscapeNestedTypeName(string typeName)
        {
            // In C#, nested types use '+' in their metadata names but '.' in code
            // We need to ensure we're using '.' for code generation
            return typeName.Replace('+', '.');
        }
    }
}