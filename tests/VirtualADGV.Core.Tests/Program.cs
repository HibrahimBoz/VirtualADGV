using System.Collections.Concurrent;
using System.ComponentModel;
using System.Globalization;
using System.Reflection;
using VirtualADGV.Data;
using VirtualADGV.Grid;

internal static class Program
{
    private static int _pass, _fail;
    private static void Check(string name, bool condition)
    {
        if (condition) { _pass++; Console.WriteLine("  ok   " + name); }
        else { _fail++; Console.WriteLine("  FAIL " + name); }
    }
    private static bool Near(double a, double b) => Math.Abs(a - b) < 1e-12;
    private static int Main()
    {
        try
        {
            BucketsAndDrag();
            Search();
            Selection();
            Pages();
            ForwardingAndMetadata();
        }
        catch (Exception ex) { Check("unexpected exception: " + ex, false); }
        Console.WriteLine($"VirtualADGV.Core.Tests: {_pass} gecti, {_fail} basarisiz");
        return _fail == 0 ? 0 : 1;
    }

    private static void BucketsAndDrag()
    {
        Check("bucket total 0", MinimapModel.Bucketize(new long[] { 0 }, 0, 3).SequenceEqual(new long[] { -1, -1, -1 }));
        Check("bucket height 0", MinimapModel.Bucketize(new long[] { 0 }, 10, 0).Length == 0);
        Check("bucket boundaries and smallest ordinal", MinimapModel.Bucketize(new long[] { -1, 100, 99, 1, 0, 98 }, 100, 10)
            .SequenceEqual(new long[] { 0, -1, -1, -1, -1, -1, -1, -1, -1, 98 }));
        long huge = (long)int.MaxValue + 100;
        Check("bucket total above int.MaxValue", MinimapModel.Bucketize(new[] { 0L, huge / 2, huge - 1 }, huge, 3)
            .SequenceEqual(new[] { 0L, huge / 2, huge - 1 }));
        Check("bucket long.MaxValue last bucket", MinimapModel.Bucketize(new[] { long.MaxValue - 1 }, long.MaxValue, 7)[6] == long.MaxValue - 1);

        var model = new MinimapModel { Marks = new long[] { 499, 480, 520 } };
        model.SetGeometry(1000, 4, 100);
        Check("snap exact bucket keeps smallest ordinal", Near(model.RatioAt(52.1, true), .480));
        model.Marks = new long[] { 480, 520 };
        Check("snap two buckets and upper tie wins", Near(model.RatioAt(54, true), .480));
        Check("snap outside two buckets keeps pointer", Near(model.RatioAt(59, true), .55));
        Check("snap disabled", Near(model.RatioAt(54, false), .50));
        Check("pointer clamps", model.RatioAt(-10, false) == 0 && model.RatioAt(500, false) == 1);

        var random = new Random(462);
        int comparisons = 0;
        bool same = true;
        foreach (int total in new[] { 0, 1, 7, 1000, 1_000_000, int.MaxValue })
        foreach (double height in new[] { 0d, 1d, 7.25, 180d, 811.5 })
        {
            var marks = Enumerable.Range(0, 80).Select(_ => total == 0 ? -1L : (long)(random.NextDouble() * total)).ToArray();
            model.Marks = marks;
            model.SetGeometry(total, 4, height);
            for (int i = -3; i <= 103; i++)
            foreach (bool snap in new[] { false, true })
            {
                double y = 4 + height * i / 100;
                double oldRatio = LegacyMinimapMath.RatioAt(marks, total, 4, height, y, snap);
                same &= oldRatio == model.RatioAt(y, snap);
                comparisons++;
            }
        }
        Check($"legacy MinimapAdorner.RatioAt parity ({comparisons})", same);
        model.Marks = new long[] { 480, 520 };
        model.Selection = new long[] { 123 };
        model.SearchHits = new long[] { 999 };
        model.SetGeometry(1000, 4, 100);
        Check("three bucket layers", model.SelectionBuckets[12] == 123 && model.SearchBuckets[99] == 999);
        model.BeginDrag(54);
        Check("begin drag snaps and requests timer", model.IsDragging && model.NeedsDeferredScroll && Near(model.TargetRatio, .480));
        model.MoveDrag(54);
        Check("move drag does not snap", Near(model.TargetRatio, .50) && model.NeedsDeferredScroll);
        Check("row hint external format", model.FormatRowHint("Satir {0}", CultureInfo.InvariantCulture) == "Satir 501");
        Check("deferred scroll consumed once", model.ConsumeDeferredScroll() && !model.ConsumeDeferredScroll() && model.IsDragging);
        model.MoveDrag(104);
        Check("row hint last row", model.FormatRowHint("{0}", CultureInfo.InvariantCulture) == "1000");
        Check("end drag final scroll once", model.EndDrag() && !model.EndDrag() && !model.NeedsDeferredScroll);
        model.BeginDrag(4);
        model.CancelDrag();
        model.MoveDrag(104);
        Check("cancel drag and ignore idle move", !model.IsDragging && !model.NeedsDeferredScroll && !model.EndDrag() && model.TargetRatio == 0);
        model.SetGeometry(long.MaxValue, 0, 100);
        model.BeginDrag(100);
        Check("64 bit last row hint", model.FormatRowHint("{0}", CultureInfo.InvariantCulture) == long.MaxValue.ToString(CultureInfo.InvariantCulture));
        model.CancelDrag();
        model.SetGeometry(0, 0, 0);
        model.BeginDrag(1);
        Check("empty map has no deferred scroll", !model.NeedsDeferredScroll && model.RatioAt(1, true) == 0);
        var view = MinimapModel.CalculateViewport(900, 1000, 100);
        Check("viewport last frame", Near(view.TopRatio, .9) && Near(view.HeightRatio, .1));
        view = MinimapModel.CalculateViewport(1000, 1000, 1, .06);
        Check("viewport minimum height and clamp", Near(view.TopRatio, .94) && Near(view.HeightRatio, .06));
        view = MinimapModel.CalculateViewport(-5, 100, 200);
        Check("viewport covers short extent", view.TopRatio == 0 && view.HeightRatio == 1);
        Check("viewport empty extent", MinimapModel.CalculateViewport(1, 0, 1).HeightRatio == 0);
        Check("target offset centered and bounded", MinimapModel.TargetOffset(.5, 1000, 100) == 450 &&
            MinimapModel.TargetOffset(0, 1000, 100) == 0 && MinimapModel.TargetOffset(1, 1000, 100) == 900);
    }

    private static RowSearch MakeSearch(object?[] rows, string? format = null, bool caseSensitive = false)
        => new(rows.Length, i => rows[i], new[] { new RowSearchColumn(row => row, format) }, CultureInfo.GetCultureInfo("tr-TR"), caseSensitive);

    private static void Search()
    {
        Check("search memory limit", RowSearch.InMemoryRowLimit == 200_000);
        Check("search localized display format", MakeSearch(new object?[] { 12.345m }, "0.00").FindNext("12,35", 0) == 0);
        Check("search invariant raw value", MakeSearch(new object?[] { 12.345m }, "0.00").FindNext("12.345", 0) == 0);
        Check("search composite format", MakeSearch(new object?[] { 12.345m }, "Value {0:0.0}").FindNext("Value 12,3", 0) == 0);
        Check("search bad format fallback", MakeSearch(new object?[] { 12.345m }, "{broken").FindNext("12,345", 0) == 0);
        Check("search DBNull and null are empty", MakeSearch(new object?[] { DBNull.Value, null }, "Value {0}").FindAll("Value").Count == 0);
        Check("search IFormattable raw", MakeSearch(new object?[] { new FormattableValue() }, "display").FindNext("invariant raw", 0) == 0);
        Check("search IFormattable display", MakeSearch(new object?[] { new FormattableValue() }, "display").FindNext("formatted", 0) == 0);
        Check("search case insensitive", MakeSearch(new object?[] { "Alpha", "ALPHA", "alpha" }).FindAll("alpha").Count == 3);
        Check("search ordinal case sensitive", MakeSearch(new object?[] { "Alpha", "ALPHA", "alpha" }, caseSensitive: true).FindAll("alpha").SequenceEqual(new[] { 2 }));
        var search = MakeSearch(new object?[] { "hit", "miss", "hit" });
        Check("FindNext inclusive start", search.FindNext("hit", 2) == 2);
        Check("FindNext negative start", search.FindNext("hit", -1) == 0);
        Check("FindNext advances without wrapping", search.FindNext("hit", 1) == 2 && search.FindNext("hit", 3) == -1 && search.FindNext("hit", int.MaxValue) == -1);
        Check("FindAll ordered", search.FindAll("hit").SequenceEqual(new[] { 0, 2 }));
        int reads = 0;
        search = new RowSearch(3, i => { reads++; return "hit"; }, new[] { new RowSearchColumn(row => row), new RowSearchColumn(row => row) }, CultureInfo.InvariantCulture, false);
        Check("empty query reads no rows", search.FindNext("", 0) == -1 && search.FindAll("").Count == 0 && reads == 0);
        Check("FindNext stops on first match", search.FindNext("hit", 0) == 0 && reads == 1);
        Check("FindAll does not duplicate rows", search.FindAll("hit").SequenceEqual(new[] { 0, 1, 2 }));
        search = new RowSearch(3, _ => throw new Exception("No column should read no rows"), Array.Empty<RowSearchColumn>(), CultureInfo.InvariantCulture, false);
        Check("no columns reads no rows", search.FindAll("hit").Count == 0);
    }

    private sealed class FormattableValue : IFormattable
    {
        public string ToString(string? format, IFormatProvider? provider)
            => format == null && Equals(provider, CultureInfo.InvariantCulture) ? "invariant raw" : "formatted";
    }

    private static void Selection()
    {
        var selection = new OrdinalSelection();
        Check("selection add unique", selection.Add(5) && !selection.Add(5) && selection.Contains(5));
        selection.AddRange(9, 7);
        selection.AddRange(6, 8);
        Check("selection merge and reverse range", selection.ToSortedArray().SequenceEqual(new long[] { 5, 6, 7, 8, 9 }));
        Check("selection remove splits", selection.Remove(7) && !selection.Remove(7) && selection.ToSortedArray().SequenceEqual(new long[] { 5, 6, 8, 9 }));
        Check("selection result limit", selection.ToSortedArray(2).SequenceEqual(new long[] { 5, 6 }) && selection.ToSortedArray(0).Length == 0);
        selection.Clear();
        Check("selection clear", !selection.Contains(5) && selection.ToSortedArray().Length == 0);
        selection.AddRange(0, long.MaxValue);
        Check("selection huge range stays compact", selection.Contains(long.MaxValue) && selection.Contains((long)int.MaxValue + 1) && selection.ToSortedArray(3).SequenceEqual(new long[] { 0, 1, 2 }));
        Check("selection remove endpoints without overflow", selection.Remove(0) && selection.Remove(long.MaxValue) && !selection.Contains(0) && !selection.Contains(long.MaxValue));
        selection.Clear();
        selection.AddRange(long.MaxValue - 2, long.MaxValue);
        Check("selection long.MaxValue enumeration", selection.ToSortedArray(10).SequenceEqual(new[] { long.MaxValue - 2, long.MaxValue - 1, long.MaxValue }));
        selection.Clear();
        var expected = new SortedSet<long>();
        var random = new Random(2000);
        bool same = true;
        for (int i = 0; i < 1000; i++)
        {
            long first = random.Next(200), last = random.Next(200);
            switch (random.Next(3))
            {
                case 0: same &= selection.Add(first) == expected.Add(first); break;
                case 1: same &= selection.Remove(first) == expected.Remove(first); break;
                default:
                    selection.AddRange(first, last);
                    for (long n = Math.Min(first, last); n <= Math.Max(first, last); n++) expected.Add(n);
                    break;
            }
            same &= selection.ToSortedArray(200).SequenceEqual(expected);
        }
        Check("selection range operations vs SortedSet (1000)", same);
    }

    private static void Pages()
    {
        var previous = SynchronizationContext.Current;
        try
        {
            SynchronizationContext.SetSynchronizationContext(null);
            using (var source = new FakeSource())
            using (var list = new VirtualSourceList(source, new VirtualSourceListOptions { PageSize = 10, SyncWait = TimeSpan.Zero }))
            using (var arrived = new ManualResetEventSlim())
            {
                VirtualPageLoadedEventArgs? page = null;
                object? sender = null;
                int thread = 0;
                list.PageLoaded += (s, e) => { sender = s; page = e; thread = Environment.CurrentManagedThreadId; arrived.Set(); };
                var row = (VirtualSourceRow)list.GetRow(1);
                bool notified = false;
                row.PropertyChanged += (_, e) => notified |= e.PropertyName == "";
                Check("page placeholder before completion", !row.IsLoaded);
                source.Complete();
                Check("PageLoaded without context", arrived.Wait(5000) && page?.Start == 0 && page.Count == 2 && ReferenceEquals(sender, list) && thread != Environment.CurrentManagedThreadId);
                Check("PageLoaded preserves PropertyChanged", row.IsLoaded && notified && Equals(row["Value"], "second"));
            }

            var context = new QueuedContext();
            SynchronizationContext.SetSynchronizationContext(context);
            using (var source = new FakeSource())
            using (var list = new VirtualSourceList(source, new VirtualSourceListOptions { PageSize = 10, SyncWait = TimeSpan.Zero }))
            {
                int events = 0, callbackThread = 0;
                bool loadedAtEvent = false;
                var row = (VirtualSourceRow)list.GetRow(0);
                list.PageLoaded += (_, _) => { events++; callbackThread = Environment.CurrentManagedThreadId; loadedAtEvent = row.IsLoaded; };
                source.Complete();
                Check("PageLoaded posts to captured context", context.Posted.Wait(5000) && events == 0 && !row.IsLoaded);
                context.Drain();
                Check("PageLoaded on context after placeholder fill", events == 1 && loadedAtEvent && callbackThread == Environment.CurrentManagedThreadId);
                _ = list.GetRow(0);
                Check("cached page sends no duplicate event", events == 1 && source.Reads == 1);
            }

            context = new QueuedContext();
            SynchronizationContext.SetSynchronizationContext(context);
            using (var source = new FakeSource())
            using (var list = new VirtualSourceList(source))
            {
                int events = 0;
                list.PageLoaded += (_, e) => { if (e.Start == 0 && e.Count == 2) events++; };
                source.Complete();
                var row = list.GetRow(0);
                Check("fast page needs no placeholder", row.IsLoaded);
                Check("fast page still posts PageLoaded", context.Posted.Wait(5000));
                context.Drain();
                Check("fast page PageLoaded count", events == 1);
            }
        }
        finally { SynchronizationContext.SetSynchronizationContext(previous); }
    }

    private sealed class QueuedContext : SynchronizationContext
    {
        private readonly ConcurrentQueue<Action> _callbacks = new();
        internal readonly ManualResetEventSlim Posted = new();
        public override void Post(SendOrPostCallback callback, object? state) { _callbacks.Enqueue(() => callback(state)); Posted.Set(); }
        internal void Drain() { while (_callbacks.TryDequeue(out var callback)) callback(); }
    }

    private sealed class FakeSource : IVirtualTableSource
    {
        private readonly TaskCompletionSource<VirtualRowPage> _completion = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal int Reads;
        public long Count => 2;
        public bool IsCountExact => true;
        public bool SupportsExactOrdinals => true;
        public IReadOnlyList<VirtualColumnInfo> Columns { get; } = new[] { new VirtualColumnInfo("Value", typeof(string)) };
        public event EventHandler? CountChanged { add { } remove { } }
        public Task<VirtualRowPage> GetPageAsync(long start, int count, CancellationToken cancellationToken) { Interlocked.Increment(ref Reads); return _completion.Task; }
        internal void Complete() => _completion.SetResult(new VirtualRowPage(0, new long[] { 10, 11 }, new object?[]?[] { new object?[] { "first" }, new object?[] { "second" } }));
        public Task<long[]> GetOrdinalsAsync(IReadOnlyCollection<long> keys, int limit, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<long[]> FindAsync(VirtualSearchRequest request, long start, int limit, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<long> SeekAsync(double fraction, CancellationToken cancellationToken) => Task.FromResult(0L);
        public void Dispose() { }
    }

    private static void ForwardingAndMetadata()
    {
        var core = typeof(IVirtualTableSource).Assembly;
        var wpf = Assembly.Load("VirtualADGV.WPF");
        Check("data assembly is VirtualADGV.Core", core.GetName().Name == "VirtualADGV.Core");
        var moved = core.GetExportedTypes().Where(t => t.Namespace == "VirtualADGV.Data" && t != typeof(VirtualPageLoadedEventArgs)).ToArray();
        var forwards = wpf.GetForwardedTypes();
        Check("all 10 original data types forwarded", moved.Length == 10 && forwards.Length == 10 && moved.All(forwards.Contains));
        Check("forward resolution from WPF assembly", moved.All(t => wpf.GetType(t.FullName!, true) == t && Type.GetType(t.FullName + ", " + wpf.FullName, true) == t));
        Check("Core and WPF version 2.0.0.0", core.GetName().Version == new Version(2, 0, 0, 0) && wpf.GetName().Version == new Version(2, 0, 0, 0));
        Check("Core and WPF unsigned", core.GetName().GetPublicKeyToken()!.Length == 0 && wpf.GetName().GetPublicKeyToken()!.Length == 0);
        Check("Core references no UI assembly", core.GetReferencedAssemblies().All(a => a.Name != "PresentationFramework" && a.Name != "PresentationCore" && a.Name != "WindowsBase" && a.Name != "System.Windows.Forms"));
        var defaults = new VirtualGridTexts();
        var wpfTexts = Activator.CreateInstance(wpf.GetType("VirtualADGV.WPF.VirtualDataGridStrings", true)!);
        Check("shared text defaults unchanged", typeof(VirtualGridTexts).GetProperties().All(p => Equals(p.GetValue(defaults), wpfTexts!.GetType().GetProperty(p.Name)!.GetValue(wpfTexts))));
    }
}
