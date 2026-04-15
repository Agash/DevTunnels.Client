namespace DevTunnels.Client.Access;

/// <summary>
/// Represents a single access control entry returned by the CLI.
/// </summary>
public sealed record DevTunnelAccessEntry
{
    /// <summary>Gets the entry type, such as <c>Anonymous</c>.</summary>
    public string Type { get; init; } = string.Empty;

    /// <summary>Gets a value indicating whether the rule is a deny rule.</summary>
    public bool IsDeny { get; init; }

    /// <summary>Gets a value indicating whether the rule is inherited.</summary>
    public bool IsInherited { get; init; }

    /// <summary>Gets the subjects associated with the rule.</summary>
    public IReadOnlyList<string> Subjects { get; init; } = [];

    /// <summary>Gets the scopes associated with the rule.</summary>
    public IReadOnlyList<string> Scopes { get; init; } = [];
}
