using System;
using DomainBridge;

namespace DomainBridge.Sample
{
    // Example showing interface implementation
    public interface ICalculator
    {
        int Add(int a, int b);
        int Subtract(int a, int b);
    }

    public interface IScientificCalculator : ICalculator
    {
        double Square(double value);
        double SquareRoot(double value);
    }

    public class ScientificCalculator : IScientificCalculator
    {
        public int Add(int a, int b) => a + b;
        public int Subtract(int a, int b) => a - b;
        public double Square(double value) => value * value;
        public double SquareRoot(double value) => Math.Sqrt(value);
    }

    // Bridge will implement IScientificCalculator and ICalculator interfaces
    [DomainBridge(typeof(ScientificCalculator))]
    public partial class ScientificCalculatorBridge
    {
        // Generated code will include:
        // - public partial class ScientificCalculatorBridge : MarshalByRefObject, IScientificCalculator, ICalculator
        // - All interface methods will be delegated to the wrapped instance
    }
}