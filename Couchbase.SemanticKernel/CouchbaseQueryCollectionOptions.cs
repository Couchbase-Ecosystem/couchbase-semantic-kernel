using System.Text.Json;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.VectorData;

namespace Couchbase.SemanticKernel;

/// <summary>
/// Configuration options for Couchbase Query-based (Hyperscale/Composite) vector store record collections.
/// </summary>
public class CouchbaseQueryCollectionOptions : ICouchbaseCollectionOptions
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
    /// Gets or sets the name of the GSI index to use for vector search operations.
    /// </summary>
    public string? IndexName { get; set; }

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
    /// Number of vector dimensions (required for Hyperscale/Composite scenarios).
    /// </summary>
    public int? VectorDimensions { get; set; }

    /// <summary>
    /// Similarity metric string (DOT, COSINE, EUCLIDEAN, EUCLIDEAN_SQUARED). Defaults to DOT.
    /// </summary>
    public string? SimilarityMetric { get; set; } = "DOT";

    /// <summary>
    /// Quantization/IVF description string (e.g., "IVF1024,SQ8", "IVF,PQ32x8").
    /// </summary>
    public string? QuantizationSettings { get; set; } = "IVF,SQ8";

    /// <summary>
    /// Number of centroids to probe (nprobe) for Hyperscale ANN_DISTANCE. Defaults to 1.
    /// </summary>
    public int? CentroidsToProbe { get; set; } = 1;

    /// <summary>
    /// Optional list of scalar storage field names to include in the Composite index
    /// after the VECTOR column. These enable SQL++ pre-filtering before vector search.
    /// Only used when IndexType is "Composite".
    /// </summary>
    public IReadOnlyList<string>? CompositeScalarKeys { get; set; }

    /// <summary>
    /// Optional WHERE clause to create a partial index that only includes documents matching the condition.
    /// Example: "_type = 'hotel'" or "status = 'active' AND deleted != true"
    /// </summary>
    public string? IndexWhereClause { get; set; }

    /// <summary>
    /// Initializes a new instance of the <see cref="CouchbaseQueryCollectionOptions"/> class.
    /// </summary>
    public CouchbaseQueryCollectionOptions()
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="CouchbaseQueryCollectionOptions"/> class by copying from another instance.
    /// </summary>
    /// <param name="other">The instance to copy from.</param>
    public CouchbaseQueryCollectionOptions(CouchbaseQueryCollectionOptions? other)
    {
        if (other is not null)
        {
            Definition = other.Definition;
            EmbeddingGenerator = other.EmbeddingGenerator;
            IndexName = other.IndexName;
            JsonSerializerOptions = other.JsonSerializerOptions;
            JsonDocumentCustomMapper = other.JsonDocumentCustomMapper;
            DistanceFunction = other.DistanceFunction;
            VectorDimensions = other.VectorDimensions;
            SimilarityMetric = other.SimilarityMetric;
            QuantizationSettings = other.QuantizationSettings;
            CentroidsToProbe = other.CentroidsToProbe;
            CompositeScalarKeys = other.CompositeScalarKeys;
            IndexWhereClause = other.IndexWhereClause;
        }
    }
}