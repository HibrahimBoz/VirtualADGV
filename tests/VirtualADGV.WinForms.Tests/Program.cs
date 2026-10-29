using System.Data;
using System.Diagnostics;
using System.Drawing;
using System.Reflection;
using VirtualADGV.Data;
using Zuby.ADGV;

// WinForms 2.0 koşumu: sanal kaynak, pencereli kip, mini harita, çapraz vurgu, arama, 1.2 regresyonları, ekran görüntüleri.
// Gerçek Form ve gerçek çizim kullanılır. Başarısızlıkta çıkış kodu 1. "--memory" WinForms RowCount bellek ölçümüdür.
internal static class Program
{
    static int oks, fails;
    static string Dir = ".";
    static void Check(string name, bool ok, string detail = "")
    {
        if (ok) oks++; else fails++;
        Console.WriteLine($"{(ok ? "OK  " : "FAIL")} {name}{(detail.Length > 0 ? "  -> " + detail : "")}");
    }
    const BindingFlags F = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;
    static object? Field(object o, string name) => o.GetType().GetField(name, F)?.GetValue(o);
    static void Call(object o, string name, params object?[] a) =>
        o.GetType().GetMethods(F).First(x => x.Name == name && x.GetParameters().Length == a.Length).Invoke(o, a);

    [STAThread]
    static int Main(string[] args)
    {
        if (args.Length > 0 && args[0] == "--memory") return MemoryBenchmark.Run(args);
        Dir = args.Length > 0 ? args[0] : "winforms_shots";
        Directory.CreateDirectory(Dir);
        Application.EnableVisualStyles();
        var form = new Form { Width = 1100, Height = 700, StartPosition = FormStartPosition.Manual, Left = 30, Top = 30, Text = "VirtualADGV WinForms 2.0" };
        form.Shown += async (_, __) =>
        {
            try { await Run(form); }
            catch (Exception ex) { fails++; Console.WriteLine("Beklenmeyen hata: " + ex); }
            Console.WriteLine($"VirtualADGV.WinForms.Tests: {oks} gecti, {fails} basarisiz");
            form.Close();
        };
        Application.Run(form);
        return fails == 0 ? 0 : 1;
    }

    static async Task Pump(int ms = 50)
    {
        var sw = Stopwatch.StartNew();
        while (sw.ElapsedMilliseconds < ms) { Application.DoEvents(); await Task.Delay(5); }
    }
    static async Task<bool> Until(Func<bool> condition, int timeoutMs = 5000)
    {
        var sw = Stopwatch.StartNew();
        while (sw.ElapsedMilliseconds < timeoutMs) { if (condition()) return true; Application.DoEvents(); await Task.Delay(5); }
        return condition();
    }

    static AdvancedDataGridView NewGrid(Form form)
    {
        foreach (Control c in form.Controls.Cast<Control>().ToArray()) { form.Controls.Remove(c); c.Dispose(); }
        var grid = new AdvancedDataGridView { Dock = DockStyle.Fill };
        form.Controls.Add(grid);
        return grid;
    }

    static object? TopValue(AdvancedDataGridView g, string column)
    {
        int row = g.FirstDisplayedScrollingRowIndex;
        return row < 0 ? null : g[g.Columns[column]!.Index, row].Value;
    }

    static async Task Run(Form form)
    {
        // ---- a) 1 M satırlık kaynak ----
        var grid = NewGrid(form);
        var src = new FakeSource(1_000_000);
        grid.SetVirtualSource(src);
        await Pump(200);
        Check("a) kaynak: RowCount = 1.000.000, sutun 3", grid.RowCount == 1_000_000 && grid.ColumnCount == 3, $"{grid.RowCount} / {grid.ColumnCount}");
        Check("a) pencereli degil (1 M esikte)", !grid.IsWindowedSource);
        bool filled = await Until(() => TopValue(grid, "No") is long);
        Check("a) ilk sayfa geldi, gorunur hucre dolu", filled, $"{TopValue(grid, "No")}");
        Check("a) sayfa gelince gorunur alan tek dikdortgenle yenilendi", grid.PageInvalidationCount > 0, $"PageInvalidationCount={grid.PageInvalidationCount}");
        var rnd = new Random(7);
        int good = 0, total = 0; string firstBad = "";
        for (int k = 0; k < 200; k++)
        {
            long o = rnd.NextInt64(0, 1_000_000 - 100);
            await grid.SourceList!.PrefetchAsync(o, 50, CancellationToken.None);
            grid.ScrollToOrdinal(o);
            await Pump(5);
            total++;
            object? v = TopValue(grid, "No");
            if (grid.FirstVisibleOrdinal == o && v is long n && n == o && Equals(TopValue(grid, "Ad"), "kayit " + (o % 50))) good++;
            else if (firstBad.Length == 0) firstBad = $"o={o} first={grid.FirstVisibleOrdinal} v={v}";
        }
        Check("a) 200 rastgele sira: ust satir ve degerleri dogru", good == total, $"{good}/{total} {firstBad}");
        await Shot(form, "kaynak_1M_acik");

        // ---- b) pencereli kip, 3 milyar satır ----
        var huge = new FakeSource(3_000_000_000);
        grid.SetVirtualSource(huge);
        await Pump(200);
        Check("b) 3 milyar: pencereli kip, RowCount = pencere", grid.IsWindowedSource && grid.RowCount <= AdvancedDataGridView.DefaultWindowSize, $"RowCount={grid.RowCount}");
        long target = 2_999_999_000;
        await grid.SourceList!.PrefetchAsync(target, 100, CancellationToken.None);
        grid.ScrollToOrdinal(target);
        await Pump(100);
        await Until(() => TopValue(grid, "No") is long);
        long expectFirst = Math.Min(target, 3_000_000_000 - grid.DisplayedRowCount(false));
        Check("b) ScrollToOrdinal(2.999.999.000): ilk gorunur sira ve deger", grid.FirstVisibleOrdinal == expectFirst && TopValue(grid, "No") is long n2 && n2 == grid.FirstVisibleOrdinal,
            $"first={grid.FirstVisibleOrdinal} beklenen={expectFirst} deger={TopValue(grid, "No")}");
        grid.ScrollToOrdinal(1_234_567_890);
        await Pump(50);
        await Until(() => TopValue(grid, "No") is long n3 && n3 == 1_234_567_890, 5000);
        Check("b) pencere geri kayar: 1.234.567.890", grid.FirstVisibleOrdinal == 1_234_567_890 && Equals(TopValue(grid, "No"), 1_234_567_890L), $"first={grid.FirstVisibleOrdinal}");

        // Tutamaç sürüklenirken (ThumbTrack) pencere yeniden ortalanmaz; aynı konumda 20 olay pencereyi kaydırmamalı.
        // Gerçek sürüklemede satırı WinForms kaydırır; aynısı, atamanın kendi Scroll olayı yeniden konumlamadan yapılır.
        var moving = typeof(AdvancedDataGridView).GetField("_movingWindow", F)!;
        moving.SetValue(grid, true);
        grid.FirstDisplayedScrollingRowIndex = (int)(grid.RowCount * 0.9);
        moving.SetValue(grid, false);
        long windowBefore = grid.SourceList!.WindowStart;
        var onScroll = typeof(DataGridView).GetMethod("OnScroll", F, null, new[] { typeof(ScrollEventArgs) }, null)!;
        for (int k = 0; k < 20; k++)
            onScroll.Invoke(grid, new object[] { new ScrollEventArgs(ScrollEventType.ThumbTrack, grid.FirstDisplayedScrollingRowIndex, ScrollOrientation.VerticalScroll) });
        Check("b) tutamac suruklenirken pencere kacmaz", grid.SourceList.WindowStart == windowBefore, $"once {windowBefore}, sonra {grid.SourceList.WindowStart}");
        onScroll.Invoke(grid, new object[] { new ScrollEventArgs(ScrollEventType.EndScroll, grid.FirstDisplayedScrollingRowIndex, ScrollOrientation.VerticalScroll) });
        Check("b) birakinca pencere bir kez kayar", grid.SourceList.WindowStart != windowBefore, $"{grid.SourceList.WindowStart}");

        // ---- c + g) mini harita sürükleme, 1 M kaynak ----
        grid.SetVirtualSource(src);
        await Pump(300);
        var map = grid.Minimap!;
        Check("c) mini harita gorunur, sag ustte", map.Visible && map.Right > grid.ClientSize.Width - 80 && map.Top < 80, $"{map.Bounds}");
        long before = grid.FirstVisibleOrdinal;
        var stepMs = new List<double>();
        double y0 = 5, y1 = map.Height - 5;
        map.BeginDrag(y0);
        bool moved = false;
        for (int k = 1; k <= 60; k++)
        {
            var sw = Stopwatch.StartNew();
            map.MoveDrag(y0 + (y1 - y0) * k / 60);
            Application.DoEvents();
            stepMs.Add(sw.Elapsed.TotalMilliseconds);
            if (grid.FirstVisibleOrdinal != before) moved = true;
            await Task.Delay(10); // 100 ms bekleme dolmaz
        }
        Check("c) surukleme sirasinda tablo kaymaz", !moved, $"once={before} simdi={grid.FirstVisibleOrdinal}");
        Check("g) surukleme adimi UI suresi < 50 ms", stepMs.Max() < 50, $"ort {stepMs.Average():F1} ms, max {stepMs.Max():F1} ms");
        double mid = map.Height * 0.5;
        for (int k = 0; k < 5; k++) { map.MoveDrag(mid); await Task.Delay(10); }
        var swEnd = Stopwatch.StartNew();
        double ratio = map.Model.TargetRatio;
        await map.EndDragAsync();
        Application.DoEvents();
        double endMs = swEnd.Elapsed.TotalMilliseconds;
        await Until(() => TopValue(grid, "No") is long);
        double center = (grid.FirstVisibleOrdinal + grid.DisplayedRowCount(false) / 2.0) / 1_000_000.0;
        Check("c) birakinca hedefe gider (+-%1)", Math.Abs(center - ratio) < 0.01, $"merkez {center:F4}, hedef {ratio:F4}, birakma {endMs:F0} ms");
        Check("c) birakinca satirlar dolu", TopValue(grid, "No") is long n4 && n4 == grid.FirstVisibleOrdinal);
        // 100 ms durunca kayar (bırakmadan)
        map.BeginDrag(map.Height * 0.2);
        await Pump(400);
        double center2 = (grid.FirstVisibleOrdinal + grid.DisplayedRowCount(false) / 2.0) / 1_000_000.0;
        Check("c) 100 ms durunca kayar", Math.Abs(center2 - map.Model.TargetRatio) < 0.01, $"merkez {center2:F4}, hedef {map.Model.TargetRatio:F4}");
        await map.EndDragAsync();
        // İşarete yapışma
        grid.SetMinimapMarks(new long[] { 700_000 });
        await Pump(50);
        map.RefreshMap();
        double markY = 3 + 0.7 * map.Model.MapHeight + 1;
        map.BeginDrag(markY);
        double snapped = map.Model.TargetRatio;
        await map.EndDragAsync();
        Check("c) tik isarete yapisir", Math.Abs(snapped - 0.7) < 0.002, $"oran {snapped:F4}");
        grid.SetMinimapMarks(new long[] { 10, 250_000, 500_000, 750_000, 999_000 });

        // ---- e) arama: kaynak modu ----
        int findCalls = src.FindCalls;
        long? hit = await grid.SearchAsync("kayit 7");
        Check("e) kaynak modu: FindAsync cagrildi, ilk eslesme 7", src.FindCalls > findCalls && hit == 7, $"hit={hit} cagri={src.FindCalls - findCalls}");
        Check("e) kaynak modu: isabetler mini haritada", grid.MinimapSearchHits.Count > 0, $"{grid.MinimapSearchHits.Count}");
        long? hit2 = await grid.SearchAsync("kayit 7", searchNext: true);
        Check("e) sonraki eslesme 57", hit2 == 57, $"hit2={hit2}");
        await Pump(200);
        await Shot(form, "kaynak_minimap_arama_acik");
        grid.SetTheme(true);
        await Pump(100);
        await Shot(form, "kaynak_minimap_arama_koyu");

        // ---- d + e) bağlı kip (DataTable) ----
        grid = NewGrid(form);
        var dt = new DataTable();
        dt.Columns.Add("No", typeof(int)); dt.Columns.Add("Ad", typeof(string)); dt.Columns.Add("Deger", typeof(double));
        for (int i = 0; i < 100_000; i++) dt.Rows.Add(i, "kayit " + (i % 50), i * 0.5);
        var bs = new BindingSource { DataSource = dt };
        grid.DataSource = bs;
        grid.FilterStringChanged += (s, e) => bs.Filter = grid.FilterString;
        await Pump(300);
        var all = grid.FindAll("kayit 7");
        Check("e) bagli kip: FindAll('kayit 7') = 2.000", all.Count == 2000 && all[0] == 7 && all[1] == 57, $"{all.Count}");
        Check("e) bagli kip: FindNext(…, 8) = 57", grid.FindNext("kayit 7", 8) == 57);
        int unshared = 0;
        for (int i = 0; i < grid.Rows.Count; i += 997) if (grid.Rows.SharedRow(i).Index >= 0) unshared++;
        Check("e) arama satirlari paylasimdan cikarmadi", unshared <= 5, $"ornekte paylasimsiz {unshared}");
        // Çapraz vurgu (piksel)
        grid.ClearSelection();
        grid.CurrentCell = grid[1, 3];
        grid[1, 3].Selected = true;
        await Pump(150);
        using (var bmp = new Bitmap(grid.Width, grid.Height))
        {
            grid.DrawToBitmap(bmp, new Rectangle(Point.Empty, grid.Size));
            Color Mid(int col, int row) { var r = grid.GetCellDisplayRectangle(col, row, true); return bmp.GetPixel(r.Left + r.Width - 4, r.Top + r.Height / 2); }
            Color sameRow = Mid(2, 3), sameCol = Mid(1, 6), other = Mid(2, 6);
            Check("d) capraz vurgu: ayni satir ve sutun boyali, digeri degil", sameRow.ToArgb() != other.ToArgb() && sameCol.ToArgb() != other.ToArgb(),
                $"satir {sameRow}, sutun {sameCol}, diger {other}");
            grid.IsCrosshairEnabled = false;
            await Pump(100);
            grid.DrawToBitmap(bmp, new Rectangle(Point.Empty, grid.Size));
            Check("d) vurgu kapali: satir boyanmaz", Mid(2, 3).ToArgb() == Mid(2, 6).ToArgb(), $"{Mid(2, 3)} / {Mid(2, 6)}");
            grid.IsCrosshairEnabled = true;
        }
        await Shot(form, "bagli_vurgu_acik");

        // ---- f) 1.2 regresyonları: #176 ve sayaç ----
        grid.SetMinimapMarks(null);
        dt.Clear();
        for (int i = 0; i < 200; i++) dt.Rows.Add(i, "kayit " + (i % 12), i % 9 * 1.0);
        await Pump(200);
        var col = grid.Columns["Ad"]!;
        grid.SetChecklistTextFilterRemoveNodesOnSearchMode(col, false);
        var menu = (ToolStripDropDown)col.HeaderCell.GetType().GetProperty("MenuStrip")!.GetValue(col.HeaderCell)!;
        var label = (Label)Field(menu, "checkListCounterLabel")!;
        var tree = (TreeView)Field(menu, "checkList")!;
        foreach (bool dark in new[] { false, true })
        {
            grid.SetTheme(dark);
            string t = dark ? "koyu" : "acik";
            grid.ShowMenuStrip(col); await Pump(250);
            Check($"f) [{t}] sayac 'Öğe 13 / 13'", label.Text == "Öğe 13 / 13" && label.Visible, $"'{label.Text}'");
            Call(menu, "NodeCheckChange", tree.Nodes[0]);
            foreach (var n in tree.Nodes.Cast<TreeNode>().Where(n => n.Text.StartsWith("kayit")).Take(2).ToArray()) Call(menu, "NodeCheckChange", n);
            Call(menu, "CheckFilterButtonEnabled");
            Check($"f) [{t}] iki deger: 'Öğe 2 / 13'", label.Text == "Öğe 2 / 13", $"'{label.Text}'");
            Call(menu, "Button_ok_Click", null, EventArgs.Empty); await Pump(200);
            for (int k = 0; k < 2; k++) { grid.ShowMenuStrip(col); await Pump(200); Call(menu, "Button_cancel_Click", null, EventArgs.Empty); await Pump(150); }
            grid.ShowMenuStrip(col); await Pump(250);
            int selected = tree.Nodes.Cast<TreeNode>().Count(n => n.Text.StartsWith("kayit") && n.GetType().GetProperty("CheckState")!.GetValue(n)!.Equals(CheckState.Checked));
            Check($"f) [{t}] #176: iki Iptal sonrasi liste dolu, secim korunur", tree.Nodes.Count >= 14 && selected == 2, $"dugum {tree.Nodes.Count}, secili {selected}");
            await Shot(form, $"bagli_suzgec_{t}", menu);
            menu.Close(); await Pump(100);
            grid.CleanFilter(); await Pump(150);
        }
    }

    static async Task Shot(Form form, string name, ToolStripDropDown? menu = null)
    {
        await Pump(100);
        var r = form.Bounds;
        if (menu != null && menu.Visible) r = Rectangle.Union(r, menu.Bounds);
        using var bmp = new Bitmap(r.Width, r.Height);
        using (var g = Graphics.FromImage(bmp)) g.CopyFromScreen(r.Location, Point.Empty, r.Size);
        bmp.Save(Path.Combine(Dir, name + ".png"));
    }
}

/// <summary>Sahte kaynak: No = sıra, Ad = "kayit " + (sıra % 50), Deger = sıra * 0,5. Sayfalar eşzamansız gelir.</summary>
internal sealed class FakeSource : IVirtualTableSource
{
    public FakeSource(long count) { Count = count; }
    public long Count { get; }
    public bool IsCountExact => true;
    public event EventHandler? CountChanged { add { } remove { } }
    public bool SupportsExactOrdinals => true;
    public int FindCalls;
    public IReadOnlyList<VirtualColumnInfo> Columns { get; } = new[]
    {
        new VirtualColumnInfo("No", typeof(long)), new VirtualColumnInfo("Ad", typeof(string)), new VirtualColumnInfo("Deger", typeof(double))
    };
    public async Task<VirtualRowPage> GetPageAsync(long start, int count, CancellationToken ct)
    {
        await Task.Delay(3, ct);
        count = (int)Math.Max(0, Math.Min(count, Count - start));
        var keys = new long[count]; var values = new object?[]?[count];
        for (int i = 0; i < count; i++) { long o = start + i; keys[i] = o; values[i] = new object?[] { o, "kayit " + (o % 50), o * 0.5 }; }
        return new VirtualRowPage(start, keys, values);
    }
    public Task<long[]> GetOrdinalsAsync(IReadOnlyCollection<long> keys, int limit, CancellationToken ct)
        => Task.FromResult(keys.Where(k => k >= 0 && k < Count).OrderBy(k => k).Take(limit).ToArray());
    public Task<long[]> FindAsync(VirtualSearchRequest r, long start, int limit, CancellationToken ct)
    {
        Interlocked.Increment(ref FindCalls);
        // "kayit N": N < 50 ise sıra ≡ N (mod 50). Yalnız bu kalıp desteklenir (sınama kaynağı).
        if (!r.Text.StartsWith("kayit ") || !int.TryParse(r.Text.Substring(6), out int n) || n >= 50) return Task.FromResult(Array.Empty<long>());
        var hits = new List<long>();
        long first = r.Backward ? start - ((start - n) % 50 + 50) % 50 : start + ((n - start) % 50 + 50) % 50;
        for (long o = first; o >= 0 && o < Count && hits.Count < limit; o += r.Backward ? -50 : 50) hits.Add(o);
        return Task.FromResult(hits.ToArray());
    }
    public Task<long> SeekAsync(double f, CancellationToken ct) => Task.FromResult((long)(Math.Max(0, Math.Min(1, f)) * (Count - 1)));
    public void Dispose() { }
}
