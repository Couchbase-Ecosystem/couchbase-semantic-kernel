using Microsoft.Extensions.AI;

namespace Couchbase.SemanticKernel;

/// <summary>
/// Interface for mapping between a data model and the Couchbase storage model.
/// </summary>
/// <typeparam name="TRecord">The data model to map to and from.</typeparam>
public interface ICouchbaseMapper<TRecord>
{
    /// <summary>
    /// Map from a data model to the storage model that will be stored in Couchbase.
    /// </summary>
    /// <param name="dataModel">The data model to map.</param>
    /// <param name="generatedEmbeddings">Generated embeddings to include in the storage model, if any.</param>
    /// <returns>The storage model.</returns>
    byte[]? MapFromDataToStorageModel(TRecord dataModel, Embedding<float>?[]? generatedEmbeddings = null);

    /// <summary>
    /// Map from the Couchbase storage model to a data model.
    /// </summary>
    /// <param name="storageModel">The storage model to map.</param>
    /// <param name="includeVectors">Whether to include vectors in the mapped data model.</param>
    /// <returns>The data model.</returns>
    TRecord MapFromStorageToDataModel(byte[]? storageModel, bool includeVectors);
}