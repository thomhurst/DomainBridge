using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using DomainBridge;
using TUnit.Assertions;
using TUnit.Core;

namespace DomainBridge.Tests
{
    public partial class GenericTypeTests
    {
        #region Test Types
        
        // Generic class with simple type parameter
        [Serializable]
        public class GenericService<T>
        {
            public T Value { get; set; }
            
            public GenericService(T value)
            {
                Value = value;
            }
            
            public T GetValue()
            {
                return Value;
            }
            
            public void SetValue(T value)
            {
                Value = value;
            }
            
            public TResult Transform<TResult>(Func<T, TResult> transformer)
            {
                return transformer(Value);
            }
        }
        
        // Generic class with multiple type parameters
        [Serializable]
        public class KeyValueService<TKey, TValue> where TKey : IComparable<TKey>
        {
            private readonly Dictionary<TKey, TValue> _data = new Dictionary<TKey, TValue>();
            
            public void Add(TKey key, TValue value)
            {
                _data[key] = value;
            }
            
            public TValue Get(TKey key)
            {
                return _data.TryGetValue(key, out var value) ? value : default(TValue);
            }
            
            public bool Contains(TKey key)
            {
                return _data.ContainsKey(key);
            }
        }
        
        // Generic class with constraints
        public class ConstrainedService<T> where T : class, IDisposable, new()
        {
            public T CreateInstance()
            {
                return new T();
            }
            
            public void Process(T item)
            {
                using (item)
                {
                    // Process item
                }
            }
        }
        
        // Generic interface
        public interface IRepository<T> where T : class
        {
            void Add(T entity);
            T GetById(int id);
            IEnumerable<T> GetAll();
        }
        
        // Generic class implementing generic interface
        [Serializable]
        public class Repository<T> : IRepository<T> where T : class
        {
            private readonly Dictionary<int, T> _entities = new Dictionary<int, T>();
            private int _nextId = 1;
            
            public void Add(T entity)
            {
                _entities[_nextId++] = entity;
            }
            
            public T GetById(int id)
            {
                return _entities.TryGetValue(id, out var entity) ? entity : null;
            }
            
            public IEnumerable<T> GetAll()
            {
                return _entities.Values;
            }
        }
        
        // Test entity for repository
        [Serializable]
        public class TestEntity
        {
            public string Name { get; set; }
            public int Value { get; set; }
        }
        
        // Bridge definitions
        [DomainBridge(typeof(GenericService<>))]
        public partial class GenericServiceBridge<T> { }
        
        [DomainBridge(typeof(KeyValueService<,>))]
        public partial class KeyValueServiceBridge<TKey, TValue> where TKey : IComparable<TKey> { }
        
        [DomainBridge(typeof(Repository<>))]
        public partial class RepositoryBridge<T> where T : class { }
        
        #endregion
        
        [Test]
        public async Task GenericBridge_WithSimpleTypeParameter_Works()
        {
            using (var bridge = GenericServiceBridge<string>.Create(() => new GenericService<string>("Hello")))
            {
                // Test getting value
                var value = bridge.GetValue();
                await Assert.That(value).IsEqualTo("Hello");
                
                // Test setting value
                bridge.SetValue("World");
                await Assert.That(bridge.GetValue()).IsEqualTo("World");
                
                // Test property
                bridge.Value = "Property";
                await Assert.That(bridge.Value).IsEqualTo("Property");
            }
        }
        
        [Test]
        public async Task GenericBridge_WithValueType_Works()
        {
            using (var bridge = GenericServiceBridge<int>.Create(() => new GenericService<int>(42)))
            {
                await Assert.That(bridge.GetValue()).IsEqualTo(42);
                
                bridge.SetValue(100);
                await Assert.That(bridge.GetValue()).IsEqualTo(100);
            }
        }
        
        [Test]
        public async Task GenericBridge_WithMultipleTypeParameters_Works()
        {
            using (var bridge = KeyValueServiceBridge<string, int>.Create(() => new KeyValueService<string, int>()))
            {
                bridge.Add("one", 1);
                bridge.Add("two", 2);
                
                await Assert.That(bridge.Get("one")).IsEqualTo(1);
                await Assert.That(bridge.Get("two")).IsEqualTo(2);
                await Assert.That(bridge.Contains("one")).IsTrue();
                await Assert.That(bridge.Contains("three")).IsFalse();
            }
        }
        
        [Test]
        public async Task GenericBridge_ImplementingGenericInterface_Works()
        {
            using (var bridge = RepositoryBridge<TestEntity>.Create(() => new Repository<TestEntity>()))
            {
                var entity1 = new TestEntity { Name = "Entity1", Value = 10 };
                var entity2 = new TestEntity { Name = "Entity2", Value = 20 };
                
                bridge.Add(entity1);
                bridge.Add(entity2);
                
                var retrieved = bridge.GetById(1);
                await Assert.That(retrieved).IsNotNull();
                await Assert.That(retrieved.Name).IsEqualTo("Entity1");
                
                var all = bridge.GetAll();
                await Assert.That(all).IsNotNull();
                
                int count = 0;
                foreach (var entity in all)
                {
                    count++;
                }
                await Assert.That(count).IsEqualTo(2);
            }
        }
        
        [Test]
        public async Task GenericBridge_WithNestedGenerics_Works()
        {
            using (var bridge = GenericServiceBridge<List<string>>.Create(() => new GenericService<List<string>>(new List<string> { "a", "b", "c" })))
            {
                var list = bridge.GetValue();
                await Assert.That(list).IsNotNull();
                await Assert.That(list.Count).IsEqualTo(3);
                await Assert.That(list[0]).IsEqualTo("a");
            }
        }
        
        [Test]
        public async Task GenericBridge_MultipleInstances_AreIndependent()
        {
            using (var bridge1 = GenericServiceBridge<string>.Create(() => new GenericService<string>("Instance1")))
            using (var bridge2 = GenericServiceBridge<string>.Create(() => new GenericService<string>("Instance2")))
            {
                await Assert.That(bridge1.GetValue()).IsEqualTo("Instance1");
                await Assert.That(bridge2.GetValue()).IsEqualTo("Instance2");
                
                bridge1.SetValue("Modified1");
                await Assert.That(bridge1.GetValue()).IsEqualTo("Modified1");
                await Assert.That(bridge2.GetValue()).IsEqualTo("Instance2");
            }
        }
        
        [Test]
        public async Task GenericBridge_DifferentTypeArguments_CreateDifferentBridges()
        {
            using (var stringBridge = GenericServiceBridge<string>.Create(() => new GenericService<string>("Text")))
            using (var intBridge = GenericServiceBridge<int>.Create(() => new GenericService<int>(123)))
            {
                await Assert.That(stringBridge.GetValue()).IsEqualTo("Text");
                await Assert.That(intBridge.GetValue()).IsEqualTo(123);
                
                // Verify they are different types
                await Assert.That(stringBridge.GetType()).IsNotEqualTo(intBridge.GetType());
            }
        }
    }
}