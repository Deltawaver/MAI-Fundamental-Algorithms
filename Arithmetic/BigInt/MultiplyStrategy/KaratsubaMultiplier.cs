using Arithmetic.BigInt.Interfaces;

namespace Arithmetic.BigInt.MultiplyStrategy;

internal class KaratsubaMultiplier : IMultiplier
{
    private const int ThresholdSize = 32; // Порог использования классического умножения
    
    public BetterBigInteger Multiply(BetterBigInteger a, BetterBigInteger b) 
    {
        if (a is null) throw new ArgumentNullException(nameof(a));
        if (b is null) throw new ArgumentNullException(nameof(b));
        
        if (a.IsZero || b.IsZero)
            return BetterBigInteger.FromMagnitude([0u], false);
        
        bool isNegative = a.IsNegative ^ b.IsNegative;
        uint[] result = MultiplyKaratsuba(a.GetDigits(), b.GetDigits());

        return BetterBigInteger.FromMagnitude(result, isNegative);
    }

    private static uint[] MultiplyKaratsuba(ReadOnlySpan<uint> left, ReadOnlySpan<uint> right)
    {
        uint[] normalizedLeft = BetterBigInteger.NormalizeCopy(left);
        uint[] normalizedRight = BetterBigInteger.NormalizeCopy(right);

        int leftLength = BetterBigInteger.GetRealLength(normalizedLeft);
        int rightLength = BetterBigInteger.GetRealLength(normalizedRight);

        if (leftLength == 0 || rightLength == 0)
        {
            return [0u];
        }

        int size = Math.Max(leftLength, rightLength);

        if (size <= ThresholdSize)
        {
            return BetterBigInteger.ClassicMultiply(normalizedLeft, normalizedRight);
        }

        int half = size / 2;

        // Разбиваем числа на младшую и старшую части
        ReadOnlySpan<uint> leftLow = left.Length <= half ? left : left.Slice(0, half);
        ReadOnlySpan<uint> leftHigh = left.Length <= half ? ReadOnlySpan<uint>.Empty : left.Slice(half);
        ReadOnlySpan<uint> rightLow = right.Length <= half ? right : right.Slice(0, half);
        ReadOnlySpan<uint> rightHigh = right.Length <= half ? ReadOnlySpan<uint>.Empty : right.Slice(half);
        
        // z0 = x0 * y0
        uint[] z0 = MultiplyKaratsuba(leftLow, rightLow);

        // z2 = x1 * y1
        uint[] z2 = MultiplyKaratsuba(leftHigh, rightHigh);

        // x0 + x1 и y0 + y1
        uint[] leftSum = BetterBigInteger.AddMagnitudes(leftLow, leftHigh);
        uint[] rightSum = BetterBigInteger.AddMagnitudes(rightLow, rightHigh);
        
        // z1 = (x0 + x1) * (y0 + y1)
        uint[] z1 = MultiplyKaratsuba(leftSum, rightSum);

        // z1 = z1 - z0 - z2
        z1 = BetterBigInteger.SubtractMagnitudes(z1, z0);
        z1 = BetterBigInteger.SubtractMagnitudes(z1, z2);

        // Сдвигаем части на нужное количество слов
        uint[] middlePart = ShiftLeftByWords(z1, half);
        uint[] highPart = ShiftLeftByWords(z2, 2 * half);

        // Собираем результат: z0 + z1 * B^half + z2 * B^(2*half)
        uint[] result = BetterBigInteger.AddMagnitudes(z0, middlePart);
        result = BetterBigInteger.AddMagnitudes(result, highPart);

        return BetterBigInteger.NormalizeCopy(result);
    }

    /// Сдвиг массива влево на указанное количество 32-битных слов
    private static uint[] ShiftLeftByWords(ReadOnlySpan<uint> digits, int wordShift)
    {
        if (wordShift == 0)
            return digits.ToArray();
        
        int length = BetterBigInteger.GetRealLength(digits);
        if (length == 0)
            return [0u];
        
        uint[] result = new uint[length + wordShift];
        
        for (int i = 0; i < length; i++)
        {
            result[i + wordShift] = digits[i];
        }
        
        return result;
    }
}