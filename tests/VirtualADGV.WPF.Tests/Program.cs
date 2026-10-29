using System.Collections;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using VirtualADGV.Data;
using VirtualADGV.WPF;

internal static class Program
{
    private static int _pass, _fail;
    private static string _shots = "shots";
    private static void Check(string ad, bool kosul, string ayrinti = "")
    {
        if (kosul) { _pass++; Console.WriteLine($"  ok   {ad}"); }
        else { _fail++; Console.WriteLine($"  FAIL {ad} {ayrinti}"); }
    }

    [STAThread]
    private static int Main(string[] args)
    {
        Console.OutputEncoding = System.Text.Encoding.UTF8;
        CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("tr-TR");
        _shots = Path.GetFullPath(args.Length > 0 ? args[0] : "shots");
        Directory.CreateDirectory(_shots);
        var app = new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
        app.Dispatcher.BeginInvoke(new Action(async () =>
        {
            try
            {
                await RunMemory(false);
                await RunMemory(true);
                await RunLarge();
                await RunThumbHit();
                await RunSelectionLimit();
                RunSearchValues();
                await RunSourceContract();
                await RunWindowedGrid();
                await RunPrefetch(false);
                await RunPrefetch(true);
            }
            catch (Exception ex) { Check("beklenmeyen istisna", false, ex.ToString()); }
            finally { app.Shutdown(); }
        }));
        app.Run();
        Console.WriteLine($"VirtualAdgvTests: {_pass} gecti, {_fail} basarisiz");
        return _fail == 0 ? 0 : 1;
    }

    private static Task Settle(int ms = 70) => Task.Delay(ms);
    // Zamanlamaya bağlı adımlar sabit süre değil koşul bekler (yüklü makinede 200 ms yetmeyebilir).
    private static async Task<bool> Until(Func<bool> done, int ms = 5000)
    {
        var timer = Stopwatch.StartNew();
        while (!done()) { if (timer.ElapsedMilliseconds > ms) return false; await Task.Delay(20); }
        await Task.Delay(30);
        return true;
    }
    private static List<Satir> Rows(int count) => Enumerable.Range(0, count).Select(i => new Satir(i)).ToList();
    private static VirtualDataGrid MakeGrid(IEnumerable source, bool dark)
    {
        var grid = new VirtualDataGrid
        {
            AutoGenerateColumns = false, ItemsSource = source, SelectionUnit = DataGridSelectionUnit.CellOrRowHeader,
            SelectionMode = DataGridSelectionMode.Extended, IsReadOnly = true, RowHeight = 36,
            Language = XmlLanguage.GetLanguage("tr-TR"), FontSize = 13
        };
        string[] paths = { "No", "Deger", "Ad", "Etkin", "Tarih", "Aciklama" };
        for (int i = 0; i < paths.Length; i++)
            grid.Columns.Add(new DataGridTextColumn { Header = paths[i], Width = i == 5 ? 230 : 145,
                Binding = new Binding(paths[i]) { StringFormat = i == 1 ? "0.###" : i == 4 ? "dd.MM.yyyy" : null } });
        Theme(grid, dark);
        grid.Strings.SearchLabel = "Ara:";
        grid.Strings.ColumnLabel = "Sütun:";
        grid.Strings.AllColumns = "Tüm sütunlar";
        grid.Strings.CaseSensitive = "Büyük/küçük harf";
        grid.Strings.FindNext = "Sonrakini Bul";
        grid.Strings.FindReset = "Baştan Ara";
        grid.Strings.SearchNoMatch = "Eşleşme bulunamadı.";
        grid.Strings.SearchEndReached = "Eşleşme bulunamadı (tablonun sonuna gelindi; «Baştan Ara» ile yeniden arayın).";
        grid.Strings.SearchMatchCountFormat = "{0:N0} eşleşme";
        grid.Strings.MinimapRowFormat = "Satır: {0:N0}";
        return grid;
    }
    private static void Theme(VirtualDataGrid grid, bool dark)
    {
        var colors = new Dictionary<string, string>
        {
            ["ThemeGridBg"] = dark ? "#09090B" : "#FFFFFF", ["ThemeGridAltBg"] = dark ? "#101014" : "#F8FAFC",
            ["ThemeText"] = dark ? "#FFFFFF" : "#18181B", ["ThemeSurface"] = dark ? "#18181B" : "#F1F5F9",
            ["ThemeSurface2"] = dark ? "#27272A" : "#FFFFFF", ["ThemeSurface3"] = dark ? "#3F3F46" : "#E2E8F0",
            ["ThemeHighlightBg"] = dark ? "#B89400" : "#D9AD00", ["ThemeAccent"] = "#2563EB",
            ["ThemeSearchHitBg"] = "#FF8C00", ["ThemeGridHeaderBg"] = dark ? "#18181B" : "#E2E8F0",
            ["ThemeGridHeaderFg"] = dark ? "#FFFFFF" : "#18181B", ["ThemeGridLine"] = dark ? "#27272A" : "#CBD5E1",
            ["ThemeBorder"] = dark ? "#52525B" : "#94A3B8", ["ThemeSubtleText"] = dark ? "#D4D4D8" : "#334155",
            ["ThemeScrollThumb"] = dark ? "#71717A" : "#94A3B8", ["ThemeScrollThumbHover"] = "#2563EB"
        };
        foreach (var pair in colors) grid.Resources[pair.Key] = new SolidColorBrush((Color)ColorConverter.ConvertFromString(pair.Value));
    }
    private static (Window Window, AdornerDecorator Decorator) Show(VirtualDataGrid grid, string title)
    {
        var decorator = new AdornerDecorator { Child = grid };
        var window = new Window { Title = title, Width = 1120, Height = 660, Content = decorator,
            WindowStartupLocation = WindowStartupLocation.CenterScreen };
        window.Show();
        grid.UpdateLayout();
        return (window, decorator);
    }
    private static void SelectCell(VirtualDataGrid grid, object item, int column = 2)
    {
        grid.UnselectAllCells();
        grid.SelectedItem = null;
        grid.CurrentCell = new DataGridCellInfo(item, grid.Columns[column]);
        grid.SelectedCells.Add(new DataGridCellInfo(item, grid.Columns[column]));
    }
    private static bool Near(double actual, double expected, double tolerance = 1.5) => Math.Abs(actual - expected) <= tolerance;

    private static async Task RunMemory(bool dark)
    {
        string tag = dark ? "koyu" : "acik";
        var rows = Rows(1000);
        var grid = MakeGrid(rows, dark);
        grid.IsSearchVisible = true;
        var (window, decorator) = Show(grid, "VirtualADGV 1.1.0 — " + tag);
        await Settle();
        Check(tag + " adorner kuruldu", grid.Crosshair != null && grid.Minimap != null);
        var cross = grid.Crosshair!;
        var map = grid.Minimap!;
        var scroll = grid.OverlayScrollViewer!;
        var presenter = grid.OverlayPresenter!;
        Check(tag + " acik arama kutusu yerine tablo kaydiricisi", scroll.TemplatedParent == grid && scroll.ExtentHeight > 1000);
        SelectCell(grid, rows[5]);
        await Settle();
        var row = (DataGridRow)grid.ItemContainerGenerator.ContainerFromItem(rows[5]);
        var header = VirtualDataGrid.VisualDescendants<DataGridColumnHeader>(grid).First(h => h.Column == grid.Columns[2]);
        Check(tag + " tek hucre satir seridi", cross.RowStrips.Count == 1 && Near(cross.RowStrips[0].Y, row.TranslatePoint(new Point(), presenter).Y)
            && Near(cross.RowStrips[0].Width, presenter.ActualWidth));
        Check(tag + " tek hucre sutun seridi", cross.ColumnStrips.Count == 1 && Near(cross.ColumnStrips[0].X, header.TranslatePoint(new Point(), presenter).X)
            && Near(cross.ColumnStrips[0].Width, header.ActualWidth) && Near(cross.ColumnStrips[0].Height, presenter.ActualHeight));
        grid.IsCrosshairEnabled = false;
        Check(tag + " capraz vurgu kapatilabilir", cross.RowStrips.Count == 0 && cross.ColumnStrips.Count == 0);
        grid.IsCrosshairEnabled = true;
        Check(tag + " capraz vurgu yeniden acilir", cross.RowStrips.Count == 1 && cross.ColumnStrips.Count == 1);
        foreach (var column in grid.Columns.Where(c => c != grid.Columns[2])) grid.SelectedCells.Add(new DataGridCellInfo(rows[5], column));
        await Settle();
        Check(tag + " tum sutunlar secilince sutun seridi yok", cross.RowStrips.Count == 1 && cross.ColumnStrips.Count == 0);
        grid.SelectionUnit = DataGridSelectionUnit.FullRow;
        grid.SelectedItem = rows[5];
        await Settle();
        Check(tag + " tam satir sutun seridi yok", cross.ColumnStrips.Count == 0 && cross.RowStrips.Count == 1);
        grid.SelectionUnit = DataGridSelectionUnit.CellOrRowHeader;
        SelectCell(grid, rows[5]);
        await Settle();
        double oldY = cross.RowStrips[0].Y;
        scroll.ScrollToVerticalOffset(54);
        await Settle();
        Check(tag + " kaydirmada satir seridi", cross.RowStrips.Count == 1 && cross.RowStrips[0].Y < oldY);
        scroll.ScrollToVerticalOffset(7000);
        await Settle();
        Check(tag + " geri donusumde yanlis satir boyanmaz", cross.RowStrips.Count == 0);
        scroll.ScrollToHome();
        await Settle();
        Check(tag + " geri donuste secili satir boyanir", cross.RowStrips.Count == 1);
        CollectionViewSource.GetDefaultView(rows).Refresh();
        await Settle();
        Check(tag + " Reset capraz vurguyu temizler", cross.RowStrips.Count == 0 && cross.ColumnStrips.Count == 0);
        var view = CollectionViewSource.GetDefaultView(rows);
        SelectCell(grid, rows[5]);
        await Settle();
        view.SortDescriptions.Add(new SortDescription("No", ListSortDirection.Descending));
        await Settle();
        Check(tag + " siralama eski sira vurgusunu temizler", cross.RowStrips.Count == 0 && cross.ColumnStrips.Count == 0);
        SelectCell(grid, rows[5]);
        await Settle();
        Check(tag + " siralamada secim sira onbellegi yenilenir", map.SelectionBuckets.Any(i => i == 994));
        view.SortDescriptions.Clear();
        await Settle();
        SelectCell(grid, rows[5]);
        await Settle();
        grid.Columns[2].Width = 180;
        grid.Columns[2].DisplayIndex = 1;
        grid.FrozenColumnCount = 1;
        scroll.ScrollToHorizontalOffset(75);
        await Settle();
        header = VirtualDataGrid.VisualDescendants<DataGridColumnHeader>(grid).First(h => h.Column == grid.Columns[2]);
        Check(tag + " genislik sira yatay kaydirma", cross.ColumnStrips.Count == 1 &&
            Near(cross.ColumnStrips[0].Right, header.TranslatePoint(new Point(), presenter).X + header.ActualWidth));
        SelectCell(grid, rows[5], 0);
        await Settle();
        header = VirtualDataGrid.VisualDescendants<DataGridColumnHeader>(grid).First(h => h.Column == grid.Columns[0]);
        Check(tag + " donmus sutun konumu", cross.ColumnStrips.Count == 1 && Near(cross.ColumnStrips[0].X, header.TranslatePoint(new Point(), presenter).X));
        scroll.ScrollToHorizontalOffset(0);
        grid.SetMinimapMarks(new long[] { 990, 10, 500 });
        await Settle();
        Check(tag + " mini harita kovalari", map.MarkBuckets[1] == 10 && map.MarkBuckets[90] == 500 && map.MarkBuckets[178] == 990);
        Check(tag + " mini harita sag ustte", Near(map.MapBounds.Right, presenter.ActualWidth - 4) && Near(map.MapBounds.Top, 4));
        grid.MinimapWidth = 72;
        grid.MinimapHeight = 10000;
        Check(tag + " mini harita boyut siniri", Near(map.MapBounds.Width, 72) && Near(map.MapBounds.Height, presenter.ActualHeight - 8));
        grid.MinimapWidth = 60;
        grid.MinimapHeight = 180;
        Check(tag + " mini harita disi tik gecirir", VisualTreeHelper.HitTest(map, new Point(10, 10)) == null);
        Check(tag + " mini harita ici tik yakalar", VisualTreeHelper.HitTest(map, new Point(map.MapBounds.X + 5, map.MapBounds.Y + 5)) != null);
        grid.SetMinimapMarks(null);
        map.ClickAt(map.MapBounds.Y + map.MapBounds.Height * 0.5);
        await Settle();
        Check(tag + " tik merkezler Pixel", Near(scroll.VerticalOffset, map.TargetOffset(0.5)), $"{scroll.VerticalOffset} / {map.TargetOffset(0.5)}");
        // Tablonun kendi tekerlek adımı (referans), sonra mini harita üstünden aynı tekerlek: adım AYNI olmalı.
        // Adorner ScrollContentPresenter'ın AdornerLayer'ındadır; olay iletildikten sonra işlenmemiş bırakılsaydı
        // kabarcık aynı ScrollViewer'a ikinci kez ulaşır, tablo iki kat kayardı.
        double directBefore = scroll.VerticalOffset;
        presenter.RaiseEvent(new MouseWheelEventArgs(Mouse.PrimaryDevice, Environment.TickCount, -120) { RoutedEvent = Mouse.MouseWheelEvent });
        await Settle();
        double directStep = scroll.VerticalOffset - directBefore;
        double wheelBefore = scroll.VerticalOffset;
        var wheel = new MouseWheelEventArgs(Mouse.PrimaryDevice, Environment.TickCount, -120) { RoutedEvent = Mouse.MouseWheelEvent };
        map.RaiseEvent(wheel);
        await Settle();
        double mapStep = scroll.VerticalOffset - wheelBefore;
        Check(tag + " tekerlek tabloyu bir adim kaydirir (iki kat degil)", directStep > 0 && Near(mapStep, directStep), $"harita={mapStep} tablo={directStep}");
        double before = scroll.VerticalOffset;
        map.BeginDrag(map.MapBounds.Y + map.MapBounds.Height * 0.75);
        Check(tag + " surukleme hemen kaydirmaz", scroll.VerticalOffset == before && map.RowHint.Length > 0);
        await Settle(35);
        Check(tag + " surukleme 100 ms oncesi bekler", scroll.VerticalOffset == before);
        await Settle(160);
        Check(tag + " surukleme 100 ms sonrasi kaydirir", Near(scroll.VerticalOffset, map.TargetOffset(0.75)));
        map.MoveDrag(map.MapBounds.Y + map.MapBounds.Height * 0.25);
        map.EndDrag();
        await Settle();
        Check(tag + " birakma beklemeden kaydirir", Near(scroll.VerticalOffset, map.TargetOffset(0.25)));
        grid.SetMinimapMarks(new long[] { 10, 500, 501, 990 });
        grid.SelectedItem = rows[7];
        object selected = grid.SelectedItem;
        map.ClickAt(map.MapBounds.Y + map.MapBounds.Height * 0.5 + 1);
        await Settle();
        Check(tag + " isaret kovasi ilk satiri merkezler secimi korur", Near(scroll.VerticalOffset, map.TargetOffset(0.5)) && ReferenceEquals(grid.SelectedItem, selected));
        VirtualizingPanel.SetScrollUnit(grid, ScrollUnit.Item);
        await Settle();
        grid.SetMinimapMarks(null);
        map.ClickAt(map.MapBounds.Y + map.MapBounds.Height * 0.5);
        await Settle();
        Check(tag + " tik merkezler Item", Near(scroll.VerticalOffset, map.TargetOffset(0.5)));
        Check(tag + " cerceve boyutsuz oran", Near(map.ViewportBounds.Y - map.MapBounds.Y, scroll.VerticalOffset / scroll.ExtentHeight * map.MapBounds.Height)
            && map.ViewportBounds.Height >= 6);
        grid.IsMinimapVisible = false;
        Check(tag + " mini harita ayarla gizlenir", map.Visibility == Visibility.Collapsed);
        grid.IsMinimapVisible = true;
        grid.ItemsSource = Rows(5);
        await Settle();
        Check(tag + " kaydirma cubugu yokken gizlenir", map.Visibility == Visibility.Collapsed);
        grid.ItemsSource = rows;
        VirtualizingPanel.SetScrollUnit(grid, ScrollUnit.Pixel);
        await Settle();

        grid.IsSearchVisible = true;
        await Settle();
        var input = (TextBox)grid.Template.FindName("PART_SearchTextBox", grid);
        var combo = (ComboBox)grid.Template.FindName("PART_SearchColumnComboBox", grid);
        combo.SelectedItem = "Ad";
        input.Text = "hedef";
        grid.TriggerSearch(false);
        await Settle();
        Check(tag + " yerlesik arama bulur secer", ReferenceEquals(grid.SelectedItem, rows[5]));
        Check(tag + " arama tum isaretler ve durum", grid.MinimapSearchHits.SequenceEqual(new long[] { 5, 105, 205, 305, 405, 505, 605, 705, 805, 905 })
            && grid.SearchStatus == "10 eşleşme");
        grid.SelectedItem = rows[999];
        grid.TriggerSearch(true);
        Check(tag + " arama sarmaz sonunu bildirir", grid.SearchStatus == grid.Strings.SearchEndReached && ReferenceEquals(grid.SelectedItem, rows[999]));
        bool requested = false;
        EventHandler<SearchEventArgs> handler = (_, _) => requested = true;
        grid.SearchRequested += handler;
        grid.TriggerSearch(false);
        Check(tag + " abone varsa yerlesik arama yok", requested && ReferenceEquals(grid.SelectedItem, rows[999]));
        grid.SearchRequested -= handler;
        input.Text = "";
        Check(tag + " bos arama isaretleri temizler", grid.MinimapSearchHits.Count == 0 && grid.SearchStatus == "");
        input.Text = "olmayan deger";
        grid.TriggerSearch(false);
        Check(tag + " eslesme yok durumu", grid.SearchStatus == grid.Strings.SearchNoMatch);
        input.Text = "hedef";
        grid.TriggerSearch(false);
        grid.IsSearchVisible = false;
        Check(tag + " kapanan arama isaretleri temizler", grid.MinimapSearchHits.Count == 0);

        scroll.ScrollToHome();
        grid.UnselectAllCells();
        grid.SelectedItem = null;
        await Settle();
        int resets = 0;
        ((INotifyCollectionChanged)grid.Items).CollectionChanged += (_, e) => { if (e.Action == NotifyCollectionChangedAction.Reset) resets++; };
        var cell = VirtualDataGrid.VisualDescendants<DataGridCell>(grid).First(c => c.Column == grid.Columns[1]);
        ApplicationCommands.SelectAll.Execute(null, grid);
        await Settle();
        Check(tag + " Ctrl+A DP ve gorunen hucre Refreshsiz", grid.Columns.All(VirtualDataGrid.GetIsHighlighted)
            && cell.Background is SolidColorBrush b && b.Color == Color.FromRgb(0x25, 0x63, 0xEB) && resets == 0);
        ApplicationCommands.SelectAll.Execute(null, grid);
        header = VirtualDataGrid.VisualDescendants<DataGridColumnHeader>(grid).First(h => h.Column == grid.Columns[1]);
        grid.RaiseEvent(new MouseButtonEventArgs(Mouse.PrimaryDevice, Environment.TickCount, MouseButton.Left)
            { RoutedEvent = Control.PreviewMouseDoubleClickEvent, Source = VisualTreeHelper.GetChild(header, 0) });
        await Settle();
        Check(tag + " sutun secimi DP ve gorunen hucre Refreshsiz", VirtualDataGrid.GetIsHighlighted(grid.Columns[1]) &&
            cell.Background is SolidColorBrush b2 && b2.Color == Color.FromRgb(0x25, 0x63, 0xEB) && resets == 0);
        grid.SelectEntireColumn(grid.Columns[1]);
        grid.IsSearchVisible = true;
        input.Text = "hedef";
        grid.TriggerSearch(false);
        await Settle();
        scroll.ScrollToVerticalOffset(240 * grid.RowHeight);
        await Settle();
        SelectCell(grid, rows[250]);
        grid.SetMinimapMarks(new long[] { 10, 500, 990 });
        await Settle();
        SaveShot(decorator, "adgv_" + tag + ".png");
        Check(tag + " ekran goruntusunde adorner", decorator.AdornerLayer.GetAdorners(presenter)?.Length == 2);
        // Tüketici şablonu: standart DataGrid şablonu ve kendi hücre stili.
        var standard = new DataGrid();
        standard.ApplyTemplate();
        var standardStyle = (Style)Application.Current.FindResource(typeof(DataGrid));
        grid.Template = standardStyle.Setters.OfType<Setter>().First(s => s.Property == Control.TemplateProperty).Value as ControlTemplate;
        grid.CellStyle = new Style(typeof(DataGridCell));
        grid.ApplyTemplate();
        await Settle();
        grid.ScrollIntoView(rows[5]);
        await Settle();
        SelectCell(grid, rows[5]);
        await Settle();
        Check(tag + " tuketici sablonunda adorner", grid.Crosshair != null && grid.Crosshair.RowStrips.Count == 1 && grid.Minimap?.Visibility == Visibility.Visible);
        var oldLayer = AdornerLayer.GetAdornerLayer(grid.OverlayPresenter!);
        var oldPresenter = grid.OverlayPresenter!;
        decorator.Child = null;
        await Settle();
        Check(tag + " Unloaded adorner temizligi", grid.Crosshair == null && grid.Minimap == null && (oldLayer.GetAdorners(oldPresenter)?.Length ?? 0) == 0);
        decorator.Child = grid;
        await Settle();
        Check(tag + " yeniden Loaded iki adorner", AdornerLayer.GetAdornerLayer(grid.OverlayPresenter!).GetAdorners(grid.OverlayPresenter!)?.Length == 2);
        Check(tag + " eski sablonun katmanlari kaldirildi", (decorator.AdornerLayer.GetAdorners(presenter)?.Length ?? 0) == 0);
        window.Close();
    }

    private static async Task RunLarge()
    {
        var source = new SanalListe(1_000_000);
        var grid = MakeGrid(source, true);
        grid.OrdinalResolver = item => item is Satir row ? row.No : null;
        VirtualizingPanel.SetCacheLength(grid, new VirtualizationCacheLength(0));
        VirtualizingPanel.SetScrollUnit(grid, ScrollUnit.Item);
        var (window, decorator) = Show(grid, "VirtualADGV 1.1.0 — 1.000.000 satır");
        await Settle();
        var map = grid.Minimap!;
        var scroll = grid.OverlayScrollViewer!;
        var marks = Enumerable.Range(0, 200_000).Select(i => (long)i * 5).ToArray();
        int before = source.Reads;
        grid.SetMinimapMarks(marks);
        await Settle();
        Check("buyuk tabloda isaretler satir okumaz", source.Reads == before, $"okuma farki={source.Reads - before}");
        var visual = new DrawingVisual();
        using (var dc = visual.RenderOpen()) map.DrawMap(dc);
        for (int i = 1; i <= 3; i++)
        {
            map.InvalidateBuckets();
            var timer = Stopwatch.StartNew();
            using (var dc = visual.RenderOpen()) map.DrawMap(dc);
            timer.Stop();
            Console.WriteLine(FormattableString.Invariant($"MINIMAP kare {i}: {timer.Elapsed.TotalMilliseconds:F4} ms (200000 isaret / 1000000 satir)"));
            Check("buyuk tablo kare " + i + " <16 ms", timer.Elapsed.TotalMilliseconds < 16);
        }
        before = source.Reads;
        map.ClickAt(map.MapBounds.Y + map.MapBounds.Height * 0.5);
        await Settle();
        int clickReads = source.Reads - before;
        Check("buyuk tablo tik merkezler", Near(scroll.VerticalOffset, map.TargetOffset(0.5)));
        before = source.Reads;
        double offset = scroll.VerticalOffset;
        map.BeginDrag(map.MapBounds.Y + map.MapBounds.Height * 0.7);
        for (int i = 0; i < 30; i++) map.MoveDrag(map.MapBounds.Y + map.MapBounds.Height * (0.7 + i * 0.002));
        Check("buyuk tablo surukleme bekler okumaz", source.Reads == before && scroll.VerticalOffset == offset);
        map.EndDrag();
        await Settle();
        int dragReads = source.Reads - before;
        Console.WriteLine($"SANAL this[i]: tik={clickReads}, surukleme={dragReads}, gerceklesen={VirtualDataGrid.VisualDescendants<DataGridRow>(grid).Count()}");
        Check("buyuk tabloda toplu Items[i] erisimi yok", clickReads <= 100 && dragReads <= 100);
        Check("buyuk tabloda cerceve en az 6 px", map.ViewportBounds.Height >= 6);
        var visible = VirtualDataGrid.VisualDescendants<DataGridRow>(grid).First(r => r.IsVisible);
        SelectCell(grid, visible.Item);
        grid.SetMinimapSearchHits(new long[] { 100000, 250000, 600000, 900000 });
        await Settle();
        Check("buyuk tabloda resolver secim katmani", map.SelectionBuckets.Any(i => i >= 0));
        SaveShot(decorator, "adgv_buyuk_koyu.png");
        grid.OrdinalResolver = null;
        await Settle();
        Check("buyuk tabloda resolver yoksa secim katmani yok", map.SelectionBuckets.All(i => i < 0));
        grid.IsSearchVisible = true;
        await Settle();
        ((TextBox)grid.Template.FindName("PART_SearchTextBox", grid)).Text = "hedef";
        before = source.Reads;
        grid.TriggerSearch(false);
        Check("sanal tabloda yerlesik arama taramaz", source.Reads == before);
        grid.ItemsSource = new SanalListe(1000);
        await Settle();
        var small = (SanalListe)grid.ItemsSource;
        before = small.Reads;
        grid.TriggerSearch(false);
        Check("kucuk sanal IList de aramada taranmaz", small.Reads == before);
        window.Close();
    }

    // Tema tutamaca MinHeight verince Track tutamacın yerini kendi (küçük) boyuyla ayırır; sonraki sayfa düğmesi tutamacın
    // alt kısmını örter. Ölçüldü (MinHeight=24 tema, 1 M satır): tutamacın ortasına basış PageDown'a gidiyor, sürükleme hiç başlamıyordu.
    private static async Task RunThumbHit()
    {
        // Kütüphanenin kendi teması sabit boylu tutamaç kullanır (Track.ViewportSize = NaN): düzeltme ona dokunmamalı.
        var plainGrid = MakeGrid(new SanalListe(1_000_000), false);
        var (plainWindow, _) = Show(plainGrid, "VirtualADGV — kendi tema");
        await Settle();
        var plainViewer = plainGrid.OverlayScrollViewer!;
        var plainTrack = VirtualDataGrid.VisualDescendants<Track>((ScrollBar)plainViewer.Template.FindName("PART_VerticalScrollBar", plainViewer)).First();
        var plainBar = (ScrollBar)plainViewer.Template.FindName("PART_VerticalScrollBar", plainViewer);
        Check("MinHeight vermeyen temada Track.ViewportSize cubugunkiyle ayni (gorunum degismez)",
            plainTrack.ViewportSize == plainBar.ViewportSize, $"track={plainTrack.ViewportSize} cubuk={plainBar.ViewportSize}");
        plainWindow.Close();

        var grid = MakeGrid(new SanalListe(1_000_000), false);
        VirtualizingPanel.SetScrollUnit(grid, ScrollUnit.Item);
        // Uygulama temalarındaki gibi orantılı tutamaç: ViewportSize şablondan, MinHeight 24, sayfa düğmeleri tıklanabilir (saydam arka plan).
        grid.Resources[typeof(ScrollBar)] = (Style)XamlReader.Parse(
            "<Style TargetType='ScrollBar' xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation'><Setter Property='Width' Value='14'/>" +
            "<Setter Property='Template'><Setter.Value><ControlTemplate TargetType='ScrollBar'>" +
            "<Track x:Name='PART_Track' xmlns:x='http://schemas.microsoft.com/winfx/2006/xaml' IsDirectionReversed='True' ViewportSize='{TemplateBinding ViewportSize}'>" +
            "<Track.DecreaseRepeatButton><RepeatButton Command='ScrollBar.PageUpCommand' Background='Transparent'><RepeatButton.Template><ControlTemplate TargetType='RepeatButton'><Border Background='{TemplateBinding Background}'/></ControlTemplate></RepeatButton.Template></RepeatButton></Track.DecreaseRepeatButton>" +
            "<Track.Thumb><Thumb MinHeight='24'/></Track.Thumb>" +
            "<Track.IncreaseRepeatButton><RepeatButton Command='ScrollBar.PageDownCommand' Background='Transparent'><RepeatButton.Template><ControlTemplate TargetType='RepeatButton'><Border Background='{TemplateBinding Background}'/></ControlTemplate></RepeatButton.Template></RepeatButton></Track.IncreaseRepeatButton>" +
            "</Track></ControlTemplate></Setter.Value></Setter></Style>");
        var (window, _) = Show(grid, "VirtualADGV — tutamaç isabeti");
        await Settle();
        var viewer = grid.OverlayScrollViewer!;
        var bar = (ScrollBar)viewer.Template.FindName("PART_VerticalScrollBar", viewer);
        var track = VirtualDataGrid.VisualDescendants<Track>(bar).First();
        viewer.ScrollToVerticalOffset(viewer.ScrollableHeight / 2);
        await Settle();
        window.UpdateLayout();
        // İsabet testi temaya bağlıdır (sayfa düğmesinin arka planı boşsa tıklama tutamaca düşer); ölçüt örtüşmedir:
        // tutamacın alt kenarı ile sonraki sayfa düğmesinin üst kenarı (Track koordinatında, dikeyde yön ters).
        double Overlap()
        {
            double thumbBottom = track.Thumb.TranslatePoint(new Point(0, track.Thumb.ActualHeight), track).Y;
            var increase = track.IncreaseRepeatButton;
            double increaseTop = increase.TranslatePoint(new Point(0, 0), track).Y;
            return thumbBottom - increaseTop;
        }
        string Hit()
        {
            var p = track.Thumb.TranslatePoint(new Point(track.Thumb.ActualWidth / 2, track.Thumb.ActualHeight * 0.75), track);
            for (var d = VisualTreeHelper.HitTest(track, p)?.VisualHit as DependencyObject; d != null && d != track; d = VisualTreeHelper.GetParent(d))
                if (d is Thumb || d is RepeatButton) return d.GetType().Name;
            return "yok";
        }
        double overlap = Overlap();
        Check("MinHeight'li tutamaci sayfa dugmesi ortmez", overlap <= 0.5, $"ortusme={overlap:F1} px, tutamac={track.Thumb.ActualHeight:F0} px");
        Check("tutamacin alt kismina basis tutamaca gider", Hit() == nameof(Thumb), $"isabet={Hit()}");
        // Denetim: düzeltme kaldırılınca örtüşme geri gelmeli (test kusuru gerçekten görüyor mu).
        track.SetBinding(Track.ViewportSizeProperty, new Binding(nameof(ScrollBar.ViewportSize)) { Source = bar });
        window.UpdateLayout();
        double without = Overlap();
        Check("denetim: duzeltme yokken sayfa dugmesi tutamacin bir kismini orter", without > 5, $"ortusme={without:F1} px");
        Check("denetim: duzeltme yokken basis sayfa dugmesine gider", Hit() == nameof(RepeatButton), $"isabet={Hit()}");
        window.Close();
    }

    private static async Task RunSelectionLimit()
    {
        var rows = Rows(12000);
        var grid = MakeGrid(rows, true);
        var (window, _) = Show(grid, "VirtualADGV seçim sınırı");
        await Settle();
        for (int i = 0; i < rows.Count; i++)
            for (int c = 0; c < 5; c++) grid.SelectedCells.Add(new DataGridCellInfo(rows[i], grid.Columns[c]));
        await Settle();
        Check("60000 secili hucre yalniz sutun seridi", grid.SelectedCells.Count == 60000 && grid.Crosshair!.RowStrips.Count == 0 && grid.Crosshair.ColumnStrips.Count > 0);
        window.Close();
    }

    private static void RunSearchValues()
    {
        var grid = MakeGrid(Rows(1000), false);
        grid.ApplyTemplate();
        Check("AdornerLayer yokken sessiz", grid.Crosshair == null && grid.Minimap == null);
        Check("arama StringFormat gorunen metin", GridSearch.FindNext(grid, "0,123", "Deger", false, 0) == 0);
        Check("arama ham invariant metin", GridSearch.FindNext(grid, "0.12345", "Deger", false, 0) == 0);
        Check("arama baslik oku temizlenir", GridSearch.FindNext(grid, "hedef", "Ad ⏷", false, 0) == 5);
        Check("arama buyuk kucuk duyarli", GridSearch.FindNext(grid, "HEDEF", "Ad", true, 0) == -1);
        Check("arama InvariantCultureIgnoreCase", GridSearch.FindNext(grid, "if", "Aciklama", false, 0) == 0);
        Check("arama start ve sarma yok", GridSearch.FindNext(grid, "hedef", "Ad", false, 906) == -1 && GridSearch.FindNext(grid, "hedef", "Ad", false, 6) == 105);
        ((Binding)((DataGridBoundColumn)grid.Columns[1]).Binding).StringFormat = "{bozuk";
        Check("arama bozuk StringFormat ham deger", GridSearch.FindNext(grid, "0.12345", "Deger", false, 0) == 0);
        Check("arama bool tarih", GridSearch.FindNext(grid, "True", "Etkin", true, 0) == 0 && GridSearch.FindNext(grid, "01.01.2026", "Tarih", true, 0) == 0);
        var table = new DataTable();
        table.Columns.Add("Deger", typeof(double));
        table.Rows.Add(12.3456);
        grid.ItemsSource = table.DefaultView;
        Check("arama DataRowView", GridSearch.FindNext(grid, "12.3456", "Deger", false, 0) == 0 && GridSearch.FindAll(grid, "12", null, false).Count == 1);
    }

    // 1.2.0: veri kaynağı sözleşmesi + VirtualSourceList (yer tutucu, önbellek, 64 bit sıra).
    private static async Task RunSourceContract()
    {
        var fast = new SahteKaynak(5_000, 0);
        using (var list = new VirtualSourceList(fast))
        {
            Check("kucuk kaynak pencereli degil", !list.IsWindowed && list.Count == 5_000 && list.WindowStart == 0);
            var row = (IVirtualRow)list[4_321]!;
            Check("hizli kaynak beklenir, yer tutucu yok", row.IsLoaded && Convert.ToInt64(row["No"]) == 4_321 && row.Key == SahteKaynak.KeyOf(4_321));
            Check("sutun adi buyuk/kucuk harf duyarsiz", (string?)row["ad"] == "Kayıt 4321");
            Check("esitlik gorsel sira", list[4_321]!.Equals(row) && list.IndexOf(row) == 4_321 && !list[4_320]!.Equals(row));
            Check("ITypedList sutunlari", ((ITypedList)list).GetItemProperties(null).Count == 6);
            int reads = fast.Reads;
            _ = list[4_322];
            Check("ayni sayfa yeniden okunmaz", fast.Reads == reads);
        }
        var slow = new SahteKaynak(10_000, 600);
        using (var list = new VirtualSourceList(slow))
        {
            var row = (VirtualSourceRow)list[7]!;
            bool notified = false;
            row.PropertyChanged += (_, e) => notified |= e.PropertyName == "";
            Check("yavas kaynakta yer tutucu doner", !row.IsLoaded && row["No"] == null && row.Ordinal == 7);
            var timer = Stopwatch.StartNew();
            for (int i = 8; i < 48; i++) _ = list[i];
            timer.Stop();
            Check("ayni sayfanin oteki satirlari yeniden beklemez (40 satir)", timer.ElapsedMilliseconds < 200, $"{timer.ElapsedMilliseconds} ms");
            await Until(() => row.IsLoaded);
            Check("sayfa gelince yer tutucu dolar ve bildirir", row.IsLoaded && Convert.ToInt64(row["No"]) == 7 && notified);
        }
        var growing = new SahteKaynak(1_000, 0);
        using (var list = new VirtualSourceList(growing))
        {
            int resets = 0;
            list.CollectionChanged += (_, e) => resets += e.Action == NotifyCollectionChangedAction.Reset ? 1 : 0;
            growing.SetCount(2_500);
            await Until(() => resets > 0);
            Check("kaynak sayisi degisince tablo bastan yuklenir", resets == 1 && list.Count == 2_500 && Convert.ToInt64(((IVirtualRow)list[2_400]!)["No"]) == 2_400);
        }
        var broken = new SahteKaynak(3_000, 0) { Fail = true };
        using (var list = new VirtualSourceList(broken))
        {
            object? row = null; bool threw = false;
            try { row = list[10]; } catch { threw = true; }
            Check("hatali kaynak istisna firlatmaz, bos satir", !threw && row is IVirtualRow r && !r.IsLoaded);
        }
        var huge = new SahteKaynak(20_000_000_000, 0);
        using (var list = new VirtualSourceList(huge))
        {
            Check("20 milyar satir pencereli", list.IsWindowed && list.TotalCount == 20_000_000_000 && list.Count == list.WindowSize);
            var last = list.GetRow(19_999_999_999);
            Check("64 bit sira okunur", last.IsLoaded && Convert.ToInt64(last["No"]) == 19_999_999_999 && last.Key == SahteKaynak.KeyOf(19_999_999_999));
            Check("pencere sona kirpilir", list.MoveWindow(long.MaxValue) && list.WindowStart == 20_000_000_000 - list.WindowSize);
            Check("pencere ici indeks", list.WindowIndexOf(19_999_999_999) == list.WindowSize - 1 && list.WindowIndexOf(0) == -1);
            Check("pencere basa kirpilir", list.MoveWindow(-5) && list.WindowStart == 0 && !list.MoveWindow(0));
            long seek = await huge.SeekAsync(0.5, CancellationToken.None);
            Check("kesin kaynakta SeekAsync", seek == 9_999_999_999);
        }
    }

    // Pencereli kip: eşik küçültülerek (50 000 satır, pencere 4 000) arayüzde sınanır; 20 milyarla aynı yol.
    private static async Task RunWindowedGrid()
    {
        var source = new SahteKaynak(50_000, 0);
        var list = new VirtualSourceList(source, new VirtualSourceListOptions { WindowThreshold = 10_000, WindowSize = 4_000 });
        var grid = MakeGrid(list, false);
        VirtualizingPanel.SetScrollUnit(grid, ScrollUnit.Item);
        var (window, decorator) = Show(grid, "VirtualADGV 1.2.0 — pencereli kip");
        await Settle(150);
        async Task Moved() { await Until(() => !grid.WindowRestorePendingForTest); }
        Check("pencereli kipte 64 bit kaydirma cubugu gorunur", grid.IsWindowedSource && grid.WindowScrollBarForTest?.IsVisible == true);
        Check("pencereli kipte mini harita gizli", grid.Minimap?.Visibility != Visibility.Visible);
        Check("ilk gorunen satir 0", grid.FirstVisibleOrdinal == 0);
        grid.ScrollToOrdinal(30_000);
        await Moved();
        long first = grid.FirstVisibleOrdinal;
        Check("pencere disi siraya kaydirma", list.WindowStart > 0 && first == 30_000, $"ilk={first} pencere={list.WindowStart}");
        var row = (IVirtualRow)list[list.WindowIndexOf(30_002)]!;
        SelectCell(grid, row);
        await Settle();
        grid.ScrollToOrdinal(1_000);
        await Moved();
        Check("geri kaydirma", grid.FirstVisibleOrdinal == 1_000 && list.WindowIndexOf(30_002) < 0, $"ilk={grid.FirstVisibleOrdinal}");
        grid.ScrollToOrdinal(30_000);
        await Moved();
        bool kept = grid.SelectedCells.Count == 1 && grid.SelectedCells[0].Item is IVirtualRow sel && sel.Ordinal == 30_002;
        Check("pencere kayinca secim gorsel siraya gore korunur", kept, $"secili={grid.SelectedCells.Count}");
        var bar = grid.WindowScrollBarForTest!;
        Check("64 bit cubuk konumu", Near(bar.Value, 30_000, 0.5) && Near(bar.Maximum, 50_000 - bar.ViewportSize, 0.5), $"deger={bar.Value} en={bar.Maximum}");
        // Geri koyma bitmeden ikinci kayma: saklanan seçim kaybolmamalı.
        grid.ScrollToOrdinal(10_000);
        grid.ScrollToOrdinal(20_000);
        await Moved();
        grid.ScrollToOrdinal(30_000);
        await Moved();
        bool keptTwice = grid.SelectedCells.Count == 1 && grid.SelectedCells[0].Item is IVirtualRow sel2 && sel2.Ordinal == 30_002;
        Check("geri koyma bitmeden art arda kayma secimi korur", keptTwice, $"secili={grid.SelectedCells.Count}");
        grid.OnWindowScrollBar(49_000);
        await Moved();
        Check("cubuktan sona atlama", grid.FirstVisibleOrdinal >= 49_000 - 30 && list.WindowStart + list.Count == 50_000, $"ilk={grid.FirstVisibleOrdinal}");
        var viewer = grid.OverlayScrollViewer!;
        viewer.ScrollToVerticalOffset(0);
        await Settle(100);
        await Moved();
        Check("pencere kenarinda kendiliginden kayar", list.WindowStart < 46_000 && grid.FirstVisibleOrdinal > 0, $"pencere={list.WindowStart} ilk={grid.FirstVisibleOrdinal}");
        var native = viewer.Template.FindName("PART_VerticalScrollBar", viewer) as ScrollBar;
        Check("64 bit cubuk tablonun kendi cubugunun ustunde (icerigi ortmez)", grid.VerticalScrollBarVisibility == ScrollBarVisibility.Visible
            && native != null && Near(grid.WindowScrollBarForTest!.ActualWidth, native.ActualWidth, 0.5) && native.ActualWidth > 0
            && Near(grid.WindowScrollBarForTest.TranslatePoint(new Point(0, 0), native).X, 0, 0.5), $"cubuk={grid.WindowScrollBarForTest?.ActualWidth} kendi={native?.ActualWidth}");
        SaveShot(decorator, "adgv_pencereli.png");
        grid.ItemsSource = Rows(200);
        await Settle(150);
        Check("pencereli olmayan kaynaga donunce katman kalkar, cubuk ayari geri gelir",
            !grid.IsWindowedSource && grid.WindowScrollBarForTest == null &&
            grid.ReadLocalValue(DataGrid.VerticalScrollBarVisibilityProperty) == DependencyProperty.UnsetValue);
        window.Close();
        list.Dispose();
    }

    private static async Task RunPrefetch(bool itemScroll)
    {
        string tag = itemScroll ? "on yukleme Item " : "on yukleme Pixel ";
        var source = new OnYuklemeliListe(10_000);
        var grid = MakeGrid(source, false);
        VirtualizingPanel.SetScrollUnit(grid, itemScroll ? ScrollUnit.Item : ScrollUnit.Pixel);
        var (window, decorator) = Show(grid, "VirtualADGV — ertelenmis kaydirma");
        await Settle();
        var map = grid.Minimap!;
        var viewer = grid.OverlayScrollViewer!;
        double Y(double ratio) => map.MapBounds.Top + map.MapBounds.Height * ratio;
        double height = itemScroll ? 1 : grid.RowHeight;
        double before = viewer.VerticalOffset;
        double target = map.TargetOffset(0.5);
        map.BeginDrag(Y(0.5));
        map.EndDrag();
        var first = source.Requests.Single();
        Check(tag + "mini harita birakma once dogru araligi yukler", first.Start == (int)Math.Floor(target / height)
            && first.Count == 2 * (int)Math.Ceiling(viewer.ViewportHeight / height));
        await Settle();
        Check(tag + "mini harita yukleme bitmeden kaydirmaz", viewer.VerticalOffset == before);
        first.Completion.SetResult(true);
        Check(tag + "mini harita yukleme sonrasi kaydirir", await Until(() => Near(viewer.VerticalOffset, target)));

        before = viewer.VerticalOffset;
        map.ClickAt(Y(0.2));
        var stale = source.Requests.Last();
        map.ClickAt(Y(0.8));
        var latest = source.Requests.Last();
        Check(tag + "yeni hedef onceki beklemeyi iptal eder", stale.Token.IsCancellationRequested && !latest.Token.IsCancellationRequested);
        // Sahte kaynak iptali bilerek dinlemez: geç tamamlanan eski sonuç yine de kaydıramaz.
        stale.Completion.SetResult(true);
        await Settle();
        Check(tag + "iptal edilmis sonuc kaydirmaz", viewer.VerticalOffset == before);
        target = map.TargetOffset(0.8);
        latest.Completion.SetResult(true);
        Check(tag + "son hedef kazanir", await Until(() => Near(viewer.VerticalOffset, target)));

        int calls = source.Requests.Count;
        before = viewer.VerticalOffset;
        map.BeginDrag(Y(0.4));
        Check(tag + "mini harita duraklama yuklemeyi baslatir", await Until(() => source.Requests.Count > calls));
        stale = source.Requests.Last();
        map.MoveDrag(Y(0.6));
        Check(tag + "mini harita hareketi bekleyen duraklamayi iptal eder", stale.Token.IsCancellationRequested);
        map.EndDrag();
        latest = source.Requests.Last();
        stale.Completion.SetResult(true);
        await Settle();
        Check(tag + "eski duraklama kaydirmaz", viewer.VerticalOffset == before);
        target = map.TargetOffset(0.6);
        latest.Completion.SetResult(true);
        Check(tag + "mini harita birakma son araliga gider", await Until(() => Near(viewer.VerticalOffset, target)));

        var bar = (ScrollBar)viewer.Template.FindName("PART_VerticalScrollBar", viewer);
        var track = VirtualDataGrid.VisualDescendants<Track>(bar).Single();
        var thumb = track.Thumb;
        before = viewer.VerticalOffset;
        calls = source.Requests.Count;
        thumb.RaiseEvent(new DragStartedEventArgs(0, 0) { RoutedEvent = Thumb.DragStartedEvent });
        double delta = track.ValueFromDistance(0, -40);
        target = Math.Clamp(before + delta, bar.Minimum, bar.Maximum);
        thumb.RaiseEvent(new DragDeltaEventArgs(0, -40) { RoutedEvent = Thumb.DragDeltaEvent });
        Check(tag + "DragDelta tutamaci tasir icerigi tasimaz", Near(bar.Value, target) && viewer.VerticalOffset == before);
        var hint = typeof(VirtualDataGrid).GetField("_thumbHint", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!.GetValue(grid) as ToolTip;
        string expectedHint = string.Format(CultureInfo.CurrentCulture, grid.Strings.MinimapRowFormat, (long)Math.Floor(target / height) + 1);
        Check(tag + "tutamac hedef satir ipucu", hint?.IsOpen == true && Equals(hint.Content, expectedHint));
        await Settle(35);
        Check(tag + "DragDelta 100 ms oncesi sorgu ve kaydirma yok", viewer.VerticalOffset == before && source.Requests.Count == calls);
        thumb.RaiseEvent(new DragCompletedEventArgs(0, -40, false) { RoutedEvent = Thumb.DragCompletedEvent });
        latest = source.Requests.Last();
        Check(tag + "DragCompleted dogru araligi yukler", source.Requests.Count == calls + 1 && latest.Start == (int)Math.Floor(target / height)
            && latest.Count == 2 * (int)Math.Ceiling(viewer.ViewportHeight / height) && hint?.IsOpen == false);
        await Settle();
        Check(tag + "DragCompleted yukleme bitmeden kaydirmaz", viewer.VerticalOffset == before);
        latest.Completion.SetResult(true);
        Check(tag + "DragCompleted yukleme sonrasi hedefe gider", await Until(() => Near(viewer.VerticalOffset, target)));

        before = viewer.VerticalOffset;
        calls = source.Requests.Count;
        thumb.RaiseEvent(new DragStartedEventArgs(0, 0) { RoutedEvent = Thumb.DragStartedEvent });
        target = Math.Clamp(before + track.ValueFromDistance(0, 20), bar.Minimum, bar.Maximum);
        thumb.RaiseEvent(new DragDeltaEventArgs(0, 20) { RoutedEvent = Thumb.DragDeltaEvent });
        Check(tag + "tutamac 100 ms duraklamada once yukler", await Until(() => source.Requests.Count > calls) && viewer.VerticalOffset == before);
        source.Requests.Last().Completion.SetResult(true);
        Check(tag + "tutamac duraklama sonrasi hedefe gider", await Until(() => Near(viewer.VerticalOffset, target)));
        thumb.RaiseEvent(new DragCompletedEventArgs(0, 20, false) { RoutedEvent = Thumb.DragCompletedEvent });
        Check(tag + "ayni hedefte birakma ikinci yukleme baslatmaz", source.Requests.Count == calls + 1);

        target = map.TargetOffset(0.3);
        map.ClickAt(Y(0.3));
        source.Requests.Last().Completion.SetException(new InvalidOperationException("sahte ön yükleme hatası"));
        Check(tag + "on yukleme hatasi senkron yedege duser", await Until(() => Near(viewer.VerticalOffset, target)));
        target = map.TargetOffset(0.7);
        map.ClickAt(Y(0.7));
        source.Requests.Last().Completion.SetCanceled();
        Check(tag + "kaynagin kendi iptali senkron yedege duser", await Until(() => Near(viewer.VerticalOffset, target)));

        source.Ready = true;
        target = map.TargetOffset(0.9);
        map.ClickAt(Y(0.9));
        grid.UpdateLayout();
        Check(tag + "hazir kaynak senkron kaydirir", Near(viewer.VerticalOffset, target));
        source.Ready = false;
        map.ClickAt(Y(0.1));
        stale = source.Requests.Last();
        var columns = grid.Columns.ToArray();
        grid.ItemsSource = Rows(10_000);
        await Settle();
        before = viewer.VerticalOffset;
        stale.Completion.SetResult(true);
        await Settle();
        Check(tag + "kaynak degisince eski yukleme kaydirmaz", stale.Token.IsCancellationRequested && viewer.VerticalOffset == before);
        Check(tag + "kaynak degisince sutun nesneleri korunur", grid.Columns.SequenceEqual(columns));
        target = map.TargetOffset(0.5);
        map.ClickAt(Y(0.5));
        grid.UpdateLayout();
        Check(tag + "arayuzsuz kaynak mini haritada hemen kayar", Near(viewer.VerticalOffset, target));
        window.Close();
    }

    private sealed class OnYuklemeIstegi
    {
        internal OnYuklemeIstegi(int start, int count, CancellationToken token) { Start = start; Count = count; Token = token; }
        internal readonly int Start, Count;
        internal readonly CancellationToken Token;
        internal readonly TaskCompletionSource<bool> Completion = new(TaskCreationOptions.RunContinuationsAsynchronously);
    }

    private sealed class OnYuklemeliListe : SanalListe, IVirtualPrefetchList
    {
        internal OnYuklemeliListe(int count) : base(count) { }
        internal readonly List<OnYuklemeIstegi> Requests = new();
        internal bool Ready;
        public Task PrefetchAsync(int start, int count, CancellationToken cancellationToken)
        {
            var request = new OnYuklemeIstegi(start, count, cancellationToken);
            Requests.Add(request);
            return Ready ? Task.CompletedTask : request.Completion.Task;
        }
    }

    private sealed class SahteKaynak : IVirtualTableSource
    {
        private readonly int _delay;
        public SahteKaynak(long count, int delayMs) { Count = count; _delay = delayMs; }
        public event EventHandler? CountChanged;
        public void SetCount(long count) { Count = count; CountChanged?.Invoke(this, EventArgs.Empty); }
        public void Dispose() { }
        public static long KeyOf(long ordinal) => 1_000_000_000_000L + ordinal;
        public int Reads;
        public bool Fail;
        public long Count { get; private set; }
        public bool IsCountExact => true;
        public bool SupportsExactOrdinals => true;
        public IReadOnlyList<VirtualColumnInfo> Columns { get; } = new[]
        {
            new VirtualColumnInfo("No", typeof(long)), new VirtualColumnInfo("Deger", typeof(double)), new VirtualColumnInfo("Ad", typeof(string)),
            new VirtualColumnInfo("Etkin", typeof(bool)), new VirtualColumnInfo("Tarih", typeof(DateTime)), new VirtualColumnInfo("Aciklama", typeof(string))
        };
        public async Task<VirtualRowPage> GetPageAsync(long start, int count, CancellationToken ct)
        {
            Interlocked.Increment(ref Reads);
            if (_delay > 0) await Task.Delay(_delay, ct);
            if (Fail) throw new InvalidOperationException("sahte hata");
            var keys = new long[count];
            var values = new object?[count][];
            for (int i = 0; i < count; i++)
            {
                long n = start + i;
                keys[i] = KeyOf(n);
                values[i] = new object?[] { n, n + 0.12345, "Kayıt " + n, n % 2 == 0, new DateTime(2026, 1, 1).AddMinutes(n % 1_000_000), "IF örnek " + n };
            }
            return new VirtualRowPage(start, keys, values);
        }
        public Task<long[]> GetOrdinalsAsync(IReadOnlyCollection<long> keys, int limit, CancellationToken ct)
            => Task.FromResult(keys.Select(k => k - KeyOf(0)).Where(o => o >= 0 && o < Count).OrderBy(o => o).Take(limit).ToArray());
        public Task<long[]> FindAsync(VirtualSearchRequest request, long start, int limit, CancellationToken ct) => Task.FromResult(Array.Empty<long>());
        public Task<long> SeekAsync(double fraction, CancellationToken ct) => Task.FromResult((long)Math.Floor(fraction * (Count - 1)));
    }

    private static void SaveShot(AdornerDecorator decorator, string name)
    {
        decorator.UpdateLayout();
        var bitmap = new RenderTargetBitmap((int)Math.Ceiling(decorator.ActualWidth), (int)Math.Ceiling(decorator.ActualHeight), 96, 96, PixelFormats.Pbgra32);
        bitmap.Render(decorator);
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(bitmap));
        string path = Path.Combine(_shots, name);
        using (var stream = File.Create(path)) encoder.Save(stream);
        Check("ekran goruntusu " + name, File.Exists(path) && new FileInfo(path).Length > 1000);
    }

    private sealed class Satir
    {
        public Satir(int no) { No = no; }
        public int No { get; }
        public double Deger => No + 0.12345;
        public string Ad => No % 100 == 5 ? "hedef " + No : "Kayıt " + No;
        public bool Etkin => No % 2 == 0;
        public DateTime Tarih => new DateTime(2026, 1, 1).AddMinutes(No);
        public string Aciklama => "IF örnek " + No;
        public override bool Equals(object? obj) => obj is Satir row && row.No == No;
        public override int GetHashCode() => No;
    }

    private class SanalListe : IList, INotifyCollectionChanged
    {
        public SanalListe(int count) { Count = count; }
        public int Reads { get; private set; }
        public int Count { get; }
        public object? this[int index] { get { Reads++; return new Satir(index); } set => throw new NotSupportedException(); }
        public bool IsFixedSize => true;
        public bool IsReadOnly => true;
        public bool IsSynchronized => false;
        public object SyncRoot => this;
        public event NotifyCollectionChangedEventHandler? CollectionChanged { add { } remove { } }
        public int IndexOf(object? value) => value is Satir row && row.No >= 0 && row.No < Count ? row.No : -1;
        public bool Contains(object? value) => IndexOf(value) >= 0;
        public IEnumerator GetEnumerator() { for (int i = 0; i < Count; i++) yield return this[i]; }
        public void CopyTo(Array array, int index) => throw new NotSupportedException();
        public int Add(object? value) => throw new NotSupportedException();
        public void Clear() => throw new NotSupportedException();
        public void Insert(int index, object? value) => throw new NotSupportedException();
        public void Remove(object? value) => throw new NotSupportedException();
        public void RemoveAt(int index) => throw new NotSupportedException();
    }
}
