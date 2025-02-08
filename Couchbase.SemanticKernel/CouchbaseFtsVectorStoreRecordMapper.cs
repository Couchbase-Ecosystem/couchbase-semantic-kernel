using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using System.Text.Json.Nodes;
using Couchbase.SemanticKernel.Data;
using Microsoft.Extensions.VectorData;

namespace Couchbase.SemanticKernel;

/// <summary>
/// Class for mapping between a model object and a JSON document stored in Couchbase.
/// </summary>
/// <typeparam name="TRecord">The consumer data model to map to.</typeparam>
[ExcludeFromCodeCoverage]
public sealed class CouchbaseFtsVectorStoreRecordMapper<TRecord> : IVectorStoreRecordMapper<TRecord, byte[]>
{
    private readonly JsonSerializerOptions? _jsonSerializerOptions;
    private readonly VectorStoreRecordPropertyReader _propertyReader;

    public CouchbaseFtsVectorStoreRecordMapper(JsonSerializerOptions? jsonSerializerOptions, VectorStoreRecordPropertyReader propertyReader)
    {
        _jsonSerializerOptions = jsonSerializerOptions;
        _propertyReader = propertyReader;
    }

    /// <inheritdoc />
    public byte[] MapFromDataToStorageModel(TRecord dataModel)
    {
        var jsonObject = new JsonObject();
        
        foreach(var property in _propertyReader.KeyPropertiesInfo)
        {
            var storageName = _propertyReader.GetJsonPropertyName(property.Name);
            var value = property.GetValue(dataModel);
            jsonObject[storageName] = value is not null ? JsonValue.Create(value) : JsonValue.Create("");
        }
        
        foreach(var property in _propertyReader.DataPropertiesInfo)
        {
            var storageName = _propertyReader.GetJsonPropertyName(property.Name);
            var value = property.GetValue(dataModel);
            jsonObject[storageName] = value is not null ? JsonValue.Create(value) : JsonValue.Create("");
        }
        
        // Convert ReadOnlyMemory<float> vectors to float[] for storage
        foreach (var property in _propertyReader.VectorPropertiesInfo)
        {
            var storageName = _propertyReader.GetJsonPropertyName(property.Name);
            var value = property.GetValue(dataModel);

            if (value is ReadOnlyMemory<float> rom)
            {
                jsonObject[storageName] = JsonSerializer.SerializeToNode(rom.ToArray());
            }
            else
            {
                jsonObject[storageName] = value is not null ? JsonValue.Create(value) : JsonValue.Create("");
            }
        }

        // Serialize JsonObject to JSON bytes
        return JsonSerializer.SerializeToUtf8Bytes(jsonObject, _jsonSerializerOptions);
    }

    /// <inheritdoc />
    public TRecord MapFromStorageToDataModel(byte[] storageModel, StorageToDataModelMapperOptions options)
    {
        // Deserialize JSON into a dictionary
        var dictionary = JsonSerializer.Deserialize<Dictionary<string, object?>>(storageModel, _jsonSerializerOptions)!;

        // Create an instance of TRecord
        var record = (TRecord)this._propertyReader.ParameterLessConstructorInfo.Invoke(null);

        // Process key
        var keyPropertiesInfoWithValues = VectorStoreRecordMapping.BuildPropertiesInfoWithValues(
            this._propertyReader.KeyPropertiesInfo,
            this._propertyReader.JsonPropertyNamesMap,
            dictionary,
            (object? value, Type type) =>
            {
                if (value is JsonElement jsonElement)
                {
                    return jsonElement.Deserialize(type, _jsonSerializerOptions);
                }
                return value;
            });

        VectorStoreRecordMapping.SetPropertiesOnRecord(record, keyPropertiesInfoWithValues);

        // Process data properties with conversion function
        var dataPropertiesInfoWithValues = VectorStoreRecordMapping.BuildPropertiesInfoWithValues(
            this._propertyReader.DataPropertiesInfo,
            this._propertyReader.JsonPropertyNamesMap,
            dictionary,
            (object? value, Type type) =>
            {
                if (value is JsonElement jsonElement)
                {
                    return jsonElement.Deserialize(type, _jsonSerializerOptions);
                }
                return value;
            });

        VectorStoreRecordMapping.SetPropertiesOnRecord(record, dataPropertiesInfoWithValues);

        // Process vector properties
        if (options.IncludeVectors)
        {
            var vectorPropertiesInfoWithValues = VectorStoreRecordMapping.BuildPropertiesInfoWithValues(
                this._propertyReader.VectorPropertiesInfo,
                this._propertyReader.JsonPropertyNamesMap,
                dictionary,
                (object? vector, Type type) =>
                {
                    if (vector is JsonElement jsonElement)
                    {
                        var array = jsonElement.Deserialize<float[]>(_jsonSerializerOptions);
                        return array is not null ? new ReadOnlyMemory<float>(array) : null;
                    }
                    return null;
                });

            VectorStoreRecordMapping.SetPropertiesOnRecord(record, vectorPropertiesInfoWithValues);
        }

        return record;
    }
}