using System.Text.Json;
using Microsoft.Extensions.AI;

namespace Couchbase.SemanticKernel;

/// <summary>
/// Options when creating a <see cref="CouchbaseVectorStore"/>.
/// </summary>
public sealed class CouchbaseVectorStoreOptions
{

    internal static readonly CouchbaseVectorStoreOptions Default = new();

    public CouchbaseVectorStoreOptions()
    {
    }

    internal CouchbaseVectorStoreOptions(CouchbaseVectorStoreOptions? source)
    {
        EmbeddingGenerator = source?.EmbeddingGenerator;
        IndexType = source?.IndexType ?? CouchbaseIndexType.Hyperscale;
    }

    /// <summary>
    /// Gets or sets the default embedding generator to use when generating vectors embeddings with this vector store.
    /// </summary>
    public IEmbeddingGenerator? EmbeddingGenerator { get; set; }

    /// <summary>
    /// Gets or sets the default index type to use for vector operations.
    /// This determines whether collections will use Search (FTS), Hyperscale, or Composite indexes by default.
    /// Individual collections can override this setting.
    /// </summary>
    public CouchbaseIndexType IndexType { get; set; } = CouchbaseIndexType.Hyperscale;
}