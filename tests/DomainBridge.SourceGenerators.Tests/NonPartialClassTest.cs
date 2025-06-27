using System;
using System.Linq;
using System.Threading.Tasks;
using TUnit.Core;
using TUnit.Assertions;
using Microsoft.CodeAnalysis;

namespace DomainBridge.SourceGenerators.Tests;

public class NonPartialClassTest
{
    [Test]
    public async Task GeneratesBridgeEvenWithoutPartialKeyword()
    {
        var source = """
            using DomainBridge;
            
            namespace TestNamespace
            {
                // Note: NOT marked as partial - this will cause a compiler error
                [DomainBridge(typeof(SimpleService))]
                public class SimpleServiceBridge { }
                
                public class SimpleService
                {
                    public string GetMessage() => "Hello";
                }
            }
            """;

        var result = TestHelper.RunGenerator(source);
        var output = TestHelper.GetGeneratedOutput(result);
        
        // Verify the bridge was still generated
        await Assert.That(output).Contains("public partial class SimpleServiceBridge : global::System.MarshalByRefObject, global::System.IDisposable");
        await Assert.That(output).Contains("public string GetMessage()");
        
        // Note: The compilation will have errors due to duplicate type definition
        // But that's expected - the compiler will give a clear error message
        var compilation = result.Results.First().GeneratedSources.Any();
        await Assert.That(compilation).IsTrue();
    }
    
    [Test] 
    public async Task CompilationFailsWithClearErrorForNonPartialClass()
    {
        var source = """
            using DomainBridge;
            
            namespace TestNamespace
            {
                [DomainBridge(typeof(SimpleService))]
                public class SimpleServiceBridge { }
                
                public class SimpleService
                {
                    public string GetMessage() => "Hello";
                }
            }
            """;

        // Run the generator and get the compilation
        var syntaxTree = Microsoft.CodeAnalysis.CSharp.CSharpSyntaxTree.ParseText(source);
        var generatedSyntaxTree = Microsoft.CodeAnalysis.CSharp.CSharpSyntaxTree.ParseText(TestHelper.GetGeneratedOutput(TestHelper.RunGenerator(source)));
        
        var references = new[]
        {
            MetadataReference.CreateFromFile(typeof(object).Assembly.Location),
            MetadataReference.CreateFromFile(typeof(DomainBridgeAttribute).Assembly.Location),
        };

        var compilation = Microsoft.CodeAnalysis.CSharp.CSharpCompilation.Create(
            assemblyName: "TestWithNonPartial",
            syntaxTrees: new[] { syntaxTree, generatedSyntaxTree },
            references: references);

        var diagnostics = compilation.GetDiagnostics();
        
        // Should have errors about duplicate type definition
        var errors = diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error);
        await Assert.That(errors.Any()).IsTrue();
        
        // The error should mention the type already being defined
        var typeExistsError = errors.Any(e => 
            e.Id == "CS0101" || // The namespace already contains a definition
            e.GetMessage().Contains("already contains a definition") ||
            e.GetMessage().Contains("SimpleServiceBridge"));
        
        await Assert.That(typeExistsError).IsTrue();
    }
}