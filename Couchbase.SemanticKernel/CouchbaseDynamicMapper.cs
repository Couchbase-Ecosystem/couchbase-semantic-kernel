using System.Text.Json;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.VectorData.ProviderServices;

namespace Couchbase.SemanticKernel;

/// <summary>
/// A mapper that maps between a dynamic Dictionary&lt;string, object?&gt; data model and the Couchbase storage model.
/// </summary>
internal sealed class CouchbaseDynamicMapper : ICouchbaseMapper<Dictionary<string, object?>>
{
    /// <summary>The collection model.</summary>
    private readonly CollectionModel _model;

    /// <summary>The JSON serializer options to use for serialization.</summary>
    private readonly JsonSerializerOptions _jsonSerializerOptions;

    /// <summary>
    /// Initializes a new instance of the <see cref="CouchbaseDynamicMapper"/> class.
    /// </summary>
    /// <param name="model">The collection model.</param>
    /// <param name="jsonSerializerOptions">The JSON serializer options to use for serialization.</param>
    public CouchbaseDynamicMapper(CollectionModel model, JsonSerializerOptions? jsonSerializerOptions = null)
    {
        _model = model ?? throw new ArgumentNullException(nameof(model));
        _jsonSerializerOptions = jsonSerializerOptions ?? JsonSerializerOptions.Default;
    }

    /// <inheritdoc />
    public byte[]? MapFromDataToStorageModel(Dictionary<string, object?> dataModel, Embedding<float>?[]? generatedEmbeddings = null)
    {
        if (dataModel == null)
        {
            throw new ArgumentNullException(nameof(dataModel));
        }

        // Create a copy of the data model to avoid modifying the original
        var storageModel = new Dictionary<string, object?>(dataModel);

        // Update vector properties with generated embeddings if provided
        if (generatedEmbeddings != null)
        {
            for (var i = 0; i < _model.VectorProperties.Count && i < generatedEmbeddings.Length; i++)
            {
                var vectorProperty = _model.VectorProperties[i];
                var generatedEmbedding = generatedEmbeddings[i];

                if (generatedEmbedding != null)
                {
                    storageModel[vectorProperty.StorageName] = generatedEmbedding.Vector.ToArray();
                }
            }
        }

        // Serialize to JSON bytes
        return JsonSerializer.SerializeToUtf8Bytes(storageModel, _jsonSerializerOptions);
    }

    /// <inheritdoc />
    public Dictionary<string, object?> MapFromStorageToDataModel(byte[]? storageModel, bool includeVectors)
    {
        if (storageModel == null)
        {
            throw new ArgumentNullException(nameof(storageModel));
        }

        var dataModel = JsonSerializer.Deserialize<Dictionary<string, object?>>(storageModel, _jsonSerializerOptions)
            ?? throw new InvalidOperationException("Failed to deserialize storage model.");

        // Handle vector properties based on includeVectors flag
        if (!includeVectors)
        {
            foreach (var vectorProperty in _model.VectorProperties)
            {
                dataModel.Remove(vectorProperty.StorageName);
            }
        }
        else
        {
            // Ensure vector properties are in the correct format
            foreach (var vectorProperty in _model.VectorProperties)
            {
                if (dataModel.TryGetValue(vectorProperty.StorageName, out var vectorValue) && vectorValue != null)
                {
                    // Convert to ReadOnlyMemory<float> if it's an array
                    if (vectorValue is JsonElement jsonElement && jsonElement.ValueKind == JsonValueKind.Array)
                    {
                        var floatArray = JsonSerializer.Deserialize<float[]>(jsonElement.GetRawText(), _jsonSerializerOptions);
                        if (floatArray != null)
                        {
                            dataModel[vectorProperty.StorageName] = new ReadOnlyMemory<float>(floatArray);
                        }
                    }
                    else if (vectorValue is float[] floatArray)
                    {
                        dataModel[vectorProperty.StorageName] = new ReadOnlyMemory<float>(floatArray);
                    }
                }
            }
        }

        return dataModel;
    }
} 