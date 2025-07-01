using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;
using DomainBridge.SourceGenerators.Models;
using DomainBridge.SourceGenerators.Services;

namespace DomainBridge.SourceGenerators
{
    [Generator]
    public class DomainBridgePatternGenerator : ISourceGenerator
    {
        public void Initialize(GeneratorInitializationContext context)
        {
            context.RegisterForSyntaxNotifications(() => new SyntaxReceiver());
        }

        public void Execute(GeneratorExecutionContext context)
        {
            try
            {
                if (!(context.SyntaxReceiver is SyntaxReceiver receiver))
                {
                    return;
                }

                var compilation = context.Compilation;
                var domainBridgeAttribute = compilation.GetTypeByMetadataName("DomainBridge.DomainBridgeAttribute");
                var appDomainBridgeableAttribute = compilation.GetTypeByMetadataName("DomainBridge.AppDomainBridgeableAttribute");
                
                // Skip if no relevant attributes are found
                if (domainBridgeAttribute == null && appDomainBridgeableAttribute == null)
                {
                    return;
                }

                var analyzer = new TypeAnalyzer();
                var generator = new BridgeClassGenerator();
                var typeFilter = new TypeFilter(context);
                var typeCollector = new TypeCollector(typeFilter);
                var explicitlyMarkedTypes = new List<(INamedTypeSymbol targetType, string bridgeClassName, string bridgeNamespace)>();
                var partialClassesToGenerate = new List<(ClassDeclarationSyntax classDecl, INamedTypeSymbol targetType, AttributeConfiguration config)>();
                var interfacesToGenerate = new List<(InterfaceDeclarationSyntax interfaceDecl, INamedTypeSymbol interfaceType)>();

                // First pass: collect interfaces marked with [AppDomainBridgeable]
                if (appDomainBridgeableAttribute != null)
                {
                    foreach (var interfaceDeclaration in receiver.CandidateInterfaces)
                    {
                        try
                        {
                            var model = compilation.GetSemanticModel(interfaceDeclaration.SyntaxTree);
                            var interfaceSymbol = model.GetDeclaredSymbol(interfaceDeclaration) as INamedTypeSymbol;
                            
                            if (interfaceSymbol == null)
                            {
                                continue;
                            }

                            // Check if interface has [AppDomainBridgeable] attribute
                            var attribute = interfaceSymbol.GetAttributes()
                                .FirstOrDefault(attr => SymbolEqualityComparer.Default.Equals(attr.AttributeClass, appDomainBridgeableAttribute));
                            
                            if (attribute == null)
                            {
                                continue;
                            }

                            // Check if the interface can be bridged (not blacklisted)
                            if (TypeBlacklist.IsBlacklisted(interfaceSymbol))
                            {
                                context.ReportDiagnostic(
                                    Diagnostic.Create(
                                        DiagnosticsHelper.TypeBlacklisted,
                                        interfaceDeclaration.Identifier.GetLocation(),
                                        interfaceSymbol.Name,
                                        TypeBlacklist.GetBlacklistReason(interfaceSymbol)));
                                continue;
                            }

                            // Check generic constraints for unbridgeable types
                            if (HasUnbridgeableGenericConstraints(interfaceSymbol))
                            {
                                context.ReportDiagnostic(
                                    Diagnostic.Create(
                                        DiagnosticsHelper.GenericConstraintUnbridgeable,
                                        interfaceDeclaration.Identifier.GetLocation(),
                                        interfaceSymbol.Name,
                                        "contains unbridgeable constraint"));
                                continue;
                            }

                            interfacesToGenerate.Add((interfaceDeclaration, interfaceSymbol));
                        }
                        catch (Exception ex)
                        {
                            var diagnostic = Diagnostic.Create(
                                new DiagnosticDescriptor(
                                    "DBG305",
                                    "Interface Bridge Generation Failed",
                                    $"Failed to process interface {{0}}: {{1}}",
                                    "DomainBridge",
                                    DiagnosticSeverity.Error,
                                    true),
                                interfaceDeclaration.GetLocation(),
                                interfaceDeclaration.Identifier.Text,
                                ex.Message);
                            
                            context.ReportDiagnostic(diagnostic);
                        }
                    }
                }

                // Second pass: collect classes with [DomainBridge] (legacy approach)  
                if (domainBridgeAttribute != null)
                {
                    foreach (var classDeclaration in receiver.CandidateClasses)
                {
                    try
                    {
                        var model = compilation.GetSemanticModel(classDeclaration.SyntaxTree);
                        var classSymbol = model.GetDeclaredSymbol(classDeclaration) as INamedTypeSymbol;
                        
                        if (classSymbol == null)
                        {
                            continue;
                        }

                        // Check if class has [DomainBridge(typeof(...))] attribute
                        var attribute = classSymbol.GetAttributes()
                            .FirstOrDefault(attr => SymbolEqualityComparer.Default.Equals(attr.AttributeClass, domainBridgeAttribute));
                        
                        if (attribute == null)
                        {
                            continue;
                        }

                        // Emit deprecation warning for legacy approach
                        var targetTypeName = attribute.ConstructorArguments.Length > 0 
                            ? attribute.ConstructorArguments[0].Value?.ToString() ?? "unknown"
                            : "unknown";
                        context.ReportDiagnostic(
                            Diagnostic.Create(
                                DiagnosticsHelper.LegacyAttributeUsage,
                                classDeclaration.Identifier.GetLocation(),
                                targetTypeName));

                        // Check if the class is declared as partial
                        if (!classDeclaration.Modifiers.Any(m => m.IsKind(SyntaxKind.PartialKeyword)))
                        {
                            context.ReportDiagnostic(
                                Diagnostic.Create(
                                    DiagnosticsHelper.MissingPartialKeyword,
                                    classDeclaration.Identifier.GetLocation(),
                                    classSymbol.Name));
                            // Continue generating anyway - this will cause a compilation error
                            // but provides a better developer experience
                        }
                        
                        // Get the target type from the attribute
                        if (attribute.ConstructorArguments.Length > 0)
                        {
                            var targetTypeValue = attribute.ConstructorArguments[0].Value;
                            if (targetTypeValue is INamedTypeSymbol targetType)
                            {
                                // Validate that the target type can be bridged
                                if (targetType.IsValueType)
                                {
                                    context.ReportDiagnostic(
                                        DiagnosticsHelper.CreateUnbridgeableTypeDiagnostic(targetType, classDeclaration.GetLocation()));
                                }
                                
                                // Get the bridge class name and namespace from the partial class declaration
                                var bridgeClassName = classSymbol.Name;
                                var bridgeNamespace = classSymbol.ContainingNamespace?.IsGlobalNamespace == true
                                    ? ""
                                    : classSymbol.ContainingNamespace?.ToDisplayString() ?? "";
                                
                                explicitlyMarkedTypes.Add((targetType, bridgeClassName, bridgeNamespace));
                                var config = ExtractAttributeConfiguration(attribute);
                                partialClassesToGenerate.Add((classDeclaration, targetType, config));
                            }
                            else
                            {
                                // Report diagnostic if target type is not found
                                var targetTypeNameForDiagnostic = attribute.ConstructorArguments[0].Value?.ToString() ?? "unknown";
                                context.ReportDiagnostic(
                                    Diagnostic.Create(
                                        DiagnosticsHelper.TypeNotFound,
                                        classDeclaration.GetLocation(),
                                        targetTypeNameForDiagnostic));
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        var diagnostic = Diagnostic.Create(
                            new DiagnosticDescriptor(
                                "DBG001",
                                "Bridge Generation Failed",
                                $"Failed to generate bridge for {{0}}: {{1}}",
                                "DomainBridge",
                                DiagnosticSeverity.Error,
                                true),
                            classDeclaration.GetLocation(),
                            classDeclaration.Identifier.Text,
                            ex.ToString());
                        
                        context.ReportDiagnostic(diagnostic);
                    }
                    }
                }
                
                // Collect all types that need bridges
                var allTypesNeedingBridges = typeCollector.CollectTypes(explicitlyMarkedTypes);
                
                // Create type resolver with all collected types
                var typeResolver = new BridgeTypeResolver(allTypesNeedingBridges);
                var enhancedGenerator = new EnhancedBridgeClassGenerator(typeResolver, analyzer);
                
                // First, generate bridges for partial classes using the enhanced generator
                foreach (var (classDecl, targetType, config) in partialClassesToGenerate)
                {
                    try
                    {
                        var model = compilation.GetSemanticModel(classDecl.SyntaxTree);
                        var classSymbol = model.GetDeclaredSymbol(classDecl) as INamedTypeSymbol;
                        if (classSymbol == null)
                        {
                            continue;
                        }

                        // Create bridge info for explicitly marked type using the actual class name and namespace
                        var bridgeNamespace = classSymbol.ContainingNamespace?.IsGlobalNamespace == true
                            ? ""
                            : classSymbol.ContainingNamespace?.ToDisplayString() ?? "";
                        
                        // For generic classes, include the type parameters in the class name
                        var bridgeClassName = classSymbol.Name;
                        if (classSymbol.IsGenericType)
                        {
                            var typeParams = string.Join(", ", classSymbol.TypeParameters.Select(tp => tp.Name));
                            bridgeClassName = $"{bridgeClassName}<{typeParams}>";
                        }
                        
                        var bridgeInfo = new BridgeTypeInfo(targetType, isExplicitlyMarked: true, 
                            explicitBridgeClassName: bridgeClassName,
                            explicitBridgeNamespace: bridgeNamespace);
                        
                        // Generate using enhanced generator for async support
                        var generatedCode = enhancedGenerator.GenerateBridgeClass(bridgeInfo, targetType, config, context);
                        context.AddSource(bridgeInfo.FileName, SourceText.From(generatedCode, Encoding.UTF8));
                    }
                    catch (Exception ex)
                    {
                        var diagnostic = Diagnostic.Create(
                            new DiagnosticDescriptor(
                                "DBG004",
                                "Partial Bridge Generation Failed",
                                "Failed to generate partial bridge for {0}: {1}",
                                "DomainBridge",
                                DiagnosticSeverity.Error,
                                true),
                            classDecl.GetLocation(),
                            classDecl.Identifier.Text,
                            ex.Message);
                        context.ReportDiagnostic(diagnostic);
                    }
                }
                
                // Generate bridges for interfaces marked with [AppDomainBridgeable]
                foreach (var (interfaceDecl, interfaceType) in interfacesToGenerate)
                {
                    try
                    {
                        // Create bridge info for interface
                        var interfaceNamespace = interfaceType.ContainingNamespace?.IsGlobalNamespace == true
                            ? ""
                            : interfaceType.ContainingNamespace?.ToDisplayString() ?? "";
                        
                        // Generate bridge class name: IUserService -> UserServiceBridge
                        var bridgeClassName = interfaceType.Name.StartsWith("I") && interfaceType.Name.Length > 1 && char.IsUpper(interfaceType.Name[1])
                            ? interfaceType.Name.Substring(1) + "Bridge"
                            : interfaceType.Name + "Bridge";
                        
                        var bridgeInfo = new BridgeTypeInfo(interfaceType, isExplicitlyMarked: true, 
                            explicitBridgeClassName: bridgeClassName,
                            explicitBridgeNamespace: interfaceNamespace);
                        
                        // Generate bridge implementation for the interface
                        var generatedCode = enhancedGenerator.GenerateBridgeClass(bridgeInfo, interfaceType, null, context);
                        context.AddSource(bridgeInfo.FileName, SourceText.From(generatedCode, Encoding.UTF8));
                    }
                    catch (Exception ex)
                    {
                        var diagnostic = Diagnostic.Create(
                            new DiagnosticDescriptor(
                                "DBG306",
                                "Interface Bridge Generation Failed",
                                "Failed to generate bridge for interface {0}: {1}",
                                "DomainBridge",
                                DiagnosticSeverity.Error,
                                true),
                            interfaceDecl.GetLocation(),
                            interfaceDecl.Identifier.Text,
                            ex.Message);
                        context.ReportDiagnostic(diagnostic);
                    }
                }
                
                // Then generate auto-discovered bridges
                var autoDiscoveredBridges = allTypesNeedingBridges
                    .Where(kvp => !kvp.Value.IsExplicitlyMarked)
                    .ToDictionary(kvp => kvp.Key, kvp => kvp.Value,
                        SymbolEqualityComparer.Default);
                    
                if (autoDiscoveredBridges.Count > 0)
                {
                    // Generate bridges for auto-discovered types
                    foreach (var kvp in autoDiscoveredBridges)
                    {
                        var targetType = kvp.Key as INamedTypeSymbol;
                        var bridgeInfo = kvp.Value;
                        
                        try
                        {
                            // Auto-discovered types don't have configuration
                            var generatedCode = enhancedGenerator.GenerateBridgeClass(bridgeInfo, targetType!, null, context);
                            context.AddSource(bridgeInfo.FileName, SourceText.From(generatedCode, Encoding.UTF8));
                        }
                        catch (Exception ex)
                        {
                            var diagnostic = Diagnostic.Create(
                                new DiagnosticDescriptor(
                                    "DBG003",
                                    "Auto Bridge Generation Failed",
                                    "Failed to generate auto bridge for {0}: {1}",
                                    "DomainBridge",
                                    DiagnosticSeverity.Error,
                                    true),
                                Location.None,
                                targetType?.Name,
                                ex.Message);
                            context.ReportDiagnostic(diagnostic);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                // Report a diagnostic for any unhandled exception
                var diagnostic = Diagnostic.Create(
                    new DiagnosticDescriptor(
                        "DBG000",
                        "Generator Failed",
                        "DomainBridge generator failed: {0}",
                        "DomainBridge",
                        DiagnosticSeverity.Error,
                        true),
                    Location.None,
                    ex.ToString());
                
                context.ReportDiagnostic(diagnostic);
            }
        }

        /// <summary>
        /// Checks if a type has generic constraints that reference unbridgeable types
        /// </summary>
        private bool HasUnbridgeableGenericConstraints(INamedTypeSymbol type)
        {
            foreach (var typeParameter in type.TypeParameters)
            {
                foreach (var constraint in typeParameter.ConstraintTypes)
                {
                    if (TypeBlacklist.IsBlacklisted(constraint))
                    {
                        return true;
                    }
                }
            }
            return false;
        }

        private AttributeConfiguration ExtractAttributeConfiguration(AttributeData attribute)
        {
            var config = new AttributeConfiguration();
            
            foreach (var namedArg in attribute.NamedArguments)
            {
                switch (namedArg.Key)
                {
                    case "PrivateBinPath":
                        config.PrivateBinPath = namedArg.Value.Value as string;
                        break;
                    case "ApplicationBase":
                        config.ApplicationBase = namedArg.Value.Value as string;
                        break;
                    case "ConfigurationFile":
                        config.ConfigurationFile = namedArg.Value.Value as string;
                        break;
                    case "EnableShadowCopy":
                        config.EnableShadowCopy = namedArg.Value.Value is bool b && b;
                        break;
                    case "AssemblySearchPaths":
                        config.AssemblySearchPaths = namedArg.Value.Value as string;
                        break;
                    case "FactoryMethod":
                        config.FactoryMethod = namedArg.Value.Value as string;
                        break;
                }
            }
            
            return config;
        }

        private void GenerateFileHeader(CodeBuilder builder)
        {
            builder.AppendLine("// <auto-generated />");
            builder.AppendLine("#nullable enable");
            builder.AppendLine("using System;");
            builder.AppendLine("using System.Collections.Generic;");
            builder.AppendLine("using System.Collections.Concurrent;");
            builder.AppendLine("using System.Linq;");
            builder.AppendLine("using DomainBridge;");
            builder.AppendLine();
        }

        private class SyntaxReceiver : ISyntaxReceiver
        {
            public List<ClassDeclarationSyntax> CandidateClasses { get; } = new List<ClassDeclarationSyntax>();
            public List<InterfaceDeclarationSyntax> CandidateInterfaces { get; } = new List<InterfaceDeclarationSyntax>();

            public void OnVisitSyntaxNode(SyntaxNode syntaxNode)
            {
                // Check for classes with attributes (old [DomainBridge] approach)
                if (syntaxNode is ClassDeclarationSyntax classDeclaration &&
                    classDeclaration.AttributeLists.Count > 0)
                {
                    CandidateClasses.Add(classDeclaration);
                }
                
                // Check for interfaces with attributes (new [AppDomainBridgeable] approach)
                if (syntaxNode is InterfaceDeclarationSyntax interfaceDeclaration &&
                    interfaceDeclaration.AttributeLists.Count > 0)
                {
                    CandidateInterfaces.Add(interfaceDeclaration);
                }
            }
        }
    }
}