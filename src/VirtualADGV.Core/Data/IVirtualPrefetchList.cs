namespace VirtualADGV.Data;

/// <summary>Kaydırmadan önce hedef satırları isteğe bağlı olarak önbelleğe alan liste.</summary>
public interface IVirtualPrefetchList
{
    /// <summary>Sıfır tabanlı <paramref name="start"/> konumundan başlayarak <paramref name="count"/> satırı hazırlar.</summary>
    Task PrefetchAsync(int start, int count, CancellationToken cancellationToken);
}
