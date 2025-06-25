using System.Linq;
using Microsoft.CodeAnalysis;
using DomainBridge.SourceGenerators.Models;

namespace DomainBridge.SourceGenerators.Services
{
    internal class InterfaceGenerator
    {
        private readonly TypeNameResolver _typeNameResolver;

        public InterfaceGenerator(TypeNameResolver typeNameResolver)
        {
            _typeNameResolver = typeNameResolver;
        }

        public void Generate(CodeBuilder builder, TypeModel model)
        {
            builder.AppendLine($"public interface I{model.Name}");
            builder.OpenBlock("");

            // Generate properties
            foreach (var property in model.Properties.Where(p => !p.IsIgnored))
            {
                var typeName = _typeNameResolver.GetProxyTypeName(property.Type);
                var accessors = new[] 
                { 
                    property.HasGetter ? "get;" : null,
                    property.HasSetter ? "set;" : null
                }.Where(a => a != null);
                
                builder.AppendLine($"{typeName} {property.Name} {{ {string.Join(" ", accessors)} }}");
            }

            if (model.Properties.Any() && (model.Methods.Any() || model.Events.Any()))
                builder.AppendLine();

            // Generate methods
            foreach (var method in model.Methods.Where(m => !m.IsIgnored))
            {
                var returnType = _typeNameResolver.GetProxyTypeName(method.ReturnType);
                var parameters = string.Join(", ", method.Parameters.Select(p =>
                {
                    var paramType = _typeNameResolver.GetProxyTypeName(p.Type);
                    var defaultValue = p.HasDefaultValue ? $" = {FormatDefaultValue(p.DefaultValue)}" : "";
                    return $"{paramType} {p.Name}{defaultValue}";
                }));

                builder.AppendLine($"{returnType} {method.Name}({parameters});");
            }

            if (model.Methods.Any() && model.Events.Any())
                builder.AppendLine();

            // Generate events
            foreach (var evt in model.Events.Where(e => !e.IsIgnored))
            {
                var eventType = _typeNameResolver.GetProxyTypeName(evt.Type);
                builder.AppendLine($"event {eventType} {evt.Name};");
            }

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