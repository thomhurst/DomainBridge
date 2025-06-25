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
                        if (attribute.ConstructorArguments.Length > 0 && 
                            attribute.ConstructorArguments[0].Value is INamedTypeSymbol targetType)
                        {
                            var code = GenerateBridgeClass(classSymbol, targetType, compilation);
                            context.AddSource($"{classSymbol.Name}.g.cs", SourceText.From(code, Encoding.UTF8));
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

        private string GenerateBridgeClass(INamedTypeSymbol bridgeClass, INamedTypeSymbol targetType, Compilation compilation)
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
                
                var typeModel = analyzer.AnalyzeType(typeSymbol);
                typeModels[typeFullName] = typeModel;
                
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

            // Generate the code
            var builder = new CodeBuilder();
            var typeNameResolver = new TypeNameResolver(processedTypes);
            var bridgeGenerator = new BridgeClassGenerator(typeNameResolver);
            
            // File header
            builder.AppendLine("// <auto-generated />");
            builder.AppendLine("using System;");
            builder.AppendLine("using System.Collections.Generic;");
            builder.AppendLine("using System.Collections.Concurrent;");
            builder.AppendLine("using System.Linq;");
            builder.AppendLine("using DomainBridge;");
            builder.AppendLine();
            
            // Use the bridge class's namespace
            var namespaceName = bridgeClass.ContainingNamespace.IsGlobalNamespace 
                ? "DomainBridge.Generated" 
                : bridgeClass.ContainingNamespace.ToDisplayString();
                
            builder.AppendLine($"namespace {namespaceName}");
            builder.OpenBlock("");
            
            // Generate the bridge class
            var targetModel = typeModels[targetType.ToDisplayString()];
            bridgeGenerator.Generate(builder, bridgeClass.Name, targetModel);
            
            // Generate bridge classes for nested types
            foreach (var kvp in typeModels.Where(t => t.Key != targetType.ToDisplayString()))
            {
                builder.AppendLine();
                var nestedTypeName = kvp.Value.Name + "Bridge";
                bridgeGenerator.Generate(builder, nestedTypeName, kvp.Value);
            }
            
            builder.CloseBlock();
            
            return builder.ToString();
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