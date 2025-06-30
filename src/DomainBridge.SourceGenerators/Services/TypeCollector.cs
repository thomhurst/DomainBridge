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
                    case IMethodSymbol method when method.MethodKind == MethodKind.Ordinary:
                        // Process return type for both static and instance methods
                        ProcessType(method.ReturnType);
                        foreach (var parameter in method.Parameters)
                        {
                            ProcessType(parameter.Type);
                        }
                        // Also process generic type constraints
                        foreach (var typeParam in method.TypeParameters)
                        {
                            foreach (var constraint in typeParam.ConstraintTypes)
                            {
                                ProcessType(constraint);
                            }
                        }
                        break;
                        
                    case IPropertySymbol property:
                        // Process property type for both static and instance properties
                        ProcessType(property.Type);
                        // Process indexer parameters
                        if (property.IsIndexer)
                        {
                            foreach (var parameter in property.Parameters)
                            {
                                ProcessType(parameter.Type);
                            }
                        }
                        break;
                        
                    case IEventSymbol eventSymbol:
                        // Process event type for both static and instance events
                        ProcessType(eventSymbol.Type);
                        break;
                        
                    case IFieldSymbol field when field.DeclaredAccessibility == Accessibility.Public:
                        // Process field type for both static and instance fields
                        ProcessType(field.Type);
                        break;
                        
                    case INamedTypeSymbol nestedType:
                        // Process nested types
                        ProcessNamedType(nestedType);
                        break;
                }
            }
            
            // Analyze generic type parameters and constraints
            foreach (var typeParam in type.TypeParameters)
            {
                foreach (var constraint in typeParam.ConstraintTypes)
                {
                    ProcessType(constraint);
                }
            }
            
            // Also analyze base types and interfaces
            if (type.BaseType != null && type.BaseType.SpecialType != SpecialType.System_Object)
            {
                ProcessType(type.BaseType);
            }
            
            // Process all interfaces including inherited ones
            foreach (var interfaceType in type.AllInterfaces)
            {
                ProcessType(interfaceType);
                // Also analyze interface members for deep type discovery
                if (interfaceType is INamedTypeSymbol namedInterface)
                {
                    foreach (var member in namedInterface.GetMembers())
                    {
                        if (member.DeclaredAccessibility == Accessibility.Public)
                        {
                            switch (member)
                            {
                                case IMethodSymbol method:
                                    ProcessType(method.ReturnType);
                                    foreach (var param in method.Parameters)
                                    {
                                        ProcessType(param.Type);
                                    }
                                    break;
                                case IPropertySymbol property:
                                    ProcessType(property.Type);
                                    break;
                                case IEventSymbol eventSymbol:
                                    ProcessType(eventSymbol.Type);
                                    break;
                            }
                        }
                    }
                }
            }
        }
        
        private void ProcessType(ITypeSymbol type)
        {
            if (type == null)
                return;
                
            // Handle different type kinds
            switch (type)
            {
                case IArrayTypeSymbol arrayType:
                    ProcessType(arrayType.ElementType);
                    break;
                    
                case IPointerTypeSymbol pointerType:
                    // Process the pointed-to type even though we can't bridge pointers
                    ProcessType(pointerType.PointedAtType);
                    break;
                    
                case IDynamicTypeSymbol:
                    // Dynamic types are handled at runtime
                    break;
                    
                case ITypeParameterSymbol typeParameter:
                    // Process type parameter constraints
                    foreach (var constraint in typeParameter.ConstraintTypes)
                    {
                        ProcessType(constraint);
                    }
                    break;
                    
                case INamedTypeSymbol namedType:
                    // Handle nullable value types
                    if (namedType.OriginalDefinition?.SpecialType == SpecialType.System_Nullable_T)
                    {
                        foreach (var typeArg in namedType.TypeArguments)
                        {
                            ProcessType(typeArg);
                        }
                    }
                    // Handle generic types
                    else if (namedType.IsGenericType)
                    {
                        // Process all type arguments
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
                    
                    // Also process any nested types within this type
                    foreach (var nestedType in namedType.GetTypeMembers())
                    {
                        ProcessNamedType(nestedType);
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