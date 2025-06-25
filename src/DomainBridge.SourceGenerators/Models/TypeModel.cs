using System;
using System.Collections.Generic;
using Microsoft.CodeAnalysis;

namespace DomainBridge.SourceGenerators.Models
{
    internal class TypeModel
    {
        public INamedTypeSymbol Symbol { get; }
        public string Name { get; }
        public string Namespace { get; }
        public string FullName { get; }
        public List<PropertyModel> Properties { get; } = new List<PropertyModel>();
        public List<MethodModel> Methods { get; } = new List<MethodModel>();
        public List<EventModel> Events { get; } = new List<EventModel>();
        public bool IsProcessed { get; set; }

        public TypeModel(INamedTypeSymbol symbol)
        {
            Symbol = symbol ?? throw new ArgumentNullException(nameof(symbol));
            Name = symbol.Name;
            Namespace = symbol.ContainingNamespace?.ToDisplayString() ?? "";
            FullName = symbol.ToDisplayString();
        }
    }

    internal class PropertyModel
    {
        public string Name { get; }
        public ITypeSymbol Type { get; }
        public bool HasGetter { get; }
        public bool HasSetter { get; }
        public bool IsIgnored { get; }

        public PropertyModel(string name, ITypeSymbol type, bool hasGetter, bool hasSetter, bool isIgnored = false)
        {
            Name = name;
            Type = type;
            HasGetter = hasGetter;
            HasSetter = hasSetter;
            IsIgnored = isIgnored;
        }
    }

    internal class MethodModel
    {
        public string Name { get; }
        public ITypeSymbol ReturnType { get; }
        public List<ParameterModel> Parameters { get; } = new List<ParameterModel>();
        public bool IsIgnored { get; }

        public MethodModel(string name, ITypeSymbol returnType, bool isIgnored = false)
        {
            Name = name;
            ReturnType = returnType;
            IsIgnored = isIgnored;
        }
    }

    internal class ParameterModel
    {
        public string Name { get; }
        public ITypeSymbol Type { get; }
        public bool HasDefaultValue { get; }
        public object? DefaultValue { get; }

        public ParameterModel(string name, ITypeSymbol type, bool hasDefaultValue = false, object? defaultValue = null)
        {
            Name = name;
            Type = type;
            HasDefaultValue = hasDefaultValue;
            DefaultValue = defaultValue;
        }
    }

    internal class EventModel
    {
        public string Name { get; }
        public ITypeSymbol Type { get; }
        public bool IsIgnored { get; }

        public EventModel(string name, ITypeSymbol type, bool isIgnored = false)
        {
            Name = name;
            Type = type;
            IsIgnored = isIgnored;
        }
    }
}