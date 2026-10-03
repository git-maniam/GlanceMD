using System.Security.Cryptography;
using System.Text;
using GlanceMD.Core;

namespace GlanceMD.Core.Tests;

public sealed class DocumentLoaderTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), $"GlanceMD-{Guid.NewGuid():N}");

    public DocumentLoaderTests() => Directory.CreateDirectory(_directory);

    [Fact]
    public async Task LoadAsync_ReadsUtf8WithoutChangingSource()
    {
        var path = Path.Combine(_directory, "readme.md");
        await File.WriteAllTextAsync(path, "# Hello 🌍", new UTF8Encoding(false));
        var before = SHA256.HashData(await File.ReadAllBytesAsync(path));
        var timestamp = File.GetLastWriteTimeUtc(path);

        var result = await new DocumentLoader().LoadAsync(new DocumentRequest(path));

        Assert.Equal("# Hello 🌍", result.Markdown);
        Assert.Equal(before, SHA256.HashData(await File.ReadAllBytesAsync(path)));
        Assert.Equal(timestamp, File.GetLastWriteTimeUtc(path));
    }

    [Theory]
    [InlineData("notes.txt")]
    [InlineData("page.html")]
    public async Task LoadAsync_RejectsUnsupportedExtension(string fileName)
    {
        var path = Path.Combine(_directory, fileName);
        await File.WriteAllTextAsync(path, "text");
        await Assert.ThrowsAsync<DocumentLoadException>(() =>
            new DocumentLoader().LoadAsync(new DocumentRequest(path)));
    }

    public void Dispose() => Directory.Delete(_directory, recursive: true);
}
