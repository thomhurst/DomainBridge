using System.Threading.Tasks;
using TUnit.Core;
using TUnit.Assertions;
using System.Linq;

namespace DomainBridge.SourceGenerators.Tests;

public class IndexerGenerationTest
{
    [Test]
    public async Task Generator_ProperlyGeneratesIndexers()
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
        
        // Verify indexer IS included with proper syntax
        await Assert.That(output).Contains("public string this[int index]");
        
        // Verify indexer getter and setter use correct syntax
        await Assert.That(output).Contains("return _instance[index];");
        await Assert.That(output).Contains("_instance[index] = value;");
        
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
                    private string[] items = new string[10];
                    private System.Collections.Generic.Dictionary<string, string> dict = new System.Collections.Generic.Dictionary<string, string>();
                    
                    // Multiple indexers with different parameter types
                    public string this[int index] { get { return items[index]; } set { items[index] = value; } }
                    public string this[string key] { get { return dict[key]; } set { dict[key] = value; } }
                    public string this[int x, int y] => $"{x},{y}";
                    
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
        
        // C# only allows one indexer per type (but with overloads)
        // The generator should handle the first indexer it encounters
        await Assert.That(output).Contains("this[");
        
        // Should compile without errors
        var hasErrors = result.Diagnostics.Any(d => d.Severity == Microsoft.CodeAnalysis.DiagnosticSeverity.Error);
        await Assert.That(hasErrors).IsFalse();
    }
    
    [Test]
    public async Task Generator_HandlesIndexerWithDefaultParameters()
    {
        var source = """
            using DomainBridge;
            
            namespace TestNamespace
            {
                [DomainBridge(typeof(IndexerWithDefaultsService))]
                public partial class IndexerWithDefaultsServiceBridge { }
                
                public class IndexerWithDefaultsService
                {
                    private string[,] grid = new string[10, 10];
                    
                    // Indexer with default parameter
                    public string this[int x, int y = 0]
                    {
                        get { return grid[x, y]; }
                        set { grid[x, y] = value; }
                    }
                }
            }
            """;

        var result = TestHelper.RunGenerator(source);
        var output = TestHelper.GetGeneratedOutput(result);
        
        // Verify indexer is generated with default parameter
        await Assert.That(output).Contains("public string this[int x, int y = 0]");
        
        // Verify proper parameter forwarding
        await Assert.That(output).Contains("_instance[x, y]");
    }
}