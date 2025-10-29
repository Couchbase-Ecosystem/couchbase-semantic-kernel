using Microsoft.Extensions.VectorData;

namespace CouchbaseVectorSearchDemo;

/// <summary>
/// Sample model class that represents a glossary entry.
/// </summary>
/// <remarks>
/// Note that each property is decorated with an attribute that specifies how the property should be treated by the vector store.
/// This allows us to create a collection in the vector store and upsert and retrieve instances of this class without any further configuration.
/// </remarks>
internal sealed class Glossary
{
    [VectorStoreKey]
    public string Key { get; set; } = string.Empty;

    [VectorStoreData]
    public string Category { get; set; } = string.Empty;

    [VectorStoreData]
    public string Term { get; set; } = string.Empty;

    [VectorStoreData]
    public string Definition { get; set; } = string.Empty;

    [VectorStoreVector(Dimensions: 1536)]
    public ReadOnlyMemory<float> DefinitionEmbedding { get; set; }
}