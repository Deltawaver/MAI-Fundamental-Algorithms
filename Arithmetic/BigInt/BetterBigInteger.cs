using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using Arithmetic.BigInt.Interfaces;
using Arithmetic.BigInt.MultiplyStrategy;

namespace Arithmetic.BigInt;

public sealed class BetterBigInteger : IBigInteger
{
    // Размер одного слова в битах (uint = 4 байта * 8 бит)
    private const int WordBitSize = sizeof(uint) * 8;
    
    // Половина размера слова для разбиения при умножении (16 бит)
    private const int HalfWordBits = WordBitSize / 2;
    
    // Маска для выделения младшей половины слова (0xFFFF)
    private const uint LowWordMask = (1u << HalfWordBits) - 1;

    // Критерии выбора стратегии умножения
    private const int ThresholdSimple = 32;
    private const int ThresholdKaratsuba = 128;
    
    private static readonly IMultiplier _simpleMult = new SimpleMultiplier();
    private static readonly IMultiplier _karatsubaMult = new KaratsubaMultiplier();
    private static readonly IMultiplier _fftMult = new FftMultiplier();
    
    private int _negativeFlag;
    private uint _singleWord;
    private uint[]? _multiWord;
    
    private BetterBigInteger()
    {
        _negativeFlag = 0;
        _singleWord = 0;
        _multiWord = null;
    }
    
    public bool IsNegative => _negativeFlag == 1 && !IsZero;
    internal bool IsZero => _multiWord is null && _singleWord == 0u;
    private int WordCount => _multiWord?.Length ?? 1;
    
    public BetterBigInteger(uint[] digits, bool isNegative = false)
    {
        if (digits is null)
            throw new ArgumentNullException(nameof(digits));
        
        StoreDigits(digits, isNegative);
    }
    
    public BetterBigInteger(IEnumerable<uint> digits, bool isNegative = false)
    {
        if (digits is null)
            throw new ArgumentNullException(nameof(digits));
        
        StoreDigits(digits.ToArray(), isNegative);
    }
    
    public BetterBigInteger(string value, int radix)
    {
        if (value is null)
            throw new ArgumentNullException(nameof(value));
        
        if (radix < 2 || radix > 36)
            throw new ArgumentOutOfRangeException(nameof(radix), "Radix must be between 2 and 36");
        
        string input = value.Trim();
        if (input.Length == 0)
            throw new FormatException("Empty string is not a valid number");
        
        bool negative = false;
        int startIdx = 0;
        
        if (input[0] == '+')
            startIdx = 1;
        else if (input[0] == '-')
        {
            negative = true;
            startIdx = 1;
        }
        
        if (startIdx >= input.Length)
            throw new FormatException("No digits after sign");
        
        uint[] magnitude = [0u];
        
        for (int i = startIdx; i < input.Length; i++)
        {
            int digitVal = CharToDigit(input[i]);
            if (digitVal < 0 || digitVal >= radix)
                throw new FormatException($"Invalid digit '{input[i]}' for radix {radix}");
            
            magnitude = MultiplyByUInt(magnitude, (uint)radix);
            magnitude = AddUInt(magnitude, (uint)digitVal);
        }
        
        StoreDigits(magnitude, negative);
    }
    
    public ReadOnlySpan<uint> GetDigits()
    {
        return _multiWord ?? [_singleWord];
    }
    
    public int CompareTo(IBigInteger? other)
    {
        if (other is null)
            return 1;
        
        if (IsNegative != other.IsNegative)
            return IsNegative ? -1 : 1;
        
        int cmp = CompareMagnitudes(GetDigits(), other.GetDigits());
        return IsNegative ? -cmp : cmp;
    }
    
    public bool Equals(IBigInteger? other)
    {
        return CompareTo(other) == 0;
    }
    
    public override bool Equals(object? obj)
    {
        return obj is IBigInteger other && Equals(other);
    }
    
    public override int GetHashCode()
    {
        HashCode hash = new();
        hash.Add(IsNegative);
        
        ReadOnlySpan<uint> digits = GetDigits();
        int realLen = GetRealLength(digits);
        
        if (realLen == 0)
        {
            hash.Add(0u);
            return hash.ToHashCode();
        }
        
        for (int i = 0; i < realLen; i++)
            hash.Add(digits[i]);
        
        return hash.ToHashCode();
    }
    
    public static BetterBigInteger operator +(BetterBigInteger a, BetterBigInteger b)
    {
        if (a is null) throw new ArgumentNullException(nameof(a));
        if (b is null) throw new ArgumentNullException(nameof(b));
        
        ReadOnlySpan<uint> left = a.GetDigits();
        ReadOnlySpan<uint> right = b.GetDigits();
        
        if (a.IsNegative == b.IsNegative)
            return FromMagnitude(AddMagnitudes(left, right), a.IsNegative);
        
        int comparison = CompareMagnitudes(left, right);
        
        if (comparison == 0)
            return Zero();
        
        if (comparison > 0)
            return FromMagnitude(SubtractMagnitudes(left, right), a.IsNegative);
        
        return FromMagnitude(SubtractMagnitudes(right, left), b.IsNegative);
    }
    
    public static BetterBigInteger operator -(BetterBigInteger a, BetterBigInteger b)
    {
        if (a is null) throw new ArgumentNullException(nameof(a));
        if (b is null) throw new ArgumentNullException(nameof(b));
        
        return a + (-b);
    }
    
    public static BetterBigInteger operator -(BetterBigInteger a)
    {
        if (a is null) throw new ArgumentNullException(nameof(a));
        
        if (a.IsZero)
            return Zero();
        
        return FromMagnitude(a.GetDigits(), !a.IsNegative);
    }
    
    public static BetterBigInteger operator /(BetterBigInteger a, BetterBigInteger b)
    {
        if (a is null) throw new ArgumentNullException(nameof(a));
        if (b is null) throw new ArgumentNullException(nameof(b));
        
        if (b.IsZero)
            throw new DivideByZeroException("Division by zero");
        
        uint[] quotient = DivideMagnitudes(a.GetDigits(), b.GetDigits(), out uint[] remainder);
        bool negative = (a.IsNegative ^ b.IsNegative) && !IsMagnitudeZero(quotient);
        return FromMagnitude(quotient, negative);
    }
    
    public static BetterBigInteger operator %(BetterBigInteger a, BetterBigInteger b)
    {
        if (a is null) throw new ArgumentNullException(nameof(a));
        if (b is null) throw new ArgumentNullException(nameof(b));
        
        if (b.IsZero)
            throw new DivideByZeroException("Modulo by zero");
        
        DivideMagnitudes(a.GetDigits(), b.GetDigits(), out uint[] remainder);
        bool negative = a.IsNegative && !IsMagnitudeZero(remainder);
        return FromMagnitude(remainder, negative);
    }
    
    public static BetterBigInteger operator *(BetterBigInteger a, BetterBigInteger b)
    {
        if (a is null) throw new ArgumentNullException(nameof(a));
        if (b is null) throw new ArgumentNullException(nameof(b));
        
        int size = Math.Max(a.WordCount, b.WordCount);
        
        IMultiplier strategy = size < ThresholdSimple 
            ? _simpleMult 
            : size < ThresholdKaratsuba 
                ? _karatsubaMult 
                : _fftMult;
        
        return strategy.Multiply(a, b);
    }
    
    public static BetterBigInteger operator ~(BetterBigInteger a)
    {
        if (a is null) throw new ArgumentNullException(nameof(a));
        
        int targetLen = Math.Max(1, a.WordCount + 1);
        uint[] words = ToTwoComplement(a, targetLen);
        
        for (int i = 0; i < words.Length; i++)
            words[i] = ~words[i];
        
        return FromTwoComplement(words);
    }
    
    public static BetterBigInteger operator &(BetterBigInteger a, BetterBigInteger b)
    {
        return BitwiseOp(a, b, (x, y) => x & y);
    }
    
    public static BetterBigInteger operator |(BetterBigInteger a, BetterBigInteger b)
    {
        return BitwiseOp(a, b, (x, y) => x | y);
    }
    
    public static BetterBigInteger operator ^(BetterBigInteger a, BetterBigInteger b)
    {
        return BitwiseOp(a, b, (x, y) => x ^ y);
    }
    
    public static BetterBigInteger operator <<(BetterBigInteger a, int shift)
    {
        if (a is null) throw new ArgumentNullException(nameof(a));
        
        if (shift == int.MinValue)
            throw new ArgumentOutOfRangeException(nameof(shift), "Cannot negate int.MinValue");
        
        if (shift < 0)
            return a >> -shift;
        
        if (shift == 0 || a.IsZero)
            return FromMagnitude(a.GetDigits(), a.IsNegative);
        
        return FromMagnitude(ShiftLeftMagnitude(a.GetDigits(), shift), a.IsNegative);
    }
    
    public static BetterBigInteger operator >>(BetterBigInteger a, int shift)
    {
        if (a is null) throw new ArgumentNullException(nameof(a));
        
        if (shift == int.MinValue)
            throw new ArgumentOutOfRangeException(nameof(shift), "Cannot negate int.MinValue");
        
        if (shift < 0)
            return a << -shift;
        
        if (shift == 0 || a.IsZero)
            return FromMagnitude(a.GetDigits(), a.IsNegative);
        
        if (!a.IsNegative)
            return FromMagnitude(ShiftRightMagnitude(a.GetDigits(), shift), false);
        
        BetterBigInteger one = FromMagnitude([1u], false);
        BetterBigInteger temp = (one << shift) - one;
        BetterBigInteger adjusted = Abs(a) + temp;
        BetterBigInteger shifted = FromMagnitude(ShiftRightMagnitude(adjusted.GetDigits(), shift), false);
        return shifted.IsZero ? Zero() : -shifted;
    }
    
    public static bool operator ==(BetterBigInteger? a, BetterBigInteger? b)
    {
        if (ReferenceEquals(a, b))
            return true;
        if (a is null || b is null)
            return false;
        return a.Equals(b);
    }
    
    public static bool operator !=(BetterBigInteger? a, BetterBigInteger? b) => !(a == b);
    public static bool operator <(BetterBigInteger a, BetterBigInteger b) => a.CompareTo(b) < 0;
    public static bool operator >(BetterBigInteger a, BetterBigInteger b) => a.CompareTo(b) > 0;
    public static bool operator <=(BetterBigInteger a, BetterBigInteger b) => a.CompareTo(b) <= 0;
    public static bool operator >=(BetterBigInteger a, BetterBigInteger b) => a.CompareTo(b) >= 0;
    
    public override string ToString() => ToString(10);
    
    public string ToString(int radix)
    {
        if (radix < 2 || radix > 36)
            throw new ArgumentOutOfRangeException(nameof(radix), "Radix must be between 2 and 36");
        
        if (IsZero)
            return "0";
        
        uint[] work = NormalizeCopy(GetDigits());
        int len = work.Length;
        StringBuilder reversed = new();
        
        while (len > 0)
        {
            uint rem = DivideBySmall(work, ref len, (uint)radix);
            reversed.Append(DigitToChar((int)rem));
        }
        
        if (IsNegative)
            reversed.Append('-');
        
        char[] chars = reversed.ToString().ToCharArray();
        Array.Reverse(chars);
        return new string(chars);
    }
    
    #region Helper Methods
    
    private static BetterBigInteger Zero() => new();
    private static BetterBigInteger Abs(BetterBigInteger value) => FromMagnitude(value.GetDigits(), false);
    internal static BetterBigInteger FromMagnitude(ReadOnlySpan<uint> digits, bool isNegative) 
        => new(digits.ToArray(), isNegative);
    
    private static bool IsMagnitudeZero(ReadOnlySpan<uint> digits) => GetRealLength(digits) == 0;
    
    internal static int GetRealLength(ReadOnlySpan<uint> digits)
    {
        int len = digits.Length;
        while (len > 0 && digits[len - 1] == 0u)
            len--;
        return len;
    }
    
    internal static uint[] NormalizeCopy(ReadOnlySpan<uint> digits)
    {
        int len = GetRealLength(digits);
        if (len == 0)
            return [0u];
        
        uint[] result = new uint[len];
        for (int i = 0; i < len; i++)
            result[i] = digits[i];
        return result;
    }
    
    private static int CompareMagnitudes(ReadOnlySpan<uint> left, ReadOnlySpan<uint> right)
    {
        int leftLen = GetRealLength(left);
        int rightLen = GetRealLength(right);
        
        if (leftLen != rightLen)
            return leftLen < rightLen ? -1 : 1;
        
        for (int i = leftLen - 1; i >= 0; i--)
        {
            if (left[i] != right[i])
                return left[i] < right[i] ? -1 : 1;
        }
        return 0;
    }
    
    private static uint AddWord(uint x, uint y, uint carryIn, out uint carryOut)
    {
        uint sum = x + y;
        uint carry1 = sum < x ? 1u : 0u;
        
        uint result = sum + carryIn;
        uint carry2 = result < sum ? 1u : 0u;
        
        carryOut = carry1 + carry2;
        return result;
    }
    
    private static void MultiplyWord(uint x, uint y, out uint low, out uint high)
    {
        // Используем константы вместо магических чисел 16 и 0xFFFF
        uint xLow = x & LowWordMask;
        uint xHigh = x >> HalfWordBits;
        uint yLow = y & LowWordMask;
        uint yHigh = y >> HalfWordBits;
        
        uint p00 = xLow * yLow;
        uint p01 = xLow * yHigh;
        uint p10 = xHigh * yLow;
        uint p11 = xHigh * yHigh;
        
        uint mid = p00 >> HalfWordBits;
        uint carry = 0;
        
        uint sum = mid + (p01 & LowWordMask);
        if (sum < mid) carry++;
        mid = sum;
        
        sum = mid + (p10 & LowWordMask);
        if (sum < (mid & LowWordMask)) carry++;
        mid = sum;
        
        low = (p00 & LowWordMask) | ((mid & LowWordMask) << HalfWordBits);
        high = p11 + (p01 >> HalfWordBits) + (p10 >> HalfWordBits) + (mid >> HalfWordBits) + carry;
    }
    
    private static void PropagateCarry(uint[] array, int index, uint value)
    {
        uint carry = value;
        int pos = index;
        
        while (carry != 0)
        {
            uint sum = array[pos] + carry;
            carry = sum < array[pos] ? 1u : 0u;
            array[pos] = sum;
            pos++;
        }
    }
    
    internal static uint[] AddMagnitudes(ReadOnlySpan<uint> left, ReadOnlySpan<uint> right)
    {
        int maxLen = Math.Max(left.Length, right.Length);
        uint[] result = new uint[maxLen + 1];
        uint carry = 0;
        
        for (int i = 0; i < maxLen; i++)
        {
            uint leftVal = i < left.Length ? left[i] : 0u;
            uint rightVal = i < right.Length ? right[i] : 0u;
            result[i] = AddWord(leftVal, rightVal, carry, out carry);
        }
        
        result[maxLen] = carry;
        return NormalizeCopy(result);
    }
    
    internal static uint[] SubtractMagnitudes(ReadOnlySpan<uint> left, ReadOnlySpan<uint> right)
    {
        if (CompareMagnitudes(left, right) < 0)
            throw new ArgumentException("left must be >= right");
        
        uint[] result = new uint[left.Length];
        uint borrow = 0;
        
        for (int i = 0; i < left.Length; i++)
        {
            uint rightVal = i < right.Length ? right[i] : 0u;
            uint diff = left[i] - rightVal;
            uint newBorrow = diff > left[i] ? 1u : 0u;
            
            diff = diff - borrow;
            if (diff > (left[i] - rightVal)) 
                newBorrow++;
            
            result[i] = diff;
            borrow = newBorrow;
        }
        
        return NormalizeCopy(result);
    }
    
    internal static uint[] ClassicMultiply(ReadOnlySpan<uint> left, ReadOnlySpan<uint> right)
    {
        int leftLen = GetRealLength(left);
        int rightLen = GetRealLength(right);
        
        if (leftLen == 0 || rightLen == 0)
            return [0u];
        
        uint[] result = new uint[leftLen + rightLen];
        
        for (int i = 0; i < leftLen; i++)
        {
            uint acc = 0;
            
            for (int j = 0; j < rightLen; j++)
            {
                MultiplyWord(left[i], right[j], out uint low, out uint high);
                
                uint sum = AddWord(result[i + j], low, acc, out uint accFromLow);
                result[i + j] = sum;
                
                uint nextAcc = high + accFromLow;
                if (nextAcc < high)
                    PropagateCarry(result, i + j + 2, 1u);
                
                acc = nextAcc;
            }
            
            PropagateCarry(result, i + rightLen, acc);
        }
        
        return NormalizeCopy(result);
    }
    
    private static uint[] AddUInt(ReadOnlySpan<uint> digits, uint value)
    {
        if (value == 0)
            return NormalizeCopy(digits);
        
        int len = GetRealLength(digits);
        if (len == 0)
            return [value];
        
        uint[] result = new uint[len + 1];
        uint carry = value;
        int i = 0;
        
        while (i < len && carry != 0)
        {
            uint sum = digits[i] + carry;
            result[i] = sum;
            carry = sum < digits[i] ? 1u : 0u;
            i++;
        }
        
        while (i < len)
        {
            result[i] = digits[i];
            i++;
        }
        
        if (carry > 0)
            result[len] = carry;
        
        return NormalizeCopy(result);
    }
    
    private static uint[] MultiplyByUInt(ReadOnlySpan<uint> digits, uint factor)
    {
        int len = GetRealLength(digits);
        if (len == 0 || factor == 0)
            return [0u];
        if (factor == 1)
            return NormalizeCopy(digits);
        
        uint[] result = new uint[len + 1];
        uint carry = 0;
        
        for (int i = 0; i < len; i++)
        {
            MultiplyWord(digits[i], factor, out uint low, out uint high);
            result[i] = AddWord(low, carry, 0, out uint newCarry);
            carry = high + newCarry;
        }
        
        result[len] = carry;
        return NormalizeCopy(result);
    }
    
    private static uint[] ShiftLeftMagnitude(ReadOnlySpan<uint> digits, int shift)
    {
        int len = GetRealLength(digits);
        if (len == 0 || shift == 0)
            return NormalizeCopy(digits);
        
        int wordShift = shift / WordBitSize;
        int bitShift = shift % WordBitSize;
        uint[] result = new uint[len + wordShift + 1];
        
        if (bitShift == 0)
        {
            for (int i = 0; i < len; i++)
                result[i + wordShift] = digits[i];
            return NormalizeCopy(result);
        }
        
        uint carry = 0;
        for (int i = 0; i < len; i++)
        {
            uint current = digits[i];
            result[i + wordShift] = (current << bitShift) | carry;
            carry = current >> (WordBitSize - bitShift);
        }
        
        result[len + wordShift] = carry;
        return NormalizeCopy(result);
    }
    
    private static uint[] ShiftRightMagnitude(ReadOnlySpan<uint> digits, int shift)
    {
        int len = GetRealLength(digits);
        if (len == 0 || shift == 0)
            return NormalizeCopy(digits);
        
        int wordShift = shift / WordBitSize;
        int bitShift = shift % WordBitSize;
        
        if (wordShift >= len)
            return [0u];
        
        int resultLen = len - wordShift;
        uint[] result = new uint[resultLen];
        
        if (bitShift == 0)
        {
            for (int i = wordShift; i < len; i++)
                result[i - wordShift] = digits[i];
            return NormalizeCopy(result);
        }
        
        uint carry = 0;
        // Вычисляем маску динамически
        uint mask = (1u << bitShift) - 1u;
        
        for (int i = len - 1; i >= wordShift; i--)
        {
            uint current = digits[i];
            result[i - wordShift] = (current >> bitShift) | (carry << (WordBitSize - bitShift));
            carry = current & mask;
        }
        
        return NormalizeCopy(result);
    }
    
    private static int GetBitLength(ReadOnlySpan<uint> digits)
    {
        int len = GetRealLength(digits);
        if (len == 0)
            return 0;
        
        uint highest = digits[len - 1];
        return ((len - 1) * WordBitSize) + (WordBitSize - BitOperations.LeadingZeroCount(highest));
    }
    
    private static bool TestBit(ReadOnlySpan<uint> digits, int bitPos)
    {
        int wordIdx = bitPos / WordBitSize;
        if (wordIdx >= digits.Length)
            return false;
        
        int offset = bitPos % WordBitSize;
        return ((digits[wordIdx] >> offset) & 1u) != 0;
    }
    
    private static uint[] DivideMagnitudes(ReadOnlySpan<uint> dividend, ReadOnlySpan<uint> divisor, out uint[] remainder)
    {
        uint[] workDividend = NormalizeCopy(dividend);
        uint[] workDivisor = NormalizeCopy(divisor);
        
        if (IsMagnitudeZero(workDivisor))
            throw new DivideByZeroException();
        
        if (CompareMagnitudes(workDividend, workDivisor) < 0)
        {
            remainder = workDividend;
            return [0u];
        }
        
        int bitLength = GetBitLength(workDividend);
        uint[] quotient = new uint[(bitLength + WordBitSize - 1) / WordBitSize];
        uint[] acc = [0u];
        
        for (int bit = bitLength - 1; bit >= 0; bit--)
        {
            acc = ShiftLeftMagnitude(acc, 1);
            
            if (TestBit(workDividend, bit))
                acc = AddUInt(acc, 1u);
            
            if (CompareMagnitudes(acc, workDivisor) >= 0)
            {
                acc = SubtractMagnitudes(acc, workDivisor);
                quotient[bit / WordBitSize] |= 1u << (bit % WordBitSize);
            }
        }
        
        remainder = NormalizeCopy(acc);
        return NormalizeCopy(quotient);
    }
    
    private static uint DivideBySmall(uint[] digits, ref int length, uint divisor)
    {
        if (divisor == 0)
            throw new DivideByZeroException();
        
        uint rem = 0;
        
        for (int i = length - 1; i >= 0; i--)
        {
            uint quotWord = 0;
            uint currentWord = digits[i];
            
            for (int bit = WordBitSize - 1; bit >= 0; bit--)
            {
                rem <<= 1;
                if (((currentWord >> bit) & 1u) != 0)
                    rem |= 1u;
                
                if (rem >= divisor)
                {
                    rem -= divisor;
                    quotWord |= 1u << bit;
                }
            }
            
            digits[i] = quotWord;
        }
        
        while (length > 0 && digits[length - 1] == 0)
            length--;
        
        return rem;
    }
    
    private static BetterBigInteger BitwiseOp(BetterBigInteger a, BetterBigInteger b, Func<uint, uint, uint> op)
    {
        if (a is null) throw new ArgumentNullException(nameof(a));
        if (b is null) throw new ArgumentNullException(nameof(b));
        
        int targetWords = Math.Max(a.WordCount, b.WordCount) + 1;
        uint[] leftWords = ToTwoComplement(a, targetWords);
        uint[] rightWords = ToTwoComplement(b, targetWords);
        uint[] resultWords = new uint[targetWords];
        
        for (int i = 0; i < targetWords; i++)
            resultWords[i] = op(leftWords[i], rightWords[i]);
        
        return FromTwoComplement(resultWords);
    }
    
    private static uint[] ToTwoComplement(BetterBigInteger value, int wordCount)
    {
        uint[] words = new uint[wordCount];
        ReadOnlySpan<uint> digits = value.GetDigits();
        int copyLen = Math.Min(GetRealLength(digits), wordCount);
        
        for (int i = 0; i < copyLen; i++)
            words[i] = digits[i];
        
        if (!value.IsNegative)
            return words;
        
        for (int i = 0; i < words.Length; i++)
            words[i] = ~words[i];
        
        uint carry = 1;
        for (int i = 0; i < words.Length; i++)
        {
            uint sum = words[i] + carry;
            words[i] = sum;
            carry = sum == 0 ? 1u : 0u;
            if (carry == 0)
                break;
        }
        
        return words;
    }
    
    private static BetterBigInteger FromTwoComplement(uint[] words)
    {
        bool negative = (words[^1] & (1u << (WordBitSize - 1))) != 0;
        
        if (!negative)
            return FromMagnitude(words, false);
        
        uint[] magnitude = new uint[words.Length];
        for (int i = 0; i < words.Length; i++)
            magnitude[i] = ~words[i];
        
        uint carry = 1;
        for (int i = 0; i < magnitude.Length; i++)
        {
            uint sum = magnitude[i] + carry;
            magnitude[i] = sum;
            carry = sum == 0 ? 1u : 0u;
            if (carry == 0)
                break;
        }
        
        magnitude = NormalizeCopy(magnitude);
        return IsMagnitudeZero(magnitude) ? Zero() : FromMagnitude(magnitude, true);
    }
    
    private void StoreDigits(uint[] digits, bool isNegative)
    {
        int len = GetRealLength(digits);
        
        if (len == 0)
        {
            _negativeFlag = 0;
            _singleWord = 0;
            _multiWord = null;
            return;
        }
        
        if (len == 1)
        {
            _negativeFlag = digits[0] == 0 ? 0 : (isNegative ? 1 : 0);
            _singleWord = digits[0];
            _multiWord = null;
            return;
        }
        
        _negativeFlag = isNegative ? 1 : 0;
        _singleWord = 0;
        _multiWord = new uint[len];
        Array.Copy(digits, _multiWord, len);
    }
    
    private static int CharToDigit(char c)
    {
        if (c >= '0' && c <= '9')
            return c - '0';
        if (c >= 'A' && c <= 'Z')
            return c - 'A' + 10;
        if (c >= 'a' && c <= 'z')
            return c - 'a' + 10;
        return -1;
    }
    
    private static char DigitToChar(int digit)
    {
        return digit < 10 ? (char)('0' + digit) : (char)('A' + digit - 10);
    }
    
    #endregion
}

#dotnet test Arithmetic.Tests/ -v normal