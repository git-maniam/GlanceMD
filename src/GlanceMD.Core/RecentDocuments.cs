namespace GlanceMD.Core;

public sealed record RecentDocument(string FullPath, DateTimeOffset LastOpened)
{
    public string DisplayName => Path.GetFileName(FullPath);
    public string Folder => Path.GetDirectoryName(FullPath) ?? string.Empty;
}

public static class RecentDocuments
{
    public const int Capacity = 10;

    public static IReadOnlyList<RecentDocument> Add(
        IEnumerable<RecentDocument> existing,
        string fullPath,
        DateTimeOffset openedAt)
    {
        var normalized = Path.GetFullPath(fullPath);
        return existing
            .Where(item => !string.Equals(item.FullPath, normalized, StringComparison.OrdinalIgnoreCase))
            .Prepend(new RecentDocument(normalized, openedAt))
            .Take(Capacity)
            .ToArray();
    }
}
