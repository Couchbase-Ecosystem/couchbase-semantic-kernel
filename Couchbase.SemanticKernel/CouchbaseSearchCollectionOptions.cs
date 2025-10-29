using System.Text.Json;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.VectorData;

namespace Couchbase.SemanticKernel;

/// <summary>
/// Configuration options for Couchbase FTS-based vector store record collections.
/// </summary>
public class CouchbaseSearchCollectionOptions : ICouchbaseCollectionOptions
{
    /// <summary>
    /// Gets or sets the collection definition that defines the schema of the collection.
    /// </summary>
    public VectorStoreCollectionDefinition? Definition { get; set; }

    /// <summary>
    /// Gets or sets the embedding generator to use for generating embeddings for vector properties that don't have values.
    /// </summary>
    public IEmbeddingGenerator? EmbeddingGenerator { get; set; }

    /// <summary>
    /// Gets or sets the name of the FTS index to use for vector search operations.
    /// </summary>
    public string? IndexName { get; set; }

    /// <summary>
    /// Gets or sets the number of candidates to consider during vector search.
    /// </summary>
    public uint? NumCandidates { get; set; }

    /// <summary>
    /// Gets or sets the boost factor for vector search operations.
    /// </summary>
    public float? Boost { get; set; }

    /// <summary>
    /// Gets or sets the JSON serializer options to use for serialization.
    /// </summary>
    public JsonSerializerOptions? JsonSerializerOptions { get; set; }

    /// <summary>
    /// Gets or sets a custom mapper for converting between data models and storage models.
    /// </summary>
    public object? JsonDocumentCustomMapper { get; set; }

    /// <summary>
    /// Distance function to use for vector similarity calculations.
    /// </summary>
    public string? DistanceFunction { get; set; }

    /// <summary>
    /// Initializes a new instance of the <see cref="CouchbaseSearchCollectionOptions"/> class.
    /// </summary>
    public CouchbaseSearchCollectionOptions()
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="CouchbaseSearchCollectionOptions"/> class by copying from another instance.
    /// </summary>
    /// <param name="other">The instance to copy from.</param>
    public CouchbaseSearchCollectionOptions(CouchbaseSearchCollectionOptions? other)
    {
        if (other is not null)
        {
            Definition = other.Definition;
            EmbeddingGenerator = other.EmbeddingGenerator;
            IndexName = other.IndexName;
            NumCandidates = other.NumCandidates;
            Boost = other.Boost;
            JsonSerializerOptions = other.JsonSerializerOptions;
            JsonDocumentCustomMapper = other.JsonDocumentCustomMapper;
            DistanceFunction = other.DistanceFunction;
        }
    }
}