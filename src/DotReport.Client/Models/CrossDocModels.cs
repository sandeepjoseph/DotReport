namespace DotReport.Client.Models;

// ── Cross-document intelligence results ─────────────────────────────────────

public sealed record AmountMatch(
    string   Formatted,   // "$1,234.56"
    decimal  Value,
    string   DocA,
    string   DocB,
    string   ContextA,
    string   ContextB);

public sealed record DateCluster(
    string       NormalizedDate,
    List<string> DocumentNames);

public sealed record CrossDocReport
{
    public List<AmountMatch>   MatchingAmounts { get; init; } = new();
    public List<AmountMatch>   Discrepancies   { get; init; } = new();
    public List<DateCluster>   DateClusters    { get; init; } = new();
    public string              Summary         { get; init; } = string.Empty;
    public bool                HasInsights     => MatchingAmounts.Count > 0 ||
                                                  Discrepancies.Count   > 0 ||
                                                  DateClusters.Count    > 0;

    public static readonly CrossDocReport Empty = new();
}

// ── Inference tier tracking ──────────────────────────────────────────────────

public enum InferenceTier
{
    Tier1_Primary   = 1,   // ONNX primary model — full quality
    Tier2_Backup    = 2,   // ONNX backup model only
    Tier3_RuleBased = 3,   // Built-in extractor — guaranteed
}

public sealed class InferenceTierState
{
    public InferenceTier   Tier               { get; set; } = InferenceTier.Tier1_Primary;
    public int             ConsecutiveFails   { get; set; }
    public string          StatusLabel        => Tier switch
    {
        InferenceTier.Tier1_Primary   => "PRIMARY",
        InferenceTier.Tier2_Backup    => "BACKUP",
        InferenceTier.Tier3_RuleBased => "RULE ENGINE",
        _                             => "UNKNOWN",
    };
    public string          StatusClass        => Tier switch
    {
        InferenceTier.Tier1_Primary   => "tier--ok",
        InferenceTier.Tier2_Backup    => "tier--warn",
        InferenceTier.Tier3_RuleBased => "tier--fallback",
        _                             => "",
    };
    public bool            IsDegraded         => Tier != InferenceTier.Tier1_Primary;
}
