using System.Linq;
using Microsoft.CodeAnalysis;
using DomainBridge.SourceGenerators.Models;

namespace DomainBridge.SourceGenerators.Services
{
    internal class BridgeClassGenerator
    {
        private readonly TypeNameResolver _typeNameResolver;

        public BridgeClassGenerator(TypeNameResolver typeNameResolver)
        {
            _typeNameResolver = typeNameResolver;
        }

        public void Generate(CodeBuilder builder, string bridgeClassName, TypeModel targetModel)
        {
            // Generate the partial class implementation
            builder.OpenBlock($"public partial class {bridgeClassName}");

            GenerateFields(builder);
            GenerateStaticInstance(builder, bridgeClassName, targetModel);
            GenerateConstructors(builder, bridgeClassName);
            GenerateFactoryMethods(builder, bridgeClassName, targetModel);
            GenerateWrapInstanceMethod(builder);
            GenerateDelegatingMembers(builder, targetModel);
            GenerateDisposalMethod(builder);

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
                builder.AppendLine("private static " + className + "? _instance;");
                builder.AppendLine();
                builder.OpenBlock("public static " + className + " Instance");
                builder.OpenBlock("get");
                builder.OpenBlock("if (_instance == null)");
                builder.OpenBlock("lock (_lock)");
                builder.OpenBlock("if (_instance == null)");
                builder.AppendLine("_instance = CreateIsolated();");
                builder.CloseBlock();
                builder.CloseBlock();
                builder.CloseBlock();
                builder.AppendLine("return _instance;");
                builder.CloseBlock();
                builder.CloseBlock();
                builder.AppendLine();
            }
        }

        private void GenerateConstructors(CodeBuilder builder, string className)
        {
            // Internal constructor for wrapping instances
            builder.OpenBlock($"internal {className}(dynamic instance)");
            builder.AppendLine("_instance = instance ?? throw new ArgumentNullException(nameof(instance));");
            builder.CloseBlock();
            builder.AppendLine();

            // Protected constructor for derived classes
            builder.OpenBlock($"protected {className}()");
            builder.AppendLine("_instance = GetOrCreateRemoteInstance();");
            builder.CloseBlock();
            builder.AppendLine();
        }

        private void GenerateFactoryMethods(CodeBuilder builder, string className, TypeModel targetModel)
        {
            // CreateIsolated method
            builder.OpenBlock($"public static {className} CreateIsolated(DomainConfiguration? config = null)");
            builder.AppendLine("EnsureIsolatedDomain(config);");
            builder.AppendLine("var instance = GetOrCreateRemoteInstance();");
            builder.AppendLine($"return new {className}(instance);");
            builder.CloseBlock();
            builder.AppendLine();

            // EnsureIsolatedDomain method
            builder.OpenBlock("private static void EnsureIsolatedDomain(DomainConfiguration? config = null)");
            builder.OpenBlock("if (_isolatedDomain == null)");
            builder.OpenBlock("lock (_lock)");
            builder.OpenBlock("if (_isolatedDomain == null)");
            builder.AppendLine("config = config ?? new DomainConfiguration();");
            builder.AppendLine($"config.TargetAssembly = typeof({targetModel.Symbol.ToDisplayString()}).Assembly.FullName;");
            builder.AppendLine();
            builder.AppendLine("var setup = new AppDomainSetup");
            builder.OpenBlock("");
            builder.AppendLine("ApplicationBase = config.ApplicationBase ?? AppDomain.CurrentDomain.BaseDirectory,");
            builder.AppendLine("PrivateBinPath = config.PrivateBinPath,");
            builder.AppendLine("ConfigurationFile = config.ConfigurationFile");
            builder.CloseBlock(";");
            builder.AppendLine();
            builder.AppendLine($"_isolatedDomain = AppDomain.CreateDomain(\"{className}_IsolatedDomain\", null, setup);");
            builder.CloseBlock();
            builder.CloseBlock();
            builder.CloseBlock();
            builder.CloseBlock();
            builder.AppendLine();

            // GetOrCreateRemoteInstance method
            builder.OpenBlock("private static dynamic GetOrCreateRemoteInstance()");
            builder.OpenBlock("if (_remoteProxy == null)");
            builder.OpenBlock("lock (_lock)");
            builder.OpenBlock("if (_remoteProxy == null)");
            builder.AppendLine("EnsureIsolatedDomain();");
            builder.AppendLine();
            builder.AppendLine("// Create proxy factory in isolated domain");
            builder.AppendLine("var proxyType = typeof(DomainBridge.Runtime.ProxyFactory);");
            builder.AppendLine("var factory = _isolatedDomain!.CreateInstanceAndUnwrap(");
            builder.AppendLine("    proxyType.Assembly.FullName,");
            builder.AppendLine("    proxyType.FullName) as dynamic;");
            builder.AppendLine();
            builder.AppendLine($"// Get instance of {targetModel.Name}");
            builder.AppendLine($"_remoteProxy = factory.CreateProxy(\"{targetModel.Symbol.ToDisplayString()}\");");
            builder.CloseBlock();
            builder.CloseBlock();
            builder.CloseBlock();
            builder.AppendLine("return _remoteProxy;");
            builder.CloseBlock();
            builder.AppendLine();
        }

        private void GenerateWrapInstanceMethod(CodeBuilder builder)
        {
            // Helper method to wrap instances in their bridge classes
            builder.OpenBlock("private static T WrapInstance<T>(dynamic instance) where T : class");
            builder.AppendLine("if (instance == null) return null;");
            builder.AppendLine();
            builder.AppendLine("// Use reflection to find the constructor");
            builder.AppendLine("var bridgeType = typeof(T);");
            builder.AppendLine("var constructor = bridgeType.GetConstructor(");
            builder.AppendLine("    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance,");
            builder.AppendLine("    null,");
            builder.AppendLine("    new[] { typeof(object) },");
            builder.AppendLine("    null);");
            builder.AppendLine();
            builder.AppendLine("if (constructor == null)");
            builder.AppendLine("{");
            builder.AppendLine("    throw new InvalidOperationException($\"Bridge type {bridgeType.Name} must have an internal constructor that takes a dynamic/object parameter.\");");
            builder.AppendLine("}");
            builder.AppendLine();
            builder.AppendLine("return (T)constructor.Invoke(new object[] { instance });");
            builder.CloseBlock();
            builder.AppendLine();
        }

        private void GenerateDelegatingMembers(CodeBuilder builder, TypeModel targetModel)
        {
            // Generate properties
            foreach (var property in targetModel.Properties.Where(p => !p.IsIgnored))
            {
                var propertyType = _typeNameResolver.GetProxyTypeName(property.Type);
                
                builder.OpenBlock($"public {propertyType} {property.Name}");

                if (property.HasGetter)
                {
                    builder.OpenBlock("get");
                    if (_typeNameResolver.IsComplexType(property.Type))
                    {
                        // Need to wrap complex return types
                        var typeName = (property.Type as INamedTypeSymbol)?.Name ?? property.Type.Name;
                        builder.AppendLine($"var value = _instance.{property.Name};");
                        builder.AppendLine($"return value == null ? null : WrapInstance<{typeName}Bridge>(value);");
                    }
                    else
                    {
                        builder.AppendLine($"return _instance.{property.Name};");
                    }
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
                var returnType = _typeNameResolver.GetProxyTypeName(method.ReturnType);
                var parameters = string.Join(", ", method.Parameters.Select(p =>
                {
                    var paramType = _typeNameResolver.GetProxyTypeName(p.Type);
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
                else if (_typeNameResolver.IsComplexType(method.ReturnType))
                {
                    var typeName = (method.ReturnType as INamedTypeSymbol)?.Name ?? method.ReturnType.Name;
                    builder.AppendLine($"var result = {methodCall};");
                    builder.AppendLine($"return result == null ? null : WrapInstance<{typeName}Bridge>(result);");
                }
                else
                {
                    builder.AppendLine($"return {methodCall};");
                }

                builder.CloseBlock();
                builder.AppendLine();
            }

            // Generate events (simplified for now)
            foreach (var evt in targetModel.Events.Where(e => !e.IsIgnored))
            {
                var eventType = _typeNameResolver.GetProxyTypeName(evt.Type);
                builder.AppendLine($"public event {eventType} {evt.Name}");
                builder.OpenBlock("");
                builder.AppendLine($"add {{ _instance.{evt.Name} += value; }}");
                builder.AppendLine($"remove {{ _instance.{evt.Name} -= value; }}");
                builder.CloseBlock();
                builder.AppendLine();
            }
        }

        private void GenerateDisposalMethod(CodeBuilder builder)
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
            builder.AppendLine("_instance = null;");
            builder.CloseBlock();
            builder.CloseBlock();
            builder.CloseBlock();
            builder.CloseBlock();
        }

        private string FormatDefaultValue(object? value)
        {
            if (value == null) return "null";
            if (value is string str) return $"\"{str}\"";
            if (value is bool b) return b ? "true" : "false";
            return value.ToString() ?? "null";
        }
    }
}