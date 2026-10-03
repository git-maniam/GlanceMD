namespace GlanceMD.Core;

public interface IDocumentLoader
{
    Task<LoadedDocument> LoadAsync(DocumentRequest request, CancellationToken cancellationToken = default);
}

public interface IMarkdownRenderer
{
    Task<RenderedDocument> RenderAsync(LoadedDocument document, CancellationToken cancellationToken = default);
}

public interface IResourcePolicy
{
    ResourceDecision Evaluate(string documentPath, string requestedReference);
}

public sealed class DocumentLoadException(string message, Exception? innerException = null)
    : Exception(message, innerException);
