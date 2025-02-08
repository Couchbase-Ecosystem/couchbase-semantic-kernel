using System.Text.Json;

namespace Couchbase.SemanticKernel;

/// <summary>
/// Options when creating a <see cref="CouchbaseVectorStore"/>.
/// </summary>
public sealed class CouchbaseVectorStoreOptions
{
    /// <summary>
    /// An optional factory to use for constructing <see cref="CouchbaseFtsVectorStoreRecordCollection{TRecord}"/> instances, if a custom record collection is required.
    /// </summary>
    public ICouchbaseVectorStoreRecordCollectionFactory? VectorStoreCollectionFactory { get; init; }
    
    /// <summary>
    /// Gets or sets the JSON serializer options to use when converting between the data model and the Couchbase document.
    /// </summary>
    public JsonSerializerOptions? JsonSerializerOptions { get; init; }
}