using GlanceMD.Core;

namespace GlanceMD.Core.Tests;

public sealed class ResourcePolicyTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), $"GlanceMD-{Guid.NewGuid():N}");

    public ResourcePolicyTests() => Directory.CreateDirectory(_directory);

    [Fact]
    public void Evaluate_AllowsRasterImageInsideDocumentFolder()
    {
        var image = Path.Combine(_directory, "image.png");
        File.WriteAllBytes(image, [1, 2, 3]);
        var result = new ResourcePolicy().Evaluate(Path.Combine(_directory, "doc.md"), "image.png");
        Assert.Equal(ResourceDecisionKind.AllowedLocalImage, result.Kind);
        Assert.Equal(image, result.ResolvedPath);
    }

    [Theory]
    [InlineData("../secret.png")]
    [InlineData("javascript:alert(1)")]
    [InlineData("file:///C:/Windows/win.ini")]
    [InlineData("data:text/html,boom")]
    public void Evaluate_BlocksUnsafeReferences(string reference)
    {
        var result = new ResourcePolicy().Evaluate(Path.Combine(_directory, "doc.md"), reference);
        Assert.Equal(ResourceDecisionKind.Blocked, result.Kind);
    }

    [Theory]
    [InlineData("https://example.com")]
    [InlineData("mailto:test@example.com")]
    public void Evaluate_RequiresConfirmationForExternalLinks(string reference)
    {
        var result = new ResourcePolicy().Evaluate(Path.Combine(_directory, "doc.md"), reference);
        Assert.Equal(ResourceDecisionKind.RequiresConfirmation, result.Kind);
    }

    public void Dispose() => Directory.Delete(_directory, recursive: true);
}
