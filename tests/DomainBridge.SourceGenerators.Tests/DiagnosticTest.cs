using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using TUnit.Assertions;
using TUnit.Core;

namespace DomainBridge.SourceGenerators.Tests
{
    public class DiagnosticTest
    {
        [Test]
        public async Task ReportsDBG200ForSealedTypes()
        {
            var source = @"
using DomainBridge;
using System;

namespace TestNamespace
{
    public sealed class SealedType
    {
        public string Name { get; set; }
    }

    public class TestType
    {
        public SealedType GetSealed() => new SealedType();
    }

    [DomainBridge(typeof(TestType))]
    public partial class TestTypeBridge { }
}
";

            var compilation = await TestHelper.CreateCompilation(source);
            var generator = new DomainBridgePatternGenerator();
            var driver = CSharpGeneratorDriver.Create(generator);

            var result = driver.RunGeneratorsAndUpdateCompilation(compilation, out var outputCompilation, out var diagnostics);

            // Debug output
            Console.WriteLine($"Total diagnostics: {diagnostics.Length}");
            foreach (var diag in diagnostics)
            {
                Console.WriteLine($"Diagnostic: {diag.Id} - {diag.GetMessage()}");
            }

            // Check for DBG200
            var dbg200 = diagnostics.FirstOrDefault(d => d.Id == "DBG200");
            await Assert.That(dbg200).IsNotNull();
            await Assert.That(dbg200!.GetMessage()).Contains("SealedType");
        }
    }
}