using System.Linq;
using Microsoft.CodeAnalysis;
using DomainBridge.SourceGenerators.Models;

namespace DomainBridge.SourceGenerators.Services
{
    internal class ProxyGenerator
    {
        private readonly TypeNameResolver _typeNameResolver;

        public ProxyGenerator(TypeNameResolver typeNameResolver)
        {
            _typeNameResolver = typeNameResolver;
        }

        public void Generate(CodeBuilder builder, TypeModel model)
        {
            var className = $"{model.Name}Proxy";
            
            builder.OpenBlock($"public class {className} : MarshalByRefObject, I{model.Name}");
            
            GenerateFields(builder);
            GenerateConstructor(builder, className);
            GenerateFactoryMethod(builder, className);
            GenerateProperties(builder, model);
            GenerateMethods(builder, model);
            GenerateEvents(builder, model);
            GenerateLifetimeService(builder);
            
            builder.CloseBlock();
        }

        private void GenerateFields(CodeBuilder builder)
        {
            builder.AppendLine("private readonly dynamic _instance;");
            builder.AppendLine("private static readonly ConcurrentDictionary<object, WeakReference> _cache = new ConcurrentDictionary<object, WeakReference>();");
            builder.AppendLine();
        }

        private void GenerateConstructor(CodeBuilder builder, string className)
        {
            builder.OpenBlock($"private {className}(object instance)");
            builder.AppendLine("_instance = instance ?? throw new ArgumentNullException(nameof(instance));");
            builder.CloseBlock();
            builder.AppendLine();
        }

        private void GenerateFactoryMethod(CodeBuilder builder, string className)
        {
            builder.OpenBlock($"internal static {className} GetOrCreate(object instance)");
            builder.AppendLine("if (instance == null) return null;");
            builder.AppendLine();
            builder.AppendLine("// Try to get from cache");
            builder.OpenBlock("if (_cache.TryGetValue(instance, out var weakRef) && weakRef.IsAlive)");
            builder.AppendLine($"return ({className})weakRef.Target;");
            builder.CloseBlock();
            builder.AppendLine();
            builder.AppendLine("// Create new proxy");
            builder.AppendLine($"var proxy = new {className}(instance);");
            builder.AppendLine("_cache[instance] = new WeakReference(proxy);");
            builder.AppendLine("return proxy;");
            builder.CloseBlock();
            builder.AppendLine();
        }

        private void GenerateProperties(CodeBuilder builder, TypeModel model)
        {
            foreach (var property in model.Properties.Where(p => !p.IsIgnored))
            {
                var proxyType = _typeNameResolver.GetProxyTypeName(property.Type);
                
                builder.OpenBlock($"public {proxyType} {property.Name}");

                if (property.HasGetter)
                {
                    builder.OpenBlock("get");
                    GeneratePropertyGetter(builder, property);
                    builder.CloseBlock();
                }

                if (property.HasSetter)
                {
                    builder.OpenBlock("set");
                    GeneratePropertySetter(builder, property);
                    builder.CloseBlock();
                }

                builder.CloseBlock();
                builder.AppendLine();
            }
        }

        private void GeneratePropertyGetter(CodeBuilder builder, PropertyModel property)
        {
            if (_typeNameResolver.IsComplexType(property.Type))
            {
                var typeName = (property.Type as INamedTypeSymbol)?.Name ?? property.Type.Name;
                builder.AppendLine($"var value = _instance.{property.Name};");
                builder.AppendLine($"return value == null ? null : {typeName}Proxy.GetOrCreate(value);");
            }
            else if (_typeNameResolver.IsCollectionType(property.Type))
            {
                GenerateCollectionConversion(builder, property.Type, $"_instance.{property.Name}");
            }
            else
            {
                builder.AppendLine($"return _instance.{property.Name};");
            }
        }

        private void GeneratePropertySetter(CodeBuilder builder, PropertyModel property)
        {
            if (_typeNameResolver.IsComplexType(property.Type))
            {
                builder.AppendLine("throw new NotSupportedException(\"Setting complex types is not supported\");");
            }
            else
            {
                builder.AppendLine($"_instance.{property.Name} = value;");
            }
        }

        private void GenerateMethods(CodeBuilder builder, TypeModel model)
        {
            foreach (var method in model.Methods.Where(m => !m.IsIgnored))
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
                    builder.AppendLine($"return result == null ? null : {typeName}Proxy.GetOrCreate(result);");
                }
                else if (_typeNameResolver.IsCollectionType(method.ReturnType))
                {
                    builder.AppendLine($"var result = {methodCall};");
                    GenerateCollectionConversion(builder, method.ReturnType, "result");
                }
                else
                {
                    builder.AppendLine($"return {methodCall};");
                }

                builder.CloseBlock();
                builder.AppendLine();
            }
        }

        private void GenerateEvents(CodeBuilder builder, TypeModel model)
        {
            foreach (var evt in model.Events.Where(e => !e.IsIgnored))
            {
                var eventType = _typeNameResolver.GetProxyTypeName(evt.Type);
                builder.AppendLine($"public event {eventType} {evt.Name};");
                builder.AppendLine();
                
                // TODO: Implement event proxying
            }
        }

        private void GenerateLifetimeService(CodeBuilder builder)
        {
            builder.OpenBlock("public override object InitializeLifetimeService()");
            builder.AppendLine("return null; // Infinite lifetime");
            builder.CloseBlock();
        }

        private void GenerateCollectionConversion(CodeBuilder builder, ITypeSymbol collectionType, string sourceVar)
        {
            if (collectionType is IArrayTypeSymbol arrayType && _typeNameResolver.IsComplexType(arrayType.ElementType))
            {
                var elementTypeName = (arrayType.ElementType as INamedTypeSymbol)?.Name ?? arrayType.ElementType.Name;
                builder.AppendLine($"return {sourceVar}?.Select(x => {elementTypeName}Proxy.GetOrCreate(x)).ToArray();");
            }
            else if (collectionType is INamedTypeSymbol namedType && _typeNameResolver.IsGenericCollection(namedType))
            {
                var elementType = namedType.TypeArguments.First();
                if (_typeNameResolver.IsComplexType(elementType))
                {
                    var elementTypeName = (elementType as INamedTypeSymbol)?.Name ?? elementType.Name;
                    builder.AppendLine($"return {sourceVar}?.Select(x => {elementTypeName}Proxy.GetOrCreate(x)).ToList();");
                }
                else
                {
                    builder.AppendLine($"return {sourceVar};");
                }
            }
            else
            {
                builder.AppendLine($"return {sourceVar};");
            }
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