# DomainBridge

A .NET library that automatically generates AppDomain isolation proxies to solve assembly version conflicts.

## Features

- ✨ **Automatic Proxy Generation** - Source generators create all proxy classes at compile time
- 🔒 **Complete Isolation** - Run third-party libraries in isolated AppDomains  
- 🚀 **Type-Safe** - Strongly typed classes with full IntelliSense support
- 🎯 **Minimal Code Changes** - Use libraries almost exactly as before
- 🔄 **Handles Nested Types** - Automatically proxies returned objects

## Quick Start

1. Install the NuGet package:
```bash
dotnet add package DomainBridge
```

2. Create a bridge class:
```csharp
[DomainBridge(typeof(ThirdPartyApplication))]
public partial class ThirdPartyApplicationBridge
{
    // Source generator creates all members from ThirdPartyApplication
}
```

3. Use it exactly like the original:
```csharp
// Just use the bridge class directly!
var app = ThirdPartyApplicationBridge.Instance;
app.Connect("localhost", 8080);

// Nested objects are automatically bridged too
var db = app.GetDatabase(1);        // Returns DatabaseBridge
var doc = db.GetDocument("id");     // Returns DocumentBridge
Console.WriteLine(doc.Name);

// Clean up when done
ThirdPartyApplicationBridge.UnloadDomain();
```

## How It Works

DomainBridge uses C# source generators to:
1. Analyze the type specified in `[DomainBridge(typeof(...))]`
2. Generate a partial class implementation with all members
3. Marshal calls across AppDomain boundaries
4. Automatically create bridge classes for nested types

## Real-World Example

```csharp
// Your code - dealing with a problematic third-party library
[DomainBridge(typeof(LegacyApp))]
public partial class LegacyAppBridge { }

// Usage - looks exactly like normal code!
var app = LegacyAppBridge.Instance;
var result = app.ProcessData(input);

// But it's actually running in complete isolation!
// Different assembly versions, no conflicts, no crashes
```

## Configuration

You can optionally configure the isolated domain:

```csharp
// Override CreateIsolated method to provide custom configuration
var app = LegacyAppBridge.CreateIsolated(new DomainConfiguration
{
    PrivateBinPath = "ThirdPartyLibs",
    EnableShadowCopy = true,
    AssemblyMappings = new Dictionary<string, string>
    {
        ["OldAssembly"] = @"C:\libs\OldAssembly.dll"
    }
});
```

## Benefits

- **Solve Version Conflicts** - Load different assembly versions in separate AppDomains
- **Prevent Crashes** - Isolate unstable third-party libraries
- **Zero Reflection** - All proxies are generated at compile time
- **Natural API** - Use the bridge class just like the original

## Requirements

- .NET Framework 4.7.2 or higher (AppDomains are not supported in .NET Core/.NET 5+)
- C# 9.0 or higher (for source generators)

## License

MIT