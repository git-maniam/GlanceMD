using System.Text;

namespace GlanceMD.Core;

public sealed class DocumentLoader : IDocumentLoader
{
    private static readonly HashSet<string> SupportedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".md", ".markdown", ".mdown", ".mkd"
    };

    public async Task<LoadedDocument> LoadAsync(
        DocumentRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        string fullPath;
        try
        {
            fullPath = Path.GetFullPath(request.FullPath);
        }
        catch (Exception exception) when (exception is ArgumentException or NotSupportedException)
        {
            throw new DocumentLoadException("The document path is invalid.", exception);
        }

        if (!SupportedExtensions.Contains(Path.GetExtension(fullPath)))
        {
            throw new DocumentLoadException("Choose a Markdown file (.md, .markdown, .mdown, or .mkd). ");
        }

        FileInfo info;
        try
        {
            info = new FileInfo(fullPath);
            if (!info.Exists)
            {
                throw new DocumentLoadException("The Markdown file no longer exists.");
            }

            if (info.Length > GlanceMdLimits.MarkdownFileBytes)
            {
                throw new DocumentLoadException("This file is larger than the 10 MiB viewing limit.");
            }
        }
        catch (DocumentLoadException)
        {
            throw;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            throw new DocumentLoadException("GlanceMD cannot access this file.", exception);
        }

        try
        {
            await using var stream = new FileStream(
                fullPath,
                FileMode.Open,
                FileAccess.Read,
                FileShare.ReadWrite | FileShare.Delete,
                bufferSize: 64 * 1024,
                options: FileOptions.Asynchronous | FileOptions.SequentialScan);

            using var reader = new StreamReader(
                stream,
                new UTF8Encoding(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true),
                detectEncodingFromByteOrderMarks: true,
                bufferSize: 64 * 1024,
                leaveOpen: false);

            var markdown = await reader.ReadToEndAsync(cancellationToken).ConfigureAwait(false);
            return new LoadedDocument(
                fullPath,
                Path.GetFileName(fullPath),
                markdown,
                info.LastWriteTimeUtc,
                info.Length);
        }
        catch (DecoderFallbackException exception)
        {
            throw new DocumentLoadException("The document is not valid UTF-8 or BOM-marked UTF-16 text.", exception);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            throw new DocumentLoadException("GlanceMD could not read this file.", exception);
        }
    }
}
