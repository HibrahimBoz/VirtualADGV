# VirtualADGV.WPF

> **English** | [Türkçe](#türkçe)

An advanced, high-performance WPF DataGrid control with Excel-like filtering, sorting, search, a crosshair highlight, a VS Code–style minimap and full dark/light theme support. Designed for large datasets (1M+ rows).

---

## What's new in 1.1.0

- **Crosshair highlight** — when a cell is selected, its whole row and whole column are tinted with a faint color across the grid.
- **Minimap** — a translucent overview of the entire table in the top-right corner. It shows marked rows, selected rows and search hits in separate lanes, plus the current viewport. Click or drag on it to scroll. It never reads row contents, so it works on virtualized sources with millions of rows (measured: 3–4 ms per frame for 1,000,000 rows and 200,000 marks).
- **Built-in in-memory search** — if nothing handles `SearchRequested`, `Ctrl+F` searches in-memory sources (`DataView`, arrays, `List<T>`, `Collection<T>`, and an `ICollectionView` over them, up to 200,000 rows) by itself. The result appears in the search bar ("N matches" / "No matches found.").
- Both overlays are WPF `Adorner`s, independent of your cell styles and control templates.
- **Fixes:** clicking the filter button of the empty filler header (right of the last column) no longer throws; the top-left corner button no longer causes a layout loop; Ctrl+A / column double-click no longer call `Items.Refresh()` (which reset scroll position on large virtual sources).

---

## Features

### Filtering
- **Excel-style filter popup** on every column header
- **Checkbox list** for text/numeric columns — select individual values
- **Hierarchical tree view** for date columns — group by Year → Month → Day
- **Custom filter dialog** — build compound conditions (e.g., `> 100 AND < 500`)
- **Cascading filter (Excel-style)** — values that no longer exist under the other columns' active filters are shown **disabled** via `LoadingFilterEventArgs.EnabledValues`
- **Operators:**
  - Text: equals, does not equal, begins with, ends with, contains, does not contain
  - Number: equals, does not equal, greater than, greater/less than or equal, between
  - Date: equals, before, after, before/after or equal
- Filter indicator on column header; one-click clear filter

### Sorting
- Ascending / Descending sort per column via the filter popup
- Sort direction indicator on column header; one-click clear sort

### Search (Ctrl+F)
- Collapsible search panel toggled with `Ctrl+F`
- Search in all columns or one column; case-sensitive option
- Find next / restart from the beginning; the search does not wrap
- **Built-in for in-memory sources** (1.1.0) — or handle `SearchRequested` yourself (e.g., database-backed grids)

### Crosshair highlight (1.1.0)
- Row strip and column strip for the selected cell(s), drawn on an overlay; the colors under it stay visible
- Full-row selection draws only the row strip
- Above 50,000 selected cells only column strips are drawn
- Cleared when the source is reset (filter / sort change)

### Minimap (1.1.0)
- Top-right, translucent; more opaque on mouse-over; hidden when there is no vertical scroll bar
- Lanes: marked rows (`SetMinimapMarks`) · selected rows · search hits (`SetMinimapSearchHits`) · viewport frame
- Click: the target row is centered. Drag: the frame and a "Row: N" hint follow the mouse; the grid scrolls when you pause (100 ms) or release. Clicking a mark jumps to the first marked row in that pixel; the selection does not change
- The mouse wheel passes through to the grid
- Positions are **zero-based visual ordinals** (after filter and sort). Push them again after the view changes

### Theming
- Full **Dark** and **Light** mode; switched at runtime via `SetTheme(isDarkMode: bool)`
- All colors come from `DynamicResource` keys — compatible with custom themes (see [Theme keys](#theme-keys))

### Performance
- Built on WPF's row/column virtualization
- `LoadingFilterValuesAsync` — async value loading keeps the UI responsive
- Overlays scale with the number of visible rows and marks, not with the total row count

### Localization
- All UI strings are configurable via `grid.Strings.*`; default is **English**

---

## Installation

```bash
dotnet add package VirtualADGV.WPF --version 1.1.0
```

Or in your project file:

```xml
<PackageReference Include="VirtualADGV.WPF" Version="1.1.0" />
```

---

## Usage

### 1. Add the XML namespace

```xml
xmlns:adgv="clr-namespace:VirtualADGV.WPF;assembly=VirtualADGV.WPF"
```

### 2. Declare the grid and the filter popup

```xml
<!-- Filter popup (shared by all column headers) -->
<Popup x:Name="GlobalFilterPopup" StaysOpen="False" Placement="Bottom">
    <adgv:AdvancedCustomFilterPopup x:Name="FilterControl" Width="240" />
</Popup>

<!-- The grid must be inside an AdornerDecorator (a Window already has one) for the overlays to appear -->
<adgv:VirtualDataGrid x:Name="MyGrid"
    ItemsSource="{Binding MyDataView}"
    FilterPopup="{Binding ElementName=GlobalFilterPopup}"
    IsCrosshairEnabled="True"
    IsMinimapVisible="True" />
```

### 3. Handle filter and sort events

```csharp
MyGrid.LoadingFilterValuesAsync = async (sender, args) =>
{
    args.DistinctValues = await GetDistinctValuesAsync(args.ColumnName);
    args.ActiveFilters  = GetActiveFilters(args.ColumnName);      // shown checked
    args.EnabledValues  = await GetEnabledValuesAsync(args.ColumnName); // optional, cascading filter
};

MyGrid.FilterChanged += (sender, args) =>
    ApplyFilter(args.ColumnName, args.FilterString);   // SQL-like condition, null = cleared

MyGrid.SortChanged += (sender, args) =>
    ApplySort(args.ColumnName, args.SortDirection);    // "ASC", "DESC" or "NONE"
```

### 4. Search

In-memory sources need no code: `Ctrl+F` works out of the box. For a database-backed or paged source, handle the event yourself (the built-in search is then not used):

```csharp
MyGrid.SearchRequested += async (sender, args) =>
{
    long row = await FindNextRowAsync(args.SearchText, args.ColumnName, args.IsCaseSensitive, args.SearchNext);
    // select / scroll to the row ...
    MyGrid.SetMinimapSearchHits(await FindAllRowOrdinalsAsync(args.SearchText, args.ColumnName, args.IsCaseSensitive));
};
```

`GridSearch.FindNext(...)` and `GridSearch.FindAll(...)` are also public helpers for in-memory grids.

### 5. Minimap marks (e.g., rows highlighted by navigation)

```csharp
// Zero-based visual ordinals after the current filter and sort, on the UI thread.
MyGrid.SetMinimapMarks(new long[] { 120, 4_500, 98_000 });
MyGrid.SetMinimapMarks(null); // clear

// Virtual / paged source: tell the grid the visual ordinal of a row object in O(1),
// so the selection lane does not enumerate the source.
MyGrid.OrdinalResolver = item => myPagedList.IndexOf(item) is int i && i >= 0 ? i : null;
```

Without a resolver, the grid builds an item→ordinal map once for sources up to 200,000 rows; for larger sources the selection lane is not drawn.

### 6. Theme

```csharp
MyGrid.SetTheme(isDarkMode: true);
```

### 7. Localization (Turkish example)

```csharp
MyGrid.Strings.AllColumns             = "Tüm Sütunlar";
MyGrid.Strings.Apply                  = "Filtrele";
MyGrid.Strings.Cancel                 = "İptal";
MyGrid.Strings.SelectAll              = "(Tümünü Seç)";
MyGrid.Strings.SearchNoMatch          = "Eşleşme bulunamadı.";
MyGrid.Strings.SearchEndReached       = "Eşleşme bulunamadı (tablonun sonuna gelindi; «Baştan Ara» ile yeniden arayın).";
MyGrid.Strings.SearchMatchCountFormat = "{0:N0} eşleşme";
MyGrid.Strings.MinimapRowFormat       = "Satır: {0:N0}";
// ... see VirtualDataGridStrings for all properties
```

---

## API Reference

### `VirtualDataGrid`

| Member | Type | Description |
|--------|------|-------------|
| `FilterPopup` | `Popup` | The popup used for column filtering |
| `IsSearchVisible` | `bool` | Show/hide the search panel |
| `Strings` | `VirtualDataGridStrings` | Localization strings |
| `LoadingFilterValuesAsync` | `Func<object, LoadingFilterEventArgs, Task>` | Async filter value loader |
| `FilterChanged` | `EventHandler<FilterEventArgs>` | Fired when a filter is applied or cleared |
| `SortChanged` | `EventHandler<SortEventArgs>` | Fired when the sort direction changes |
| `SearchRequested` | `EventHandler<SearchEventArgs>` | Fired when a search is executed; if nobody handles it, the built-in in-memory search runs |
| `IsCrosshairEnabled` | `bool` (DP, default `true`) | Row + column strips for the selected cell(s) |
| `CrosshairBrush` | `Brush?` (DP) | Strip brush; `null` = `ThemeCrosshairBg` resource or built-in default |
| `IsMinimapVisible` | `bool` (DP, default `true`) | Show/hide the minimap |
| `MinimapWidth` / `MinimapHeight` | `double` (DP, 60 / 180) | Minimap size in pixels |
| `SetMinimapMarks(IReadOnlyList<long>?)` | method | Marked rows (zero-based visual ordinals); `null` clears |
| `SetMinimapSearchHits(IReadOnlyList<long>?)` | method | Search-hit rows (zero-based visual ordinals); `null` clears |
| `OrdinalResolver` | `Func<object, long?>?` | Visual ordinal of a row object (for virtual sources) |
| `SetTheme(bool)` | method | Switch Dark/Light theme |
| `UpdateSearchColumns()` | method | Refresh the search column dropdown |
| `SetColumnFilterEnabled(int, bool)` | method | Enable/disable filter per column |
| `SetColumnSortEnabled(int, bool)` | method | Enable/disable sort per column |

### `GridSearch` (static)

| Member | Description |
|--------|-------------|
| `FindNext(DataGrid, text, columnHeader, caseSensitive, startIndex)` | Index of the next matching visible row, or `-1` |
| `FindAll(DataGrid, text, columnHeader, caseSensitive)` | Indices of all matching visible rows |

Matching: "contains", on the displayed text (honors `Binding.StringFormat`) or the raw invariant value; case-insensitive comparison is culture-invariant.

### `LoadingFilterEventArgs`

| Member | Type | Description |
|--------|------|-------------|
| `ColumnName` | `string` | The column whose filter popup is opening |
| `ColumnType` | `Type` | Column data type (drives numeric/date behavior) |
| `DistinctValues` | `IEnumerable<string>` | Full distinct value list to display |
| `ActiveFilters` | `IEnumerable<string>` | Values currently selected (shown checked) |
| `EnabledValues` | `ISet<string>?` | Values still present under other columns' filters; others are shown disabled. `null` = all enabled |

### Attached Properties

| Property | Target | Description |
|----------|--------|-------------|
| `IsHighlighted` | Column / Row | Visual highlight (e.g., on double-click) |
| `IsColumnFiltered` | Column | Shows the filter indicator on the header |
| `ColumnSortDirection` | Column | `"ASC"`, `"DESC"` or `"NONE"` |
| `IsFilterEnabled` | Column | Allow the filter popup on this column |
| `IsSortEnabled` | Column | Allow sorting on this column |

### Theme keys

The control reads these brushes with `DynamicResource` / `TryFindResource`. Define them in your application resources; if a key is missing, a built-in default is used.

| Key | Used for |
|-----|----------|
| `ThemeGridBg`, `ThemeGridAltBg`, `ThemeGridLine`, `ThemeText`, `ThemeSubtleText`, `ThemeSurface`, `ThemeBorder`, `ThemeAccent` | Grid, popup and search bar |
| `ThemeHighlightBg` | Marked rows (grid and minimap) |
| `ThemeCrosshairBg` (1.1.0) | Crosshair strips — use a translucent color |
| `ThemeSearchHitBg` (1.1.0) | Search hits on the minimap |

---

## Requirements

- .NET 8.0 (Windows), WPF
- `ItemsSource` as `System.Data.DataView` enables automatic column type detection in the filter popup
- The overlays need an `AdornerLayer` (any `Window` provides one)

## License

MIT

---

---

# Türkçe

> [English](#virtualadgvwpf) | **Türkçe**

Büyük veri setleri (1M+ satır) için Excel benzeri filtreleme, sıralama, arama, çapraz vurgu, VS Code tarzı mini harita ve tam koyu/açık tema desteği sunan gelişmiş WPF DataGrid kontrolü.

---

## 1.1.0'da yenilikler

- **Çapraz vurgu** — bir hücre seçilince o hücrenin satırı ve sütunu tablo boyunca soluk renkle boyanır.
- **Mini harita** — sağ üstte tablonun tamamının saydam özeti. İşaretli satırları, seçili satırları ve arama eşleşmelerini ayrı şeritlerde, ekrandaki bölümü de çerçeveyle gösterir. Tıklayarak ya da sürükleyerek kaydırılır. Satır içeriğini okumaz; bu yüzden milyonlarca satırlık sanal kaynaklarda da çalışır (ölçüldü: 1.000.000 satır ve 200.000 işarette kare başına 3–4 ms).
- **Yerleşik bellek içi arama** — `SearchRequested` olayını işleyen yoksa `Ctrl+F`, bellek içi kaynaklarda (`DataView`, diziler, `List<T>`, `Collection<T>` ve bunları saran `ICollectionView`, en çok 200.000 satır) aramayı kendisi yapar. Sonuç arama çubuğunda yazar ("N eşleşme" / "Eşleşme bulunamadı.").
- İki katman da WPF `Adorner`'ıdır; hücre stillerinizden ve şablonlarınızdan bağımsızdır.
- **Düzeltmeler:** son sütunun sağındaki boş başlıktaki huni düğmesi artık istisna fırlatmaz; sol üst köşe düğmesi artık yerleşim döngüsü üretmez; Ctrl+A ve sütuna çift tık artık `Items.Refresh()` çağırmaz (büyük sanal kaynakta kaydırma konumunu sıfırlıyordu).

## Özellikler

- **Filtreleme:** her sütunda Excel tarzı huni; değer listesi, tarih ağacı, özel koşul penceresi, kademeli filtre (`EnabledValues`).
- **Sıralama:** huni penceresinden artan / azalan; başlıkta yön göstergesi.
- **Arama (Ctrl+F):** tüm sütunlarda ya da bir sütunda; büyük/küçük harf seçeneği; sonraki / baştan ara; sarma yok. Bellek içi kaynakta yerleşik; veritabanı kaynağında `SearchRequested` ile.
- **Çapraz vurgu:** seçili hücrenin satır ve sütun şeridi; tam satır seçiminde yalnız satır; 50.000 hücrenin üstünde yalnız sütun; süzgeç ya da sıralama değişince temizlenir.
- **Mini harita:** sağ üstte, saydam; fare üstündeyken daha belirgin; dikey kaydırma çubuğu yoksa gizli. Şeritler: işaretler · seçim · arama · görünüm çerçevesi. Tık: hedef satır ortaya gelir. Sürükleme: 100 ms durunca ya da bırakınca tablo kayar. Tekerlek tabloya geçer. Sıralar **sıfır tabanlı görsel sıradır** (süzgeç ve sıralama sonrası); görünüm değişince yeniden gönderin.
- **Tema:** `SetTheme(isDarkMode)`; bütün renkler `DynamicResource` anahtarlarından (yukarıdaki *Theme keys* tablosu).

## Kurulum

```bash
dotnet add package VirtualADGV.WPF --version 1.1.0
```

## Kullanım

Kod örnekleri yukarıdaki İngilizce *Usage* bölümündedir. Kısa özet:

```csharp
// Mini harita işaretleri (sıfır tabanlı görsel sıra, UI iş parçacığında)
MyGrid.SetMinimapMarks(new long[] { 120, 4_500, 98_000 });
// Sanal / sayfalı kaynakta satırın görsel sırası O(1)
MyGrid.OrdinalResolver = item => sayfaliListe.IndexOf(item) is int i && i >= 0 ? i : null;
// Veritabanı kaynağında arama eşleşmeleri
MyGrid.SetMinimapSearchHits(eslesmeSiralari);
// Türkçe metinler
MyGrid.Strings.SearchNoMatch    = "Eşleşme bulunamadı.";
MyGrid.Strings.MinimapRowFormat = "Satır: {0:N0}";
```

## Lisans

MIT
