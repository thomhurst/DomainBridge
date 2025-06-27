# Debugging Source Generator Issues

If the source generator isn't producing output, check:

## 1. Enable Generator Output
Add this to your project file to see generated files:
```xml
<PropertyGroup>
  <EmitCompilerGeneratedFiles>true</EmitCompilerGeneratedFiles>
  <CompilerGeneratedFilesOutputPath>$(BaseIntermediateOutputPath)\GeneratedFiles</CompilerGeneratedFilesOutputPath>
</PropertyGroup>
```

## 2. Check Build Output
Look for diagnostic messages:
- DBG000: Generator Failed (general failure)
- DBG001: Bridge Generation Failed (specific bridge failed)
- DBG002: Type Analysis Failed (couldn't analyze target type)
- DBG003: Auto Bridge Generation Failed (auto-discovery failed)

## 3. Common Issues

### Missing partial keyword
The source generator will now generate code even without the `partial` keyword, but this will result in a compiler error:
```csharp
// ⚠️ Will generate, but causes compiler error CS0101
[DomainBridge(typeof(MyService))]
public class MyServiceBridge { }

// ✅ Correct usage
[DomainBridge(typeof(MyService))]
public partial class MyServiceBridge { }
```
The compiler error will be clear: "The namespace 'X' already contains a definition for 'MyServiceBridge'"

### Missing references
Ensure your project references:
```xml
<ItemGroup>
  <PackageReference Include="DomainBridge.Core" Version="x.x.x" />
  <PackageReference Include="DomainBridge.Attributes" Version="x.x.x" />
</ItemGroup>
```

### Analyzer not referenced correctly
The source generator must be referenced as an analyzer:
```xml
<ItemGroup>
  <PackageReference Include="DomainBridge.Core" Version="x.x.x" />
</ItemGroup>
```

## 4. Verify Generator is Running
1. Add a syntax error in generated code location (e.g., create a method with same signature)
2. If you get a duplicate member error, the generator is running
3. If no error, the generator isn't running

## 5. Check Generated Files
After building with `EmitCompilerGeneratedFiles=true`, check:
- `obj\Debug\net472\GeneratedFiles\DomainBridge.SourceGenerators\DomainBridge.SourceGenerators.DomainBridgePatternGenerator\`

## 6. Enable Detailed MSBuild Output
```bash
dotnet build -v detailed > build.log 2>&1
```
Search the log for "DomainBridge" to see generator execution.

## 7. Debugger Attachment
For development, add to generator code:
```csharp
#if DEBUG
System.Diagnostics.Debugger.Launch();
#endif
```