# DomainBridge

A .NET library that automatically generates AppDomain isolation proxies to solve assembly version conflicts.

## Features

- ✨ **Automatic Proxy Generation** - Source generators create all proxy classes at compile time
- 🔒 **Complete Isolation** - Run third-party libraries in isolated AppDomains  
- 🚀 **Type-Safe** - Strongly typed classes with full IntelliSense support
- 🎯 **Minimal Code Changes** - Use libraries almost exactly as before
- 🔄 **Handles Nested Types** - Automatically proxies returned objects

## Quick Start

### Method 1: Bridge Pattern (Recommended)

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

### Method 2: Interface Pattern

1. Mark the target type:
```csharp
[DomainBridge]
public class ThirdPartyApplication
{
    public static ThirdPartyApplication Instance { get; }
    public Database GetDatabase(int id);
}
```

2. Use the generated interface:
```csharp
var app = DomainBridge.Create<IThirdPartyApplication>();
var db = app.GetDatabase(1);
var doc = db.GetDocument("id");
```

## How It Works

DomainBridge uses C# source generators to:
1. Analyze types marked with `[DomainBridge]` or referenced in bridge classes
2. Generate strongly-typed proxy classes 
3. Marshal calls across AppDomain boundaries
4. Automatically handle nested object returns

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

## Benefits

- **Solve Version Conflicts** - Load different assembly versions in separate AppDomains
- **Prevent Crashes** - Isolate unstable third-party libraries
- **Zero Reflection** - All proxies are generated at compile time
- **Natural API** - Use the bridge class just like the original

## License

MIT