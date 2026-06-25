using Arithmetic.BigInt.Interfaces;
using System;
using System.Linq;

namespace Arithmetic.BigInt.MultiplyStrategy;

internal class FftMultiplier : IMultiplier
{
    // Простое число вида k * 2^n + 1, подходящее для NTT
    private const uint Modulus = 2013265921u; 
    private const uint Root = 31u;            

    public BetterBigInteger Multiply(BetterBigInteger a, BetterBigInteger b)
    {
        if (a is null || b is null)
            throw new ArgumentNullException();
        
        if (a.IsZero || b.IsZero)
            return new BetterBigInteger([0u]);

        var digitsA = a.GetDigits();
        var digitsB = b.GetDigits();
        
        var bytesA = ExpandToBytes(digitsA);
        var bytesB = ExpandToBytes(digitsB);

        // Длина результата в байтах
        int resultLen = bytesA.Length + bytesB.Length - 1;
        
        // Находим степень двойки, большую или равную длине результата
        int size = 1;
        while (size < resultLen) 
            size <<= 1;
        
        var bufferA = new uint[size];
        var bufferB = new uint[size];
        
        Array.Copy(bytesA, bufferA, bytesA.Length);
        Array.Copy(bytesB, bufferB, bytesB.Length);
        
        Transform(bufferA, inverse: false);
        Transform(bufferB, inverse: false);

        // Поэлементное умножение в частотной области
        for (int i = 0; i < size; i++)
        {
            bufferA[i] = MulMod(bufferA[i], bufferB[i]);
        }
        
        Transform(bufferA, inverse: true);
        
        uint[] finalBytes = new uint[resultLen];
        uint carry = 0;
        
        for (int i = 0; i < resultLen; i++)
        {
            ulong sum = (ulong)bufferA[i] + carry;
            
            finalBytes[i] = (uint)(sum & 0xFF);
            carry = (uint)(sum >> 8);         
        }
        
        int extraIdx = resultLen;
        while (carry > 0)
        {
            if (extraIdx >= finalBytes.Length)
                Array.Resize(ref finalBytes, finalBytes.Length + 1);
                
            finalBytes[extraIdx] = (uint)(carry & 0xFF);
            carry >>= 8;
            extraIdx++;
        }

        // Преобразуем массив байтов обратно в массив uint
        uint[] resultWords = CompressFromBytes(finalBytes, extraIdx);
        
        // Определяем знак
        bool isNegative = a.IsNegative ^ b.IsNegative;

        return new BetterBigInteger(resultWords, isNegative);
    }
    
    private static void Transform(uint[] data, bool inverse)
    {
        int n = data.Length;
        
        for (int i = 1, j = 0; i < n; i++)
        {
            int bit = n >> 1;
            for (; (j & bit) != 0; bit >>= 1)
                j ^= bit;
            j ^= bit;

            if (i < j)
                (data[i], data[j]) = (data[j], data[i]);
        }
        
        for (int len = 2; len <= n; len <<= 1)
        {
            uint rootPower = PowMod(Root, (Modulus - 1) / (uint)len);
            
            if (inverse)
            {
                rootPower = InvMod(rootPower);
            }

            for (int i = 0; i < n; i += len)
            {
                uint w = 1;
                int half = len / 2;
                
                for (int j = 0; j < half; j++)
                {
                    uint u = data[i + j];
                    uint v = MulMod(data[i + j + half], w);
                    
                    data[i + j] = AddMod(u, v);
                    data[i + j + half] = SubMod(u, v);

                    w = MulMod(w, rootPower);
                }
            }
        }

        // 3. Нормализация для обратного преобразования
        if (inverse)
        {
            uint nInv = InvMod((uint)n);
            for (int i = 0; i < n; i++)
            {
                data[i] = MulMod(data[i], nInv);
            }
        }
    }

    #region Math Helpers

    // Умножение по модулю
    private static uint MulMod(uint a, uint b)
    {
        return (uint)(((ulong)a * b) % Modulus);
    }

    // Сложение по модулю
    private static uint AddMod(uint a, uint b)
    {
        uint res = a + b;
        return res >= Modulus ? res - Modulus : res;
    }

    // Вычитание по модулю
    private static uint SubMod(uint a, uint b)
    {
        return a >= b ? a - b : a + Modulus - b;
    }

    // Возведение в степень по модулю (бинарное возведение)
    private static uint PowMod(uint baseVal, uint exp)
    {
        uint res = 1;
        baseVal %= Modulus;
        
        while (exp > 0)
        {
            if ((exp & 1) == 1)
                res = MulMod(res, baseVal);
            
            baseVal = MulMod(baseVal, baseVal);
            exp >>= 1;
        }
        return res;
    }

    // Поиск обратного элемента через теорему Ферма
    private static uint InvMod(uint a)
    {
        return PowMod(a, Modulus - 2);
    }

    #endregion

    #region Conversion Helpers
    
    /// Разбивает массив uint на массив byte (представленных как uint).
    /// Каждый uint разбивается на 4 байта: [b0, b1, b2, b3].
    private static uint[] ExpandToBytes(ReadOnlySpan<uint> source)
    {
        uint[] result = new uint[source.Length * 4];
        for (int i = 0; i < source.Length; i++)
        {
            uint val = source[i];
            int offset = i * 4;
            result[offset]     = val & 0xFF;
            result[offset + 1] = (val >> 8) & 0xFF;
            result[offset + 2] = (val >> 16) & 0xFF;
            result[offset + 3] = (val >> 24) & 0xFF;
        }
        return result;
    }
    
    /// Упаковывает массив байтов (uint[0..255]) обратно в массив uint.
    private static uint[] CompressFromBytes(uint[] bytes, int length)
    {
        int uintCount = (length + 3) / 4;
        uint[] result = new uint[uintCount];

        for (int i = 0; i < length; i++)
        {
            int uintIndex = i / 4;
            int shift = (i % 4) * 8;
            result[uintIndex] |= (bytes[i] << shift);
        }
        
        return result;
    }

    #endregion
}