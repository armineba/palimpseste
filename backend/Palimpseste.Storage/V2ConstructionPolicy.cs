namespace Palimpseste.Storage;

/// <summary>Shared, bounded construction policy for a V2 job and its additive recovery windows.</summary>
public static class V2ConstructionPolicy
{
    public const string Version = "sp.construction-policy/2.0";
    public const int RevisionsPerWindow = 4;
    public const int MaxRevision = 31;
    public const int MaxRevisionBase = MaxRevision - RevisionsPerWindow + 1;
    public const int AttemptBudget = 32;
}
