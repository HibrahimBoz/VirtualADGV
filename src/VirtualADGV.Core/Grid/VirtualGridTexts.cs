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
}
