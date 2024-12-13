using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using System.Text.Json.Nodes;
using Connectors.Memory.Couchbase.Diagnostics;
using Microsoft.Extensions.VectorData;

namespace Couchbase.SemanticKernel.Connectors.Couchbase;

/// <summary>
/// Class for mapping between a JSON object stored in Couchbase and the consumer data model.
/// </summary>
/// <typeparam name="TRecord">The consumer data model to map to or from.</typeparam>
[ExcludeFromCodeCoverage]
public sealed class CouchbaseVectorStoreRecordMapper<TRecord> : IVectorStoreRecordMapper<TRecord, JsonObject>
{
    /// <summary>The JSON serializer options to use when converting between the data model and the Couchbase record.</summary>
    private readonly JsonSerializerOptions _jsonSerializerOptions;
    
    /// <summary>A dictionary that maps from a property name to the storage name that should be used when serializing it to JSON for data and vector properties.</summary>
    private readonly Dictionary<string, string> _storagePropertyNames;

    /// <summary>
    /// Initializes a new instance of the <see cref="CouchbaseVectorStoreRecordMapper{TRecord}"/> class.
    /// </summary>
    /// <param name="keyStoragePropertyName">The storage property name of the key field.</param>
    /// <param name="storagePropertyNames">A dictionary mapping property names to storage property names.</param>
    /// <param name="jsonSerializerOptions">The JSON serializer options to use for serialization and deserialization.</param>
    public CouchbaseVectorStoreRecordMapper(
        Dictionary<string, string> storagePropertyNames,
        JsonSerializerOptions jsonSerializerOptions)
    {
        Verify.NotNull(jsonSerializerOptions);
        
        this._storagePropertyNames = storagePropertyNames;
        this._jsonSerializerOptions = jsonSerializerOptions;
    }

    /// <inheritdoc />
    public JsonObject MapFromDataToStorageModel(TRecord dataModel)
    {
        var jsonObject = JsonSerializer.SerializeToNode(dataModel, this._jsonSerializerOptions)!.AsObject();
        return jsonObject;
    }

    /// <inheritdoc />
    public TRecord MapFromStorageToDataModel(JsonObject storageModel, StorageToDataModelMapperOptions options)
    {
        return storageModel.Deserialize<TRecord>(this._jsonSerializerOptions)!;
    }
}