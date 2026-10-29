namespace VirtualADGV;

// Small equivalents available on both .NET Framework 4.6.2 and .NET 8.
internal static class Compat
{
    internal static int Clamp(int value, int min, int max) => value < min ? min : value > max ? max : value;
    internal static double Clamp(double value, double min, double max) => value < min ? min : value > max ? max : value;
    internal static void Fill<T>(T[] array, T value)
    {
        for (int i = 0; i < array.Length; i++) array[i] = value;
    }
}
