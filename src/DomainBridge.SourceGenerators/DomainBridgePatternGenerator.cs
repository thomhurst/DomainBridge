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
                    return;

                var compilation = context.Compilation;
                var domainBridgeAttribute = compilation.GetTypeByMetadataName("DomainBridge.DomainBridgeAttribute");
                
                if (domainBridgeAttribute == null)
                    return;

                var analyzer = new TypeAnalyzer();
                var bridgeGenerator = new BridgeClassGenerator();

                foreach (var classDeclaration in receiver.CandidateClasses)
                {
                    try
                    {
                        var model = compilation.GetSemanticModel(classDeclaration.SyntaxTree);
                        var classSymbol = model.GetDeclaredSymbol(classDeclaration) as INamedTypeSymbol;
                        
                        if (classSymbol == null) continue;

                        // Check if class has [DomainBridge(typeof(...))] attribute
                        var attribute = classSymbol.GetAttributes()
                            .FirstOrDefault(attr => SymbolEqualityComparer.Default.Equals(attr.AttributeClass, domainBridgeAttribute));
                        
                        if (attribute == null) continue;

                        // Get the target type from the attribute
                        if (attribute.ConstructorArguments.Length > 0)
                        {
                            var targetTypeValue = attribute.ConstructorArguments[0].Value;
                            if (targetTypeValue is INamedTypeSymbol targetType)
                            {
                                var config = ExtractAttributeConfiguration(attribute);
                                
                                // Analyze only the target type
                                var typeModel = analyzer.AnalyzeType(targetType);
                                
                                // Generate the bridge class
                                var builder = new CodeBuilder();
                                GenerateFileHeader(builder);
                                
                                var namespaceName = classSymbol.ContainingNamespace.IsGlobalNamespace 
                                    ? "DomainBridge.Generated" 
                                    : classSymbol.ContainingNamespace.ToDisplayString();
                                
                                builder.AppendLine($"namespace {namespaceName}");
                                builder.OpenBlock("");
                                
                                bridgeGenerator.Generate(builder, classSymbol.Name, typeModel, config);
                                
                                builder.CloseBlock();
                                
                                context.AddSource($"{classSymbol.Name}.g.cs", SourceText.From(builder.ToString(), Encoding.UTF8));
                            }
                            else
                            {
                                // Report diagnostic if target type is not found
                                var diagnostic = Diagnostic.Create(
                                    new DiagnosticDescriptor(
                                        "DBG002",
                                        "Invalid Target Type",
                                        "The target type for {0} could not be resolved",
                                        "DomainBridge",
                                        DiagnosticSeverity.Error,
                                        true),
                                    classDeclaration.GetLocation(),
                                    classSymbol.Name);
                                
                                context.ReportDiagnostic(diagnostic);
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
                if (syntaxNode is ClassDeclarationSyntax classDeclaration &&
                    classDeclaration.AttributeLists.Count > 0 &&
                    classDeclaration.Modifiers.Any(m => m.IsKind(SyntaxKind.PartialKeyword)))
                {
                    CandidateClasses.Add(classDeclaration);
                }
            }
        }
    }
}