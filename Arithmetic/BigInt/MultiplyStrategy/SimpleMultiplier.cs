using Arithmetic.BigInt.Interfaces;

namespace Arithmetic.BigInt.MultiplyStrategy;

internal class SimpleMultiplier : IMultiplier
{
    public BetterBigInteger Multiply(BetterBigInteger a, BetterBigInteger b)
    {
        if (a is null) throw new ArgumentNullException(nameof(a));
        if (b is null) throw new ArgumentNullException(nameof(b));

        bool isNegative = a.IsNegative ^ b.IsNegative;
        uint[] product = BetterBigInteger.ClassicMultiply(a.GetDigits(), b.GetDigits());
        
        return BetterBigInteger.FromMagnitude(product, isNegative);
    }
}