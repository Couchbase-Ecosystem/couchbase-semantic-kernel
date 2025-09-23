using System;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.VectorData.ProviderServices;

namespace Couchbase.SemanticKernel;

/// <summary>
/// A mapper that maps between the generic Semantic Kernel data model and the model that the data is stored under,
/// within Couchbase.
/// </summary>
internal sealed class CouchbaseMapper<TKey, TRecord> :
    ICouchbaseMapper<TRecord>
    where TKey : notnull
    where TRecord : notnull
{
    /// <summary>
    /// A model representing a record in a vector store collection.
    /// </summary>
    private readonly CollectionModel _model;

    /// <summary>
    /// The JSON serializer options to use for serialization.
    /// </summary>
    private readonly JsonSerializerOptions _jsonSerializerOptions;

    /// <summary>
    /// Initializes a new instance of the <see cref="CouchbaseMapper{TKey, TRecord}" /> class.
    /// </summary>
    /// <param name="model">A model representing a record in a vector store collection.</param>
    /// <param name="jsonSerializerOptions">The JSON serializer options to use.</param>
    public CouchbaseMapper(
        CollectionModel model,
        JsonSerializerOptions? jsonSerializerOptions = null)
    {
        _model = model ?? throw new ArgumentNullException(nameof(model));
        _jsonSerializerOptions = jsonSerializerOptions ?? JsonSerializerOptions.Default;
    }

    /// <inheritdoc />
    public byte[]? MapFromDataToStorageModel(TRecord dataModel, Embedding<float>?[]? generatedEmbeddings = null)
    {
        if (dataModel == null)
        {
            throw new ArgumentNullException(nameof(dataModel));
        }

        var keyProperty = _model.KeyProperty;
        var keyValue = keyProperty.GetValueAsObject(dataModel) ?? throw new InvalidOperationException("Key can not be 'null'.");

        var document = SerializeToJsonObject(dataModel, _jsonSerializerOptions);

        // In Couchbase, the key aka. id is stored outside the document payload.
        document.Remove(keyProperty.StorageName);

        // Update vector properties.
        for (var i = 0; i < _model.VectorProperties.Count; i++)
        {
            var vectorProperty = _model.VectorProperties[i];

            Embedding<float>? embedding = null;

            if (vectorProperty.Type == typeof(Embedding<float>))
            {
                // System.Text.Json serializes `Embedding<T>` as complex object by default, but we have to make sure the vector is
                // stored in array representation instead.
                embedding = vectorProperty.GetValueAsObject(dataModel) as Embedding<float>;
            }

            if (generatedEmbeddings?[i] is { } generatedEmbedding)
            {
                // Use generated embedding for this vector property.
                embedding = generatedEmbedding;
            }

            if (embedding is null)
            {
                continue;
            }

            document[vectorProperty.StorageName] = JsonValue.Create(embedding.Vector.ToArray());
        }

        // Convert to byte array for Couchbase storage
        return JsonSerializer.SerializeToUtf8Bytes(document, _jsonSerializerOptions);
    }

    /// <inheritdoc />
    public TRecord MapFromStorageToDataModel(byte[]? storageModel, bool includeVectors)
    {
        if (storageModel == null)
        {
            throw new ArgumentNullException(nameof(storageModel));
        }

        // Deserialize from byte array to JSON object
        var document = JsonSerializer.Deserialize<JsonObject>(storageModel, _jsonSerializerOptions)
            ?? throw new InvalidOperationException("Failed to deserialize storage model from JSON.");

        // Update vector properties.
        foreach (var vectorProperty in _model.VectorProperties)
        {
            if (!document.TryGetPropertyValue(vectorProperty.StorageName, out var value) || (value is null))
            {
                // Skip property if it's not in the storage model or `null`.
                continue;
            }

            if (!includeVectors || !CouchbaseModelBuilder.IsVectorPropertyTypeValidCore(vectorProperty.Type, out _))
            {
                // For vector properties which have embedding generation configured, we need to remove the embeddings
                // before deserializing (we can't go back from an embedding to e.g. string).
                document.Remove(vectorProperty.StorageName);
                continue;
            }

            if (vectorProperty.Type != typeof(Embedding<float>))
            {
                continue;
            }

            // Create `Embedding<T>` from array representation.
            if (value is JsonArray jsonArray)
            {
                var floatArray = JsonSerializer.Deserialize<float[]>(jsonArray, _jsonSerializerOptions);
                if (floatArray != null)
                {
                    var embedding = new Embedding<float>(floatArray);
                    document[vectorProperty.StorageName] = JsonValue.Create(embedding);
                }
            }
        }

        return JsonSerializer.Deserialize<TRecord>(document, _jsonSerializerOptions)!;
    }

    /// <summary>
    /// Serializes an object to a JsonObject using the configured serializer options.
    /// </summary>
    /// <typeparam name="T">The type of object to serialize.</typeparam>
    /// <param name="obj">The object to serialize.</param>
    /// <param name="options">The JSON serializer options.</param>
    /// <returns>A JsonObject representation of the object.</returns>
    private static JsonObject SerializeToJsonObject<T>(T obj, JsonSerializerOptions options)
    {
        return JsonSerializer.SerializeToNode(obj, options)?.AsObject()
            ?? throw new InvalidOperationException("Failed to serialize object to JsonObject.");
    }
}