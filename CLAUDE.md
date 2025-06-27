# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

DomainBridge is a .NET library that uses C# source generators to automatically create AppDomain isolation proxies, solving assembly version conflicts in .NET Framework applications. The library generates proxy classes at compile time that marshal calls across AppDomain boundaries.

## Key Architecture

### Source Generator Pattern
The core functionality relies on Roslyn source generators:
- `DomainBridgePatternGenerator` analyzes types marked with `[DomainBridge]` attribute
- Generates partial class implementations with all members from the target type
- Creates proxy classes for nested return types automatically
- All generation happens at compile time (no runtime reflection)

### Project Structure
```
src/
├── DomainBridge.Attributes/     # Attribute definitions (netstandard2.0)
├── DomainBridge.Core/           # Runtime proxy factory & AppDomain management (net472)
├── DomainBridge.SourceGenerators/ # Compile-time code generation (netstandard2.0)
└── DomainBridge.Pipeline/       # Build automation with ModularPipelines (net9.0)
tests/
└── DomainBridge.Tests/          # TUnit test framework
```

### Key Technical Constraints
- Core library MUST target .NET Framework 4.7.2 (AppDomains are not available in .NET Core/.NET 5+)
- Source generators MUST target .NET Standard 2.0 for broad compatibility
- Generated proxy classes inherit from `MarshalByRefObject` for cross-domain marshaling
- All bridged types must be serializable or inherit from MarshalByRefObject

## Development Commands

### Build
```bash
# Build entire solution
dotnet build

# Build in Release mode
dotnet build -c Release

# Build specific project
dotnet build src/DomainBridge.Core/DomainBridge.Core.csproj
```

### Test
```bash
# Run all tests
dotnet test

# Run with detailed output
dotnet test --logger "console;verbosity=detailed"

# Run specific test project
dotnet test tests/DomainBridge.Tests/DomainBridge.Tests.csproj
```

### Pipeline Operations
```bash
# Run full pipeline (build, test, package)
cd src/DomainBridge.Pipeline
dotnet run

# Development mode (local NuGet feed)
DOTNET_ENVIRONMENT=Development dotnet run

# Production mode (publish to NuGet.org)
DOTNET_ENVIRONMENT=Production dotnet run
```

### Package
```bash
# Create NuGet package
dotnet pack src/DomainBridge.Core/DomainBridge.Core.csproj -c Release

# Pack with specific version
dotnet pack src/DomainBridge.Core/DomainBridge.Core.csproj -c Release -p:PackageVersion=1.0.0
```

## Working with Source Generators

When modifying the source generator:
1. Changes in `DomainBridge.SourceGenerators` require rebuilding consumer projects
2. Generated code is visible in: `obj/Debug/net472/generated/`
3. Enable source generator debugging in tests with `<EmitCompilerGeneratedFiles>true</EmitCompilerGeneratedFiles>`
4. Test project has source generator output inspection enabled

## Testing Approach

The project uses TUnit (v0.25.21) for testing:
- Source generator tests verify generated code output
- Core tests verify runtime proxy behavior
- Integration tests demonstrate end-to-end scenarios

## CI/CD Pipeline

GitHub Actions workflow (`.github/workflows/dotnet.yml`):
- Triggers on push to main and PRs
- Uses .NET 9.0.x for pipeline execution
- Environment-based configuration (Development for PRs, Production for main)
- Manual workflow dispatch option for package publishing
- NuGet API key stored in `NUGET__APIKEY` secret

## Common Development Tasks

### Adding New Bridge Features
1. Modify `DomainBridgePatternGenerator` to generate new code patterns
2. Update `ProxyFactory` in Core if runtime changes needed
3. Add tests demonstrating the feature
4. Generated code should maintain compatibility with existing usage

### Debugging Source Generators
1. Set breakpoints in generator code
2. Use `Debugger.Launch()` in generator for interactive debugging
3. Examine generated output in `obj/` directories
4. Use `EmitCompilerGeneratedFiles` in test projects

### Publishing Updates
1. Pipeline automatically generates version numbers
2. Local development publishes to `%LocalNuGetFolder%`
3. Production publishes require `NuGet__ApiKey` secret
4. Manual publish via workflow dispatch with `publish-packages` input

## Important Considerations

- Generated proxy classes must handle cross-domain marshaling correctly
- All exceptions should be serializable for cross-domain propagation
- Assembly resolution happens in isolated domains via `AssemblyResolver`
- Configuration through `DomainConfiguration` affects isolated domain setup
- Shadow copying can be enabled for runtime assembly updates