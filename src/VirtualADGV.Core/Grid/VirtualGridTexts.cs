namespace VirtualADGV.Grid;

/// <summary>Customizable English defaults shared by grid adapters.</summary>
public class VirtualGridTexts
{
    /// <summary>Label for searching all columns.</summary>
    public string AllColumns { get; set; } = "All Columns";
    /// <summary>Search box placeholder.</summary>
    public string SearchPlaceholder { get; set; } = "Search...";
    /// <summary>Search box label.</summary>
    public string SearchLabel { get; set; } = "Search:";
    /// <summary>Column selector label.</summary>
    public string ColumnLabel { get; set; } = "Column:";
    /// <summary>Case sensitivity label.</summary>
    public string CaseSensitive { get; set; } = "Case Sensitive";
    /// <summary>Find next action.</summary>
    public string FindNext { get; set; } = "Find Next";
    /// <summary>Restart search action.</summary>
    public string FindReset { get; set; } = "Restart Search";
    /// <summary>Text when no match exists.</summary>
    public string SearchNoMatch { get; set; } = "No matches found.";
    /// <summary>Text after searching to the end without another match.</summary>
    public string SearchEndReached { get; set; } = "No matches found (end of table reached; use Restart Search).";
    /// <summary>Format for the total match count.</summary>
    public string SearchMatchCountFormat { get; set; } = "{0:N0} matches";
    /// <summary>Format for a one-based row hint.</summary>
    public string MinimapRowFormat { get; set; } = "Row: {0:N0}";
    /// <summary>Find previous action.</summary>
    public string FindPrevious { get; set; } = "Find Previous";
    /// <summary>Close search action.</summary>
    public string CloseSearch { get; set; } = "Close";
    /// <summary>Text while a source search is pending.</summary>
    public string SearchLoading { get; set; } = "Searching...";
    /// <summary>In-memory search guard.</summary>
    public string SearchLimitExceeded { get; set; } = "Use a virtual source to search more than 200,000 rows.";
    /// <summary>Search failure, with the exception message as argument zero.</summary>
    public string SearchErrorFormat { get; set; } = "Search failed: {0}";
    /// <summary>A source checklist needs an asynchronous values provider.</summary>
    public string SourceFilterUnavailable { get; set; } = "Filter list requires an asynchronous values provider.";

    /// <summary>Turkish defaults for adapters that use Turkish as their default language.</summary>
    public static VirtualGridTexts CreateTurkish() => new VirtualGridTexts
    {
        AllColumns = "Tüm sütunlar", SearchPlaceholder = "Ara...", SearchLabel = "Ara:",
        ColumnLabel = "Sütun:", CaseSensitive = "Büyük/küçük harf", FindNext = "Sonrakini Bul",
        FindPrevious = "Öncekini Bul", FindReset = "Baştan Ara", CloseSearch = "Kapat",
        SearchNoMatch = "Eşleşme bulunamadı.",
        SearchEndReached = "Eşleşme bulunamadı (tablonun sonuna gelindi; «Baştan Ara» ile yeniden arayın).",
        SearchMatchCountFormat = "{0:N0} eşleşme", MinimapRowFormat = "Satır: {0:N0}",
        SearchLoading = "Aranıyor...",
        SearchLimitExceeded = "200.000 satırdan fazla veri için sanal kaynak araması kullanın.",
        SearchErrorFormat = "Arama başarısız: {0}",
        SourceFilterUnavailable = "Filtre listesi için FilterAsyncLoadingNeeded gerekli."
    };
}
