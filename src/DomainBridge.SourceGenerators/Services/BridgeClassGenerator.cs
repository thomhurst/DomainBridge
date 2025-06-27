using System;
using System.Collections.Generic;
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
            var classDeclaration = $"public partial class {bridgeClassName} : global::System.MarshalByRefObject, global::System.IDisposable";
            
            // Add interfaces if any
            if (targetModel.Interfaces.Any())
            {
                var interfaceList = string.Join(", ", targetModel.Interfaces.Select(i => i.ToDisplayString()));
                classDeclaration += $", {interfaceList}";
            }
            
            builder.OpenBlock(classDeclaration);

            GenerateFields(builder);
            GenerateStaticInstance(builder, bridgeClassName, targetModel);
            GenerateConstructors(builder, bridgeClassName, targetModel, config);
            GenerateCreateMethods(builder, bridgeClassName, targetModel);
            GenerateDelegatingMembers(builder, targetModel);
            GenerateDisposalImplementation(builder);

            builder.CloseBlock();
        }

        private void GenerateFields(CodeBuilder builder)
        {
            builder.AppendLine("private readonly global::System.AppDomain _appDomain;");
            builder.AppendLine("private readonly dynamic _instance;");
            builder.AppendLine("private bool _disposed;");
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
                builder.AppendLine("/// <summary>");
                builder.AppendLine($"/// Gets a bridge around the static Instance property of {targetModel.Symbol.Name}");
                builder.AppendLine("/// </summary>");
                builder.OpenBlock($"public static {className} Instance");
                builder.OpenBlock("get");
                builder.AppendLine($"var targetInstance = global::{targetModel.Symbol.ToDisplayString()}.Instance;");
                builder.AppendLine($"// Note: This creates a bridge in the current AppDomain, not an isolated one");
                builder.AppendLine($"return new {className}(targetInstance, global::System.AppDomain.CurrentDomain);");
                builder.CloseBlock();
                builder.CloseBlock();
                builder.AppendLine();
            }
        }

        private void GenerateConstructors(CodeBuilder builder, string className, TypeModel targetModel, AttributeConfiguration? config)
        {
            // Private constructor that takes instance and AppDomain
            builder.AppendLine("/// <summary>");
            builder.AppendLine($"/// Internal constructor for wrapping an existing instance of {targetModel.Symbol.Name}");
            builder.AppendLine("/// </summary>");
            builder.OpenBlock($"private {className}(dynamic instance, global::System.AppDomain appDomain)");
            builder.AppendLine("_instance = instance ?? throw new global::System.ArgumentNullException(nameof(instance));");
            builder.AppendLine("_appDomain = appDomain ?? throw new global::System.ArgumentNullException(nameof(appDomain));");
            builder.CloseBlock();
            builder.AppendLine();
        }

        private void GenerateCreateMethods(CodeBuilder builder, string className, TypeModel targetModel)
        {
            // Generate Create method with factory
            builder.AppendLine("/// <summary>");
            builder.AppendLine($"/// Creates a new isolated instance of {targetModel.Symbol.Name} in a separate AppDomain");
            builder.AppendLine("/// </summary>");
            builder.AppendLine("/// <param name=\"factory\">Factory function to create the target instance in the isolated AppDomain</param>");
            builder.AppendLine("/// <param name=\"config\">Optional AppDomain configuration</param>");
            builder.AppendLine("/// <returns>A bridge instance that must be disposed when no longer needed</returns>");
            builder.OpenBlock($"public static {className} Create(global::System.Func<{targetModel.Symbol.ToDisplayString()}> factory, global::DomainBridge.DomainConfiguration? config = null)");
            builder.AppendLine("if (factory == null) throw new global::System.ArgumentNullException(nameof(factory));");
            builder.AppendLine();
            builder.AppendLine("// Create configuration");
            builder.AppendLine("config = config ?? new global::DomainBridge.DomainConfiguration();");
            builder.AppendLine($"config.TargetAssembly = config.TargetAssembly ?? typeof({targetModel.Symbol.ToDisplayString()}).Assembly.FullName;");
            builder.AppendLine();
            builder.AppendLine("// Create AppDomain");
            builder.AppendLine("var setup = new global::System.AppDomainSetup");
            builder.AppendLine("{");
            builder.AppendLine("    ApplicationBase = config.ApplicationBase ?? global::System.AppDomain.CurrentDomain.BaseDirectory,");
            builder.AppendLine("    PrivateBinPath = config.PrivateBinPath,");
            builder.AppendLine("    ConfigurationFile = config.ConfigurationFile");
            builder.AppendLine("};");
            builder.AppendLine();
            builder.AppendLine("if (config.EnableShadowCopy)");
            builder.AppendLine("{");
            builder.AppendLine("    setup.ShadowCopyFiles = \"true\";");
            builder.AppendLine("    setup.ShadowCopyDirectories = setup.ApplicationBase;");
            builder.AppendLine("}");
            builder.AppendLine();
            builder.AppendLine($"var domainName = $\"{className}_{{global::System.Guid.NewGuid():N}}\";");
            builder.AppendLine("var appDomain = global::System.AppDomain.CreateDomain(domainName, null, setup);");
            builder.AppendLine();
            builder.AppendLine("try");
            builder.AppendLine("{");
            builder.AppendLine("    // Create a proxy factory in the isolated domain");
            builder.AppendLine("    var proxyFactoryType = typeof(global::DomainBridge.Runtime.ProxyFactory);");
            builder.AppendLine("    var proxyFactory = (global::DomainBridge.Runtime.ProxyFactory)appDomain.CreateInstanceAndUnwrap(");
            builder.AppendLine("        proxyFactoryType.Assembly.FullName,");
            builder.AppendLine("        proxyFactoryType.FullName);");
            builder.AppendLine();
            builder.AppendLine("    // Configure the proxy factory with assembly resolver if needed");
            builder.AppendLine("    if (!string.IsNullOrEmpty(config.AssemblySearchPaths))");
            builder.AppendLine("    {");
            builder.AppendLine("        proxyFactory.ConfigureAssemblyResolver(config.AssemblySearchPaths.Split(';'));");
            builder.AppendLine("    }");
            builder.AppendLine();
            builder.AppendLine("    // Create the target instance in the isolated domain using the factory");
            builder.AppendLine($"    var targetInstance = proxyFactory.CreateInstance<{targetModel.Symbol.ToDisplayString()}>(factory);");
            builder.AppendLine();
            builder.AppendLine($"    // Create and return the bridge");
            builder.AppendLine($"    return new {className}(targetInstance, appDomain);");
            builder.AppendLine("}");
            builder.AppendLine("catch");
            builder.AppendLine("{");
            builder.AppendLine("    // If creation fails, unload the domain");
            builder.AppendLine("    try { global::System.AppDomain.Unload(appDomain); } catch { }");
            builder.AppendLine("    throw;");
            builder.AppendLine("}");
            builder.CloseBlock();
            builder.AppendLine();

            // Check if type has parameterless constructor
            var hasParameterlessConstructor = targetModel.Symbol.InstanceConstructors
                .Any(c => c.Parameters.Length == 0 && c.DeclaredAccessibility == Accessibility.Public);

            if (hasParameterlessConstructor)
            {
                builder.AppendLine("/// <summary>");
                builder.AppendLine($"/// Creates a new isolated instance of {targetModel.Symbol.Name} in a separate AppDomain using the default constructor");
                builder.AppendLine("/// </summary>");
                builder.AppendLine("/// <param name=\"config\">Optional AppDomain configuration</param>");
                builder.AppendLine("/// <returns>A bridge instance that must be disposed when no longer needed</returns>");
                builder.OpenBlock($"public static {className} Create(global::DomainBridge.DomainConfiguration? config = null)");
                builder.AppendLine($"return Create(() => new {targetModel.Symbol.ToDisplayString()}(), config);");
                builder.CloseBlock();
                builder.AppendLine();
            }
        }

        private void GenerateDelegatingMembers(CodeBuilder builder, TypeModel targetModel)
        {
            // Generate properties
            foreach (var property in targetModel.Properties.Where(p => !p.IsIgnored))
            {
                var propertyType = GetTypeDisplayString(property.Type);
                
                if (property.IsIndexer)
                {
                    // Generate indexer with parameters
                    var parameters = string.Join(", ", property.Parameters.Select(p =>
                    {
                        var paramType = GetTypeDisplayString(p.Type);
                        var defaultValue = p.HasDefaultValue ? $" = {FormatDefaultValue(p.DefaultValue)}" : "";
                        return $"{paramType} {EscapeIdentifier(p.Name)}{defaultValue}";
                    }));
                    
                    builder.OpenBlock($"public {propertyType} this[{parameters}]");
                    
                    if (property.HasGetter)
                    {
                        builder.OpenBlock("get");
                        builder.AppendLine("CheckDisposed();");
                        var args = string.Join(", ", property.Parameters.Select(p => EscapeIdentifier(p.Name)));
                        builder.AppendLine($"return _instance[{args}];");
                        builder.CloseBlock();
                    }
                    
                    if (property.HasSetter)
                    {
                        builder.OpenBlock("set");
                        builder.AppendLine("CheckDisposed();");
                        var args = string.Join(", ", property.Parameters.Select(p => EscapeIdentifier(p.Name)));
                        builder.AppendLine($"_instance[{args}] = value;");
                        builder.CloseBlock();
                    }
                }
                else
                {
                    // Generate regular property
                    builder.OpenBlock($"public {propertyType} {property.Name}");

                    if (property.HasGetter)
                    {
                        builder.OpenBlock("get");
                        builder.AppendLine("CheckDisposed();");
                        builder.AppendLine($"return _instance.{property.Name};");
                        builder.CloseBlock();
                    }

                    if (property.HasSetter)
                    {
                        builder.OpenBlock("set");
                        builder.AppendLine("CheckDisposed();");
                        builder.AppendLine($"_instance.{property.Name} = value;");
                        builder.CloseBlock();
                    }
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
                    return $"{paramType} {EscapeIdentifier(p.Name)}{defaultValue}";
                }));

                builder.OpenBlock($"public {returnType} {method.Name}({parameters})");
                builder.AppendLine("CheckDisposed();");
                
                var args = string.Join(", ", method.Parameters.Select(p => EscapeIdentifier(p.Name)));
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
                builder.AppendLine($"add {{ CheckDisposed(); _instance.{evt.Name} += value; }}");
                builder.AppendLine($"remove {{ CheckDisposed(); _instance.{evt.Name} -= value; }}");
                builder.CloseBlock();
                builder.AppendLine();
            }
        }

        private void GenerateDisposalImplementation(CodeBuilder builder)
        {
            // Generate Dispose method
            builder.AppendLine("/// <summary>");
            builder.AppendLine("/// Disposes the bridge and unloads the associated AppDomain");
            builder.AppendLine("/// </summary>");
            builder.OpenBlock("public void Dispose()");
            builder.AppendLine("Dispose(true);");
            builder.AppendLine("global::System.GC.SuppressFinalize(this);");
            builder.CloseBlock();
            builder.AppendLine();

            // Generate protected Dispose method
            builder.OpenBlock("protected virtual void Dispose(bool disposing)");
            builder.OpenBlock("if (!_disposed)");
            builder.OpenBlock("if (disposing)");
            builder.AppendLine("// Dispose managed resources");
            builder.OpenBlock("if (_appDomain != null && _appDomain != global::System.AppDomain.CurrentDomain)");
            builder.OpenBlock("try");
            builder.AppendLine("global::System.AppDomain.Unload(_appDomain);");
            builder.CloseBlock();
            builder.OpenBlock("catch (global::System.Exception ex)");
            builder.AppendLine("// Log but don't throw from Dispose");
            builder.AppendLine("global::System.Diagnostics.Debug.WriteLine($\"Failed to unload AppDomain: {ex.Message}\");");
            builder.CloseBlock();
            builder.CloseBlock();
            builder.CloseBlock();
            builder.AppendLine("_disposed = true;");
            builder.CloseBlock();
            builder.CloseBlock();
            builder.AppendLine();

            // Generate CheckDisposed method
            builder.OpenBlock("private void CheckDisposed()");
            builder.OpenBlock("if (_disposed)");
            builder.AppendLine("throw new global::System.ObjectDisposedException(GetType().FullName);");
            builder.CloseBlock();
            builder.CloseBlock();
        }

        private string GetTypeDisplayString(ITypeSymbol type)
        {
            // Always return the actual type instead of dynamic
            // The runtime will handle marshaling across AppDomain boundaries
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
            if (value is string str) return FormatStringLiteral(str);
            if (value is bool b) return b ? "true" : "false";
            return value.ToString() ?? "null";
        }

        private string EscapeIdentifier(string identifier)
        {
            // List of C# reserved keywords that need escaping
            var keywords = new HashSet<string>
            {
                "abstract", "as", "base", "bool", "break", "byte", "case", "catch", "char", "checked",
                "class", "const", "continue", "decimal", "default", "delegate", "do", "double", "else", "enum",
                "event", "explicit", "extern", "false", "finally", "fixed", "float", "for", "foreach", "goto",
                "if", "implicit", "in", "int", "interface", "internal", "is", "lock", "long", "namespace",
                "new", "null", "object", "operator", "out", "override", "params", "private", "protected", "public",
                "readonly", "ref", "return", "sbyte", "sealed", "short", "sizeof", "stackalloc", "static", "string",
                "struct", "switch", "this", "throw", "true", "try", "typeof", "uint", "ulong", "unchecked",
                "unsafe", "ushort", "using", "virtual", "void", "volatile", "while"
            };

            return keywords.Contains(identifier) ? $"@{identifier}" : identifier;
        }

        private string FormatStringLiteral(string value)
        {
            // Escape special characters in string literals
            return "\"" + value
                .Replace("\\", "\\\\")
                .Replace("\"", "\\\"")
                .Replace("\r", "\\r")
                .Replace("\n", "\\n")
                .Replace("\t", "\\t") + "\"";
        }
    }
}