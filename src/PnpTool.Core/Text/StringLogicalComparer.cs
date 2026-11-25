using System.Collections;

namespace PnpTool.Core.Text;

/// <summary>
/// Orders text the way a file manager does, so "card2" comes before "card10".
/// Original: <c>StringLogicalComparer</c>. The algorithm is copied step by step,
/// including its quirks (see remarks), because image order depends on it.
/// </summary>
/// <remarks>
/// Known quirks kept from the original: an empty second string and a string that is
/// a prefix of the other both compare as -1 regardless of argument order, so the
/// comparison is not symmetric in those cases.
/// </remarks>
public static class StringLogicalComparer
{
    public static int Compare(string? s1, string? s2)
    {
        (bool decided, int result) = CompareBeforeLoop(s1, s2);
        if (decided)
        {
            return result;
        }

        int i1 = 0;
        int i2 = 0;
        do
        {
            bool digit1 = char.IsDigit(s1!, i1);
            bool digit2 = char.IsDigit(s2!, i2);
            if (!(digit1 & digit2))
            {
                (decided, result) = CompareInsideLoop(s1!, s2!, i1, i2, digit1, digit2);
                if (decided)
                {
                    return result;
                }
            }
            else
            {
                int r = CompareNumbers(s1!, ref i1, s2!, ref i2);
                if (r != 0)
                {
                    return r;
                }
            }

            i1++;
            i2++;
            (decided, result) = CompareLengths(s1!.Length, s2!.Length, i1, i2);
        }
        while (!decided);
        return result;
    }

    private static (bool, int) CompareBeforeLoop(string? s1, string? s2)
    {
        (bool decided, int result) = CompareNullOrEmpty(s1, s2);
        if (decided)
        {
            return (true, result);
        }

        return CompareLeadingSymbol(s1!, s2!);
    }

    private static (bool, int) CompareNullOrEmpty(string? s1, string? s2)
    {
        if (s1 == null && s2 == null)
        {
            return (true, 0);
        }

        if (s1 == null)
        {
            return (true, -1);
        }

        if (s2 == null)
        {
            return (true, 1);
        }

        if (s1 == string.Empty && s2 == string.Empty)
        {
            return (true, 0);
        }

        if (s1 == string.Empty)
        {
            return (true, -1);
        }

        if (s2 == string.Empty)
        {
            return (true, -1);
        }

        return (false, 0);
    }

    // Names starting with a symbol sort before names starting with a letter or digit.
    private static (bool, int) CompareLeadingSymbol(string s1, string s2)
    {
        bool alnum1 = char.IsLetterOrDigit(s1, 0);
        bool alnum2 = char.IsLetterOrDigit(s2, 0);
        if (alnum1 && !alnum2)
        {
            return (true, 1);
        }

        if (!alnum1 & alnum2)
        {
            return (true, -1);
        }

        return (false, 0);
    }

    private static (bool, int) CompareNonDigits(string s1, string s2, int i1, int i2)
    {
        bool letter1 = char.IsLetter(s1, i1);
        bool letter2 = char.IsLetter(s2, i2);
        if ((letter1 & letter2) || (!letter1 && !letter2))
        {
            int r = (letter1 & letter2)
                ? char.ToLower(s1[i1]).CompareTo(char.ToLower(s2[i2]))
                : s1[i1].CompareTo(s2[i2]);
            if (r != 0)
            {
                return (true, r);
            }
        }

        if (!letter1 & letter2)
        {
            return (true, -1);
        }

        if (letter1 && !letter2)
        {
            return (true, 1);
        }

        return (false, 0);
    }

    private static (bool, int) CompareInsideLoop(string s1, string s2, int i1, int i2, bool digit1, bool digit2)
    {
        if (!digit1 && !digit2)
        {
            (bool decided, int result) = CompareNonDigits(s1, s2, i1, i2);
            if (decided)
            {
                return (true, result);
            }
        }

        if (digit1)
        {
            return (true, -1);
        }

        if (digit2)
        {
            return (true, 1);
        }

        return (false, 0);
    }

    private static (bool, int) CompareLengths(int length1, int length2, int i1, int i2)
    {
        if (i1 >= length1 && i2 >= length2)
        {
            return (true, 0);
        }

        if (i1 >= length1 || i2 >= length2)
        {
            return (true, -1);
        }

        return (false, 0);
    }

    private static int CompareNumbers(string s1, ref int i1, string s2, ref int i2)
    {
        int nonZeroStart1 = i1;
        int nonZeroStart2 = i2;
        int end1 = i1;
        int end2 = i2;
        ScanNumberEnd(s1, i1, ref end1, ref nonZeroStart1);
        ScanNumberEnd(s2, i2, ref end2, ref nonZeroStart2);
        int start1 = i1;
        i1 = end1 - 1;
        int start2 = i2;
        i2 = end2 - 1;
        int nonZeroLength1 = end1 - nonZeroStart1;
        int nonZeroLength2 = end2 - nonZeroStart2;
        if (nonZeroLength1 < nonZeroLength2)
        {
            return -1;
        }

        if (nonZeroLength1 > nonZeroLength2)
        {
            return 1;
        }

        int j1 = nonZeroStart1;
        int j2 = nonZeroStart2;
        while (j1 <= i1)
        {
            int r = s1[j1].CompareTo(s2[j2]);
            if (r != 0)
            {
                return r;
            }

            j1++;
            j2++;
        }

        // Same number: the one written with more leading zeros goes first.
        int length1 = end1 - start1;
        int length2 = end2 - start2;
        if (length1 == length2)
        {
            return 0;
        }

        return length1 > length2 ? -1 : 1;
    }

    private static void ScanNumberEnd(string s, int start, ref int end, ref int nonZeroStart)
    {
        nonZeroStart = start;
        end = start;
        bool countZeros = true;
        while (char.IsDigit(s, end))
        {
            if (countZeros && s[end].Equals('0'))
            {
                nonZeroStart++;
            }
            else
            {
                countZeros = false;
            }

            end++;
            if (end >= s.Length)
            {
                break;
            }
        }
    }
}

/// <summary>Non-generic adapter over <see cref="StringLogicalComparer"/>. Original: <c>NumericComparer</c>.</summary>
public sealed class NumericComparer : IComparer, IComparer<string>
{
    public int Compare(object? x, object? y) =>
        x is string a && y is string b ? StringLogicalComparer.Compare(a, b) : -1;

    public int Compare(string? x, string? y) =>
        x != null && y != null ? StringLogicalComparer.Compare(x, y) : -1;
}
