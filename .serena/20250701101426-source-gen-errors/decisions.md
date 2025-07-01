# Design Decisions: Source Generation Errors Fix

## Decision Log

### DD-001: Return Type Specification Strategy
**Date**: 2025-07-01  
**Status**: Proposed  
**Decision**: Always include explicit return types in generated method signatures  
**Rationale**: 
- C# requires return types for all methods
- Implicit typing is not allowed in method signatures
- Ensures compatibility across all C# versions

**Alternatives Considered**:
- Using dynamic return types (rejected: performance and type safety)
- Inferring from base implementation (rejected: complexity)

### DD-002: Interface Implementation Approach
**Date**: 2025-07-01  
**Status**: Proposed  
**Decision**: Generate explicit interface implementations when needed  
**Rationale**:
- Handles naming conflicts gracefully
- Provides clear separation of interface members
- Supports multiple interface inheritance

**Alternatives Considered**:
- Implicit implementations only (rejected: naming conflicts)
- Separate proxy per interface (rejected: complexity)

### DD-003: Type Resolution Method
**Date**: 2025-07-01  
**Status**: Proposed  
**Decision**: Use fully qualified type names in generated code  
**Rationale**:
- Avoids namespace conflicts
- Eliminates ambiguity
- Simplifies generator logic

**Alternatives Considered**:
- Using statements collection (rejected: conflict management)
- Type aliases (rejected: readability)

### DD-004: Error Handling Strategy
**Date**: 2025-07-01  
**Status**: Proposed  
**Decision**: Report diagnostics instead of throwing exceptions  
**Rationale**:
- Better IDE integration
- Allows partial generation
- Improved developer experience

**Alternatives Considered**:
- Fail fast with exceptions (rejected: poor UX)
- Silent failures (rejected: debugging difficulty)

### DD-005: Generic Type Handling
**Date**: 2025-07-01  
**Status**: Proposed  
**Decision**: Preserve generic constraints and variance  
**Rationale**:
- Maintains type safety
- Supports advanced generic scenarios
- Compatible with existing code

**Alternatives Considered**:
- Strip generic constraints (rejected: functionality loss)
- Generate non-generic proxies (rejected: type safety)

## Architecture Decisions

### Code Generation Structure
- Use StringBuilder for performance
- Generate one file per bridge type
- Include generated code headers
- Add nullable annotations where appropriate

### Testing Approach
- Snapshot testing for generated code
- Compilation testing for all scenarios
- Integration tests for runtime behavior
- Diagnostic verification tests

### Versioning Strategy
- Increment minor version for fixes
- No breaking changes to public API
- Maintain backward compatibility
- Document all changes

## Implementation Guidelines

### Code Style
- Follow existing project conventions
- Use meaningful variable names
- Add XML documentation
- Include inline comments for complex logic

### Performance Considerations
- Minimize allocations in generator
- Cache type symbols when possible
- Avoid repeated syntax tree traversals
- Use efficient string building

### Maintainability
- Keep methods focused and small
- Extract complex logic to helpers
- Add unit tests for new code
- Document non-obvious decisions