using System.Text.Json;
using System.Text.Json.Nodes;
using Couchbase.Search;
using Couchbase.Search.Queries.Vector;
using Microsoft.Extensions.VectorData;

namespace Couchbase.SemanticKernel.Connectors.Couchbase;

/// <summary>
/// Options when creating a <see cref="CouchbaseVectorStoreRecordCollection{TRecord}"/>.
/// </summary>
public sealed class CouchbaseVectorStoreRecordCollectionOptions<TRecord>
{
    /// <summary>
    /// Gets or sets an optional custom mapper to use when converting between the data model and the Couchbase JSON document.
    /// </summary>
    /// <remarks>
    /// If not set, the default mapper provided by the Couchbase SDK will be used.
    /// </remarks>
    public IVectorStoreRecordMapper<TRecord, JsonObject>? JsonDocumentCustomMapper { get; init; } = null;

    /// <summary>
    /// Gets or sets an optional record definition that defines the schema of the record type.
    /// </summary>
    /// <remarks>
    /// If not provided, the schema will be inferred from the record model class using reflection.
    /// In this case, the record model properties must be annotated with the appropriate attributes to indicate their usage.
    /// See <see cref="VectorStoreRecordKeyAttribute"/>, <see cref="VectorStoreRecordDataAttribute"/>, and <see cref="VectorStoreRecordVectorAttribute"/>.
    /// </remarks>
    public VectorStoreRecordDefinition? VectorStoreRecordDefinition { get; init; } = null;

    /// <summary>
    /// Gets or sets the JSON serializer options to use when converting between the data model and the Couchbase document.
    /// </summary>
    public JsonSerializerOptions? JsonSerializerOptions { get; init; } = null;

    /// <summary>
    /// The name of the Full Text Search (FTS) index to use for vector search.
    /// </summary>
    public string? IndexName { get; init; } = null;

    /// <summary>
    /// Number of max retries for vector collection operations.
    /// </summary>
    public int MaxRetries { get; init; } = 5;

    /// <summary>
    /// Delay in milliseconds between retries for vector collection operations.
    /// </summary>
    public int DelayInMilliseconds { get; init; } = 1_000;

    /// <summary>
    /// Number of nearest neighbors to use during the vector search.
    /// </summary>
    /// <remarks>
    /// If not provided, a default value will be used by the vector search query.
    /// </remarks>
    public int? NumCandidates { get; init; } = null;
    
    /// <summary>
    /// The combination logic for multiple vector queries (e.g., AND, OR).
    /// </summary>
    public VectorQueryCombination? QueryCombinationLogic { get; init; } = VectorQueryCombination.Or;

    /// <summary>
    /// Gets or sets the text query for hybrid search.
    /// </summary>
    /// <remarks>
    /// When provided, the FTS query will be combined with vector search for hybrid search.
    /// </remarks>
    public ISearchQuery? FtsQuery { get; init; } = null;

    /// <summary>
    /// Gets or sets whether vector data should be included in the search results.
    /// </summary>
    public bool IncludeVectors { get; init; } = false;

    /// <summary>
    /// Gets or sets the number of top results to return.
    /// </summary>
    public int Top { get; init; } = 10;

    /// <summary>
    /// Gets or sets the number of results to skip.
    /// </summary>
    /// <remarks>
    /// Used for pagination of vector search results.
    /// </remarks>
    public int Skip { get; init; } = 0;

    /// <summary>
    /// Gets or sets the boost value to apply to vector query results.
    /// </summary>
    /// <remarks>
    /// Boosting allows prioritizing vector results relative to other query types (e.g., FTS).
    /// </remarks>
    public float? Boost { get; init; } = null;
}
