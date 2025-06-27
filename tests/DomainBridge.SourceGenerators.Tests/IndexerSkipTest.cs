using System.Threading.Tasks;
using TUnit.Core;
using TUnit.Assertions;
using System.Linq;

namespace DomainBridge.SourceGenerators.Tests;

public class IndexerSkipTest
{
    [Test]
    public async Task Generator_SkipsIndexerProperties()
    {
        var source = """
            using DomainBridge;
            
            namespace TestNamespace
            {
                [DomainBridge(typeof(ServiceWithIndexer))]
                public partial class ServiceWithIndexerBridge { }
                
                public class ServiceWithIndexer
                {
                    private string[] items = new string[10];
                    
                    // This indexer should be skipped
                    public string this[int index]
                    {
                        get { return items[index]; }
                        set { items[index] = value; }
                    }
                    
                    // Regular property should be included
                    public int Count { get; set; }
                    
                    // Regular method should be included
                    public string GetItem(int index) => items[index];
                }
            }
            """;

        var result = TestHelper.RunGenerator(source);
        var output = TestHelper.GetGeneratedOutput(result);
        
        // Verify the bridge was generated
        await Assert.That(output).Contains("public partial class ServiceWithIndexerBridge");
        
        // Verify regular property is included
        await Assert.That(output).Contains("public int Count");
        
        // Verify regular method is included
        await Assert.That(output).Contains("public string GetItem(int index)");
        
        // Verify indexer is NOT included (no "this[" in output)
        await Assert.That(output).DoesNotContain("this[");
        
        // Verify no "Item" property is generated (indexers show up as "Item" in reflection)
        await Assert.That(output).DoesNotContain("public string Item");
        
        // The generated code should compile without errors
        var diagnostics = result.Diagnostics;
        await Assert.That(diagnostics.Any(d => d.Severity == Microsoft.CodeAnalysis.DiagnosticSeverity.Error)).IsFalse();
    }
    
    [Test]
    public async Task Generator_HandlesMultipleIndexers()
    {
        var source = """
            using DomainBridge;
            
            namespace TestNamespace
            {
                [DomainBridge(typeof(MultiIndexerService))]
                public partial class MultiIndexerServiceBridge { }
                
                public class MultiIndexerService
                {
                    // Multiple indexers with different parameter types
                    public string this[int index] => "int";
                    public string this[string key] => "string";
                    public string this[int x, int y] => "2d";
                    
                    // Regular members
                    public void DoWork() { }
                }
            }
            """;

        var result = TestHelper.RunGenerator(source);
        var output = TestHelper.GetGeneratedOutput(result);
        
        // Verify the bridge was generated
        await Assert.That(output).Contains("public partial class MultiIndexerServiceBridge");
        
        // Verify regular method is included
        await Assert.That(output).Contains("public void DoWork()");
        
        // Verify no indexers are included
        await Assert.That(output).DoesNotContain("this[");
        
        // Should compile without errors
        var hasErrors = result.Diagnostics.Any(d => d.Severity == Microsoft.CodeAnalysis.DiagnosticSeverity.Error);
        await Assert.That(hasErrors).IsFalse();
    }
}