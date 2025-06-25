using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;
using DomainBridge.SourceGenerators.Models;

namespace DomainBridge.SourceGenerators.Services
{
    internal class TypeAnalyzer
    {
        private const string DomainBridgeAttributeName = "DomainBridge.DomainBridgeAttribute";
        private const string IgnoreAttributeName = "DomainBridge.DomainBridgeIgnoreAttribute";

        public TypeModel AnalyzeType(INamedTypeSymbol typeSymbol)
        {
            if (typeSymbol == null)
                throw new ArgumentNullException(nameof(typeSymbol));
                
            var model = new TypeModel(typeSymbol);

            // Analyze properties
            try
            {
                foreach (var member in typeSymbol.GetMembers().OfType<IPropertySymbol>())
                {
                    if (member.DeclaredAccessibility != Accessibility.Public)
                        continue;
                        
                    // Skip static properties for now - we handle them separately
                    if (member.IsStatic)
                        continue;

                    var property = new PropertyModel(
                        member.Name,
                        member.Type,
                        member.GetMethod != null,
                        member.SetMethod != null,
                        HasIgnoreAttribute(member));

                    model.Properties.Add(property);
                }
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Failed to analyze properties of {typeSymbol.Name}: {ex.Message}", ex);
            }

            // Analyze methods
            try
            {
                foreach (var member in typeSymbol.GetMembers().OfType<IMethodSymbol>())
                {
                    if (member.DeclaredAccessibility != Accessibility.Public)
                        continue;
                        
                    // Skip static methods, constructors, and special methods
                    if (member.IsStatic || 
                        member.MethodKind != MethodKind.Ordinary ||
                        member.IsGenericMethod)
                        continue;

                    try
                    {
                        var method = new MethodModel(
                            member.Name,
                            member.ReturnType,
                            HasIgnoreAttribute(member));

                        foreach (var param in member.Parameters)
                        {
                            try
                            {
                                method.Parameters.Add(new ParameterModel(
                                    param.Name,
                                    param.Type,
                                    param.HasExplicitDefaultValue,
                                    param.HasExplicitDefaultValue ? param.ExplicitDefaultValue : null));
                            }
                            catch (Exception paramEx)
                            {
                                throw new InvalidOperationException($"Failed to analyze parameter {param.Name} of method {member.Name}: {paramEx.Message}", paramEx);
                            }
                        }
                        
                        model.Methods.Add(method);
                    }
                    catch (Exception methodEx)
                    {
                        throw new InvalidOperationException($"Failed to analyze method {member.Name}: {methodEx.Message}", methodEx);
                    }
                }
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Failed to analyze methods of {typeSymbol.Name}: {ex.Message}", ex);
            }

            // Analyze events
            foreach (var member in typeSymbol.GetMembers().OfType<IEventSymbol>())
            {
                if (member.DeclaredAccessibility != Accessibility.Public || member.IsStatic)
                    continue;

                var evt = new EventModel(
                    member.Name,
                    member.Type,
                    HasIgnoreAttribute(member));

                model.Events.Add(evt);
            }

            return model;
        }

        public bool HasDomainBridgeAttribute(INamedTypeSymbol typeSymbol)
        {
            return typeSymbol.GetAttributes().Any(attr =>
                attr.AttributeClass?.ToDisplayString() == DomainBridgeAttributeName);
        }

        private bool HasIgnoreAttribute(ISymbol symbol)
        {
            return symbol.GetAttributes().Any(attr =>
                attr.AttributeClass?.ToDisplayString() == IgnoreAttributeName);
        }

        public IEnumerable<ITypeSymbol> GetReferencedTypes(TypeModel model)
        {
            var types = new HashSet<ITypeSymbol>(SymbolEqualityComparer.Default);

            // From properties
            foreach (var prop in model.Properties.Where(p => !p.IsIgnored))
            {
                CollectTypes(prop.Type, types);
            }

            // From methods
            foreach (var method in model.Methods.Where(m => !m.IsIgnored))
            {
                CollectTypes(method.ReturnType, types);
                foreach (var param in method.Parameters)
                {
                    CollectTypes(param.Type, types);
                }
            }

            // From events
            foreach (var evt in model.Events.Where(e => !e.IsIgnored))
            {
                CollectTypes(evt.Type, types);
            }

            return types.Where(t => IsComplexType(t));
        }

        private void CollectTypes(ITypeSymbol type, HashSet<ITypeSymbol> types)
        {
            if (type == null)
                return;
                
            if (type is IArrayTypeSymbol arrayType)
            {
                CollectTypes(arrayType.ElementType, types);
            }
            else if (type is INamedTypeSymbol namedType)
            {
                types.Add(namedType);
                
                // Handle generic types
                foreach (var arg in namedType.TypeArguments)
                {
                    CollectTypes(arg, types);
                }
            }
        }

        private bool IsComplexType(ITypeSymbol type)
        {
            if (type.SpecialType != SpecialType.None)
                return false;

            if (type.TypeKind == TypeKind.Enum)
                return false;

            if (type.ContainingAssembly?.Name.StartsWith("System") == true)
                return false;

            return type is INamedTypeSymbol && type.TypeKind == TypeKind.Class;
        }
    }
}