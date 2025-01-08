using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using Microsoft.Extensions.VectorData;

namespace Couchbase.SemanticKernel;

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
        // If IncludeVectors is false, remove the vector property from the storage model
        if (!options.IncludeVectors)
        {
            var vectorProperty = typeof(TRecord).GetProperties()
                .FirstOrDefault(p => p.GetCustomAttributes(typeof(VectorStoreRecordVectorAttribute), inherit: true).Any());

            if (vectorProperty != null && vectorProperty.CanWrite)
            {
                vectorProperty.SetValue(storageModel, null);
            }
        }

        return storageModel;
    }
}