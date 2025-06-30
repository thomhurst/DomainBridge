using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using TUnit.Core;

namespace DomainBridge.SourceGenerators.Tests
{
    public class DuplicateNameTest
    {
        [Test]
        public async Task DoesNotGenerateDuplicateBridgeClasses()
        {
            var source = @"
using DomainBridge;
using System;

namespace TestNamespace
{
    public class MyClass
    {
        public MyClass GetSelf() => this;
        public OtherClass GetOther() => new OtherClass();
    }
    
    public class OtherClass
    {
        public MyClass GetMyClass() => new MyClass();
    }
    
    [DomainBridge(typeof(MyClass))]
    public partial class MyClassBridge { }
}";

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

            // Should not have any duplicate type definition errors
            var duplicateErrors = errors.Where(e => e.Id == "CS0101" || e.GetMessage().Contains("already contains a definition")).ToList();
            
            if (duplicateErrors.Any())
            {
                Console.WriteLine("Duplicate definition errors:");
                foreach (var error in duplicateErrors)
                {
                    Console.WriteLine($"  {error.Id}: {error.GetMessage()}");
                }
            }

            await Assert.That(duplicateErrors).IsEmpty();
        }

        [Test]
        public async Task HandlesSelfReferencingWithOtherBridges()
        {
            var source = @"
using DomainBridge;
using System;

namespace TestNamespace
{
    public class SelfRef
    {
        public SelfRef Clone() => new SelfRef();
        public SelfRef GetAnother() => new SelfRef();
    }
    
    [DomainBridge(typeof(SelfRef))]
    public partial class SelfRefBridge { }
}";

            var compilation = await TestHelper.CreateCompilation(source);
            var generator = new DomainBridgePatternGenerator();
            var driver = CSharpGeneratorDriver.Create(generator);

            var result = driver.RunGeneratorsAndUpdateCompilation(compilation, out var outputCompilation, out var diagnostics);

            // Check for compilation errors
            var allDiagnostics = outputCompilation.GetDiagnostics();
            var errors = allDiagnostics.Where(d => d.Severity == DiagnosticSeverity.Error).ToList();
            
            // Should not have any duplicate type definition errors
            var duplicateErrors = errors.Where(e => e.Id == "CS0101" || e.GetMessage().Contains("already contains a definition")).ToList();
            
            if (duplicateErrors.Any())
            {
                Console.WriteLine("Duplicate definition errors:");
                foreach (var error in duplicateErrors)
                {
                    Console.WriteLine($"  {error.Id}: {error.GetMessage()}");
                }
            }

            await Assert.That(duplicateErrors).IsEmpty();
        }

        [Test]
        public async Task HandlesGlobalNamespaceBridges()
        {
            var source = @"
using DomainBridge;
using System;

public class GlobalClass
{
    public GlobalClass Clone() => new GlobalClass();
}

[DomainBridge(typeof(GlobalClass))]
public partial class GlobalClassBridge { }
";

            var compilation = await TestHelper.CreateCompilation(source);
            var generator = new DomainBridgePatternGenerator();
            var driver = CSharpGeneratorDriver.Create(generator);

            var result = driver.RunGeneratorsAndUpdateCompilation(compilation, out var outputCompilation, out var diagnostics);

            // Check for compilation errors
            var allDiagnostics = outputCompilation.GetDiagnostics();
            var errors = allDiagnostics.Where(d => d.Severity == DiagnosticSeverity.Error).ToList();
            
            // Should not have any duplicate type definition errors
            var duplicateErrors = errors.Where(e => e.Id == "CS0101" || e.GetMessage().Contains("already contains a definition")).ToList();
            
            if (duplicateErrors.Any())
            {
                Console.WriteLine("Duplicate definition errors:");
                foreach (var error in duplicateErrors)
                {
                    Console.WriteLine($"  {error.Id}: {error.GetMessage()}");
                }
            }

            await Assert.That(duplicateErrors).IsEmpty();
        }
    }
}