using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;

namespace DomainBridge.SourceGenerators.Services
{
    internal class TypeNameResolver
    {
        private readonly HashSet<string> _processedTypes;

        public TypeNameResolver(HashSet<string> processedTypes)
        {
            _processedTypes = processedTypes;
        }

        public string GetProxyTypeName(ITypeSymbol type)
        {
            if (type.SpecialType != SpecialType.None && type.SpecialType != SpecialType.System_Object)
            {
                return type.ToDisplayString();
            }

            if (type is IArrayTypeSymbol arrayType)
            {
                var elementProxyType = GetProxyTypeName(arrayType.ElementType);
                return $"{elementProxyType}[]";
            }

            if (type is INamedTypeSymbol namedType)
            {
                if (IsGenericCollection(namedType))
                {
                    var elementType = namedType.TypeArguments.First();
                    var elementProxyType = GetProxyTypeName(elementType);
                    
                    var collectionType = namedType.Name switch
                    {
                        "List" => "List",
                        "IList" => "IList",
                        "IEnumerable" => "IEnumerable",
                        "ICollection" => "ICollection",
                        _ => namedType.Name
                    };
                    
                    return $"{collectionType}<{elementProxyType}>";
                }

                if (IsComplexType(type) && _processedTypes.Contains(type.ToDisplayString()))
                {
                    return $"{namedType.Name}Bridge";
                }
            }

            return type.ToDisplayString();
        }

        public bool IsComplexType(ITypeSymbol type)
        {
            if (type.SpecialType != SpecialType.None)
                return false;

            if (type.TypeKind == TypeKind.Enum)
                return false;

            if (type.ContainingAssembly?.Name.StartsWith("System") == true)
                return false;

            if (type.TypeKind == TypeKind.Interface)
                return false;

            return type is INamedTypeSymbol && type.TypeKind == TypeKind.Class;
        }

        public bool IsCollectionType(ITypeSymbol type)
        {
            return type is IArrayTypeSymbol ||
                   (type is INamedTypeSymbol namedType && IsGenericCollection(namedType));
        }

        public bool IsGenericCollection(INamedTypeSymbol type)
        {
            var name = type.Name;
            return (name == "List" || name == "IList" || name == "IEnumerable" ||
                    name == "ICollection" || name == "HashSet" || name == "ISet") &&
                   type.TypeArguments.Length == 1;
        }
    }
}