using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using Couchbase.KeyValue;
using Microsoft.Extensions.VectorData;

namespace Couchbase.SemanticKernel.Connectors.Couchbase;

/// <summary>
/// Class for mapping between a model object and a JSON document stored in Couchbase.
/// </summary>
/// <typeparam name="TRecord">The consumer data model to map to or from.</typeparam>
[ExcludeFromCodeCoverage]
public sealed class CouchbaseVectorStoreRecordMapper<TRecord> : IVectorStoreRecordMapper<TRecord, TRecord>
{
    /// <summary>The JSON serializer options to use when converting between the data model and the Couchbase record.</summary>
    private readonly JsonSerializerOptions _jsonSerializerOptions;

    /// <summary>
    /// Initializes a new instance of the <see cref="CouchbaseVectorStoreRecordMapper{TRecord}"/> class.
    /// </summary>
    /// <param name="jsonSerializerOptions">The JSON serializer options to use for serialization and deserialization.</param>
    public CouchbaseVectorStoreRecordMapper(JsonSerializerOptions jsonSerializerOptions)
    {
        this._jsonSerializerOptions = jsonSerializerOptions;
    }

    /// <inheritdoc />
    public TRecord MapFromDataToStorageModel(TRecord dataModel)
    {
        return dataModel;
    }

    /// <inheritdoc />
    public TRecord MapFromStorageToDataModel(TRecord storageModel, StorageToDataModelMapperOptions options)
    {
        // TODO: if options contain includeVector as false, remove the vector from the storage model.
        return storageModel;
    }
}
