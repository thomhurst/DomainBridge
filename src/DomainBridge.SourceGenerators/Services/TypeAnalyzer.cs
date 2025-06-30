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
            {
                throw new ArgumentNullException(nameof(typeSymbol));
            }

            var model = new TypeModel(typeSymbol);

            // Analyze interfaces
            foreach (var interfaceSymbol in typeSymbol.AllInterfaces)
            {
                if (interfaceSymbol.DeclaredAccessibility == Accessibility.Public)
                {
                    model.Interfaces.Add(interfaceSymbol);
                }
            }

            // Collect all members including interface members
            var allMembers = new HashSet<ISymbol>(SymbolEqualityComparer.Default);
            
            // First, collect explicit interface implementations
            var explicitImplementations = CollectExplicitInterfaceImplementations(typeSymbol);
            foreach (var impl in explicitImplementations)
            {
                allMembers.Add(impl);
            }
            
            // Then collect interface members that need to be implemented
            var interfaceMembers = CollectAllInterfaceMembers(typeSymbol);
            foreach (var member in interfaceMembers)
            {
                allMembers.Add(member);
            }

            // Analyze properties (including inherited ones and interface properties)
            try
            {
                var allProperties = GetAllAccessibleProperties(typeSymbol);
                var interfaceProperties = interfaceMembers.OfType<IPropertySymbol>();
                
                // Add type's own properties
                foreach (var member in allProperties)
                {
                    if (member.DeclaredAccessibility != Accessibility.Public)
                    {
                        continue;
                    }

                    // Skip static properties for now - we handle them separately
                    if (member.IsStatic)
                    {
                        continue;
                    }

                    var property = new PropertyModel(
                        member.Name,
                        member.Type,
                        member.GetMethod != null,
                        member.SetMethod != null,
                        HasIgnoreAttribute(member),
                        member.IsIndexer);
                        
                    // If it's an indexer, collect parameters
                    if (member.IsIndexer)
                    {
                        foreach (var param in member.Parameters)
                        {
                            property.Parameters.Add(new ParameterModel(
                                param.Name,
                                param.Type,
                                param.HasExplicitDefaultValue,
                                param.HasExplicitDefaultValue ? param.ExplicitDefaultValue : null));
                        }
                    }

                    model.Properties.Add(property);
                }
                
                // Add interface properties that aren't already implemented
                foreach (var interfaceProp in interfaceProperties)
                {
                    var signature = GetPropertySignature(interfaceProp);
                    if (!model.Properties.Any(p => GetPropertySignature(p) == signature))
                    {
                        var property = new PropertyModel(
                            interfaceProp.Name,
                            interfaceProp.Type,
                            interfaceProp.GetMethod != null,
                            interfaceProp.SetMethod != null,
                            false,
                            interfaceProp.IsIndexer)
                        {
                            IsInterfaceMember = true,
                            DeclaringInterface = interfaceProp.ContainingType
                        };
                        
                        if (interfaceProp.IsIndexer)
                        {
                            foreach (var param in interfaceProp.Parameters)
                            {
                                property.Parameters.Add(new ParameterModel(
                                    param.Name,
                                    param.Type,
                                    param.HasExplicitDefaultValue,
                                    param.HasExplicitDefaultValue ? param.ExplicitDefaultValue : null));
                            }
                        }
                        
                        model.Properties.Add(property);
                    }
                }
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Failed to analyze properties of {typeSymbol.Name}: {ex.Message}", ex);
            }

            // Analyze methods (including inherited ones and interface methods)
            try
            {
                var allMethods = GetAllAccessibleMethods(typeSymbol);
                var interfaceMethods = interfaceMembers.OfType<IMethodSymbol>()
                    .Where(m => m.MethodKind == MethodKind.Ordinary);
                
                // Add type's own methods
                foreach (var member in allMethods)
                {
                    if (member.DeclaredAccessibility != Accessibility.Public)
                    {
                        continue;
                    }

                    // Skip static methods, constructors, and special methods
                    if (member.IsStatic || 
                        member.MethodKind != MethodKind.Ordinary ||
                        member.IsGenericMethod)
                    {
                        continue;
                    }

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
                
                // Add interface methods that aren't already implemented
                foreach (var interfaceMethod in interfaceMethods)
                {
                    var signature = GetMethodSignature(interfaceMethod);
                    if (!model.Methods.Any(m => GetMethodSignature(m.Symbol) == signature))
                    {
                        var method = new MethodModel(
                            interfaceMethod.Name,
                            interfaceMethod.ReturnType,
                            interfaceMethod,
                            false)
                        {
                            IsInterfaceMember = true,
                            DeclaringInterface = interfaceMethod.ContainingType
                        };
                        
                        foreach (var param in interfaceMethod.Parameters)
                        {
                            method.Parameters.Add(new ParameterModel(
                                param.Name,
                                param.Type,
                                param.HasExplicitDefaultValue,
                                param.HasExplicitDefaultValue ? param.ExplicitDefaultValue : null));
                        }
                        
                        model.Methods.Add(method);
                    }
                }
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Failed to analyze methods of {typeSymbol.Name}: {ex.Message}", ex);
            }

            // Analyze events (including interface events)
            var interfaceEvents = interfaceMembers.OfType<IEventSymbol>();
            foreach (var member in typeSymbol.GetMembers().OfType<IEventSymbol>())
            {
                if (member.DeclaredAccessibility != Accessibility.Public || member.IsStatic)
                {
                    continue;
                }

                var evt = new EventModel(
                    member.Name,
                    member.Type,
                    HasIgnoreAttribute(member));

                model.Events.Add(evt);
            }
            
            // Add interface events that aren't already implemented
            foreach (var interfaceEvent in interfaceEvents)
            {
                if (!model.Events.Any(e => e.Name == interfaceEvent.Name))
                {
                    var evt = new EventModel(
                        interfaceEvent.Name,
                        interfaceEvent.Type,
                        false)
                    {
                        IsInterfaceMember = true,
                        DeclaringInterface = interfaceEvent.ContainingType
                    };
                    
                    model.Events.Add(evt);
                }
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
                {
                    break;
                }
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
                {
                    break;
                }
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
        
        /// <summary>
        /// Creates a signature string for a property to handle matching
        /// </summary>
        private string GetPropertySignature(IPropertySymbol property)
        {
            if (property.IsIndexer)
            {
                var parameters = string.Join(",", property.Parameters.Select(p => p.Type.ToDisplayString()));
                return $"this[{parameters}]";
            }
            return property.Name;
        }
        
        /// <summary>
        /// Creates a signature string for a PropertyModel to handle matching
        /// </summary>
        private string GetPropertySignature(PropertyModel property)
        {
            if (property.IsIndexer)
            {
                var parameters = string.Join(",", property.Parameters.Select(p => p.Type.ToDisplayString()));
                return $"this[{parameters}]";
            }
            return property.Name;
        }
        
        /// <summary>
        /// Collects all interface members that need to be implemented
        /// </summary>
        private IEnumerable<ISymbol> CollectAllInterfaceMembers(INamedTypeSymbol typeSymbol)
        {
            var members = new List<ISymbol>();
            
            foreach (var iface in typeSymbol.AllInterfaces)
            {
                foreach (var member in iface.GetMembers())
                {
                    if (member.DeclaredAccessibility == Accessibility.Public && !member.IsStatic)
                    {
                        members.Add(member);
                    }
                }
            }
            
            return members;
        }
        
        /// <summary>
        /// Collects explicit interface implementations
        /// </summary>
        private IEnumerable<ISymbol> CollectExplicitInterfaceImplementations(INamedTypeSymbol typeSymbol)
        {
            var explicitImplementations = new List<ISymbol>();
            
            foreach (var member in typeSymbol.GetMembers())
            {
                switch (member)
                {
                    case IMethodSymbol method when method.ExplicitInterfaceImplementations.Length > 0:
                        explicitImplementations.Add(method);
                        break;
                    case IPropertySymbol property when property.ExplicitInterfaceImplementations.Length > 0:
                        explicitImplementations.Add(property);
                        break;
                    case IEventSymbol evt when evt.ExplicitInterfaceImplementations.Length > 0:
                        explicitImplementations.Add(evt);
                        break;
                }
            }
            
            return explicitImplementations;
        }

    }
}