using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using TUnit.Assertions;
using TUnit.Core;

namespace DomainBridge.SourceGenerators.Tests
{
    public class ComplexInterfaceTest
    {
        [Test]
        public async Task HandlesExplicitInterfaceImplementations()
        {
            var source = @"
using DomainBridge;
using System;

namespace TestNamespace
{
    public interface IFirst
    {
        void Method();
        string Property { get; }
    }

    public interface ISecond
    {
        void Method();  // Same name as IFirst.Method
        string Property { get; }  // Same name as IFirst.Property
    }

    public class MultipleInterfaces : IFirst, ISecond
    {
        // Explicit implementations to avoid ambiguity
        void IFirst.Method() => Console.WriteLine(""First"");
        string IFirst.Property => ""First"";
        
        void ISecond.Method() => Console.WriteLine(""Second"");
        string ISecond.Property => ""Second"";
        
        // Regular member
        public void RegularMethod() => Console.WriteLine(""Regular"");
    }

    [DomainBridge(typeof(MultipleInterfaces))]
    public partial class MultipleInterfacesBridge { }
}
";

            var compilation = await TestHelper.CreateCompilation(source);
            var generator = new DomainBridgePatternGenerator();
            var driver = CSharpGeneratorDriver.Create(generator);

            var result = driver.RunGeneratorsAndUpdateCompilation(compilation, out var outputCompilation, out var diagnostics);

            // Check no errors
            await Assert.That(diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error)).IsEmpty();

            var bridgeTree = outputCompilation.SyntaxTrees.FirstOrDefault(t => t.FilePath.Contains("MultipleInterfacesBridge"));
            
            await Assert.That(bridgeTree).IsNotNull();

            var bridgeSource = bridgeTree!.ToString();

            // Verify explicit interface implementations
            await Assert.That(bridgeSource).Contains("void global::TestNamespace.IFirst.Method()");
            await Assert.That(bridgeSource).Contains("string global::TestNamespace.IFirst.Property");
            await Assert.That(bridgeSource).Contains("void global::TestNamespace.ISecond.Method()");
            await Assert.That(bridgeSource).Contains("string global::TestNamespace.ISecond.Property");
            
            // Verify regular method
            await Assert.That(bridgeSource).Contains("public void RegularMethod()");
        }

        [Test]
        public async Task HandlesGenericInterfacesWithConstraints()
        {
            var source = @"
using DomainBridge;
using System;
using System.Collections.Generic;

namespace TestNamespace
{
    public interface IRepository<T> where T : class
    {
        T GetById(int id);
        IEnumerable<T> GetAll();
        void Add(T entity);
    }

    public class Entity
    {
        public int Id { get; set; }
        public string Name { get; set; }
    }

    public class EntityRepository : IRepository<Entity>
    {
        public Entity GetById(int id) => new Entity { Id = id };
        public IEnumerable<Entity> GetAll() => new List<Entity>();
        public void Add(Entity entity) { }
    }

    [DomainBridge(typeof(EntityRepository))]
    public partial class EntityRepositoryBridge { }
}
";

            var compilation = await TestHelper.CreateCompilation(source);
            var generator = new DomainBridgePatternGenerator();
            var driver = CSharpGeneratorDriver.Create(generator);

            var result = driver.RunGeneratorsAndUpdateCompilation(compilation, out var outputCompilation, out var diagnostics);

            // Check no errors
            await Assert.That(diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error)).IsEmpty();

            var bridgeTree = outputCompilation.SyntaxTrees.FirstOrDefault(t => t.FilePath.Contains("EntityRepositoryBridge"));
            await Assert.That(bridgeTree).IsNotNull();

            var bridgeSource = bridgeTree.ToString();

            // Verify interface is implemented
            await Assert.That(bridgeSource).Contains("global::TestNamespace.IRepository<global::TestNamespace.EntityBridge>");

            // Verify methods use bridge types
            await Assert.That(bridgeSource).Contains("public global::TestNamespace.EntityBridge GetById(int id)");
            await Assert.That(bridgeSource).Contains("public global::System.Collections.Generic.IEnumerable<global::TestNamespace.EntityBridge> GetAll()");
            await Assert.That(bridgeSource).Contains("public void Add(global::TestNamespace.EntityBridge entity)");
        }

        [Test]
        public async Task HandlesInterfaceInheritanceHierarchy()
        {
            var source = @"
using DomainBridge;
using System;

namespace TestNamespace
{
    public interface IBase
    {
        void BaseMethod();
    }

    public interface IMiddle1 : IBase
    {
        void Middle1Method();
    }

    public interface IMiddle2 : IBase
    {
        void Middle2Method();
    }

    public interface IDerived : IMiddle1, IMiddle2
    {
        void DerivedMethod();
    }

    public class ComplexHierarchy : IDerived
    {
        public void BaseMethod() { }
        public void Middle1Method() { }
        public void Middle2Method() { }
        public void DerivedMethod() { }
    }

    [DomainBridge(typeof(ComplexHierarchy))]
    public partial class ComplexHierarchyBridge { }
}
";

            var compilation = await TestHelper.CreateCompilation(source);
            var generator = new DomainBridgePatternGenerator();
            var driver = CSharpGeneratorDriver.Create(generator);

            var result = driver.RunGeneratorsAndUpdateCompilation(compilation, out var outputCompilation, out var diagnostics);

            // Check no errors
            await Assert.That(diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error)).IsEmpty();

            var bridgeTree = outputCompilation.SyntaxTrees.FirstOrDefault(t => t.FilePath.Contains("ComplexHierarchyBridge"));
            await Assert.That(bridgeTree).IsNotNull();

            var bridgeSource = bridgeTree.ToString();

            // Verify all interface methods are implemented
            await Assert.That(bridgeSource).Contains("public void BaseMethod()");
            await Assert.That(bridgeSource).Contains("public void Middle1Method()");
            await Assert.That(bridgeSource).Contains("public void Middle2Method()");
            await Assert.That(bridgeSource).Contains("public void DerivedMethod()");

            // Verify interface declaration includes IDerived (which implies all base interfaces)
            await Assert.That(bridgeSource).Contains("global::TestNamespace.IDerived");
        }

        [Test]
        public async Task HandlesIndexersAndEvents()
        {
            var source = @"
using DomainBridge;
using System;

namespace TestNamespace
{
    public interface IIndexable
    {
        string this[int index] { get; set; }
        string this[string key] { get; }
    }

    public interface IEventful
    {
        event EventHandler SimpleEvent;
        event EventHandler<CustomEventArgs> CustomEvent;
    }

    public class CustomEventArgs : EventArgs
    {
        public string Message { get; set; }
    }

    public class ComplexMembers : IIndexable, IEventful
    {
        // Indexers
        public string this[int index]
        {
            get => index.ToString();
            set { }
        }

        public string this[string key] => key;

        // Events
        public event EventHandler SimpleEvent;
        public event EventHandler<CustomEventArgs> CustomEvent;

        // Explicit interface implementation of event
        event EventHandler IEventful.SimpleEvent
        {
            add { SimpleEvent += value; }
            remove { SimpleEvent -= value; }
        }
    }

    [DomainBridge(typeof(ComplexMembers))]
    public partial class ComplexMembersBridge { }
}
";

            var compilation = await TestHelper.CreateCompilation(source);
            var generator = new DomainBridgePatternGenerator();
            var driver = CSharpGeneratorDriver.Create(generator);

            var result = driver.RunGeneratorsAndUpdateCompilation(compilation, out var outputCompilation, out var diagnostics);

            // Check no errors
            await Assert.That(diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error)).IsEmpty();

            var bridgeTree = outputCompilation.SyntaxTrees.FirstOrDefault(t => t.FilePath.Contains("ComplexMembersBridge"));
            await Assert.That(bridgeTree).IsNotNull();

            var bridgeSource = bridgeTree.ToString();

            // Verify indexers
            await Assert.That(bridgeSource).Contains("public string this[int index]");
            await Assert.That(bridgeSource).Contains("public string this[string key]");

            // Verify events
            await Assert.That(bridgeSource).Contains("public event global::System.EventHandler SimpleEvent");
            await Assert.That(bridgeSource).Contains("public event global::System.EventHandler<global::TestNamespace.CustomEventArgsBridge> CustomEvent");

            // Verify explicit interface event implementation
            await Assert.That(bridgeSource).Contains("event global::System.EventHandler global::TestNamespace.IEventful.SimpleEvent");
        }
    }
}