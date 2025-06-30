# DomainBridge Bug Fix Requirements Specification

## Problem Statement

DomainBridge users are experiencing compilation errors when attempting to bridge complex objects with deeply nested type hierarchies. The primary issues are:

1. **Missing Bridge Types**: Compilation error "The type or namespace name 'PLACEHOLDERBridge' does not exist in the namespace"
2. **Interface Implementation Failures**: Compilation error "'PLACEHOLDERBridge' does not implement interface member 'INTERFACE.METHOD()'"

These errors indicate that the source generator is failing to properly generate bridge classes for complex type graphs, particularly when dealing with nested types and interface implementations.

## Solution Overview

Fix the DomainBridge source generator to properly handle complex type hierarchies by:
1. Ensuring all nested types in the object graph receive bridge classes
2. Implementing all interface members including inherited and explicit implementations
3. Resolving naming conflicts with fully qualified names
4. Providing clear diagnostics for unbridgeable types

## Functional Requirements

### FR1: Complete Type Graph Bridging
- The source generator MUST recursively analyze and create bridge classes for ALL types referenced in the target type's public API
- This includes:
  - Return types of methods and properties
  - Parameter types
  - Generic type arguments
  - Types multiple levels deep in the object graph
- Rationale: Non-serializable types cannot cross AppDomain boundaries without MarshalByRefObject proxies

### FR2: Full Interface Implementation
- Bridge classes MUST implement ALL interfaces that the target type implements
- This includes:
  - All interface members (methods, properties, events)
  - Members from inherited interfaces
  - Explicitly implemented interface members
  - Default interface implementations (if present)
- Each interface member MUST be properly delegated to the wrapped instance
- Rationale: Bridge classes should be drop-in replacements maintaining full compatibility

### FR3: Namespace Conflict Resolution
- The generator MUST use fully qualified type names to avoid naming conflicts
- When multiple types with the same name exist in different namespaces:
  - Generate unique bridge class names
  - Use global:: prefix for type references
  - Maintain proper namespace hierarchy
- Rationale: Prevent compilation errors due to ambiguous type references

### FR4: Diagnostic Reporting
- The generator MUST report clear diagnostic messages when encountering unbridgeable types
- Diagnostics should include:
  - The specific type that cannot be bridged
  - The reason (e.g., ref struct, pointer type)
  - The location in the type graph where it was encountered
  - Suggested workarounds if available

## Technical Requirements

### TR1: Type Collection Enhancement
**File:** `/src/DomainBridge.SourceGenerators/Services/TypeCollector.cs`
- Fix recursive type collection to handle all nested types
- Ensure circular references are properly handled
- Track the full type graph including generic type arguments

### TR2: Interface Member Resolution
**File:** `/src/DomainBridge.SourceGenerators/Services/TypeAnalyzer.cs`
- Enhance `AnalyzeType` to collect ALL interface members including:
  - Explicit interface implementations
  - Members from base interfaces
  - Generic interface members
- Ensure proper member signature matching

### TR3: Bridge Type Generation
**Files:** 
- `/src/DomainBridge.SourceGenerators/Services/EnhancedBridgeClassGenerator.cs`
- `/src/DomainBridge.SourceGenerators/Services/BridgeClassGenerator.cs`
- Generate all interface implementations
- Handle method overloads correctly
- Preserve parameter names and default values
- Generate proper return type wrapping for nested objects

### TR4: Type Resolution Enhancement
**File:** `/src/DomainBridge.SourceGenerators/Services/BridgeTypeResolver.cs`
- Improve type name resolution to handle:
  - Types with identical names in different namespaces
  - Nested generic types
  - Complex type constraints
- Ensure generated type references use fully qualified names

### TR5: Placeholder Name Prevention
- Investigate and fix the root cause of "PLACEHOLDER" appearing in generated code
- Ensure all type references are properly resolved before code generation
- Add validation to prevent incomplete code generation

## Implementation Hints

### Pattern: Explicit Interface Implementation
```csharp
// When generating explicit interface implementations:
void IInterface.Method() 
{
    ((IInterface)_instance).Method();
}
```

### Pattern: Fully Qualified Names
```csharp
// Use global:: prefix and full namespaces:
global::Company.Product.Namespace.TypeNameBridge
```

### Pattern: Generic Type Handling
```csharp
// Resolve generic arguments recursively:
List<NestedTypeBridge> instead of List<NestedType>
```

## Acceptance Criteria

1. **Complex Type Graphs**: Successfully generate bridges for types with 5+ levels of nested types
2. **Interface Compliance**: All bridge classes implement 100% of their target type's interfaces
3. **No Naming Conflicts**: Projects with duplicate type names in different namespaces compile without errors
4. **Clear Diagnostics**: Meaningful error messages for unbridgeable types (ref structs, pointers, etc.)
5. **Backward Compatibility**: Existing simple bridge scenarios continue to work (breaking changes allowed but document them)

## Assumptions

1. Performance is not the primary concern - correctness is more important
2. Generated code size is acceptable even for large type graphs
3. Users understand that some types (ref structs, pointers) cannot be bridged
4. The fix can introduce breaking changes if necessary for correctness

## Out of Scope

1. Performance optimizations
2. Reducing generated code size
3. Supporting .NET Core/.NET 5+ (AppDomains not available)
4. Supporting additional .NET Framework versions beyond 4.7.2

## Testing Recommendations

1. Create test cases with deeply nested type hierarchies (10+ levels)
2. Test types with multiple interfaces including inherited ones
3. Test explicit interface implementations
4. Test generic types with complex constraints
5. Test naming conflict scenarios
6. Verify diagnostic messages for unbridgeable types