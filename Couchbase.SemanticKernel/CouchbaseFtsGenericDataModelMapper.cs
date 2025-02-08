using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Extensions.VectorData;
using Couchbase.SemanticKernel.Diagnostics;

namespace Couchbase.SemanticKernel
{
    /// <summary>
    /// A mapper that maps between the generic Semantic Kernel data model and the model that the data is stored under, within Couchbase.
    /// </summary>
    internal sealed class CouchbaseFtsGenericDataModelMapper : IVectorStoreRecordMapper<VectorStoreGenericDataModel<string>, byte[]>
    {
        private readonly JsonSerializerOptions? _jsonSerializerOptions;
        private readonly IReadOnlyList<VectorStoreRecordProperty> _properties;
        private readonly Dictionary<string, string> _storagePropertyNames;

        public CouchbaseFtsGenericDataModelMapper(
            IReadOnlyList<VectorStoreRecordProperty> properties,
            JsonSerializerOptions? jsonSerializerOptions = null)
        {
            Verify.NotNull(properties);
            _properties = properties;
            _jsonSerializerOptions = jsonSerializerOptions;

            // Create a mapping from data model property names to storage property names.
            _storagePropertyNames = properties.ToDictionary(
                x => x.DataModelPropertyName,
                x => x.StoragePropertyName ?? (jsonSerializerOptions?.PropertyNamingPolicy?.ConvertName(x.DataModelPropertyName) ?? x.DataModelPropertyName)
            );
        }

        /// <inheritdoc />
        public byte[] MapFromDataToStorageModel(VectorStoreGenericDataModel<string> dataModel)
        {
            Verify.NotNull(dataModel);

            var jsonObject = new JsonObject();

            foreach (var property in _properties)
            {
                var storagePropertyName = _storagePropertyNames[property.DataModelPropertyName];
                
                if (property is VectorStoreRecordKeyProperty)
                {
                    jsonObject[storagePropertyName] = dataModel.Key;
                }
                
                else if (property is VectorStoreRecordDataProperty dataProperty)
                {
                    if (dataModel.Data is not null && dataModel.Data.TryGetValue(dataProperty.DataModelPropertyName, out var dataValue))
                    {
                        jsonObject[storagePropertyName] = dataValue is not null
                            ? JsonSerializer.SerializeToNode(dataValue, property.PropertyType, _jsonSerializerOptions)
                            : null;
                    }
                }
                
                else if (property is VectorStoreRecordVectorProperty vectorProperty)
                {
                    if (dataModel.Vectors is not null && dataModel.Vectors.TryGetValue(vectorProperty.DataModelPropertyName, out var vectorValue))
                    {
                        if (vectorValue is ReadOnlyMemory<float> rom)
                        {
                            jsonObject[storagePropertyName] = JsonSerializer.SerializeToNode(rom.ToArray(), _jsonSerializerOptions);
                        }
                        else
                        {
                            throw new VectorStoreRecordMappingException(
                                $"Unsupported vector type '{vectorValue.GetType().Name}' found on property '{vectorProperty.DataModelPropertyName}'. Only ReadOnlyMemory<float> is supported.");
                        }
                    }
                }
            }

            return JsonSerializer.SerializeToUtf8Bytes(jsonObject, _jsonSerializerOptions);
        }

        /// <inheritdoc />
        public VectorStoreGenericDataModel<string> MapFromStorageToDataModel(byte[] storageModel, StorageToDataModelMapperOptions options)
        {
            // Deserialize to a Dictionary instead of JsonObject
            var dictionary = JsonSerializer.Deserialize<Dictionary<string, object?>>(storageModel, _jsonSerializerOptions)
                             ?? throw new JsonException("Failed to deserialize storage model.");

            string? key = null;
            var dataProperties = new Dictionary<string, object?>();
            var vectorProperties = new Dictionary<string, object?>();

            foreach (var property in _properties)
            {
                var storagePropertyName = _storagePropertyNames[property.DataModelPropertyName];

                if (property is VectorStoreRecordKeyProperty)
                {
                    if (dictionary.TryGetValue(storagePropertyName, out var keyValue))
                    {
                        key = keyValue?.ToString();
                    }
                }
                else if (property is VectorStoreRecordDataProperty)
                {
                    if (dictionary.TryGetValue(storagePropertyName, out var dataValue) && dataValue is JsonElement jsonElement)
                    {
                        dataProperties[property.DataModelPropertyName] = JsonSerializer.Deserialize(jsonElement.GetRawText(), property.PropertyType, _jsonSerializerOptions);
                    }
                }
                else if (property is VectorStoreRecordVectorProperty)
                {
                    if (dictionary.TryGetValue(storagePropertyName, out var vectorValue) && vectorValue is JsonElement jsonElement && jsonElement.ValueKind == JsonValueKind.Array)
                    {
                        var array = JsonSerializer.Deserialize<float[]>(jsonElement.GetRawText(), _jsonSerializerOptions)
                                    ?? throw new VectorStoreRecordMappingException($"Failed to deserialize vector property '{storagePropertyName}'.");

                        vectorProperties[property.DataModelPropertyName] = new ReadOnlyMemory<float>(array);
                    }
                }
            }

            if (string.IsNullOrWhiteSpace(key))
            {
                throw new VectorStoreRecordMappingException("No key property was found in the record retrieved from storage.");
            }
            
            return new VectorStoreGenericDataModel<string>(key)
            {
                Data = dataProperties,
                Vectors = vectorProperties
            };
        }
    }
}
