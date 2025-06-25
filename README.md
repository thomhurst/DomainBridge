# DomainBridge

A .NET library that automatically generates AppDomain isolation proxies to solve assembly version conflicts.

## Features

- ✨ **Automatic Proxy Generation** - Source generators create all proxy classes at compile time
- 🔒 **Complete Isolation** - Run third-party libraries in isolated AppDomains
- 🚀 **Type-Safe** - Strongly typed interfaces with full IntelliSense support
- 🎯 **Minimal Code Changes** - Use libraries almost exactly as before
- 🔄 **Handles Nested Types** - Automatically proxies returned objects

## Quick Start

1. Install the NuGet package:
```bash
dotnet add package DomainBridge
```

2. Mark your entry point class:
```csharp
[DomainBridge]
public class ThirdPartyApplication
{
    public static ThirdPartyApplication Instance { get; }
    public Database GetDatabase(int id);
}
```

3. Use the generated proxy:
```csharp
// Instead of: var app = ThirdPartyApplication.Instance;
var app = DomainBridge.Create<IThirdPartyApplication>();

// Everything else stays the same!
var db = app.GetDatabase(1);
var doc = db.GetDocument("id");
var name = doc.Name;
```

## How It Works

DomainBridge uses C# source generators to:
1. Analyze types marked with `[DomainBridge]`
2. Generate interfaces for all public members
3. Create proxy classes that marshal calls across AppDomain boundaries
4. Automatically handle nested object returns

## Benefits

- **Solve Version Conflicts** - Load different assembly versions in separate AppDomains
- **Prevent Crashes** - Isolate unstable third-party libraries
- **No Reflection** - All proxies are generated at compile time
- **Clean API** - Maintain the original library's interface

## License

MIT