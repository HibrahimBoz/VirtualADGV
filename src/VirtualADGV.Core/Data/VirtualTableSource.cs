namespace VirtualADGV.Data;

// Bu dosyadaki tipler WPF tipi taşımaz: kaynak yerel bir veritabanı da olabilir, uzak bir sunucu da.

/// <summary>
/// Tabloya satır veren kaynak. Satırlar 0 tabanlı, 64 bit görsel sırayla (ordinal) adreslenir;
/// her satırın ayrıca kaynağa özgü, değişmeyen bir anahtarı (key) vardır.
/// </summary>
/// <remarks>
/// Uzak kaynak (sunucuda sıralı milyarlarca satır) <see cref="SeekAsync"/> ile bulduğu konumun anahtarını (çapa)
/// kendisi saklar; hemen ardından gelen <see cref="GetPageAsync"/> o çapaya yakın sıraları çapadan anahtarla
/// (keyset) okur, OFFSET kullanmaz. Süzgeç ve sıralama kaynağın kurulum parametresidir: değişince yeni kaynak kurulur.
/// Kaynak tutuyorsa (bağlantı, sıra tablosu) <see cref="IDisposable.Dispose"/> bırakır.
/// </remarks>
public interface IVirtualTableSource : IDisposable
{
    /// <summary>Görünen satır sayısı (süzgeç uygulanmış). <see cref="IsCountExact"/> false ise tahmindir.</summary>
    long Count { get; }

    /// <summary>Satır sayısı kesin mi? Uzak kaynak büyük sonuçta tahmini sayı verebilir.</summary>
    bool IsCountExact { get; }

    /// <summary><see cref="Count"/> değişti (ör. tahmini sayı kesinleşti). Tablo baştan yüklenir.</summary>
    event EventHandler? CountChanged;

    /// <summary>
    /// Kaynak her görsel sırayı doğrudan verebilir mi? false ise (ör. sunucuda sıralı milyarlarca satır)
    /// tablo uzak bir konuma <see cref="SeekAsync"/> ile atlar, sıralar o noktadan sonra yaklaşıktır.
    /// </summary>
    bool SupportsExactOrdinals { get; }

    /// <summary>Sütunlar, değerlerin sırasıyla.</summary>
    IReadOnlyList<VirtualColumnInfo> Columns { get; }

    /// <summary><paramref name="start"/> sırasından başlayarak en çok <paramref name="count"/> satır okur.</summary>
    Task<VirtualRowPage> GetPageAsync(long start, int count, CancellationToken cancellationToken);

    /// <summary>Verilen satır anahtarlarının görsel sıraları, artan; görünümde olmayanlar atlanır.</summary>
    Task<long[]> GetOrdinalsAsync(IReadOnlyCollection<long> keys, int limit, CancellationToken cancellationToken);

    /// <summary>
    /// Aramayla eşleşen satırların görsel sıraları: <paramref name="start"/> ve sonrası artan; istek
    /// <see cref="VirtualSearchRequest.Backward"/> ise <paramref name="start"/> ve öncesi azalan.
    /// </summary>
    Task<long[]> FindAsync(VirtualSearchRequest request, long start, int limit, CancellationToken cancellationToken);

    /// <summary>
    /// 0–1 arası konuma karşılık gelen görsel sıra. Kesin sıralı kaynakta <c>fraction × (Count − 1)</c>;
    /// uzak kaynak bunu örneklemeyle (yüzdelik) yaklaşık hesaplayabilir.
    /// </summary>
    Task<long> SeekAsync(double fraction, CancellationToken cancellationToken);
}

/// <summary>Bir satırdan bağlı alt kaynağa inmeyi destekleyen kaynak (ör. bir rapor satırının kendi ayrıntı tablosu).</summary>
public interface IVirtualRelationSource
{
    /// <summary>Kaynağın sunduğu bağlantı adları.</summary>
    IReadOnlyList<string> Relations { get; }

    /// <summary>Satır anahtarının <paramref name="relation"/> bağlantısındaki alt kaynağı; yoksa null.</summary>
    Task<IVirtualTableSource?> GetChildSourceAsync(long rowKey, string relation, CancellationToken cancellationToken);
}

/// <summary>Bir sütunun adı ve .NET tipi.</summary>
public sealed class VirtualColumnInfo
{
    /// <summary>Yeni sütun bilgisi.</summary>
    public VirtualColumnInfo(string name, Type type)
    {
        Name = name ?? throw new ArgumentNullException(nameof(name));
        Type = type ?? throw new ArgumentNullException(nameof(type));
    }
    /// <summary>Sütun adı.</summary>
    public string Name { get; }
    /// <summary>Değerlerin .NET tipi.</summary>
    public Type Type { get; }
}

/// <summary>Bir sayfa satır. <see cref="Values"/>[i] satırın değerleridir (sütun sırasıyla); okunamayan satır null.</summary>
public sealed class VirtualRowPage
{
    /// <summary>Yeni sayfa. Diziler eşit uzunlukta olmalıdır.</summary>
    public VirtualRowPage(long start, long[] keys, object?[]?[] values)
    {
        if (keys == null) throw new ArgumentNullException(nameof(keys));
        if (values == null) throw new ArgumentNullException(nameof(values));
        if (keys.Length != values.Length) throw new ArgumentException("Anahtar ve değer sayısı eşit olmalı.", nameof(values));
        Start = start;
        Keys = keys;
        Values = values;
    }
    /// <summary>İlk satırın görsel sırası.</summary>
    public long Start { get; }
    /// <summary>Satır anahtarları.</summary>
    public long[] Keys { get; }
    /// <summary>Satır değerleri.</summary>
    public object?[]?[] Values { get; }
    /// <summary>Sayfadaki satır sayısı.</summary>
    public int Count => Keys.Length;
}

/// <summary>Metin araması: hücre metninde "içerir".</summary>
public sealed class VirtualSearchRequest
{
    /// <summary>Yeni arama isteği.</summary>
    public VirtualSearchRequest(string text, string? column, bool caseSensitive, bool backward = false)
    {
        Text = text ?? "";
        Column = column;
        CaseSensitive = caseSensitive;
        Backward = backward;
    }
    /// <summary>Geriye doğru ara (önceki eşleşme): sonuçlar azalan sırada.</summary>
    public bool Backward { get; }
    /// <summary>Aranan metin.</summary>
    public string Text { get; }
    /// <summary>Yalnız bu sütunda ara; null ise bütün sütunlar.</summary>
    public string? Column { get; }
    /// <summary>Büyük/küçük harf duyarlı mı?</summary>
    public bool CaseSensitive { get; }
}
