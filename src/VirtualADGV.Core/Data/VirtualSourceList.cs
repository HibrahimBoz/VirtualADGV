using System.Collections;
using System.Collections.Specialized;
using System.ComponentModel;

namespace VirtualADGV.Data;

/// <summary>Tablo satırının kaynaktan bağımsız görünümü: görsel sıra, anahtar ve ada göre değer.</summary>
public interface IVirtualRow
{
    /// <summary>0 tabanlı, 64 bit görsel sıra (kaynağın tamamında; pencere içinde değil).</summary>
    long Ordinal { get; }
    /// <summary>Kaynağa özgü satır anahtarı; satır henüz yüklenmediyse null.</summary>
    long? Key { get; }
    /// <summary>Satır kaynaktan geldi mi? false ise yer tutucudur.</summary>
    bool IsLoaded { get; }
    /// <summary>Sütun adına göre değer (büyük/küçük harf duyarsız); yoksa null.</summary>
    object? this[string columnName] { get; }
}

/// <summary><see cref="VirtualSourceList"/> ayarları.</summary>
public sealed class VirtualSourceListOptions
{
    /// <summary>Bir sorguda okunan satır sayısı.</summary>
    public int PageSize { get; set; } = 2_000;
    /// <summary>Bellekte tutulan en çok sayfa.</summary>
    public int MaxCachedPages { get; set; } = 64;
    /// <summary>
    /// Sayfa okunurken arayüzün en çok bu kadar beklemesi. Süre içinde gelen sayfa doğrudan gösterilir
    /// (yerel kaynakta görünüm değişmez); gelmezse yer tutucu satır döner ve sayfa gelince satır yenilenir.
    /// </summary>
    public TimeSpan SyncWait { get; set; } = TimeSpan.FromMilliseconds(250);
    /// <summary>Bu sayıyı aşan kaynak pencereli kipe girer. Varsayılan: WPF'in satır sınırı (int.MaxValue).</summary>
    public long WindowThreshold { get; set; } = int.MaxValue;
    /// <summary>Pencereli kipte DataGrid'e aynı anda verilen satır sayısı.</summary>
    public int WindowSize { get; set; } = 1_000_000;
}

/// <summary>
/// <see cref="IVirtualTableSource"/>'u WPF DataGrid'in beklediği <see cref="IList"/>'e çevirir.
/// Kaynak <see cref="VirtualSourceListOptions.WindowThreshold"/> satırı aşarsa DataGrid'e kaynağın yalnız bir
/// penceresi verilir (DataGrid satırları int ile sayar); pencereyi <see cref="MoveWindow"/> kaydırır.
/// </summary>
public sealed class VirtualSourceList : IList, ITypedList, INotifyCollectionChanged, IVirtualPrefetchList, IDisposable
{
    private readonly IVirtualTableSource _source;
    private readonly VirtualSourceListOptions _options;
    private readonly PropertyDescriptorCollection _properties;
    private readonly string[] _names;
    private readonly Dictionary<string, int> _columnIndex = new(StringComparer.OrdinalIgnoreCase);
    private readonly object _lock = new();
    private readonly Dictionary<long, VirtualRowPage> _pages = new();
    private readonly LinkedList<long> _lru = new();
    private readonly Dictionary<long, Task<VirtualRowPage>> _inFlight = new();
    private readonly Dictionary<long, List<WeakReference<VirtualSourceRow>>> _waiting = new();
    // Bekleme süresini bir kez aşan sayfa: aynı sayfanın öteki satırları yeniden beklemez (40 satır × 250 ms donma olmaz).
    private readonly HashSet<long> _timedOut = new();
    private int _version;
    private readonly SynchronizationContext? _context;
    private readonly CancellationTokenSource _disposeCts = new();
    private long _windowStart;
    private bool _disposed;

    /// <summary>Kaynağı listeye bağlar. Arayüz iş parçacığında kurun: yenilemeler oraya gönderilir.</summary>
    public VirtualSourceList(IVirtualTableSource source, VirtualSourceListOptions? options = null)
    {
        _source = source ?? throw new ArgumentNullException(nameof(source));
        _options = options ?? new VirtualSourceListOptions();
        if (_options.PageSize <= 0 || _options.MaxCachedPages <= 0 || _options.WindowSize <= 0 || _options.WindowThreshold <= 0)
            throw new ArgumentOutOfRangeException(nameof(options));
        _context = SynchronizationContext.Current;
        var columns = source.Columns;
        _names = new string[columns.Count];
        var descriptors = new PropertyDescriptor[columns.Count];
        for (int i = 0; i < columns.Count; i++)
        {
            _names[i] = columns[i].Name;
            if (!_columnIndex.ContainsKey(columns[i].Name)) _columnIndex.Add(columns[i].Name, i);
            descriptors[i] = new VirtualSourceColumnDescriptor(columns[i].Name, columns[i].Type, i);
        }
        _properties = new PropertyDescriptorCollection(descriptors);
        _source.CountChanged += OnSourceCountChanged;
    }

    private void OnSourceCountChanged(object? sender, EventArgs e)
    {
        void Reset() { if (!_disposed) InvalidateCache(); }
        if (_context != null) _context.Post(_ => Reset(), null); else Reset();
    }

    /// <summary>Bağlı kaynak (liste kaynağı bırakmaz; sahibi bırakır).</summary>
    public IVirtualTableSource Source => _source;
    /// <summary>Kaynağın toplam satır sayısı (64 bit).</summary>
    public long TotalCount => _source.Count;
    /// <summary>Kaynak pencereli kipte mi?</summary>
    public bool IsWindowed => _source.Count > _options.WindowThreshold;
    /// <summary>Pencerenin ilk satırının görsel sırası; pencere yoksa 0.</summary>
    public long WindowStart => IsWindowed ? _windowStart : 0;
    /// <summary>Pencere boyutu.</summary>
    public int WindowSize => _options.WindowSize;
    /// <summary>Pencere kaydı; <see cref="INotifyCollectionChanged.CollectionChanged"/> Reset'inden önce tetiklenir.</summary>
    public event EventHandler? WindowMoving;
    /// <inheritdoc/>
    public event NotifyCollectionChangedEventHandler? CollectionChanged;

    /// <summary>A page arrived. Posted to the captured synchronization context, if any, after filling placeholders.</summary>
    public event EventHandler<VirtualPageLoadedEventArgs>? PageLoaded;

    /// <summary>DataGrid'in gördüğü satır sayısı (pencereli kipte pencere uzunluğu).</summary>
    public int Count => IsWindowed
        ? (int)Math.Min(_options.WindowSize, Math.Max(0, _source.Count - _windowStart))
        : (int)Math.Min(_source.Count, int.MaxValue);

    /// <summary>Pencereyi <paramref name="start"/> sırasından başlatır (sınırlara kırpılır). Değiştiyse true.</summary>
    public bool MoveWindow(long start)
    {
        if (!IsWindowed) return false;
        long max = Math.Max(0, _source.Count - _options.WindowSize);
        long clamped = start < 0 ? 0 : start > max ? max : start;
        if (clamped == _windowStart) return false;
        WindowMoving?.Invoke(this, EventArgs.Empty);
        _windowStart = clamped;
        CollectionChanged?.Invoke(this, new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));
        return true;
    }

    /// <summary>Görsel sıranın penceredeki indeksi; pencere dışındaysa −1.</summary>
    public int WindowIndexOf(long ordinal)
    {
        long index = ordinal - WindowStart;
        return index >= 0 && index < Count ? (int)index : -1;
    }

    /// <inheritdoc/>
    public object? this[int index]
    {
        get
        {
            if ((uint)index >= (uint)Count) throw new ArgumentOutOfRangeException(nameof(index));
            return GetRow(WindowStart + index);
        }
        set => throw new NotSupportedException();
    }

    /// <summary>Görsel sıradaki satır (kaynağın tamamında). Okunamazsa yer tutucu döner, istisna fırlatmaz.</summary>
    public IVirtualRow GetRow(long ordinal)
    {
        long page = ordinal / _options.PageSize;
        int offset = (int)(ordinal % _options.PageSize);
        VirtualRowPage? loaded = TryGetPage(page, wait: true);
        Prefetch(page + 1);
        Prefetch(page - 1);
        if (loaded != null) return RowFrom(loaded, offset, ordinal);
        var placeholder = new VirtualSourceRow(this, ordinal);
        lock (_lock)
        {
            // Sayfa beklerken gelmiş olabilir; gelmediyse satır sayfa gelince doldurulur.
            if (_pages.TryGetValue(page, out var late)) return RowFrom(late, offset, ordinal);
            if (!_waiting.TryGetValue(page, out var list)) _waiting[page] = list = new List<WeakReference<VirtualSourceRow>>();
            list.Add(new WeakReference<VirtualSourceRow>(placeholder));
        }
        return placeholder;
    }

    private IVirtualRow RowFrom(VirtualRowPage page, int offset, long ordinal)
    {
        var row = new VirtualSourceRow(this, ordinal);
        if (offset < page.Count) row.Fill(page.Keys[offset], page.Values[offset]);
        else row.Fill(null, null);
        return row;
    }

    private VirtualRowPage? TryGetPage(long page, bool wait)
    {
        Task<VirtualRowPage> task;
        lock (_lock)
        {
            if (_disposed) return null;
            if (_pages.TryGetValue(page, out var cached))
            {
                _lru.Remove(page);
                _lru.AddFirst(page);
                return cached;
            }
            task = PageTask(page);
            if (_timedOut.Contains(page)) wait = false;
        }
        if (!wait) return null;
        try
        {
            // Hızlı kaynak (yerel veritabanı) bu sürede biter: bugünkü gibi beklenir, yer tutucu görünmez.
            if (task.Wait(_options.SyncWait)) return task.Result;
        }
        catch (AggregateException) { return null; }
        lock (_lock) _timedOut.Add(page);
        return null;
    }

    private void Prefetch(long page)
    {
        if (page < 0 || page * _options.PageSize >= _source.Count) return;
        lock (_lock)
        {
            if (_disposed || _pages.ContainsKey(page)) return;
            PageTask(page);
        }
    }

    // Çağıran _lock'u tutar.
    private Task<VirtualRowPage> PageTask(long page)
    {
        if (_inFlight.TryGetValue(page, out var running)) return running;
        long start = page * _options.PageSize;
        int count = (int)Math.Min(_options.PageSize, Math.Max(0, _source.Count - start));
        var token = _disposeCts.Token;
        int version = _version;
        var fetch = Task.Run(() => _source.GetPageAsync(start, count, token), token);
        // Completion includes cache publication. Awaiting prefetch must guarantee that the
        // next paint reads the cache, even when the UI notification is still queued.
        var task = fetch.ContinueWith(t =>
        {
            Completed(page, t, version);
            return t.GetAwaiter().GetResult();
        }, CancellationToken.None, TaskContinuationOptions.None, TaskScheduler.Default);
        _inFlight[page] = task;
        return task;
    }

    /// <summary>
    /// Loads an absolute, 64-bit ordinal range before navigation, without creating rows or
    /// moving the window. Cancellation stops this waiter; shared page requests remain usable.
    /// </summary>
    public async Task PrefetchAsync(long start, int count, CancellationToken cancellationToken)
    {
        if (start < 0) throw new ArgumentOutOfRangeException(nameof(start));
        if (count < 0) throw new ArgumentOutOfRangeException(nameof(count));
        cancellationToken.ThrowIfCancellationRequested();
        var tasks = new List<Task>();
        lock (_lock)
        {
            if (_disposed) throw new ObjectDisposedException(nameof(VirtualSourceList));
            if (count == 0 || start >= TotalCount) return;
            long last = start + Math.Min((long)count, TotalCount - start) - 1;
            for (long page = start / _options.PageSize; page <= last / _options.PageSize; page++)
                if (!_pages.ContainsKey(page)) tasks.Add(PageTask(page));
        }
        var completed = Task.WhenAll(tasks);
        var cancelled = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        using (cancellationToken.Register(() => cancelled.TrySetCanceled()))
        {
            var winner = await Task.WhenAny(completed, cancelled.Task).ConfigureAwait(false);
            await winner.ConfigureAwait(false);
        }
        cancellationToken.ThrowIfCancellationRequested();
    }

    Task IVirtualPrefetchList.PrefetchAsync(int start, int count, CancellationToken cancellationToken)
        => PrefetchAsync(WindowStart + start, count, cancellationToken);

    private void Completed(long page, Task<VirtualRowPage> task, int version)
    {
        List<WeakReference<VirtualSourceRow>>? waiting = null;
        VirtualRowPage? result = task.Status == TaskStatus.RanToCompletion ? task.Result : null;
        lock (_lock)
        {
            // InvalidateCache sonrası biten eski iş yeni önbelleğe yazmaz.
            if (version != _version) return;
            _inFlight.Remove(page);
            _timedOut.Remove(page);
            if (_disposed) return;
            if (result != null)
            {
                _pages[page] = result;
                _lru.Remove(page);
                _lru.AddFirst(page);
                while (_pages.Count > _options.MaxCachedPages)
                {
                    long evict = _lru.Last!.Value;
                    _lru.RemoveLast();
                    _pages.Remove(evict);
                }
            }
            // Hata ya da iptal: bekleyen yer tutucular bırakılır (boş kalır); sonraki erişim sayfayı yeniden ister.
            if (_waiting.TryGetValue(page, out waiting)) _waiting.Remove(page);
        }
        if (result == null) return;
        void Fill()
        {
            if (waiting != null)
            foreach (var weak in waiting)
            {
                if (!weak.TryGetTarget(out var row)) continue;
                int offset = (int)(row.Ordinal - page * _options.PageSize);
                if (offset >= 0 && offset < result.Count) row.Fill(result.Keys[offset], result.Values[offset]);
                else row.Fill(null, null);
            }
            // Gönderim kuyruktayken liste bırakıldıysa ya da önbellek boşaltıldıysa olay bayat sayfayı bildirmez.
            if (_disposed || version != _version) return;
            PageLoaded?.Invoke(this, new VirtualPageLoadedEventArgs(result.Start, result.Count));
        }
        if (_context != null) _context.Post(_ => Fill(), null); else Fill();
    }

    internal int ColumnIndex(string name) => _columnIndex.TryGetValue(name, out int i) ? i : -1;
    internal string[] Names => _names;
    internal PropertyDescriptorCollection Properties => _properties;

    /// <summary>Önbelleği boşaltır (kaynak verisi değiştiyse).</summary>
    public void InvalidateCache()
    {
        lock (_lock)
        {
            _version++;
            _pages.Clear();
            _lru.Clear();
            _inFlight.Clear();
            _waiting.Clear();
            _timedOut.Clear();
        }
        CollectionChanged?.Invoke(this, new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));
    }

    /// <inheritdoc/>
    public PropertyDescriptorCollection GetItemProperties(PropertyDescriptor[]? listAccessors) => _properties;
    /// <inheritdoc/>
    public string GetListName(PropertyDescriptor[]? listAccessors) => nameof(VirtualSourceList);
    /// <inheritdoc/>
    public bool IsReadOnly => true;
    /// <inheritdoc/>
    public bool IsFixedSize => true;
    /// <inheritdoc/>
    public object SyncRoot => _lock;
    /// <inheritdoc/>
    public bool IsSynchronized => false;
    /// <inheritdoc/>
    public int Add(object? value) => throw new NotSupportedException();
    /// <inheritdoc/>
    public void Clear() => throw new NotSupportedException();
    /// <inheritdoc/>
    public bool Contains(object? value) => IndexOf(value) >= 0;
    /// <inheritdoc/>
    public int IndexOf(object? value) => value is VirtualSourceRow row && ReferenceEquals(row.Owner, this) ? WindowIndexOf(row.Ordinal) : -1;
    /// <inheritdoc/>
    public void Insert(int index, object? value) => throw new NotSupportedException();
    /// <inheritdoc/>
    public void Remove(object? value) => throw new NotSupportedException();
    /// <inheritdoc/>
    public void RemoveAt(int index) => throw new NotSupportedException();
    /// <inheritdoc/>
    public void CopyTo(Array array, int index)
    {
        for (int i = 0; i < Count; i++) array.SetValue(this[i], index + i);
    }
    /// <inheritdoc/>
    public IEnumerator GetEnumerator()
    {
        for (int i = 0; i < Count; i++) yield return this[i];
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        lock (_lock)
        {
            if (_disposed) return;
            _disposed = true;
            _pages.Clear();
            _lru.Clear();
            _waiting.Clear();
            _timedOut.Clear();
        }
        _source.CountChanged -= OnSourceCountChanged;
        _disposeCts.Cancel();
    }
}

/// <summary><see cref="VirtualSourceList"/> satırı. Eşitlik görsel sıradır: pencere kayınca aynı satır aynı kalır.</summary>
public sealed class VirtualSourceRow : IVirtualRow, ICustomTypeDescriptor, INotifyPropertyChanged
{
    private static readonly object?[] Empty = new object?[0];
    private object?[] _values = Empty;
    private long? _key;
    private bool _loaded;

    internal VirtualSourceRow(VirtualSourceList owner, long ordinal)
    {
        Owner = owner;
        Ordinal = ordinal;
    }

    internal VirtualSourceList Owner { get; }
    /// <inheritdoc/>
    public long Ordinal { get; }
    /// <inheritdoc/>
    public long? Key => _key;
    /// <inheritdoc/>
    public bool IsLoaded => _loaded;
    /// <inheritdoc/>
    public event PropertyChangedEventHandler? PropertyChanged;

    internal void Fill(long? key, object?[]? values)
    {
        _key = key;
        _values = values ?? Empty;
        _loaded = true;
        // Boş ad: bütün bağlamalar yeniden okur (yer tutucudan gerçek satıra geçiş).
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(string.Empty));
    }

    /// <inheritdoc/>
    public object? this[string columnName] => GetValue(Owner.ColumnIndex(columnName));

    internal object? GetValue(int index)
    {
        var values = _values;
        if (index < 0 || index >= values.Length) return null;
        object? v = values[index];
        return v is DBNull ? null : v;
    }

    /// <inheritdoc/>
    public override bool Equals(object? obj) => obj is VirtualSourceRow other && ReferenceEquals(other.Owner, Owner) && other.Ordinal == Ordinal;
    /// <inheritdoc/>
    public override int GetHashCode() => Ordinal.GetHashCode();

    /// <inheritdoc/>
    public PropertyDescriptorCollection GetProperties() => Owner.Properties;
    /// <inheritdoc/>
    public PropertyDescriptorCollection GetProperties(Attribute[]? attributes) => Owner.Properties;
    /// <inheritdoc/>
    public AttributeCollection GetAttributes() => AttributeCollection.Empty;
    /// <inheritdoc/>
    public string? GetClassName() => nameof(VirtualSourceRow);
    /// <inheritdoc/>
    public string? GetComponentName() => null;
    /// <inheritdoc/>
    public TypeConverter GetConverter() => TypeDescriptor.GetConverter(typeof(object));
    /// <inheritdoc/>
    public EventDescriptor? GetDefaultEvent() => null;
    /// <inheritdoc/>
    public PropertyDescriptor? GetDefaultProperty() => null;
    /// <inheritdoc/>
    public object? GetEditor(Type editorBaseType) => null;
    /// <inheritdoc/>
    public EventDescriptorCollection GetEvents() => EventDescriptorCollection.Empty;
    /// <inheritdoc/>
    public EventDescriptorCollection GetEvents(Attribute[]? attributes) => EventDescriptorCollection.Empty;
    /// <inheritdoc/>
    public object? GetPropertyOwner(PropertyDescriptor? pd) => this;
}

internal sealed class VirtualSourceColumnDescriptor : PropertyDescriptor
{
    private readonly Type _type;
    private readonly int _index;

    internal VirtualSourceColumnDescriptor(string name, Type type, int index) : base(name, null)
    {
        _type = type;
        _index = index;
    }

    public override Type ComponentType => typeof(VirtualSourceRow);
    public override bool IsReadOnly => true;
    public override Type PropertyType => _type;
    public override bool CanResetValue(object component) => false;
    public override object? GetValue(object? component) => (component as VirtualSourceRow)?.GetValue(_index);
    public override void ResetValue(object component) { }
    public override void SetValue(object? component, object? value) { }
    public override bool ShouldSerializeValue(object component) => false;
    // Satır PropertyChanged'i boş adla gönderir; WPF bağlaması INotifyPropertyChanged'i dinler.
    public override bool SupportsChangeEvents => false;
}
