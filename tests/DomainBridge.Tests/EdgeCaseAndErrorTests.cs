using System;
using System.Threading.Tasks;
using DomainBridge;
using TUnit.Assertions;
using TUnit.Core;

namespace DomainBridge.Tests
{
    /// <summary>
    /// Tests for edge cases and error scenarios
    /// </summary>
    public class EdgeCaseAndErrorTests
    {
        [Test]
        public async Task NullParameters_HandledCorrectly()
        {
            // Arrange
            var bridge = EdgeCaseTestServiceBridge.Instance;
            
            // Act & Assert - Should handle null inputs gracefully
            var result = bridge.ProcessNullableString(null!);
            await Assert.That(result).IsEqualTo("Received null");
            
            var result2 = bridge.ProcessNullableString("test");
            await Assert.That(result2).IsEqualTo("Received: test");
        }

        [Test]
        public async Task DefaultParameters_WorkCorrectly()
        {
            // Arrange
            var bridge = EdgeCaseTestServiceBridge.Instance;
            
            // Act
            var result1 = bridge.MethodWithDefaults();
            var result2 = bridge.MethodWithDefaults("custom");
            var result3 = bridge.MethodWithDefaults("custom", 42);
            
            // Assert
            await Assert.That(result1).IsEqualTo("default-10");
            await Assert.That(result2).IsEqualTo("custom-10");
            await Assert.That(result3).IsEqualTo("custom-42");
        }

        [Test]
        public async Task OverloadedMethods_ResolveCorrectly()
        {
            // Arrange
            var bridge = EdgeCaseTestServiceBridge.Instance;
            
            // Act
            var result1 = bridge.OverloadedMethod("string");
            var result2 = bridge.OverloadedMethod(123);
            var result3 = bridge.OverloadedMethod("string", 456);
            
            // Assert
            await Assert.That(result1).IsEqualTo("String: string");
            await Assert.That(result2).IsEqualTo("Int: 123");
            await Assert.That(result3).IsEqualTo("String: string, Int: 456");
        }

        [Test]
        public async Task GenericReturnTypes_WorkCorrectly()
        {
            // Arrange
            var bridge = EdgeCaseTestServiceBridge.Instance;
            
            // Act - Generic methods not supported in bridges, use specific typed methods
            var stringResult = bridge.GetStringValue("test");
            var intResult = bridge.GetIntValue(42);
            
            // Assert
            await Assert.That(stringResult).IsEqualTo("test");
            await Assert.That(intResult).IsEqualTo(42);
        }

        [Test]
        public void VoidMethods_ExecuteCorrectly()
        {
            // Arrange
            var bridge = EdgeCaseTestServiceBridge.Instance;
            
            // Act - Should not throw
            bridge.VoidMethod();
            bridge.VoidMethodWithParameter("test");
            
            // Assert - Test passes if no exception is thrown
            // No explicit assertion needed - test passes if no exception
        }

        [Test]
        public async Task PropertyAccess_WorksWithGetterAndSetter()
        {
            // Arrange
            var bridge = EdgeCaseTestServiceBridge.Instance;
            
            // Act
            bridge.TestProperty = "new value";
            var result = bridge.TestProperty;
            
            // Assert
            await Assert.That(result).IsEqualTo("new value");
        }

        [Test]
        public async Task ReadOnlyProperty_WorksCorrectly()
        {
            // Arrange
            var bridge = EdgeCaseTestServiceBridge.Instance;
            
            // Act
            var result = bridge.ReadOnlyProperty;
            
            // Assert
            await Assert.That(result).IsEqualTo("ReadOnly");
        }

        [Test]
        public async Task LargeStringValues_HandleCorrectly()
        {
            // Arrange
            var bridge = EdgeCaseTestServiceBridge.Instance;
            var largeString = new string('A', 100000); // 100KB string
            
            // Act
            var result = bridge.EchoString(largeString);
            
            // Assert
            await Assert.That(result).IsEqualTo(largeString);
        }

        [Test]
        public async Task SpecialCharacters_PreservedInStrings()
        {
            // Arrange
            var bridge = EdgeCaseTestServiceBridge.Instance;
            var specialString = "Special chars: \n\r\t\"'\\";
            
            // Act
            var result = bridge.EchoString(specialString);
            
            // Assert
            await Assert.That(result).IsEqualTo(specialString);
        }

        [Test]
        public async Task DateTime_SerializesCorrectly()
        {
            // Arrange
            var bridge = EdgeCaseTestServiceBridge.Instance;
            var testDate = new DateTime(2023, 12, 25, 10, 30, 45);
            
            // Act
            var result = bridge.ProcessDateTime(testDate);
            
            // Assert
            await Assert.That(result).IsEqualTo(testDate);
        }

        [Test]
        public async Task BooleanValues_WorkCorrectly()
        {
            // Arrange
            var bridge = EdgeCaseTestServiceBridge.Instance;
            
            // Act
            var trueResult = bridge.ToggleBoolean(true);
            var falseResult = bridge.ToggleBoolean(false);
            
            // Assert
            await Assert.That(trueResult).IsFalse();
            await Assert.That(falseResult).IsTrue();
        }

        [Test]
        [DependsOn(nameof(NullParameters_HandledCorrectly))]
        [DependsOn(nameof(DefaultParameters_WorkCorrectly))]
        [DependsOn(nameof(OverloadedMethods_ResolveCorrectly))]
        [DependsOn(nameof(GenericReturnTypes_WorkCorrectly))]
        [DependsOn(nameof(VoidMethods_ExecuteCorrectly))]
        [DependsOn(nameof(PropertyAccess_WorksWithGetterAndSetter))]
        [DependsOn(nameof(ReadOnlyProperty_WorksCorrectly))]
        [DependsOn(nameof(LargeStringValues_HandleCorrectly))]
        [DependsOn(nameof(SpecialCharacters_PreservedInStrings))]
        [DependsOn(nameof(DateTime_SerializesCorrectly))]
        [DependsOn(nameof(BooleanValues_WorkCorrectly))]
        public void Cleanup_UnloadDomains()
        {
            // Unload all domains used in this test class
            EdgeCaseTestServiceBridge.UnloadDomain();
        }
    }

    public class EdgeCaseTestService
    {
        public static EdgeCaseTestService Instance { get; } = new EdgeCaseTestService();
        
        private string _testProperty = "initial";
        
        public string ProcessNullableString(string input)
        {
            return input == null ? "Received null" : $"Received: {input}";
        }
        
        public string MethodWithDefaults(string text = "default", int number = 10)
        {
            return $"{text}-{number}";
        }
        
        public string OverloadedMethod(string input)
        {
            return $"String: {input}";
        }
        
        public string OverloadedMethod(int input)
        {
            return $"Int: {input}";
        }
        
        public string OverloadedMethod(string text, int number)
        {
            return $"String: {text}, Int: {number}";
        }
        
        public string GetStringValue(string input)
        {
            return input;
        }
        
        public int GetIntValue(int input)
        {
            return input;
        }
        
        public void VoidMethod()
        {
            // Does nothing
        }
        
        public void VoidMethodWithParameter(string parameter)
        {
            // Does nothing with parameter
        }
        
        public string TestProperty
        {
            get => _testProperty;
            set => _testProperty = value;
        }
        
        public string ReadOnlyProperty => "ReadOnly";
        
        public string EchoString(string input)
        {
            return input;
        }
        
        public DateTime ProcessDateTime(DateTime input)
        {
            return input;
        }
        
        public bool ToggleBoolean(bool input)
        {
            return !input;
        }
    }

    [DomainBridge(typeof(EdgeCaseTestService))]
    public partial class EdgeCaseTestServiceBridge { }
}