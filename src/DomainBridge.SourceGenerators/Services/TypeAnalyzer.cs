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

            // Analyze interfaces
            foreach (var interfaceSymbol in typeSymbol.AllInterfaces)
            {
                if (interfaceSymbol.DeclaredAccessibility == Accessibility.Public)
                {
                    model.Interfaces.Add(interfaceSymbol);
                }
            }

            // Analyze properties (including inherited ones)
            try
            {
                var allProperties = GetAllAccessibleProperties(typeSymbol);
                foreach (var member in allProperties)
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

            // Analyze methods (including inherited ones)
            try
            {
                var allMethods = GetAllAccessibleMethods(typeSymbol);
                foreach (var member in allMethods)
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
                            member,
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
        
        /// <summary>
        /// Gets all accessible properties including inherited ones, avoiding duplicates
        /// </summary>
        private IEnumerable<IPropertySymbol> GetAllAccessibleProperties(INamedTypeSymbol typeSymbol)
        {
            var properties = new Dictionary<string, IPropertySymbol>();
            var currentType = typeSymbol;
            
            // Walk up the inheritance chain
            while (currentType != null)
            {
                foreach (var member in currentType.GetMembers().OfType<IPropertySymbol>())
                {
                    // Only include public properties that we haven't seen yet
                    if (member.DeclaredAccessibility == Accessibility.Public && !properties.ContainsKey(member.Name))
                    {
                        properties[member.Name] = member;
                    }
                }
                
                currentType = currentType.BaseType;
                
                // Stop at System.Object to avoid including object members
                if (currentType?.SpecialType == SpecialType.System_Object)
                    break;
            }
            
            return properties.Values;
        }
        
        /// <summary>
        /// Gets all accessible methods including inherited ones, avoiding duplicates
        /// </summary>
        private IEnumerable<IMethodSymbol> GetAllAccessibleMethods(INamedTypeSymbol typeSymbol)
        {
            var methods = new Dictionary<string, IMethodSymbol>();
            var currentType = typeSymbol;
            
            // Walk up the inheritance chain
            while (currentType != null)
            {
                foreach (var member in currentType.GetMembers().OfType<IMethodSymbol>())
                {
                    // Only include public methods that we haven't seen yet
                    // Use a signature-based key to handle overloads properly
                    var signature = GetMethodSignature(member);
                    if (member.DeclaredAccessibility == Accessibility.Public && !methods.ContainsKey(signature))
                    {
                        methods[signature] = member;
                    }
                }
                
                currentType = currentType.BaseType;
                
                // Stop at System.Object to avoid including object members like ToString, GetHashCode, etc.
                if (currentType?.SpecialType == SpecialType.System_Object)
                    break;
            }
            
            return methods.Values;
        }
        
        /// <summary>
        /// Creates a signature string for a method to handle overloads properly
        /// </summary>
        private string GetMethodSignature(IMethodSymbol method)
        {
            var parameters = string.Join(",", method.Parameters.Select(p => p.Type.ToDisplayString()));
            return $"{method.Name}({parameters})";
        }

    }
}