using System.Text.Json;
using Couchbase.SemanticKernel.Diagnostics;
using Microsoft.Extensions.VectorData;

namespace Couchbase.SemanticKernel;

/// <summary>
/// A mapper that maps between the generic Semantic Kernel data model and the model that the data is stored under, within Couchbase.
/// </summary>
internal sealed class CouchbaseGenericDataModelMapper : IVectorStoreRecordMapper<VectorStoreGenericDataModel<string>, VectorStoreGenericDataModel<string>>
{
    /// <summary>A <see cref="JsonSerializerOptions"/> for serialization/deserialization of data properties</summary>
    private readonly JsonSerializerOptions _jsonSerializerOptions;

    /// <summary>The list of properties from the record definition.</summary>
    private readonly IReadOnlyList<VectorStoreRecordProperty> _properties;

    /// <summary>A dictionary that maps from a property name to the storage name.</summary>
    private readonly Dictionary<string, string> _storagePropertyNames;

    public CouchbaseGenericDataModelMapper(
        IReadOnlyList<VectorStoreRecordProperty> properties,
        Dictionary<string, string> storagePropertyNames,
        JsonSerializerOptions jsonSerializerOptions)
    {
        Verify.NotNull(properties);
        Verify.NotNull(storagePropertyNames);

        this._properties = properties;
        this._storagePropertyNames = storagePropertyNames;
        this._jsonSerializerOptions = jsonSerializerOptions;
    }

    /// <inheritdoc />
    public VectorStoreGenericDataModel<string> MapFromDataToStorageModel(VectorStoreGenericDataModel<string> dataModel)
    {
        Verify.NotNull(dataModel);

        return dataModel;
    }

    /// <inheritdoc />
    public VectorStoreGenericDataModel<string> MapFromStorageToDataModel(VectorStoreGenericDataModel<string> storageModel, StorageToDataModelMapperOptions options)
    {
        Verify.NotNull(storageModel);

        // Extract key, data properties, and vectors
        var key = storageModel.Key;
        var dataProperties = new Dictionary<string, object?>();
        var vectorProperties = new Dictionary<string, object?>();

        // Map properties from storage to data model
        foreach (var property in this._properties)
        {
            if (property is VectorStoreRecordKeyProperty)
            {
                key = storageModel.Key;
            }
            else if (property is VectorStoreRecordDataProperty dataProperty && storageModel.Data != null)
            {
                if (storageModel.Data.TryGetValue(dataProperty.DataModelPropertyName, out var dataValue))
                {
                    dataProperties[dataProperty.DataModelPropertyName] =
                        JsonSerializer.Deserialize(dataValue!.ToString()!, dataProperty.PropertyType, this._jsonSerializerOptions);
                }
            }
            else if (property is VectorStoreRecordVectorProperty vectorProperty && options.IncludeVectors && storageModel.Vectors != null)
            {
                if (storageModel.Vectors.TryGetValue(vectorProperty.DataModelPropertyName, out var vectorValue))
                {
                    vectorProperties[vectorProperty.DataModelPropertyName] =
                        JsonSerializer.Deserialize(vectorValue!.ToString()!, vectorProperty.PropertyType, this._jsonSerializerOptions);
                }
            }
        }

        if (key is null)
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