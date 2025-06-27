using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;
using DomainBridge.SourceGenerators.Models;

namespace DomainBridge.SourceGenerators.Services
{
    /// <summary>
    /// Collects all types that need bridge classes by recursively analyzing type dependencies
    /// </summary>
    internal sealed class TypeCollector
    {
        private readonly TypeFilter _typeFilter;
        private readonly HashSet<INamedTypeSymbol> _visitedTypes;
        private readonly Queue<INamedTypeSymbol> _typesToProcess;
        private readonly Dictionary<INamedTypeSymbol, BridgeTypeInfo> _collectedTypes;
        
        public TypeCollector(TypeFilter typeFilter)
        {
            _typeFilter = typeFilter ?? throw new ArgumentNullException(nameof(typeFilter));
            _visitedTypes = new HashSet<INamedTypeSymbol>(SymbolEqualityComparer.Default);
            _typesToProcess = new Queue<INamedTypeSymbol>();
            _collectedTypes = new Dictionary<INamedTypeSymbol, BridgeTypeInfo>(SymbolEqualityComparer.Default);
        }
        
        /// <summary>
        /// Collects all types that need bridges starting from the root types
        /// </summary>
        public IReadOnlyDictionary<INamedTypeSymbol, BridgeTypeInfo> CollectTypes(
            IEnumerable<INamedTypeSymbol> explicitlyMarkedTypes)
        {
            // First, add all explicitly marked types
            foreach (var type in explicitlyMarkedTypes)
            {
                if (_typeFilter.ShouldGenerateBridge(type))
                {
                    var info = new BridgeTypeInfo(type, isExplicitlyMarked: true);
                    _collectedTypes[type] = info;
                    EnqueueType(type);
                }
            }
            
            // Process the queue to find all referenced types
            while (_typesToProcess.Count > 0)
            {
                var currentType = _typesToProcess.Dequeue();
                AnalyzeType(currentType);
            }
            
            return _collectedTypes;
        }
        
        private void AnalyzeType(INamedTypeSymbol type)
        {
            // Analyze all public members
            foreach (var member in type.GetMembers())
            {
                if (member.DeclaredAccessibility != Accessibility.Public)
                    continue;
                    
                switch (member)
                {
                    case IMethodSymbol method when !method.IsStatic && method.MethodKind == MethodKind.Ordinary:
                        ProcessType(method.ReturnType);
                        foreach (var parameter in method.Parameters)
                        {
                            ProcessType(parameter.Type);
                        }
                        break;
                        
                    case IPropertySymbol property when !property.IsStatic:
                        ProcessType(property.Type);
                        break;
                        
                    case IEventSymbol eventSymbol when !eventSymbol.IsStatic:
                        ProcessType(eventSymbol.Type);
                        break;
                        
                    case IFieldSymbol field when !field.IsStatic && field.DeclaredAccessibility == Accessibility.Public:
                        ProcessType(field.Type);
                        break;
                }
            }
            
            // Also analyze base types and interfaces
            if (type.BaseType != null)
            {
                ProcessType(type.BaseType);
            }
            
            foreach (var interfaceType in type.AllInterfaces)
            {
                ProcessType(interfaceType);
            }
        }
        
        private void ProcessType(ITypeSymbol type)
        {
            // Handle different type kinds
            switch (type)
            {
                case IArrayTypeSymbol arrayType:
                    ProcessType(arrayType.ElementType);
                    break;
                    
                case INamedTypeSymbol namedType:
                    // Handle generic types
                    if (namedType.IsGenericType)
                    {
                        foreach (var typeArg in namedType.TypeArguments)
                        {
                            ProcessType(typeArg);
                        }
                        
                        // Process the generic type definition if it's a user type
                        if (namedType.ConstructedFrom != null)
                        {
                            ProcessNamedType(namedType.ConstructedFrom);
                        }
                    }
                    else
                    {
                        ProcessNamedType(namedType);
                    }
                    break;
            }
        }
        
        private void ProcessNamedType(INamedTypeSymbol type)
        {
            // Skip if already processed
            if (_collectedTypes.ContainsKey(type))
                return;
                
            // Check if this type needs a bridge
            if (_typeFilter.ShouldGenerateBridge(type))
            {
                var info = new BridgeTypeInfo(type, isExplicitlyMarked: false);
                _collectedTypes[type] = info;
                EnqueueType(type);
            }
        }
        
        private void EnqueueType(INamedTypeSymbol type)
        {
            if (!_visitedTypes.Contains(type))
            {
                _visitedTypes.Add(type);
                _typesToProcess.Enqueue(type);
            }
        }
    }
}