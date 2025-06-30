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
                
                if (domainBridgeAttribute == null)
                {
                    return;
                }

                var analyzer = new TypeAnalyzer();
                var generator = new BridgeClassGenerator();
                var typeFilter = new TypeFilter(context);
                var typeCollector = new TypeCollector(typeFilter);
                var explicitlyMarkedTypes = new List<INamedTypeSymbol>();
                var partialClassesToGenerate = new List<(ClassDeclarationSyntax classDecl, INamedTypeSymbol targetType, AttributeConfiguration config)>();

                // First pass: collect all explicitly marked types
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

                        // Check if the class is declared as partial
                        if (!classDeclaration.Modifiers.Any(m => m.IsKind(SyntaxKind.PartialKeyword)))
                        {
                            context.ReportDiagnostic(
                                Diagnostic.Create(
                                    DiagnosticsHelper.MissingPartialKeyword,
                                    classDeclaration.Identifier.GetLocation(),
                                    classSymbol.Name));
                            continue;
                        }
                        
                        // Get the target type from the attribute
                        if (attribute.ConstructorArguments.Length > 0)
                        {
                            var targetTypeValue = attribute.ConstructorArguments[0].Value;
                            if (targetTypeValue is INamedTypeSymbol targetType)
                            {
                                explicitlyMarkedTypes.Add(targetType);
                                var config = ExtractAttributeConfiguration(attribute);
                                partialClassesToGenerate.Add((classDeclaration, targetType, config));
                            }
                            else
                            {
                                // Report diagnostic if target type is not found
                                var targetTypeName = attribute.ConstructorArguments[0].Value?.ToString() ?? "unknown";
                                context.ReportDiagnostic(
                                    Diagnostic.Create(
                                        DiagnosticsHelper.TypeNotFound,
                                        classDeclaration.GetLocation(),
                                        targetTypeName));
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
                        var bridgeInfo = new BridgeTypeInfo(targetType, isExplicitlyMarked: true, 
                            explicitBridgeClassName: classSymbol.Name,
                            explicitBridgeNamespace: bridgeNamespace);
                        
                        // Generate using enhanced generator for async support
                        var generatedCode = enhancedGenerator.GenerateBridgeClass(bridgeInfo, targetType, config, context);
                        var fileName = $"{classSymbol.Name}.g.cs";
                        context.AddSource(fileName, SourceText.From(generatedCode, Encoding.UTF8));
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

            public void OnVisitSyntaxNode(SyntaxNode syntaxNode)
            {
                // Check for any class with attributes - let the compiler handle missing partial keyword
                if (syntaxNode is ClassDeclarationSyntax classDeclaration &&
                    classDeclaration.AttributeLists.Count > 0)
                {
                    CandidateClasses.Add(classDeclaration);
                }
            }
        }
    }
}