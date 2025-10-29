using System.Diagnostics;
using System.Linq.Expressions;
using System.Runtime.CompilerServices;
using Couchbase.Core.Exceptions.KeyValue;
using Couchbase.Core.IO.Transcoders;
using Couchbase.KeyValue;
using Couchbase.Management.Collections;
using Couchbase.SemanticKernel.Diagnostics;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.VectorData;
using Microsoft.Extensions.VectorData.ProviderServices;

namespace Couchbase.SemanticKernel;

/// <summary>
/// Abstract base class for Couchbase vector store collections with shared functionality.
/// </summary>
/// <typeparam name="TKey">The data type of the record key.</typeparam>
/// <typeparam name="TRecord">The data model to use for adding, updating, and retrieving data from storage.</typeparam>
public abstract class CouchbaseCollectionBase<TKey, TRecord> : VectorStoreCollection<TKey, TRecord>
    where TKey : notnull
    where TRecord : class
{
    /// <summary>The default options for vector search.</summary>
    protected static readonly VectorSearchOptions<TRecord> DefaultVectorSearchOptions = new();

    /// <summary>The default options for hybrid vector search.</summary>
    protected static readonly HybridSearchOptions<TRecord> DefaultKeywordVectorizedHybridSearchOptions = new();

    /// <summary>Metadata about vector store record collection.</summary>
    private readonly VectorStoreCollectionMetadata _collectionMetadata;

    /// <summary>The Couchbase scope to use for storing and retrieving records.</summary>
    protected readonly IScope _scope;

    /// <summary>The Couchbase collection to use for storing and retrieving records.</summary>
    protected readonly ICouchbaseCollection _collection;

    /// <summary>Optional configuration options for this class.</summary>
    private readonly ICouchbaseCollectionOptions _options;

    /// <summary>The model for this collection.</summary>
    protected readonly CollectionModel _model;

    /// <summary>The mapper to use when converting between the data model and the Couchbase record.</summary>
    protected readonly ICouchbaseMapper<TRecord> _mapper;

    /// <inheritdoc />
    public override string Name { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="CouchbaseCollectionBase{TKey,TRecord}"/> class.
    /// </summary>
    /// <param name="scope"><see cref="IScope"/> that can be used to manage the collections in Couchbase.</param>
    /// <param name="name">The name of the collection.</param>
    /// <param name="options">Optional configuration options for this class.</param>
    protected CouchbaseCollectionBase(
        IScope scope,
        string name,
        ICouchbaseCollectionOptions? options = null)
    {
        // Verify parameters
        Verify.NotNull(scope);
        Verify.NotNullOrWhiteSpace(name);

        // Validate supported key types
        if (typeof(TKey) != typeof(string))
        {
            throw new NotSupportedException("Only string keys are supported.");
        }

        _scope = scope;
        Name = name;
        _collection = scope.Collection(name);
        _options = options ?? throw new ArgumentNullException(nameof(options), "Options cannot be null");

        // Build the collection model
        _model = typeof(TRecord) == typeof(Dictionary<string, object?>)
            ? new CouchbaseModelBuilder().BuildDynamic(_options.Definition ?? throw new ArgumentException("Definition is required for dynamic collections."), _options.EmbeddingGenerator)
            : new CouchbaseModelBuilder().Build(typeof(TRecord), _options.Definition, _options.EmbeddingGenerator);

        // Initialize mapper directly (no InitializeMapper method needed)
        _mapper = typeof(TRecord) == typeof(Dictionary<string, object?>)
            ? new CouchbaseDynamicMapper(_model, _options.JsonSerializerOptions) as ICouchbaseMapper<TRecord>
                ?? throw new InvalidOperationException("Failed to create dynamic mapper.")
            : new CouchbaseMapper<TKey, TRecord>(_model, _options.JsonSerializerOptions);

        // Initialize collection metadata
        _collectionMetadata = new()
        {
            VectorStoreSystemName = CouchbaseConstants.VectorStoreSystemName,
            CollectionName = name
        };
    }

    /// <inheritdoc />
    public override Task<bool> CollectionExistsAsync(CancellationToken cancellationToken = default)
    {
        return RunOperationAsync("CheckCollectionExists", async () =>
        {
            var collectionManager = _scope.Bucket.Collections;

            // Get all scopes in the bucket
            var scopes = await collectionManager
                .GetAllScopesAsync(new GetAllScopesOptions().CancellationToken(cancellationToken))
                .ConfigureAwait(false);

            // Find the target scope
            var targetScope = scopes.FirstOrDefault(scope => scope.Name == _scope.Name);

            if (targetScope != null)
            {
                // Check if the collection exists within the scope
                return targetScope.Collections.Any(collection => collection.Name == this.Name);
            }

            // If the scope does not exist, the collection cannot exist
            return false;
        });
    }

    /// <inheritdoc />
    public override async Task EnsureCollectionExistsAsync(CancellationToken cancellationToken = default)
    {
        // 1. Check if the collection already exists
        // if (!await CollectionExistsAsync(cancellationToken).ConfigureAwait(false))
        // {
        //     // Create the collection if it does not exist
        //     await RunOperationAsync("CreateCollection", async () =>
        //     {
        //         var collectionManager = _scope.Bucket.Collections;
        //         var collectionSpec = new CollectionSpec(_scope.Name, this.Name);
        //         await collectionManager
        //             .CreateCollectionAsync(collectionSpec, null)
        //             .ConfigureAwait(false);
        //     }).ConfigureAwait(false);
        // }

        // 2. Delegate index creation to derived classes
        // await EnsureIndexExistsAsync(cancellationToken).ConfigureAwait(false);
        throw new NotImplementedException("EnsureCollectionExistsAsync is not implemented yet.");
    }

    /// <summary>
    /// Abstract method for ensuring the appropriate index exists for this collection type.
    /// </summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    // protected abstract Task EnsureIndexExistsAsync(CancellationToken cancellationToken);

    /// <inheritdoc />
    public override async Task EnsureCollectionDeletedAsync(CancellationToken cancellationToken = default)
    {
        await RunOperationAsync("DeleteCollection", async () =>
        {
            var collectionManager = _scope.Bucket.Collections;
            await collectionManager
                .DropCollectionAsync(_scope.Name, this.Name, null)
                .ConfigureAwait(false);
        }).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public override async Task<TRecord?> GetAsync(TKey key, RecordRetrievalOptions? options = null, CancellationToken cancellationToken = default)
    {
        Verify.NotNull(key);

        var keyString = key.ToString()!;
        var includeVectors = options?.IncludeVectors ?? false;

        if (includeVectors && _model.EmbeddingGenerationRequired)
        {
            throw new NotSupportedException("IncludeVectors is not supported when embedding generation is configured.");
        }

        try
        {
            var result = await RunOperationAsync("Get", async () =>
            {
                try
                {
                    var getResult = await _collection.GetAsync(keyString,
                        getOptions => getOptions.Transcoder(new RawJsonTranscoder())).ConfigureAwait(false);
                    return getResult.ContentAs<byte[]>();
                }
                catch (DocumentNotFoundException)
                {
                    return default;
                }
            }).ConfigureAwait(false);

            if (result is null)
            {
                return default;
            }

            return _mapper.MapFromStorageToDataModel(result, includeVectors);
        }
        catch (DocumentNotFoundException)
        {
            return default;
        }
    }

    /// <inheritdoc />
    public override async IAsyncEnumerable<TRecord> GetAsync(
        IEnumerable<TKey> keys,
        RecordRetrievalOptions? options = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        Verify.NotNull(keys);

        var includeVectors = options?.IncludeVectors ?? false;
        if (includeVectors && _model.EmbeddingGenerationRequired)
        {
            throw new NotSupportedException("IncludeVectors is not supported when embedding generation is configured.");
        }

        foreach (var key in keys)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var record = await GetAsync(key, options, cancellationToken).ConfigureAwait(false);
            if (record is not null)
            {
                yield return record;
            }
        }
    }

    /// <inheritdoc />
    public override async Task DeleteAsync(TKey key, CancellationToken cancellationToken = default)
    {
        Verify.NotNull(key);

        var keyString = key.ToString()!;
        await RunOperationAsync("Delete", async () =>
        {
            await _collection.RemoveAsync(keyString).ConfigureAwait(false);
        }).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public override async Task DeleteAsync(IEnumerable<TKey> keys, CancellationToken cancellationToken = default)
    {
        Verify.NotNull(keys);

        foreach (var key in keys)
        {
            await DeleteAsync(key, cancellationToken).ConfigureAwait(false);
        }
    }

    /// <inheritdoc />
    public override async Task UpsertAsync(TRecord record, CancellationToken cancellationToken = default)
    {
        Verify.NotNull(record);

        // If an embedding generator is defined, invoke it once per property.
        Embedding<float>?[]? generatedEmbeddings = null;

        var vectorPropertyCount = _model.VectorProperties.Count;
        for (var i = 0; i < vectorPropertyCount; i++)
        {
            var vectorProperty = _model.VectorProperties[i];

            if (CouchbaseModelBuilder.IsVectorPropertyTypeValidCore(vectorProperty.Type, out _))
            {
                continue;
            }

            // We have a vector property whose type isn't natively supported - we need to generate embeddings.
            Debug.Assert(vectorProperty.EmbeddingGenerator is not null);

            if (vectorProperty.TryGenerateEmbedding<TRecord, Embedding<float>>(record, cancellationToken, out var task))
            {
                generatedEmbeddings ??= new Embedding<float>?[vectorPropertyCount];
                generatedEmbeddings[i] = await task.ConfigureAwait(false);
            }
            else
            {
                throw new InvalidOperationException(
                    $"The embedding generator configured on property '{vectorProperty.ModelName}' cannot produce an embedding of type '{typeof(Embedding<float>).Name}' for the given input type.");
            }
        }

        // Convert the data model to the storage model
        var storageModel = _mapper.MapFromDataToStorageModel(record, generatedEmbeddings);

        // Get the key value
        var keyValue = _model.KeyProperty.GetValueAsObject(record)?.ToString();
        if (string.IsNullOrWhiteSpace(keyValue))
        {
            throw new InvalidOperationException($"Key property '{_model.KeyProperty.ModelName}' is not initialized.");
        }

        // Perform the upsert operation
        await RunOperationAsync("Upsert", async () =>
        {
            await _collection.UpsertAsync(
                keyValue, storageModel,
                upsertOptions => upsertOptions.Transcoder(new RawJsonTranscoder())
            ).ConfigureAwait(false);
        }).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public override async Task UpsertAsync(IEnumerable<TRecord> records, CancellationToken cancellationToken = default)
    {
        Verify.NotNull(records);

        foreach (var record in records)
        {
            if (record is not null)
            {
                await UpsertAsync(record, cancellationToken).ConfigureAwait(false);
            }
        }
    }

    /// <summary>
    /// Abstract method for vector search implementation.
    /// </summary>
    public abstract override IAsyncEnumerable<VectorSearchResult<TRecord>> SearchAsync<TInput>(
        TInput searchValue,
        int top,
        VectorSearchOptions<TRecord>? options = default,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Abstract method for hybrid search implementation.
    /// </summary>
    public abstract IAsyncEnumerable<VectorSearchResult<TRecord>> HybridSearchAsync<TInput>(
        TInput searchValue,
        ICollection<string> keywords,
        int top,
        HybridSearchOptions<TRecord>? options = null,
        CancellationToken cancellationToken = default)
        where TInput : notnull;

    /// <summary>
    /// Abstract method for filtered record retrieval.
    /// </summary>
    public abstract override IAsyncEnumerable<TRecord> GetAsync(
        Expression<Func<TRecord, bool>> filter,
        int top,
        FilteredRecordRetrievalOptions<TRecord>? options = null,
        CancellationToken cancellationToken = default);

    /// <inheritdoc />
    public override object? GetService(Type serviceType, object? serviceKey = null)
    {
        Verify.NotNull(serviceType);

        return
            serviceKey is not null ? null :
            serviceType == typeof(VectorStoreCollectionMetadata) ? _collectionMetadata :
            serviceType == typeof(IScope) ? _scope :
            serviceType == typeof(ICouchbaseCollection) ? _collection :
            serviceType.IsInstanceOfType(this) ? this :
            null;
    }

    /// <summary>
    /// Shared helper method to get search vector from input.
    /// </summary>
    protected static async ValueTask<ICollection<float>> GetSearchVectorAsync<TInput>(TInput searchValue, VectorPropertyModel vectorProperty, CancellationToken cancellationToken)
        where TInput : notnull
    {
        if (searchValue is ICollection<float> collection)
        {
            return collection;
        }

        if (searchValue is IEnumerable<float> enumerable)
        {
            return [.. enumerable];
        }

        var memory = searchValue switch
        {
            ReadOnlyMemory<float> r => r,
            Embedding<float> e => e.Vector,
            _ when vectorProperty.EmbeddingGenerator is IEmbeddingGenerator<TInput, Embedding<float>> generator
                => await generator.GenerateVectorAsync(searchValue, cancellationToken: cancellationToken).ConfigureAwait(false),

            _ => vectorProperty.EmbeddingGenerator is null
                ? throw new NotSupportedException($"The provided vector type {searchValue.GetType().FullName} is not supported by the Couchbase connector.")
                : throw new InvalidOperationException($"The embedding generator configured is incompatible with input type {typeof(TInput).Name}.")
        };

        return System.Runtime.InteropServices.MemoryMarshal.TryGetArray(memory, out var segment) && segment.Count == segment.Array!.Length
            ? segment.Array
            : memory.ToArray();
    }

    /// <summary>
    /// Shared helper method for running operations with error handling.
    /// </summary>
    protected Task RunOperationAsync(string operationName, Func<Task> operation)
    {
        return VectorStoreErrorHandler.RunOperationAsync<CouchbaseException>(
            _collectionMetadata,
            operationName,
            operation);
    }

    /// <summary>
    /// Shared helper method for running operations with error handling and return value.
    /// </summary>
    protected Task<T> RunOperationAsync<T>(string operationName, Func<Task<T>> operation)
    {
        return VectorStoreErrorHandler.RunOperationAsync<T, CouchbaseException>(
            _collectionMetadata,
            operationName,
            operation);
    }
}