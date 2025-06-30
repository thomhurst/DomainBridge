using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using TUnit.Core;

namespace DomainBridge.SourceGenerators.Tests
{
    public class SelfReferenceTest
    {
        [Test]
        public async Task HandlesSelfReferencingMethods()
        {
            var source = @"
using DomainBridge;
using System;

namespace TestNamespace
{
    public class SelfReferencingClass
    {
        public SelfReferencingClass Clone() => new SelfReferencingClass();
        public SelfReferencingClass GetInstance() => this;
        public SelfReferencingClass CreateNew(string value) => new SelfReferencingClass();
    }
    
    [DomainBridge(typeof(SelfReferencingClass))]
    public partial class SelfReferencingClassBridge { }
}
";

            var compilation = await TestHelper.CreateCompilation(source);
            var generator = new DomainBridgePatternGenerator();
            var driver = CSharpGeneratorDriver.Create(generator);

            var result = driver.RunGeneratorsAndUpdateCompilation(compilation, out var outputCompilation, out var diagnostics);

            // Check for compilation errors
            var allDiagnostics = outputCompilation.GetDiagnostics();
            var errors = allDiagnostics.Where(d => d.Severity == DiagnosticSeverity.Error).ToList();
            
            if (errors.Any())
            {
                Console.WriteLine("Compilation errors found:");
                foreach (var error in errors)
                {
                    Console.WriteLine($"  {error.Id}: {error.GetMessage()}");
                }
            }

            var bridgeTree = outputCompilation.SyntaxTrees.FirstOrDefault(t => t.FilePath.Contains("SelfReferencingClassBridge"));
            await Assert.That(bridgeTree).IsNotNull();

            var bridgeSource = bridgeTree.ToString();
            Console.WriteLine("Generated bridge source:");
            Console.WriteLine(bridgeSource);

            // The return type should reference the current bridge being generated
            await Assert.That(bridgeSource).Contains("public global::TestNamespace.SelfReferencingClassBridge Clone()");
            await Assert.That(bridgeSource).Contains("public global::TestNamespace.SelfReferencingClassBridge GetInstance()");
            await Assert.That(bridgeSource).Contains("public global::TestNamespace.SelfReferencingClassBridge CreateNew(string value)");
            
            // Should not reference the original type in return positions
            await Assert.That(bridgeSource).DoesNotContain("public global::TestNamespace.SelfReferencingClass Clone()");
            await Assert.That(bridgeSource).DoesNotContain("public global::TestNamespace.SelfReferencingClass GetInstance()");
        }

        [Test]
        public async Task HandlesNestedTypeSelfReferences()
        {
            var source = @"
using DomainBridge;
using System;

namespace TestNamespace
{
    public class OuterClass
    {
        public class InnerClass
        {
            public OuterClass GetOuter() => new OuterClass();
            public InnerClass Clone() => new InnerClass();
        }
        
        public InnerClass GetInner() => new InnerClass();
    }
    
    [DomainBridge(typeof(OuterClass.InnerClass))]
    public partial class InnerClassBridge { }
}
";

            var compilation = await TestHelper.CreateCompilation(source);
            var generator = new DomainBridgePatternGenerator();
            var driver = CSharpGeneratorDriver.Create(generator);

            var result = driver.RunGeneratorsAndUpdateCompilation(compilation, out var outputCompilation, out var diagnostics);

            // Check for compilation errors
            var allDiagnostics = outputCompilation.GetDiagnostics();
            var errors = allDiagnostics.Where(d => d.Severity == DiagnosticSeverity.Error).ToList();
            
            if (errors.Any())
            {
                Console.WriteLine("Compilation errors found:");
                foreach (var error in errors)
                {
                    Console.WriteLine($"  {error.Id}: {error.GetMessage()}");
                }
            }

            var bridgeTree = outputCompilation.SyntaxTrees.FirstOrDefault(t => t.FilePath.Contains("InnerClassBridge"));
            await Assert.That(bridgeTree).IsNotNull();

            var bridgeSource = bridgeTree.ToString();
            Console.WriteLine("Generated bridge source:");
            Console.WriteLine(bridgeSource);

            // When InnerClass.Clone() returns InnerClass, it should reference the current bridge
            await Assert.That(bridgeSource).Contains("public global::TestNamespace.InnerClassBridge Clone()");
            
            // When InnerClass.GetOuter() returns OuterClass, it should generate a bridge for OuterClass
            // This will be an auto-generated bridge since OuterClass is not explicitly marked with [DomainBridge]
            await Assert.That(bridgeSource).Contains("public global::TestNamespace.OuterClassBridge GetOuter()");
        }
    }
}