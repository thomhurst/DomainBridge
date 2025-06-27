using System.Linq;
using Microsoft.CodeAnalysis;
using DomainBridge.SourceGenerators.Models;

namespace DomainBridge.SourceGenerators.Services
{
    internal class BridgeClassGenerator
    {
        public void Generate(CodeBuilder builder, string bridgeClassName, TypeModel targetModel, AttributeConfiguration? config)
        {
            // Generate the partial class implementation
            builder.OpenBlock($"public partial class {bridgeClassName} : MarshalByRefObject");

            GenerateFields(builder);
            GenerateStaticInstance(builder, bridgeClassName, targetModel);
            GenerateConstructors(builder, bridgeClassName, targetModel, config);
            GenerateFactoryMethods(builder, bridgeClassName, targetModel, config);
            GenerateDelegatingMembers(builder, targetModel);
            GenerateDisposalMethod(builder, targetModel);

            builder.CloseBlock();
        }

        private void GenerateFields(CodeBuilder builder)
        {
            builder.AppendLine("private static readonly object _lock = new object();");
            builder.AppendLine("private static AppDomain? _isolatedDomain;");
            builder.AppendLine("private static dynamic? _remoteProxy;");
            builder.AppendLine("private readonly dynamic _instance;");
            builder.AppendLine();
        }

        private void GenerateStaticInstance(CodeBuilder builder, string className, TypeModel targetModel)
        {
            // Generate singleton Instance property if the target has one
            var hasStaticInstance = targetModel.Symbol.GetMembers("Instance")
                .OfType<IPropertySymbol>()
                .Any(p => p.IsStatic && p.DeclaredAccessibility == Accessibility.Public);

            if (hasStaticInstance)
            {
                builder.AppendLine("private static " + className + "? _singletonInstance;");
                builder.AppendLine();
                builder.OpenBlock("public static " + className + " Instance");
                builder.OpenBlock("get");
                builder.OpenBlock("if (_singletonInstance == null)");
                builder.OpenBlock("lock (_lock)");
                builder.OpenBlock("if (_singletonInstance == null)");
                builder.AppendLine("_singletonInstance = GetOrCreateRemoteBridge();");
                builder.CloseBlock();
                builder.CloseBlock();
                builder.CloseBlock();
                builder.AppendLine("return _singletonInstance;");
                builder.CloseBlock();
                builder.CloseBlock();
                builder.AppendLine();
            }
        }

        private void GenerateConstructors(CodeBuilder builder, string className, TypeModel targetModel, AttributeConfiguration? config)
        {
            // Internal constructor for wrapping instances
            builder.OpenBlock($"internal {className}(dynamic instance)");
            builder.AppendLine("_instance = instance ?? throw new ArgumentNullException(nameof(instance));");
            builder.CloseBlock();
            builder.AppendLine();

            // Public parameterless constructor - used when created in isolated domain
            builder.OpenBlock($"public {className}()");
            builder.AppendLine($"// When created in isolated domain, create the target instance directly");
            
            if (!string.IsNullOrEmpty(config?.FactoryMethod))
            {
                // Use the specified factory method
                builder.AppendLine($"_instance = {config.FactoryMethod}();");
            }
            else
            {
                // Fall back to default behavior
                builder.AppendLine($"var targetType = typeof(global::{targetModel.Symbol.ToDisplayString()});");
                builder.AppendLine($"var instanceProperty = targetType.GetProperty(\"Instance\", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);");
                builder.AppendLine($"if (instanceProperty != null && instanceProperty.CanRead)");
                builder.AppendLine($"{{");
                builder.AppendLine($"    _instance = instanceProperty.GetValue(null);");
                builder.AppendLine($"}}");
                builder.AppendLine($"else");
                builder.AppendLine($"{{");
                builder.AppendLine($"    _instance = Activator.CreateInstance(targetType);");
                builder.AppendLine($"}}");
            }
            
            builder.CloseBlock();
            builder.AppendLine();
        }

        private void GenerateFactoryMethods(CodeBuilder builder, string className, TypeModel targetModel, AttributeConfiguration? config)
        {
            // CreateIsolated method
            builder.OpenBlock($"public static {className} CreateIsolated(DomainConfiguration? config = null)");
            builder.AppendLine("EnsureIsolatedDomain(config);");
            builder.AppendLine("return GetOrCreateRemoteBridge();");
            builder.CloseBlock();
            builder.AppendLine();

            // EnsureIsolatedDomain method
            GenerateEnsureIsolatedDomainMethod(builder, className, targetModel, config);

            // GetOrCreateRemoteInstance method
            builder.OpenBlock($"private static {className} GetOrCreateRemoteBridge()");
            builder.OpenBlock("if (_remoteProxy == null)");
            builder.OpenBlock("lock (_lock)");
            builder.OpenBlock("if (_remoteProxy == null)");
            builder.AppendLine("EnsureIsolatedDomain();");
            builder.AppendLine();
            builder.AppendLine($"// Create bridge instance in isolated domain");
            builder.AppendLine($"var bridgeType = typeof({className});");
            builder.AppendLine("_remoteProxy = _isolatedDomain!.CreateInstanceAndUnwrap(");
            builder.AppendLine("    bridgeType.Assembly.FullName,");
            builder.AppendLine("    bridgeType.FullName);");
            builder.CloseBlock();
            builder.CloseBlock();
            builder.CloseBlock();
            builder.AppendLine($"return ({className})_remoteProxy;");
            builder.CloseBlock();
            builder.AppendLine();
        }

        private void GenerateDelegatingMembers(CodeBuilder builder, TypeModel targetModel)
        {
            // Generate properties
            foreach (var property in targetModel.Properties.Where(p => !p.IsIgnored))
            {
                var propertyType = GetTypeDisplayString(property.Type);
                
                builder.OpenBlock($"public {propertyType} {property.Name}");

                if (property.HasGetter)
                {
                    builder.OpenBlock("get");
                    builder.AppendLine($"return _instance.{property.Name};");
                    builder.CloseBlock();
                }

                if (property.HasSetter)
                {
                    builder.OpenBlock("set");
                    builder.AppendLine($"_instance.{property.Name} = value;");
                    builder.CloseBlock();
                }

                builder.CloseBlock();
                builder.AppendLine();
            }

            // Generate methods
            foreach (var method in targetModel.Methods.Where(m => !m.IsIgnored))
            {
                var returnType = GetTypeDisplayString(method.ReturnType);
                var parameters = string.Join(", ", method.Parameters.Select(p =>
                {
                    var paramType = GetTypeDisplayString(p.Type);
                    var defaultValue = p.HasDefaultValue ? $" = {FormatDefaultValue(p.DefaultValue)}" : "";
                    return $"{paramType} {p.Name}{defaultValue}";
                }));

                builder.OpenBlock($"public {returnType} {method.Name}({parameters})");
                
                var args = string.Join(", ", method.Parameters.Select(p => p.Name));
                var methodCall = $"_instance.{method.Name}({args})";

                if (method.ReturnType.SpecialType == SpecialType.System_Void)
                {
                    builder.AppendLine($"{methodCall};");
                }
                else
                {
                    builder.AppendLine($"return {methodCall};");
                }

                builder.CloseBlock();
                builder.AppendLine();
            }

            // Generate events
            foreach (var evt in targetModel.Events.Where(e => !e.IsIgnored))
            {
                var eventType = GetTypeDisplayString(evt.Type);
                builder.AppendLine($"public event {eventType} {evt.Name}");
                builder.OpenBlock("");
                builder.AppendLine($"add {{ _instance.{evt.Name} += value; }}");
                builder.AppendLine($"remove {{ _instance.{evt.Name} -= value; }}");
                builder.CloseBlock();
                builder.AppendLine();
            }
        }

        private void GenerateDisposalMethod(CodeBuilder builder, TypeModel targetModel)
        {
            // Static disposal method
            builder.OpenBlock("public static void UnloadDomain()");
            builder.OpenBlock("lock (_lock)");
            builder.OpenBlock("if (_isolatedDomain != null)");
            builder.OpenBlock("try");
            builder.AppendLine("AppDomain.Unload(_isolatedDomain);");
            builder.CloseBlock();
            builder.OpenBlock("catch (Exception ex)");
            builder.AppendLine("System.Diagnostics.Debug.WriteLine($\"Failed to unload domain: {ex.Message}\");");
            builder.CloseBlock();
            builder.OpenBlock("finally");
            builder.AppendLine("_isolatedDomain = null;");
            builder.AppendLine("_remoteProxy = null;");
            
            // Only clear singleton instance if the target has one
            var hasStaticInstance = targetModel.Symbol.GetMembers("Instance")
                .OfType<IPropertySymbol>()
                .Any(p => p.IsStatic && p.DeclaredAccessibility == Accessibility.Public);
            
            if (hasStaticInstance)
            {
                builder.AppendLine("_singletonInstance = null;");
            }
            
            builder.CloseBlock();
            builder.CloseBlock();
            builder.CloseBlock();
            builder.CloseBlock();
        }

        private string GetTypeDisplayString(ITypeSymbol type)
        {
            // For now, we'll use dynamic for all non-primitive types to let AppDomain handle marshaling
            if (type.SpecialType != SpecialType.None && type.SpecialType != SpecialType.System_Object)
            {
                return type.ToDisplayString();
            }

            if (type is IArrayTypeSymbol)
            {
                return "dynamic";
            }

            if (type is INamedTypeSymbol namedType)
            {
                // Keep generic collections with their type arguments
                if (IsGenericCollection(namedType))
                {
                    return type.ToDisplayString();
                }

                // For complex types, use dynamic to allow cross-domain calls
                if (IsComplexType(type))
                {
                    return "dynamic";
                }
            }

            return type.ToDisplayString();
        }

        private bool IsComplexType(ITypeSymbol type)
        {
            if (type.SpecialType != SpecialType.None)
                return false;

            if (type.TypeKind == TypeKind.Enum)
                return false;

            if (type.ContainingAssembly?.Name.StartsWith("System") == true)
                return false;

            if (type.TypeKind == TypeKind.Interface)
                return false;

            return type is INamedTypeSymbol && type.TypeKind == TypeKind.Class;
        }

        private bool IsGenericCollection(INamedTypeSymbol type)
        {
            var name = type.Name;
            return (name == "List" || name == "IList" || name == "IEnumerable" ||
                    name == "ICollection" || name == "HashSet" || name == "ISet") &&
                   type.TypeArguments.Length == 1;
        }

        private string FormatDefaultValue(object? value)
        {
            if (value == null) return "null";
            if (value is string str) return $"\"{str}\"";
            if (value is bool b) return b ? "true" : "false";
            return value.ToString() ?? "null";
        }

        private void GenerateEnsureIsolatedDomainMethod(CodeBuilder builder, string className, TypeModel targetModel, AttributeConfiguration? config)
        {
            builder.OpenBlock("private static void EnsureIsolatedDomain(DomainConfiguration? config = null)");
            builder.OpenBlock("if (_isolatedDomain == null)");
            builder.OpenBlock("lock (_lock)");
            builder.OpenBlock("if (_isolatedDomain == null)");
            
            // Create or merge configuration
            if (config != null)
            {
                builder.AppendLine("// Apply attribute configuration as defaults");
                builder.AppendLine("var defaultConfig = new DomainConfiguration");
                builder.AppendLine("{");
                builder.AppendLine($"    TargetAssembly = typeof({targetModel.Symbol.ToDisplayString()}).Assembly.FullName,");
                
                if (!string.IsNullOrEmpty(config.PrivateBinPath))
                    builder.AppendLine($"    PrivateBinPath = \"{config.PrivateBinPath}\",");
                    
                if (!string.IsNullOrEmpty(config.ApplicationBase))
                    builder.AppendLine($"    ApplicationBase = \"{config.ApplicationBase}\",");
                    
                if (!string.IsNullOrEmpty(config.ConfigurationFile))
                    builder.AppendLine($"    ConfigurationFile = \"{config.ConfigurationFile}\",");
                    
                if (config.EnableShadowCopy)
                    builder.AppendLine("    EnableShadowCopy = true,");
                    
                if (!string.IsNullOrEmpty(config.AssemblySearchPaths))
                    builder.AppendLine($"    AssemblySearchPaths = \"{config.AssemblySearchPaths}\"");
                    
                builder.AppendLine("};");
                builder.AppendLine();
                builder.AppendLine("// Merge with runtime config (runtime config takes precedence)");
                builder.AppendLine("config = config ?? defaultConfig;");
                builder.AppendLine("config.TargetAssembly = config.TargetAssembly ?? defaultConfig.TargetAssembly;");
                builder.AppendLine("config.PrivateBinPath = config.PrivateBinPath ?? defaultConfig.PrivateBinPath;");
                builder.AppendLine("config.ApplicationBase = config.ApplicationBase ?? defaultConfig.ApplicationBase;");
                builder.AppendLine("config.ConfigurationFile = config.ConfigurationFile ?? defaultConfig.ConfigurationFile;");
                builder.AppendLine("config.EnableShadowCopy = config.EnableShadowCopy || defaultConfig.EnableShadowCopy;");
                builder.AppendLine("config.AssemblySearchPaths = config.AssemblySearchPaths ?? defaultConfig.AssemblySearchPaths;");
            }
            else
            {
                builder.AppendLine("config = config ?? new DomainConfiguration();");
                builder.AppendLine($"config.TargetAssembly = typeof({targetModel.Symbol.ToDisplayString()}).Assembly.FullName;");
            }
            
            builder.AppendLine();
            builder.AppendLine("var setup = new AppDomainSetup");
            builder.AppendLine("{");
            builder.AppendLine("    ApplicationBase = config.ApplicationBase ?? AppDomain.CurrentDomain.BaseDirectory,");
            builder.AppendLine("    PrivateBinPath = config.PrivateBinPath,");
            builder.AppendLine("    ConfigurationFile = config.ConfigurationFile");
            
            if (config?.EnableShadowCopy == true)
            {
                builder.AppendLine(",");
                builder.AppendLine("    ShadowCopyFiles = \"true\",");
                builder.AppendLine("    ShadowCopyDirectories = config.ApplicationBase ?? AppDomain.CurrentDomain.BaseDirectory");
            }
            
            builder.AppendLine("};");
            builder.AppendLine();
            builder.AppendLine($"var domainName = $\"{className}_IsolatedDomain_{{Guid.NewGuid():N}}\";");
            builder.AppendLine("_isolatedDomain = AppDomain.CreateDomain(domainName, null, setup);");
            
            // Add assembly search paths if specified
            if (config?.AssemblySearchPaths != null)
            {
                builder.AppendLine();
                builder.AppendLine("// Add additional assembly search paths");
                builder.AppendLine("if (!string.IsNullOrEmpty(config.AssemblySearchPaths))");
                builder.AppendLine("{");
                builder.AppendLine("    var resolver = _isolatedDomain.CreateInstanceAndUnwrap(");
                builder.AppendLine("        typeof(DomainBridge.Runtime.AssemblyResolver).Assembly.FullName,");
                builder.AppendLine("        typeof(DomainBridge.Runtime.AssemblyResolver).FullName) as DomainBridge.Runtime.AssemblyResolver;");
                builder.AppendLine("    resolver?.AddSearchPaths(config.AssemblySearchPaths.Split(';'));");
                builder.AppendLine("}");
            }
            
            builder.CloseBlock();
            builder.CloseBlock();
            builder.CloseBlock();
            builder.CloseBlock();
            builder.AppendLine();
        }
    }
}