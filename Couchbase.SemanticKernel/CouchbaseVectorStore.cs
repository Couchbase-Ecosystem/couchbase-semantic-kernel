using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Couchbase.SemanticKernel.Diagnostics;
using Couchbase.KeyValue;
using Couchbase.Management.Collections;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.VectorData;

namespace Couchbase.SemanticKernel;

/// <summary>
/// Class for accessing the list of collections in a Couchbase vector store.
/// </summary>
/// <remarks>
/// This class can be used with collections of any schema type, but requires you to provide schema information when getting a collection.
/// </remarks>
public class CouchbaseVectorStore : VectorStore
{
    /// <summary>A general purpose definition that can be used to construct a collection when needing to proxy schema agnostic operations.</summary>
    private static readonly VectorStoreCollectionDefinition GeneralPurposeDefinition = new()
    {
        Properties =
        [
            new VectorStoreKeyProperty("Key", typeof(string)),
            new VectorStoreVectorProperty("Vector", typeof(ReadOnlyMemory<float>), 1)
        ]
    };

    /// <summary>Metadata about vector store.</summary>
    private readonly VectorStoreMetadata _metadata;

    /// <summary><see cref="IScope"/> that can be used to manage the collections in Couchbase.</summary>
    private readonly IScope _scope;

    /// <summary>Optional configuration options for this class.</summary>
    private readonly CouchbaseVectorStoreOptions _options;

    /// <summary>Optional embedding generator for automatic vector generation.</summary>
    private readonly IEmbeddingGenerator? _embeddingGenerator;

    /// <summary>
    /// Initializes a new instance of the <see cref="CouchbaseVectorStore"/> class.
    /// </summary>
    /// <param name="scope"><see cref="IScope"/> that can be used to manage the collections in Couchbase.</param>
    /// <param name="options">Optional configuration options for this class.</param>
    public CouchbaseVectorStore(IScope scope, CouchbaseVectorStoreOptions? options = null)
    {
        Verify.NotNull(scope);

        _scope = scope;
        _options = options ?? new CouchbaseVectorStoreOptions();
        _embeddingGenerator = _options.EmbeddingGenerator;

        _metadata = new()
        {
            VectorStoreSystemName = CouchbaseConstants.VectorStoreSystemName
        };
    }

    /// <inheritdoc />
    public override VectorStoreCollection<TKey, TRecord> GetCollection<TKey, TRecord>(string name, VectorStoreCollectionDefinition? definition = null)
    {
        if (typeof(TRecord) == typeof(Dictionary<string, object?>))
        {
            throw new ArgumentException("For dynamic mapping via Dictionary<string, object?>, call GetDynamicCollection() instead.");
        }

        // Use the centralized IndexType to determine which collection type to create
        return _options.IndexType switch
        {
            CouchbaseIndexType.Search => CreateSearchCollection<TKey, TRecord>(name, definition),
            CouchbaseIndexType.Bhive or CouchbaseIndexType.Composite => CreateQueryCollection<TKey, TRecord>(name, definition),
            _ => throw new ArgumentException($"Unsupported index type: {_options.IndexType}")
        };
    }

    /// <summary>
    /// Creates a search-based collection with the specified parameters.
    /// </summary>
    private VectorStoreCollection<TKey, TRecord> CreateSearchCollection<TKey, TRecord>(string name, VectorStoreCollectionDefinition? definition)
        where TKey : notnull
        where TRecord : class
    {
        var collectionOptions = new CouchbaseSearchCollectionOptions
        {
            Definition = definition,
            EmbeddingGenerator = _embeddingGenerator
        };

        return new CouchbaseSearchCollection<TKey, TRecord>(_scope, name, collectionOptions);
    }

    /// <summary>
    /// Creates a query-based collection with the specified parameters.
    /// </summary>
    private VectorStoreCollection<TKey, TRecord> CreateQueryCollection<TKey, TRecord>(string name, VectorStoreCollectionDefinition? definition)
        where TKey : notnull
        where TRecord : class
    {
        var collectionOptions = new CouchbaseQueryCollectionOptions
        {
            Definition = definition,
            EmbeddingGenerator = _embeddingGenerator
        };

        return new CouchbaseQueryCollection<TKey, TRecord>(_scope, name, collectionOptions, _options.IndexType);
    }

    /// <summary>
    /// Gets a collection with Couchbase FTS-specific options.
    /// </summary>
    /// <typeparam name="TKey">The data type of the record key.</typeparam>
    /// <typeparam name="TRecord">The data model to use for adding, updating and retrieving data from storage.</typeparam>
    /// <param name="name">The name of the collection.</param>
    /// <param name="options">Couchbase FTS-specific configuration options for the collection.</param>
    /// <returns>The FTS-based collection.</returns>
    public VectorStoreCollection<TKey, TRecord> GetCollection<TKey, TRecord>(string name, CouchbaseSearchCollectionOptions options)
        where TKey : notnull
        where TRecord : class
    {
        if (typeof(TRecord) == typeof(Dictionary<string, object?>))
        {
            throw new ArgumentException("For dynamic mapping via Dictionary<string, object?>, call GetDynamicCollection() instead.");
        }

        // Merge embedding generator from store options if not provided in collection options
        var mergedOptions = new CouchbaseSearchCollectionOptions
        {
            Definition = options.Definition,
            EmbeddingGenerator = options.EmbeddingGenerator ?? _embeddingGenerator,
            IndexName = options.IndexName,
            DistanceFunction = options.DistanceFunction,
            JsonSerializerOptions = options.JsonSerializerOptions,
            NumCandidates = options.NumCandidates,
            Boost = options.Boost,
            JsonDocumentCustomMapper = options.JsonDocumentCustomMapper
        };

        return new CouchbaseSearchCollection<TKey, TRecord>(_scope, name, mergedOptions);
    }

    /// <summary>
    /// Gets a collection with Couchbase Query-specific options (BHIVE/COMPOSITE).
    /// </summary>
    /// <typeparam name="TKey">The data type of the record key.</typeparam>
    /// <typeparam name="TRecord">The data model to use for adding, updating and retrieving data from storage.</typeparam>
    /// <param name="name">The name of the collection.</param>
    /// <param name="options">Couchbase Query-specific configuration options for the collection.</param>
    /// <returns>The Query-based collection.</returns>
    public VectorStoreCollection<TKey, TRecord> GetCollection<TKey, TRecord>(string name, CouchbaseQueryCollectionOptions options)
        where TKey : notnull
        where TRecord : class
    {
        if (typeof(TRecord) == typeof(Dictionary<string, object?>))
        {
            throw new ArgumentException("For dynamic mapping via Dictionary<string, object?>, call GetDynamicCollection() instead.");
        }

        // Merge embedding generator from store options if not provided in collection options
        var mergedOptions = new CouchbaseQueryCollectionOptions
        {
            Definition = options.Definition,
            EmbeddingGenerator = options.EmbeddingGenerator ?? _embeddingGenerator,
            IndexName = options.IndexName,
            DistanceFunction = options.DistanceFunction,
            JsonSerializerOptions = options.JsonSerializerOptions,
            VectorDimensions = options.VectorDimensions,
            SimilarityMetric = options.SimilarityMetric,
            QuantizationSettings = options.QuantizationSettings,
            CentroidsToProbe = options.CentroidsToProbe,
            CompositeScalarKeys = options.CompositeScalarKeys,
            JsonDocumentCustomMapper = options.JsonDocumentCustomMapper
        };

        return new CouchbaseQueryCollection<TKey, TRecord>(_scope, name, mergedOptions, _options.IndexType);
    }

    /// <inheritdoc />
    public override VectorStoreCollection<object, Dictionary<string, object?>> GetDynamicCollection(string name, VectorStoreCollectionDefinition definition)
    {
        // Use the centralized IndexType to determine which dynamic collection type to create
        return _options.IndexType switch
        {
            CouchbaseIndexType.Search => GetSearchDynamicCollection(name, definition),
            CouchbaseIndexType.Bhive or CouchbaseIndexType.Composite => GetQueryDynamicCollection(name, definition),
            _ => throw new ArgumentException($"Unsupported index type: {_options.IndexType}")
        };
    }

    /// <summary>
    /// Gets a dynamic collection that uses Couchbase FTS (Full-Text Search) for vector operations.
    /// </summary>
    /// <param name="name">The name of the collection.</param>
    /// <param name="definition">The collection definition.</param>
    /// <returns>A search-based dynamic collection.</returns>
    [Experimental("MEVD9001")]
    public VectorStoreCollection<object, Dictionary<string, object?>> GetSearchDynamicCollection(string name, VectorStoreCollectionDefinition definition)
    {
        var collectionOptions = new CouchbaseSearchCollectionOptions
        {
            Definition = definition,
            EmbeddingGenerator = _embeddingGenerator
        };

        return new CouchbaseSearchDynamicCollection(_scope, name, collectionOptions);
    }

    /// <summary>
    /// Gets a dynamic collection that uses Couchbase SQL++ queries (BHIVE/COMPOSITE) for vector operations.
    /// </summary>
    /// <param name="name">The name of the collection.</param>
    /// <param name="definition">The collection definition.</param>
    /// <param name="options">Query-specific options for the dynamic collection.</param>
    /// <returns>A query-based dynamic collection.</returns>
    [Experimental("MEVD9001")]
    public VectorStoreCollection<object, Dictionary<string, object?>> GetQueryDynamicCollection(
        string name, 
        VectorStoreCollectionDefinition definition, 
        CouchbaseQueryCollectionOptions? options = null)
    {
        var collectionOptions = options ?? new CouchbaseQueryCollectionOptions();
        collectionOptions.Definition = definition;
        collectionOptions.EmbeddingGenerator ??= _embeddingGenerator;

        return new CouchbaseQueryDynamicCollection(_scope, name, collectionOptions, _options.IndexType);
    }

    /// <inheritdoc />
    public override async IAsyncEnumerable<string> ListCollectionNamesAsync([EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var collectionManager = _scope.Bucket.Collections;

        var scopes = await collectionManager
            .GetAllScopesAsync(new GetAllScopesOptions().CancellationToken(cancellationToken))
            .ConfigureAwait(false);

        var targetScope = scopes.FirstOrDefault(scope => scope.Name == _scope.Name);
        if (targetScope != null)
        {
            foreach (var collection in targetScope.Collections)
            {
                yield return collection.Name;
            }
        }
    }

    /// <inheritdoc />
    public override async Task<bool> CollectionExistsAsync(string name, CancellationToken cancellationToken = default)
    {
        var collectionManager = _scope.Bucket.Collections;

        var scopes = await collectionManager
            .GetAllScopesAsync(new GetAllScopesOptions().CancellationToken(cancellationToken))
            .ConfigureAwait(false);

        var targetScope = scopes.FirstOrDefault(scope => scope.Name == _scope.Name);
        if (targetScope != null)
        {
            return targetScope.Collections.Any(collection => collection.Name == name);
        }

        return false;
    }

    /// <inheritdoc />
    public override async Task EnsureCollectionDeletedAsync(string name, CancellationToken cancellationToken = default)
    {
        if (await CollectionExistsAsync(name, cancellationToken).ConfigureAwait(false))
        {
            var collectionManager = _scope.Bucket.Collections;
            await collectionManager
                .DropCollectionAsync(_scope.Name, name, null) // Adjust based on your SDK version
                .ConfigureAwait(false);
        }
    }



    /// <inheritdoc />
    public override object? GetService(Type serviceType, object? serviceKey = null)
    {
        return
            serviceKey is not null ? null :
            serviceType == typeof(VectorStoreMetadata) ? _metadata :
            serviceType == typeof(IScope) ? _scope :
            serviceType.IsInstanceOfType(this) ? this :
            null;
    }
}