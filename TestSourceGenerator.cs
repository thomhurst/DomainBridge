using System;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using DomainBridge.SourceGenerators;

// Simple test to debug the source generator
class TestSourceGenerator
{
    static void Main()
    {
        var code = @"
using DomainBridge;

namespace Test
{
    [DomainBridge(typeof(TargetClass))]
    public partial class TargetClassBridge
    {
    }
    
    public class TargetClass
    {
        public string Name { get; set; } = ""Test"";
        
        public void DoSomething()
        {
            System.Console.WriteLine(""Doing something"");
        }
    }
}";

        var syntaxTree = CSharpSyntaxTree.ParseText(code);
        var compilation = CSharpCompilation.Create("TestAssembly")
            .AddReferences(MetadataReference.CreateFromFile(typeof(object).Assembly.Location))
            .AddReferences(MetadataReference.CreateFromFile(typeof(DomainBridge.DomainBridgeAttribute).Assembly.Location))
            .AddSyntaxTrees(syntaxTree);

        var generator = new DomainBridgePatternGenerator();
        var driver = CSharpGeneratorDriver.Create(generator);
        
        driver = driver.RunGeneratorsAndUpdateCompilation(compilation, out var outputCompilation, out var diagnostics);
        
        Console.WriteLine($"Diagnostics: {diagnostics.Length}");
        foreach (var diag in diagnostics)
        {
            Console.WriteLine($"  {diag}");
        }
        
        var results = driver.GetRunResult();
        Console.WriteLine($"Generated sources: {results.GeneratedTrees.Length}");
        
        foreach (var tree in results.GeneratedTrees)
        {
            Console.WriteLine($"Generated file: {tree.FilePath}");
            Console.WriteLine(tree.GetText());
        }
    }
}