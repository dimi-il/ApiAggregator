namespace ApiAggregator.Models;

public enum SortField { Date, Relevance, Title }

public enum SortDirection { Asc, Desc }

/// <summary>How the data of one external API was obtained for a request.</summary>
public enum SourceState
{
    /// <summary>Fetched live from the external API.</summary>
    Ok,
    /// <summary>Served from the fresh cache (no external call made).</summary>
    Cached,
    /// <summary>External API failed; last known good data was served (fallback).</summary>
    Stale,
    /// <summary>External API failed and no fallback data exists.</summary>
    Failed
}

/// <summary>Unified item every external API is mapped to.</summary>
public sealed record AggregatedItem(string Source,string Category,string Title,string? Description,string? Url,DateTimeOffset Date,double Relevance);

/// <summary>Per-source outcome, so callers can see partial results.</summary>
public sealed record SourceStatus(string Source, SourceState State, long ElapsedMs, string? Error);

public sealed record AggregationResult( IReadOnlyList<AggregatedItem> Items,IReadOnlyList<SourceStatus> Sources,int TotalCount);

public sealed record AggregationQuery
{
    
    /// <summary>Keyword used by the Currencies sources.</summary>
    public string Keyword { get; init; } = "";

    /// <summary>City used by the weather  ,Holidays  source.</summary>
    public string City { get; init; } = "";

    /// <summary>Gets or initializes the category ex "Holidays,Weather,Currency".
    public string? Category { get; init; } = "";


    /// <summary>Restrict to these sources (empty = all) .</summary>
    public IReadOnlyCollection<string> Sources { get; init; } = Array.Empty<string>();

    public DateTimeOffset? From { get; init; }
    public DateTimeOffset? To { get; init; }

    public SortField SortBy { get; init; } = SortField.Date;
    public SortDirection Order { get; init; } = SortDirection.Desc;
}
