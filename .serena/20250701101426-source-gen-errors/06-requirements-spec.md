# Requirements Specification: Fix Source Generation Errors

## Problem Statement
The DomainBridge source generator is producing compilation errors when generating proxy classes for complex types. The main issues are:

1. **Generic Type Naming Issue**: The generator creates separate bridge classes for each generic type instantiation (e.g., EventSource<string>, EventSource<int>) with incorrect C# syntax for file/class names
2. **Interface Implementation Errors**: Generated bridge classes are not properly implementing all interface members from the wrapped type

## Solution Overview
Fix the source generator to properly handle generic types and ensure complete interface implementation with proper forwarding to wrapped instances.

## Functional Requirements

### FR1: Generic Type Handling
- The generator MUST create a single generic bridge class for generic types (e.g., EventSourceBridge<T>)
- The generator MUST NOT create separate classes for each generic instantiation
- Generic type names in generated files MUST use valid C# identifier syntax (no angle brackets in filenames)

### FR2: Interface Implementation
- Bridge classes MUST implement ALL interfaces from the wrapped type
- Interface members MUST forward calls to the wrapped instance
- Explicit interface implementations MUST be preserved when the wrapped type uses them
- When multiple interfaces have members with the same name, use explicit interface implementation

### FR3: Special Interface Handling
- Even if MarshalByRefObject implements an interface (like IDisposable), the bridge MUST still forward those calls to the wrapped instance
- Generic constraints from interfaces MUST be preserved in the bridge implementation

## Technical Requirements

### TR1: File Naming for Generic Types
- Modify `BridgeTypeInfo.cs` to sanitize generic type names for file generation
- Replace angle brackets and commas with underscores or use arity notation (e.g., EventSource_1 for EventSource<T>)

### TR2: Generic Bridge Generation
- Update `EnhancedBridgeClassGenerator.cs` to:
  - Detect when the wrapped type is generic
  - Generate a generic bridge class with matching type parameters
  - Apply all generic constraints from the original type

### TR3: Interface Implementation Generation
- Enhance interface member generation in `EnhancedBridgeClassGenerator.cs` to:
  - Handle explicit interface implementations
  - Generate forwarding calls for all interface members
  - Include proper null checking and exception handling

### TR4: Method Signature Handling
- Ensure generated methods match exact signatures including:
  - Generic type parameters and constraints
  - ref/out parameters
  - Default parameter values
  - params arrays

## Implementation Hints

### File: `src/DomainBridge.SourceGenerators/Models/BridgeTypeInfo.cs`
- Update the filename generation logic to handle generic types
- Consider using `GetArityString()` or similar approach for generic type names

### File: `src/DomainBridge.SourceGenerators/Generators/EnhancedBridgeClassGenerator.cs`
- Add logic to detect and handle generic wrapped types
- Ensure interface implementation includes all members, even those from base interfaces
- Add explicit interface implementation support

### File: `src/DomainBridge.SourceGenerators/Services/TypeAnalyzer.cs`
- May need updates to properly analyze generic type constraints
- Ensure all interfaces are captured, including those with generic parameters

## Acceptance Criteria

1. **Generic Type Support**
   - ✓ A generic type like `EventSource<T>` generates a single `EventSourceBridge<T>` class
   - ✓ No compilation errors for generic type instantiations
   - ✓ Generated files have valid C# filenames

2. **Interface Implementation**
   - ✓ All interface members from wrapped type are implemented in bridge
   - ✓ Explicit interface implementations work correctly
   - ✓ IDisposable and other MarshalByRefObject interfaces still forward to wrapped instance

3. **Compilation Success**
   - ✓ No CS0101 (duplicate type definition) errors
   - ✓ No CS0535 (interface member not implemented) errors
   - ✓ Generated code compiles without warnings

4. **Behavioral Correctness**
   - ✓ All forwarded calls reach the wrapped instance
   - ✓ Generic constraints are enforced
   - ✓ Exception propagation works across AppDomain boundaries

## Assumptions
- The user is using the [DomainBridge(typeof(T))] attribute pattern exclusively
- Generic types being bridged are serializable or inherit from MarshalByRefObject
- The wrapped types are accessible from the AppDomain where bridges are created
- Performance overhead of forwarding is acceptable for the use case