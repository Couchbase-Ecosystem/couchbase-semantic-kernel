using System.Runtime.CompilerServices;
using System.Text.Json;
using Couchbase.Core.Exceptions.KeyValue;
using Couchbase.KeyValue;
using Couchbase.Management.Collections;
using Couchbase.Search;
using Couchbase.Search.Queries.Vector;
using Couchbase.SemanticKernel.Data;
using Couchbase.SemanticKernel.Diagnostics;
using Microsoft.Extensions.VectorData;
using VectorSearchOptions = Microsoft.Extensions.VectorData.VectorSearchOptions;

namespace Couchbase.SemanticKernel;

/// <summary>
/// Service for storing and retrieving vector records, using Couchbase as the underlying storage.
/// </summary>
/// <typeparam name="TRecord">The data model to use for adding, updating, and retrieving data from storage.</typeparam>
public sealed class CouchbaseVectorStoreRecordCollection<TRecord> : IVectorStoreRecordCollection<string, TRecord>
{
    /// <summary>The name of this database for telemetry purposes.</summary>
    private const string DatabaseName = "Couchbase";

    /// <summary>A <see cref="HashSet{T}"/> of types that a key on the provided model may have.</summary>
    private static readonly HashSet<Type> s_supportedKeyTypes = new()
    {
        typeof(string)
    };

    /// <summary>The default options for vector search.</summary>
    private static readonly VectorSearchOptions s_defaultVectorSearchOptions = new();

    /// <summary>The Couchbase scope to use for storing and retrieving records.</summary>
    private readonly IScope _scope;
    
    /// <summary>The Couchbase collection to use for storing and retrieving records.</summary>
    private readonly ICouchbaseCollection _collection;
    
    /// <summary>The name of the collection.</summary>
    private readonly string _collectionName;
    
    /// <summary>Optional configuration options for this class.</summary>
    private readonly CouchbaseVectorStoreRecordCollectionOptions<TRecord> _options;
    
    /// <summary>A helper to access property information for the current data model and record definition.</summary>
    private readonly VectorStoreRecordPropertyReader _propertyReader;
    
    /// <summary>A dictionary that maps from a property name to the storage name that should be used when serializing it to json for data and vector properties.</summary>
    private readonly Dictionary<string, string> _storagePropertyNames = new();
    
    /// <summary>The mapper to use when converting between the data model and the Couchbase record.</summary>
    private readonly IVectorStoreRecordMapper<TRecord, TRecord> _mapper;

    /// <inheritdoc />
    public string CollectionName { get; }
    
    /// <summary>
    /// Initializes a new instance of the <see cref="CouchbaseVectorStoreRecordCollection{TRecord}"/> class.
    /// </summary>
    /// <param name="scope"><see cref="IScope"/> that can be used to manage the collections in Couchbase.</param>
    /// <param name="collectionName">The name of the collection.</param>
    /// <param name="options">Optional configuration options for this class.</param>
    public CouchbaseVectorStoreRecordCollection(
        IScope scope,
        string collectionName,
        CouchbaseVectorStoreRecordCollectionOptions<TRecord>? options = null)
    {
        // Verify parameters
        Verify.NotNull(scope);
        Verify.NotNullOrWhiteSpace(collectionName);

        _scope = scope;
        CollectionName = collectionName;
        _collection = scope.Collection(collectionName);
        _options = options ?? new CouchbaseVectorStoreRecordCollectionOptions<TRecord>();

        // Initialize property reader
        _propertyReader = new VectorStoreRecordPropertyReader(
            typeof(TRecord),
            _options.VectorStoreRecordDefinition,
            new VectorStoreRecordPropertyReaderOptions
            {
                RequiresAtLeastOneVector = false,
                SupportsMultipleKeys = false,
                SupportsMultipleVectors = true,
                JsonSerializerOptions = this._options.JsonSerializerOptions ?? JsonSerializerOptions.Default
            });

        // Validate property types
        this._propertyReader.VerifyKeyProperties(s_supportedKeyTypes);

        // Map storage property names
        foreach (var property in _propertyReader.Properties)
        {
            _storagePropertyNames[property.DataModelPropertyName] = property.DataModelPropertyName;
        }

        // Initialize mapper
        _mapper = this.InitializeMapper(this._options.JsonSerializerOptions ?? JsonSerializerOptions.Default);
    }

    /// <inheritdoc />
    public async Task<bool> CollectionExistsAsync(CancellationToken cancellationToken = default)
    {
        return await this.RunOperationAsync("CheckCollectionExists", async () =>
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
                return targetScope.Collections.Any(collection => collection.Name == this.CollectionName);
            }

            // If the scope does not exist, the collection cannot exist
            return false;
        }).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task CreateCollectionAsync(CancellationToken cancellationToken = default)
    {
        // Run the operation to create the collection
        await this.RunOperationAsync("CreateCollection", async () =>
        {
            var collectionManager = _scope.Bucket.Collections;

            // Check if the collection already exists in the specified scope
            var scopes = await collectionManager
                .GetAllScopesAsync(new GetAllScopesOptions().CancellationToken(cancellationToken))
                .ConfigureAwait(false);

            var targetScope = scopes.FirstOrDefault(scope => scope.Name == _scope.Name);

            if (targetScope == null)
            {
                throw new InvalidOperationException($"Scope '{_scope.Name}' does not exist.");
            }

            if (targetScope.Collections.Any(collection => collection.Name == this.CollectionName))
            {
                // Collection already exists
                return;
            }

            // Create the collection in the specified scope
            var collectionSpec = new CollectionSpec(_scope.Name, this.CollectionName);
            await collectionManager
                .CreateCollectionAsync(collectionSpec, null)
                .ConfigureAwait(false);
        });
    }
    
    /// <inheritdoc />
    public async Task CreateCollectionIfNotExistsAsync(CancellationToken cancellationToken = default)
    {
        // Check if the collection already exists
        if (!await this.CollectionExistsAsync(cancellationToken).ConfigureAwait(false))
        {
            // Create the collection if it does not exist
            await this.CreateCollectionAsync(cancellationToken).ConfigureAwait(false);
        }
    }
    
    /// <inheritdoc />
    public async Task DeleteCollectionAsync(CancellationToken cancellationToken = default)
    {
        await this.RunOperationAsync("DeleteCollection", async () =>
        {
            var collectionManager = _scope.Bucket.Collections;

            // Use the scope name and collection name directly
            await collectionManager
                .DropCollectionAsync(_scope.Name, this.CollectionName, null)
                .ConfigureAwait(false);
        });
    }

    /// <inheritdoc />
    public async Task<TRecord?> GetAsync(string key, GetRecordOptions? options = null, CancellationToken cancellationToken = default)
    {
        // Validate the key
        Verify.NotNullOrWhiteSpace(key);

        const string OperationName = "Get";

        var includeVectors = options?.IncludeVectors ?? false;

        try
        {
            // Run the operation to get the record
            var result = await this.RunOperationAsync(OperationName, async () =>
            {
                try
                {
                    var getResult = await this._collection.GetAsync(key).ConfigureAwait(false);

                    return getResult.ContentAs<TRecord>();
                }
                catch (DocumentNotFoundException)
                {
                    Console.WriteLine($"Document with key '{key}' not found.");
                    return default;
                }
            }).ConfigureAwait(false);

            if (result is null)
            {
                return default;
            }

            // Map the storage model to the data model
            return VectorStoreErrorHandler.RunModelConversion(
                DatabaseName,
                this.CollectionName,
                OperationName,
                () => this._mapper.MapFromStorageToDataModel(result, new() { IncludeVectors = includeVectors })
            );
        }
        catch (DocumentNotFoundException)
        {
            // Handle document not found scenario gracefully
            return default;
        }
    }
    
    /// <inheritdoc />
    public async IAsyncEnumerable<TRecord> GetBatchAsync(
        IEnumerable<string> keys,
        GetRecordOptions? options = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        const string OperationName = "GetBatch";

        foreach (var key in keys)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var result = await this.RunOperationAsync(OperationName, async () =>
            {
                try
                {
                    var getResult = await this._collection.GetAsync(key).ConfigureAwait(false);

                    return getResult.ContentAs<TRecord>();
                }
                catch (DocumentNotFoundException)
                {
                    // Ignore missing documents in a batch context
                    return default;
                }
            }).ConfigureAwait(false);

            if (result is not null)
            {
                // Map the retrieved record to the data model
                var record = VectorStoreErrorHandler.RunModelConversion(
                    DatabaseName,
                    this.CollectionName,
                    OperationName,
                    () => this._mapper.MapFromStorageToDataModel(result, new() { IncludeVectors = options?.IncludeVectors ?? false })
                );

                if (record is null)
                {
                    throw new VectorStoreRecordMappingException($"Failed to map record for key: {key}");
                }

                yield return record;
            }
        }
    }

    /// <inheritdoc />
    public async Task DeleteAsync(string key, DeleteRecordOptions? options = null, CancellationToken cancellationToken = default)
    {
        await RunOperationAsync("Delete", async () =>
        {
            var removeOptions = new RemoveOptions();

            // Configure the options here if needed (currently null)
            await _collection.RemoveAsync(key, removeOptions).ConfigureAwait(false);
        });
    }

    /// <inheritdoc />
    public async Task DeleteBatchAsync(IEnumerable<string> keys, DeleteRecordOptions? options = null, CancellationToken cancellationToken = default)
    {
        foreach (var key in keys)
        {
            await this._collection.RemoveAsync(key).ConfigureAwait(false);
        }
    }

    /// <inheritdoc />
    public async Task<string> UpsertAsync(
        TRecord record,
        UpsertRecordOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        Verify.NotNull(record);

        const string OperationName = "Upsert";

        // Convert the data model to the storage model
        var storageModel = VectorStoreErrorHandler.RunModelConversion(
            DatabaseName,
            this.CollectionName,
            OperationName,
            () => this._mapper.MapFromDataToStorageModel(record));

        // Retrieve the key value from the storage model
        var keyPropertyName = this._propertyReader.KeyPropertyName;

        // Use reflection or a direct property lookup
        var keyProperty = typeof(TRecord).GetProperty(keyPropertyName);

        if (keyProperty == null)
        {
            throw new VectorStoreOperationException($"Key property {keyPropertyName} not found.");
        }

        var keyValue = keyProperty.GetValue(record)?.ToString();

        if (string.IsNullOrWhiteSpace(keyValue))
        {
            throw new VectorStoreOperationException($"Key property {keyPropertyName} is not initialized.");
        }

        // Perform the upsert operation
        await this.RunOperationAsync(OperationName, async () =>
        {
            await this._collection.UpsertAsync(keyValue, storageModel).ConfigureAwait(false);
        }).ConfigureAwait(false);

        return keyValue;
    }

    /// <inheritdoc />
    public async IAsyncEnumerable<string> UpsertBatchAsync(
        IEnumerable<TRecord> records,
        UpsertRecordOptions? options = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        // Verify the records are not null
        Verify.NotNull(records);

        // Process each record individually
        foreach (var record in records)
        {
            if (record is not null)
            {
                // Call the UpsertAsync method for each record
                var result = await this.UpsertAsync(record, options, cancellationToken).ConfigureAwait(false);

                // Yield the result (record key)
                yield return result;
            }
        }
    }
    
    public async Task<VectorSearchResults<TRecord>> VectorizedSearchAsync<TVector>(
    TVector vector,
    VectorSearchOptions? options = null,
    CancellationToken cancellationToken = default)
    {
        // Constants for operation and score
        const string OperationName = "VectorizedSearch";
        const string ScorePropertyName = "similarityScore";
    
        // Validate the input vector
        float[] floatVector = vector switch
        {
            float[] v => v,
            ReadOnlyMemory<float> v => v.ToArray(),
            IEnumerable<float> v => v.ToArray(),
            _ => throw new NotSupportedException(
                $"The provided vector type {vector.GetType().FullName} is not supported by the Couchbase connector. " +
                $"Supported types: float[], ReadOnlyMemory<float>, IEnumerable<float>.")
        };
    
        // Retrieve collection-level options and combine with method-level options
        var searchOptions = options ?? s_defaultVectorSearchOptions;
    
        // Retrieve the vector property for the search
        var vectorProperty = this.GetVectorPropertyForSearch(searchOptions.VectorPropertyName);
        if (vectorProperty is null)
        {
            throw new InvalidOperationException(
                "The collection does not have any vector properties, so vector search is not possible.");
        }
    
        // Map the vector field name to the storage property
        var vectorFieldName = ToCamelCase(this._storagePropertyNames[vectorProperty.DataModelPropertyName]);
    
        // Build the primary vector query
        var vectorQuery = VectorQuery.Create(
            vectorFieldName,
            floatVector,
            new VectorQueryOptions
            {
                NumCandidates = (uint?)_options.NumCandidates, 
                Boost = _options.Boost
            });
        
        var filterQuery = CouchbaseVectorStoreCollectionSearchMapping.BuildFilter(searchOptions.Filter, this._storagePropertyNames);
        
        // Construct the final search request
        var searchRequest = new SearchRequest(
            SearchQuery: filterQuery,
            VectorSearch: VectorSearch.Create(vectorQuery)
        );

        var searchResult = await this._scope.SearchAsync(
            this._options.IndexName ?? throw new InvalidOperationException("Index name is required."),
            searchRequest,
            new SearchOptions()
                .Limit(searchOptions.Top)
                .Skip(searchOptions.Skip)             
        ).ConfigureAwait(false);
        
        // Map the search results to the target data model (TRecord)
        var mappedResults = this.MapSearchResultsAsync(
            searchResult,
            ScorePropertyName,
            OperationName,
            searchOptions,
            cancellationToken);
    
        // Return the results wrapped in a VectorSearchResults object
        return await Task.FromResult(new VectorSearchResults<TRecord>(mappedResults));
    }

    private static string ToCamelCase(string input)
    {
        if (string.IsNullOrEmpty(input) || char.IsLower(input[0]))
        {
            return input; // Already camel case or invalid input
        }

        return char.ToLowerInvariant(input[0]) + input.Substring(1);
    }

    private async IAsyncEnumerable<VectorSearchResult<TRecord>> MapSearchResultsAsync(
       ISearchResult searchResult,
       string scorePropertyName,
       string operationName,
       VectorSearchOptions searchOptions,
       [EnumeratorCancellation] CancellationToken cancellationToken)
   {
       if (searchResult is null)
       {
           throw new ArgumentNullException(nameof(searchResult), "Search result cannot be null.");
       }

       foreach (var hit in searchResult.Hits)
       {
           var docId = hit.Id;
           var score = hit.Score;

           // Fetch the full document from KV by the doc ID
           var getResult = await this._collection.GetAsync(docId).ConfigureAwait(false);
           var docFromDb = getResult.ContentAs<TRecord>();

           // Optionally run your mapper (if you have a custom mapping layer)
           var record = VectorStoreErrorHandler.RunModelConversion(
               DatabaseName,
               this.CollectionName,
               operationName,
               () => this._mapper.MapFromStorageToDataModel(
                   docFromDb,
                   new StorageToDataModelMapperOptions
                   {
                       IncludeVectors = searchOptions.IncludeVectors
                   }));

           yield return new VectorSearchResult<TRecord>(record, score);
       }
   }

   private VectorStoreRecordVectorProperty? GetVectorPropertyForSearch(string? vectorFieldName)
   {
       // If vector property name is provided in options, try to find it in schema or throw an exception.
       if (!string.IsNullOrWhiteSpace(vectorFieldName))
       {
           // Check vector properties by data model property name.
           var vectorProperty = this._propertyReader.VectorProperties
               .FirstOrDefault(l => l.DataModelPropertyName.Equals(vectorFieldName, StringComparison.Ordinal));

           if (vectorProperty is not null)
           {
               return vectorProperty;
           }

           throw new InvalidOperationException($"The {typeof(TRecord).FullName} type does not have a vector property named '{vectorFieldName}'.");
       }

       // If vector property is not provided in options, return first vector property from schema.
       return this._propertyReader.VectorProperty;
   }

   private async Task RunOperationAsync(string operationName, Func<Task> operation)
    {
        try
        {
            await operation.Invoke().ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            throw new VectorStoreOperationException("Call to vector store failed.", ex)
            {
                VectorStoreType = DatabaseName,
                CollectionName = this.CollectionName,
                OperationName = operationName
            };
        }
    }
    
    private async Task<T> RunOperationAsync<T>(string operationName, Func<Task<T>> operation)
    {
        try
        {
            return await operation.Invoke().ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            throw new VectorStoreOperationException("Call to Couchbase vector store failed.", ex)
            {
                VectorStoreType = DatabaseName,
                CollectionName = this.CollectionName,
                OperationName = operationName
            };
        }
    }
    
    /// <summary>
    /// Returns custom mapper, generic data model mapper or default record mapper.
    /// </summary>
    private IVectorStoreRecordMapper<TRecord, TRecord> InitializeMapper(JsonSerializerOptions jsonSerializerOptions)
    {
        // Use custom mapper if provided
        if (this._options.JsonDocumentCustomMapper is not null)
        {
            return this._options.JsonDocumentCustomMapper;
        }

        // Check if the type is a generic data model
        if (typeof(TRecord) == typeof(VectorStoreGenericDataModel<string>))
        {
            var mapper = new CouchbaseGenericDataModelMapper(
                this._propertyReader.Properties,
                this._storagePropertyNames,
                jsonSerializerOptions);

            // Return the mapper cast to the expected type
            return (mapper as IVectorStoreRecordMapper<TRecord, TRecord>)!;
        }

        // Fallback to default mapper with correct type parameter
        return new CouchbaseVectorStoreRecordMapper<TRecord>(jsonSerializerOptions);
    }

}
