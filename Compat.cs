namespace VirtualADGV.WPF;

/// <summary>
/// .NET Framework 4.6.2 hedefinde olmayan küçük API'lerin karşılıkları. net8.0'da aynı sonucu verirler;
/// paket ek bağımlılık (System.ValueTuple, System.Memory) çekmesin diye burada durur.
/// </summary>
internal static class Compat
{
    public static int Clamp(int value, int min, int max) => value < min ? min : value > max ? max : value;
    public static double Clamp(double value, double min, double max) => value < min ? min : value > max ? max : value;
    public static long Clamp(long value, long min, long max) => value < min ? min : value > max ? max : value;
    public static bool IsFinite(double value) => !double.IsNaN(value) && !double.IsInfinity(value);

    public static void Fill<T>(T[] array, T value)
    {
        for (int i = 0; i < array.Length; i++) array[i] = value;
    }

#if NETFRAMEWORK
    public static bool Contains(this string s, string value, StringComparison comparison) => s.IndexOf(value, comparison) >= 0;

    public static TValue? GetValueOrDefault<TKey, TValue>(this Dictionary<TKey, TValue> d, TKey key) where TKey : notnull
        => d.TryGetValue(key, out var v) ? v : default;

    public static TValue GetValueOrDefault<TKey, TValue>(this Dictionary<TKey, TValue> d, TKey key, TValue fallback) where TKey : notnull
        => d.TryGetValue(key, out var v) ? v : fallback;

    public static bool TryAdd<TKey, TValue>(this Dictionary<TKey, TValue> d, TKey key, TValue value) where TKey : notnull
    {
        if (d.ContainsKey(key)) return false;
        d.Add(key, value);
        return true;
    }
#endif
}
