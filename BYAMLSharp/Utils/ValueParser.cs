namespace BYAMLSharp.Utils;

internal static class ValueParser
{
    public static unsafe T ReadValue<T>(ref byte* ptr, bool reverse)
        where T : unmanaged
    {
        int size = sizeof(T);

        if (size == 1)
            return *(T*)ptr++;

        if (size <= 0)
            return default;

        Span<byte> result = stackalloc byte[size];

        for (int i = 0; i < size; i++, ptr++)
            result[i] = *ptr;

        if (reverse)
            result.Reverse();

        fixed (byte* resultptr = result)
            return *(T*)resultptr;
    }

    public static unsafe T[] ReadValues<T>(ref byte* ptr, bool reverse, uint amount)
        where T : unmanaged
    {
        T[] array = new T[amount];

        for (uint i = 0; i < amount; i++)
            array[i] = ReadValue<T>(ref ptr, reverse);

        return array;
    }

    public static unsafe void WriteValue<T>(ref byte* ptr, T value, bool reverse)
        where T : unmanaged
    {
        int size = sizeof(T);

        if (size <= 0)
            return;

        Span<byte> bytes = new(&value, size);

        if (size == 1)
        {
            *ptr++ = bytes[0];
            return;
        }

        if (reverse)
            bytes.Reverse();

        fixed (byte* valueptr = bytes)
            for (int i = 0; i < size; i++, ptr++)
                *ptr = *valueptr;
    }

    public static unsafe void WriteValues<T>(ref byte* ptr, T[] values, bool reverse)
        where T : unmanaged
    {
        for (uint i = 0; i < values.Length; i++)
            WriteValue(ref ptr, values[i], reverse);
    }
}
