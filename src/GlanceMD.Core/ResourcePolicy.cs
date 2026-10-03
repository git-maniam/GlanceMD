namespace GlanceMD.Core;

public sealed class ResourcePolicy : IResourcePolicy
{
    private static readonly HashSet<string> ImageExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".png", ".jpg", ".jpeg", ".gif", ".bmp", ".webp"
    };

    private static readonly HashSet<string> MarkdownExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".md", ".markdown", ".mdown", ".mkd"
    };

    public ResourceDecision Evaluate(string documentPath, string requestedReference)
    {
        if (string.IsNullOrWhiteSpace(requestedReference))
        {
            return Blocked("The resource reference is empty.");
        }

        if (requestedReference.StartsWith('#'))
        {
            return new ResourceDecision(ResourceDecisionKind.RequiresConfirmation, null, "Document fragment.");
        }

        if (Uri.TryCreate(requestedReference, UriKind.Absolute, out var absoluteUri))
        {
            return absoluteUri.Scheme.ToLowerInvariant() switch
            {
                "http" or "https" or "mailto" => new(
                    ResourceDecisionKind.RequiresConfirmation,
                    absoluteUri.AbsoluteUri,
                    "External destinations require confirmation."),
                _ => Blocked($"The '{absoluteUri.Scheme}' URI scheme is not allowed.")
            };
        }

        var documentDirectory = Path.GetDirectoryName(Path.GetFullPath(documentPath));
        if (documentDirectory is null)
        {
            return Blocked("The document folder could not be determined.");
        }

        string candidate;
        try
        {
            candidate = Path.GetFullPath(
                requestedReference.Replace('/', Path.DirectorySeparatorChar),
                documentDirectory);
        }
        catch (Exception exception) when (exception is ArgumentException or NotSupportedException)
        {
            return Blocked("The local resource path is invalid.");
        }

        var relative = Path.GetRelativePath(documentDirectory, candidate);
        if (Path.IsPathRooted(relative) || relative.Equals("..", StringComparison.Ordinal) ||
            relative.StartsWith($"..{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
        {
            return Blocked("Resources outside the document folder are blocked.");
        }

        var extension = Path.GetExtension(candidate);
        if (ImageExtensions.Contains(extension))
        {
            if (!File.Exists(candidate))
            {
                return Blocked("The local image does not exist.");
            }

            if (new FileInfo(candidate).Length > GlanceMdLimits.LocalImageBytes)
            {
                return Blocked("The local image exceeds the 20 MiB limit.");
            }

            return new ResourceDecision(ResourceDecisionKind.AllowedLocalImage, candidate, "Allowed local raster image.");
        }

        if (MarkdownExtensions.Contains(extension))
        {
            return new ResourceDecision(ResourceDecisionKind.AllowedLocalMarkdown, candidate, "Allowed local Markdown document.");
        }

        return new ResourceDecision(ResourceDecisionKind.RequiresConfirmation, candidate, "Opening this local file requires confirmation.");
    }

    private static ResourceDecision Blocked(string reason) =>
        new(ResourceDecisionKind.Blocked, null, reason);
}
