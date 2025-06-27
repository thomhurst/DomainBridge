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

                // First pass: collect all bridge class information
                var bridgeClassInfos = new List<(INamedTypeSymbol bridgeClass, INamedTypeSymbol targetType, AttributeConfiguration config, ClassDeclarationSyntax syntax)>();
                
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
                                bridgeClassInfos.Add((classSymbol, targetType, config, classDeclaration));
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
                
                // Second pass: generate all files with global conflict resolution
                var allGeneratedFiles = GenerateAllBridgeClasses(bridgeClassInfos, compilation);
                
                // Add all generated files to the compilation
                foreach (var (fileName, code) in allGeneratedFiles)
                {
                    context.AddSource(fileName, SourceText.From(code, Encoding.UTF8));
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

        private List<(string fileName, string code)> GenerateAllBridgeClasses(
            List<(INamedTypeSymbol bridgeClass, INamedTypeSymbol targetType, AttributeConfiguration config, ClassDeclarationSyntax syntax)> bridgeClassInfos,
            Compilation compilation)
        {
            var result = new List<(string fileName, string code)>();
            var globalFileNames = new HashSet<string>();
            var globalTypeToBridgeMapping = new Dictionary<string, string>();
            var globalBridgeNames = new HashSet<string>();
            
            try
            {
                // First, analyze all types that will need bridges
                var allTypeModels = new Dictionary<string, TypeModel>();
                var allProcessedTypes = new HashSet<string>();
                
                foreach (var (bridgeClass, targetType, config, syntax) in bridgeClassInfos)
                {
                    try
                    {
                        // Analyze types for this bridge
                        var (processedTypes, typeModels) = AnalyzeTypes(targetType, compilation, config);
                        
                        // Merge into global collections
                        foreach (var type in processedTypes)
                        {
                            allProcessedTypes.Add(type);
                        }
                        
                        foreach (var kvp in typeModels)
                        {
                            if (!allTypeModels.ContainsKey(kvp.Key))
                            {
                                allTypeModels[kvp.Key] = kvp.Value;
                            }
                        }
                        
                        // Track the main bridge class name
                        globalBridgeNames.Add(bridgeClass.Name);
                        globalTypeToBridgeMapping[targetType.ToDisplayString()] = bridgeClass.Name;
                    }
                    catch (Exception ex)
                    {
                        // Report error for this specific bridge class
                        var diagnostic = Diagnostic.Create(
                            new DiagnosticDescriptor(
                                "DBG001",
                                "Bridge Generation Failed",
                                $"Failed to generate bridge for {{0}}: {{1}}",
                                "DomainBridge",
                                DiagnosticSeverity.Error,
                                true),
                            syntax.GetLocation(),
                            bridgeClass.Name,
                            ex.ToString());
                        
                        // Continue processing other bridge classes
                        continue;
                    }
                }
                
                // Pre-calculate all bridge names with global conflict resolution
                foreach (var kvp in allTypeModels.Where(t => !globalTypeToBridgeMapping.ContainsKey(t.Key)))
                {
                    var baseBridgeName = kvp.Value.Name + "Bridge";
                    var uniqueBridgeName = baseBridgeName;
                    
                    // Handle naming conflicts globally
                    if (globalBridgeNames.Contains(uniqueBridgeName))
                    {
                        // Try with namespace prefix first
                        if (!string.IsNullOrEmpty(kvp.Value.Namespace))
                        {
                            var namespaceParts = kvp.Value.Namespace.Split('.');
                            var lastNamespace = namespaceParts[namespaceParts.Length - 1];
                            uniqueBridgeName = lastNamespace + baseBridgeName;
                        }
                        
                        // If still conflicts, append a counter
                        var counter = 2;
                        var candidateName = uniqueBridgeName;
                        while (globalBridgeNames.Contains(candidateName))
                        {
                            candidateName = uniqueBridgeName + counter;
                            counter++;
                        }
                        uniqueBridgeName = candidateName;
                    }
                    
                    globalBridgeNames.Add(uniqueBridgeName);
                    globalTypeToBridgeMapping[kvp.Key] = uniqueBridgeName;
                }
                
                // Now generate all bridge classes with the global mapping
                var typeNameResolver = new TypeNameResolver(allProcessedTypes, globalTypeToBridgeMapping);
                var bridgeGenerator = new BridgeClassGenerator(typeNameResolver);
                
                // Generate each bridge class
                foreach (var (bridgeClass, targetType, config, syntax) in bridgeClassInfos)
                {
                    try
                    {
                        var namespaceName = bridgeClass.ContainingNamespace.IsGlobalNamespace 
                            ? "DomainBridge.Generated" 
                            : bridgeClass.ContainingNamespace.ToDisplayString();
                        
                        // Generate main bridge class
                        var mainBuilder = new CodeBuilder();
                        GenerateFileHeader(mainBuilder);
                        mainBuilder.AppendLine($"namespace {namespaceName}");
                        mainBuilder.OpenBlock("");
                        
                        var targetModel = allTypeModels[targetType.ToDisplayString()];
                        bridgeGenerator.Generate(mainBuilder, bridgeClass.Name, targetModel, config);
                        
                        mainBuilder.CloseBlock();
                        
                        var fileName = $"{bridgeClass.Name}.g.cs";
                        if (globalFileNames.Contains(fileName))
                        {
                            // This shouldn't happen with our naming strategy, but just in case
                            fileName = $"{bridgeClass.Name}_{Guid.NewGuid():N}.g.cs";
                        }
                        globalFileNames.Add(fileName);
                        result.Add((fileName, mainBuilder.ToString()));
                        
                        // Generate nested types for this bridge
                        if (config.IncludeNestedTypes)
                        {
                            // Get the types referenced by this specific bridge
                            var (processedTypes, typeModels) = AnalyzeTypes(targetType, compilation, config);
                            
                            foreach (var kvp in typeModels.Where(t => t.Key != targetType.ToDisplayString()))
                            {
                                var nestedBuilder = new CodeBuilder();
                                GenerateFileHeader(nestedBuilder);
                                nestedBuilder.AppendLine($"namespace {namespaceName}");
                                nestedBuilder.OpenBlock("");
                                
                                var uniqueBridgeName = globalTypeToBridgeMapping[kvp.Key];
                                bridgeGenerator.Generate(nestedBuilder, uniqueBridgeName, kvp.Value, null);
                                
                                nestedBuilder.CloseBlock();
                                
                                var safeFileName = uniqueBridgeName.Replace('+', '_').Replace('.', '_').Replace('<', '_').Replace('>', '_') + ".g.cs";
                                if (!globalFileNames.Contains(safeFileName))
                                {
                                    globalFileNames.Add(safeFileName);
                                    result.Add((safeFileName, nestedBuilder.ToString()));
                                }
                                // If file already exists, it means another bridge class already generated it
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        // Log error but continue with other files
                        // The error was already reported in the first pass
                    }
                }
            }
            catch (Exception ex)
            {
                // Critical error in generation logic
                throw new InvalidOperationException($"Failed to generate bridge classes: {ex.Message}", ex);
            }
            
            return result;
        }
        
        private (HashSet<string> processedTypes, Dictionary<string, TypeModel> typeModels) AnalyzeTypes(
            INamedTypeSymbol targetType, Compilation compilation, AttributeConfiguration config)
        {
            var analyzer = new TypeAnalyzer();
            var processedTypes = new HashSet<string>();
            var typesToProcess = new Queue<INamedTypeSymbol>();
            var typeModels = new Dictionary<string, TypeModel>();
            
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
                
                if (config.IncludeNestedTypes)
                {
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
            
            return (processedTypes, typeModels);
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