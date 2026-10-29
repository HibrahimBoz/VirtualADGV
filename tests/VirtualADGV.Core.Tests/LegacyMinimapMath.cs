// Frozen arithmetic from MinimapAdorner.cs before the 2.0 extraction.
// Source Git blob: 68afca6d2e70717777e548bd595cf8b9c699129e.
// Only the WPF field access is replaced with parameters. No Core helpers are used.
internal static class LegacyMinimapMath
{
    internal static long[] Bucketize(IReadOnlyList<long> ordinals, int total, int height)
    {
        var buckets = new long[height];
        Array.Fill(buckets, -1L);
        if (total <= 0 || height == 0) return buckets;
        for (int i = 0; i < ordinals.Count; i++)
        {
            long ordinal = ordinals[i];
            if (ordinal < 0 || ordinal >= total) continue;
            int y = Math.Min(height - 1, (int)(ordinal * (double)height / total));
            if (buckets[y] < 0 || ordinal < buckets[y]) buckets[y] = ordinal;
        }
        return buckets;
    }

    internal static double RatioAt(IReadOnlyList<long> marks, int total, double top, double height, double y, bool snap)
    {
        if (height <= 0) return 0;
        int bucketCount = Math.Max(1, (int)Math.Ceiling(height));
        var markBuckets = Bucketize(marks, total, bucketCount);
        double ratio = Math.Clamp((y - top) / height, 0, 1);
        if (snap)
        {
            int bucket = Math.Clamp((int)(ratio * bucketCount), 0, Math.Max(0, bucketCount - 1));
            for (int distance = 0; distance <= 2; distance++)
                foreach (int index in distance == 0 ? new[] { bucket } : new[] { bucket - distance, bucket + distance })
                    if (index >= 0 && index < markBuckets.Length && markBuckets[index] >= 0)
                        return markBuckets[index] / (double)Math.Max(1, total);
        }
        return ratio;
    }
}
