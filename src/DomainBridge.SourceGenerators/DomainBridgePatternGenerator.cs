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
                                var generatedFiles = GenerateBridgeClasses(classSymbol, targetType, compilation, config);
                                
                                foreach (var (fileName, code) in generatedFiles)
                                {
                                    context.AddSource(fileName, SourceText.From(code, Encoding.UTF8));
                                }
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
                    case "IncludeNestedTypes":
                        config.IncludeNestedTypes = namedArg.Value.Value is bool inc ? inc : true;
                        break;
                    case "FactoryMethod":
                        config.FactoryMethod = namedArg.Value.Value as string;
                        break;
                }
            }
            
            return config;
        }

        private List<(string fileName, string code)> GenerateBridgeClasses(INamedTypeSymbol bridgeClass, INamedTypeSymbol targetType, Compilation compilation, AttributeConfiguration config)
        {
            var analyzer = new TypeAnalyzer();
            var processedTypes = new HashSet<string>();
            var typesToProcess = new Queue<INamedTypeSymbol>();
            var typeModels = new Dictionary<string, TypeModel>();
            
            try
            {
                // Start with the target type
                typesToProcess.Enqueue(targetType);
                
                // Process all related types
                while (typesToProcess.Count > 0)
                {
                    var typeSymbol = typesToProcess.Dequeue();
                    if (typeSymbol == null) continue;
                    
                    var typeFullName = typeSymbol.ToDisplayString();
                    
                    if (processedTypes.Contains(typeFullName))
                        continue;
                    
                    processedTypes.Add(typeFullName);
                    
                    TypeModel typeModel;
                    try
                    {
                        typeModel = analyzer.AnalyzeType(typeSymbol);
                        typeModels[typeFullName] = typeModel;
                    }
                    catch (Exception analyzeEx)
                    {
                        throw new InvalidOperationException($"Failed to analyze type {typeFullName}. Inner exception: {analyzeEx.GetType().Name}: {analyzeEx.Message}\nStack trace: {analyzeEx.StackTrace}", analyzeEx);
                    }
                    
                    // Find referenced types
                    var referencedTypes = analyzer.GetReferencedTypes(typeModel);
                    foreach (var referencedType in referencedTypes.OfType<INamedTypeSymbol>())
                    {
                        if (referencedType != null && !processedTypes.Contains(referencedType.ToDisplayString()))
                        {
                            typesToProcess.Enqueue(referencedType);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Failed during type analysis. Bridge: {bridgeClass.Name}, Target: {targetType.Name}. Details: {ex.Message}", ex);
            }

            // Generate the code
            var result = new List<(string fileName, string code)>();
            var typeNameResolver = new TypeNameResolver(processedTypes);
            var bridgeGenerator = new BridgeClassGenerator(typeNameResolver);
            
            // Use the bridge class's namespace
            var namespaceName = bridgeClass.ContainingNamespace.IsGlobalNamespace 
                ? "DomainBridge.Generated" 
                : bridgeClass.ContainingNamespace.ToDisplayString();
            
            // Generate the main bridge class
            var mainBuilder = new CodeBuilder();
            GenerateFileHeader(mainBuilder);
            mainBuilder.AppendLine($"namespace {namespaceName}");
            mainBuilder.OpenBlock("");
            
            var targetModel = typeModels[targetType.ToDisplayString()];
            bridgeGenerator.Generate(mainBuilder, bridgeClass.Name, targetModel, config);
            
            mainBuilder.CloseBlock();
            
            result.Add(($"{bridgeClass.Name}.g.cs", mainBuilder.ToString()));
            
            // Generate bridge classes for nested types if enabled
            if (config.IncludeNestedTypes)
            {
                foreach (var kvp in typeModels.Where(t => t.Key != targetType.ToDisplayString()))
                {
                    var nestedBuilder = new CodeBuilder();
                    GenerateFileHeader(nestedBuilder);
                    nestedBuilder.AppendLine($"namespace {namespaceName}");
                    nestedBuilder.OpenBlock("");
                    
                    var nestedTypeName = kvp.Value.Name + "Bridge";
                    bridgeGenerator.Generate(nestedBuilder, nestedTypeName, kvp.Value, null); // Nested types don't get config
                    
                    nestedBuilder.CloseBlock();
                    
                    // Create a safe file name by replacing nested class separators
                    var safeFileName = nestedTypeName.Replace('+', '_').Replace('.', '_');
                    result.Add(($"{safeFileName}.g.cs", nestedBuilder.ToString()));
                }
            }
            
            return result;
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
            public List<ClassDeclarationSyntax> CandidateClasses { get; } =
            [
            ];

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