using System.Text.Json;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.VectorData;

namespace Couchbase.SemanticKernel;

/// <summary>
/// Interface for shared properties across Couchbase collection options.
/// </summary>
public interface ICouchbaseCollectionOptions
{
    /// <summary>
    /// Gets or sets the collection definition that defines the schema of the collection.
    /// </summary>
    VectorStoreCollectionDefinition? Definition { get; set; }

    /// <summary>
    /// Gets or sets the embedding generator to use for generating embeddings for vector properties that don't have values.
    /// </summary>
    IEmbeddingGenerator? EmbeddingGenerator { get; set; }

    /// <summary>
    /// Gets or sets the JSON serializer options to use for serialization.
    /// </summary>
    JsonSerializerOptions? JsonSerializerOptions { get; set; }
}