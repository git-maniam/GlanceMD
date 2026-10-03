using GlanceMD.Core;

namespace GlanceMD.Core.Tests;

public sealed class RecentDocumentsTests
{
    [Fact]
    public void Add_DeduplicatesMovesToFrontAndCapsAtTen()
    {
        var existing = Enumerable.Range(0, 12)
            .Select(index => new RecentDocument(Path.GetFullPath($"doc-{index}.md"), DateTimeOffset.UnixEpoch))
            .ToArray();

        var result = RecentDocuments.Add(existing, existing[5].FullPath, DateTimeOffset.UtcNow);

        Assert.Equal(10, result.Count);
        Assert.Equal(existing[5].FullPath, result[0].FullPath);
        Assert.Equal(1, result.Count(item => item.FullPath == existing[5].FullPath));
    }
}
