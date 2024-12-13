using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.Json.Nodes;
using Connectors.Memory.Couchbase;
using Connectors.Memory.Couchbase.Data;
using Couchbase.Core.Exceptions.KeyValue;
using Couchbase.KeyValue;
using Couchbase.Management.Collections;
using Couchbase.Search;
using Couchbase.Search.Queries.Vector;
using Microsoft.Extensions.VectorData;
using VectorSearchOptions = Microsoft.Extensions.VectorData.VectorSearchOptions;

namespace Couchbase.SemanticKernel.Connectors.Couchbase;

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

    /// <summary>A <see cref="HashSet{T}"/> of types that data properties on the provided model may have.</summary>
    private static readonly HashSet<Type> s_supportedDataTypes = new()
    {
        typeof(bool),
        typeof(bool?),
        typeof(string),
        typeof(int),
        typeof(int?),
        typeof(long),
        typeof(long?),
        typeof(float),
        typeof(float?),
        typeof(double),
        typeof(double?),
        typeof(DateTimeOffset),
        typeof(DateTimeOffset?),
    };

    /// <summary>A <see cref="HashSet{T}"/> of types that vector properties on the provided model may have.</summary>
//     private static readonly HashSet<Type> s_supportedVectorTypes = new()
//     {
// #if NET5_0_OR_GREATER
//         typeof(ReadOnlyMemory<Half>),
//         typeof(ReadOnlyMemory<Half>?),
// #endif
//         typeof(ReadOnlyMemory<float>),
//         typeof(ReadOnlyMemory<float>?),
//         typeof(ReadOnlyMemory<double>),
//         typeof(ReadOnlyMemory<double>?),
//         typeof(ReadOnlyMemory<byte>),
//         typeof(ReadOnlyMemory<byte>?),
//         typeof(ReadOnlyMemory<sbyte>),
//         typeof(ReadOnlyMemory<sbyte>?),
//     };

    /// <summary>The default options for vector search.</summary>
    private static readonly VectorSearchOptions s_defaultVectorSearchOptions = new();

    private readonly IScope _scope;
    private readonly ICouchbaseCollection _collection;
    private readonly string _collectionName;
    private readonly CouchbaseVectorStoreRecordCollectionOptions<TRecord> _options;
    private readonly VectorStoreRecordPropertyReader _propertyReader;
    private readonly Dictionary<string, string> _storagePropertyNames = new();
    private readonly IVectorStoreRecordMapper<TRecord, JsonObject> _mapper;

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
        // Verify.NotNull(scope);
        // Verify.NotNullOrWhiteSpace(collectionName);

        this._scope = scope;
        this.CollectionName = collectionName;
        this._collection = scope.Collection(collectionName);
        this._options = options ?? new CouchbaseVectorStoreRecordCollectionOptions<TRecord>();

        // Initialize property reader
        this._propertyReader = new VectorStoreRecordPropertyReader(
            typeof(TRecord),
            this._options.VectorStoreRecordDefinition,
            new VectorStoreRecordPropertyReaderOptions
            {
                RequiresAtLeastOneVector = false,
                SupportsMultipleKeys = false,
                SupportsMultipleVectors = true,
                JsonSerializerOptions = this._options.JsonSerializerOptions ?? JsonSerializerOptions.Default
            });

        // Validate property types
        this._propertyReader.VerifyKeyProperties(s_supportedKeyTypes);
        this._propertyReader.VerifyDataProperties(s_supportedDataTypes, supportEnumerable: true);
        // this._propertyReader.VerifyVectorProperties(s_supportedVectorTypes);

        // Map storage property names
        foreach (var property in this._propertyReader.Properties)
        {
            this._storagePropertyNames[property.DataModelPropertyName] = property.DataModelPropertyName;
        }

        // Initialize mapper
        this._mapper = this.InitializeMapper(this._options.JsonSerializerOptions ?? JsonSerializerOptions.Default);
    }

    public async Task<VectorSearchResults<TRecord>> VectorizedSearchAsync<TVector>(
    TVector vector,
    VectorSearchOptions? options = null,
    CancellationToken cancellationToken = default)
    {
        // Constants for operation and score
        const string OperationName = "VectorizedSearch";
        const string ScorePropertyName = "similarityScore"; // Field for similarity score
    
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
        var vectorFieldName = this._storagePropertyNames[vectorProperty.DataModelPropertyName];
    
        // Build the primary vector query
        var vectorQuery = VectorQuery.Create(
            vectorFieldName,
            floatVector,
            new VectorQueryOptions
            {
                // Todo: consider using a helper function called GetNumCandidates
                NumCandidates = (uint?)this._options.NumCandidates ?? (uint)searchOptions.Top * 10, 
                Boost = this._options.Boost
            });
        
        // Construct the final search request
        var searchRequest = new SearchRequest(
            SearchQuery: this._options.FtsQuery,
            VectorSearch: VectorSearch.Create(vectorQuery)
        );
    
        // Execute the search query using Couchbase SDK
        var searchResult = await this._scope.SearchAsync(
            this._options.IndexName ?? throw new InvalidOperationException("Index name is required."),
            searchRequest,
            new SearchOptions()
                // .Limit(searchOptions.Top)                
                // .Skip(searchOptions.Skip)               
                // .ScanConsistency(SearchScanConsistency.RequestPlus)
                // .CancellationToken(cancellationToken)
        ).ConfigureAwait(false);
        
        // Map the search results to the target data model (TRecord)
        var mappedResults = this.MapSearchResultsAsync(
            searchResult,
            ScorePropertyName,
            OperationName,
            searchOptions,
            cancellationToken);
    
        // Return the results wrapped in a VectorSearchResults object
        return new VectorSearchResults<TRecord>(mappedResults);
    }
    
//    public async Task<VectorSearchResults<TRecord>> VectorizedSearchAsync<TVector>(
//     TVector vector,
//     VectorSearchOptions? options = null,
//     CancellationToken cancellationToken = default)
// {
//     // Constants for operation and score
//     const string OperationName = "VectorizedSearch";
//     const string ScorePropertyName = "similarityScore";
//
//     // Validate the input vector
//     if (vector is not float[] floatVector)
//     {
//         throw new NotSupportedException(
//             $"The provided vector type {vector.GetType().FullName} is not supported by the Couchbase connector. " +
//             $"Supported type: float[].");
//     }
//
//     // Retrieve collection-level options and combine with method-level options
//     var searchOptions = options ?? s_defaultVectorSearchOptions;
//
//     // Retrieve the vector property for the search
//     var vectorProperty = this.GetVectorPropertyForSearch(searchOptions.VectorPropertyName);
//     if (vectorProperty is null)
//     {
//         throw new InvalidOperationException(
//             "The collection does not have any vector properties, so vector search is not possible.");
//     }
//
//     // Map the vector field name to the storage property
//     var vectorFieldName = this._storagePropertyNames[vectorProperty.DataModelPropertyName];
//
//     // Build the primary vector query
//     var vectorQuery = VectorQuery.Create(
//         vectorFieldName,
//         floatVector,
//         new VectorQueryOptions
//         {
//             NumCandidates = (uint?)this._options.NumCandidates ?? (uint)searchOptions.Top * 10,
//             Boost = this._options.Boost
//         });
//
//     // Build the filter query if provided
//     var filterQuery = BuildFilter(searchOptions.Filter, this._storagePropertyNames);
//
//     // Combine vector query and filter query
//     ISearchQuery combinedQuery = filterQuery is not null
//         ? new BooleanQuery().Must(vectorQuery).Must(filterQuery)
//         : vectorQuery;
//
//     // Construct the final search request
//     var searchRequest = new SearchRequest(combinedQuery);
//
//     // Execute the search query using Couchbase SDK
//     var searchResult = await this._scope.SearchAsync(
//         this._options.IndexName ?? throw new InvalidOperationException("Index name is required."),
//         searchRequest,
//         new SearchOptions()
//             .Limit(searchOptions.Top)
//             .Skip(searchOptions.Skip)
//             .ScanConsistency(SearchScanConsistency.RequestPlus)
//             .CancellationToken(cancellationToken)
//     ).ConfigureAwait(false);
//
//     // Map the search results to the target data model (TRecord)
//     var mappedResults = this.MapSearchResultsAsync(
//         searchResult,
//         ScorePropertyName,
//         OperationName,
//         searchOptions,
//         cancellationToken);
//
//     // Return the results wrapped in a VectorSearchResults object
//     return new VectorSearchResults<TRecord>(mappedResults);
// }
//
//    private ISearchQuery? BuildFilter(VectorSearchFilter? filter, Dictionary<string, string> propertyMappings)
//    {
//        if (filter?.FilterClauses is null)
//        {
//            return null;
//        }
//
//        var mustQueries = new List<ISearchQuery>();
//        var shouldQueries = new List<ISearchQuery>();
//
//        foreach (var clause in filter.FilterClauses)
//        {
//            switch (clause)
//            {
//                case EqualToFilterClause equalToClause:
//                    if (propertyMappings.TryGetValue(equalToClause.FieldName, out var mappedField))
//                    {
//                        var query = CreateQueryForType(mappedField, equalToClause.Value);
//                        mustQueries.Add(query);
//                    }
//                    else
//                    {
//                        throw new InvalidOperationException($"Invalid filter field: {equalToClause.FieldName}");
//                    }
//                    break;
//
//                case AnyTagEqualToFilterClause anyTagClause:
//                    if (propertyMappings.TryGetValue(anyTagClause.FieldName, out var tagField))
//                    {
//                        foreach (var value in anyTagClause.Value)
//                        {
//                            var query = CreateQueryForType(tagField, value);
//                            shouldQueries.Add(query);
//                        }
//                    }
//                    else
//                    {
//                        throw new InvalidOperationException($"Invalid filter field: {anyTagClause.FieldName}");
//                    }
//                    break;
//
//                default:
//                    throw new NotSupportedException($"Unsupported filter type: {clause.GetType().Name}");
//            }
//        }
//
//        var booleanQuery = new BooleanQuery();
//
//        if (mustQueries.Any())
//        {
//            booleanQuery.Must(new ConjunctionQuery(mustQueries.ToArray()));
//        }
//
//        if (shouldQueries.Any())
//        {
//            booleanQuery.Should(new DisjunctionQuery(shouldQueries.ToArray()));
//        }
//
//        return booleanQuery;
//    }
//    
//    private ISearchQuery CreateQueryForType(string field, object value)
//    {
//        return value switch
//        {
//            string stringValue => new TermQuery(stringValue).Field(field),
//            int intValue => new NumericRangeQuery()
//                .Field(field)
//                .Min(intValue)
//                .Max(intValue),
//            float floatValue => new NumericRangeQuery()
//                .Field(field)
//                .Min(floatValue)
//                .Max(floatValue),
//            bool boolValue => new BooleanFieldQuery(boolValue).Field(field),
//            _ => throw new NotSupportedException($"Unsupported data type: {value.GetType().Name}")
//        };
//    }



private async IAsyncEnumerable<VectorSearchResult<TRecord>> MapSearchResultsAsync(
    ISearchResult searchResult,
    string scorePropertyName,
    string operationName,
    VectorSearchOptions searchOptions,
    [EnumeratorCancellation] CancellationToken cancellationToken)
{
    foreach (var hit in searchResult.Hits)
    {
        cancellationToken.ThrowIfCancellationRequested();

        // Extract similarity score from the hit
        var score = hit.Score;

        // Convert the hit fields to JSON
        var rawJson = JsonSerializer.Serialize(hit.Fields);
        var jsonObject = JsonSerializer.Deserialize<JsonObject>(rawJson);

        if (jsonObject is null)
        {
            throw new InvalidOperationException("Failed to deserialize search hit fields into JSON.");
        }

        // Remove the score from the result object (optional, if not required downstream)
        jsonObject.Remove(scorePropertyName);

        // Map JSON object to the data model (TRecord)
        var record = VectorStoreErrorHandler.RunModelConversion(
            DatabaseName,
            this.CollectionName,
            operationName,
            () => this._mapper.MapFromStorageToDataModel(jsonObject, new() { IncludeVectors = searchOptions.IncludeVectors }));

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


   // public async Task<bool> CollectionExistsAsync(CancellationToken cancellationToken = default)
    // {
    //     // Get all scopes from the bucket
    //     var scopes = await _scope.Bucket.Collections.GetAllScopesAsync(new GetAllScopesOptions().CancellationToken(cancellationToken)).ConfigureAwait(false);
    //
    //     // Find the current scope and check if the collection exists
    //     foreach (var scope in scopes)
    //     {
    //         if (scope.Name == _scope.Name)
    //         {
    //             return scope.Collections.Any(collection => collection.Name == this.CollectionName);
    //         }
    //     }
    //
    //     return false;
    // }
    
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

    // Todo: check if we really need to check collection already exists or we can directly create it
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
                .CreateCollectionAsync(collectionSpec, null) // Pass null for CreateCollectionOptions
                .ConfigureAwait(false);
        });
    }
    
    // Todo: should you put inside RunOperationAsync?
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

    // Todo: Not working, need to debug
    /// <inheritdoc />
    public async Task<TRecord?> GetAsync(string key, GetRecordOptions? options = null, CancellationToken cancellationToken = default)
    {
        // Validate the key
        // Verify.NotNullOrWhiteSpace(key);

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
                    return getResult.ContentAs<JsonObject>(); // Ensure deserialization
                }
                catch (DocumentNotFoundException)
                {
                    Console.WriteLine($"Document with key '{key}' not found.");
                    return null;
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
    
    public async IAsyncEnumerable<TRecord> GetBatchAsync(
        IEnumerable<string> keys,
        GetRecordOptions? options = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        // Verify.NotNull(keys);

        const string OperationName = "GetBatch";

        foreach (var key in keys)
        {
            var result = await this.RunOperationAsync(OperationName, async () =>
            {
                try
                {
                    var getResult = await this._collection.GetAsync(
                        key,
                        options: null // Customize GetOptions if needed
                    ).ConfigureAwait(false);

                    return getResult;
                }
                catch (DocumentNotFoundException)
                {
                    // Ignore missing documents in a batch context
                    return null;
                }
            }).ConfigureAwait(false);

            if (result is not null)
            {
                var jsonObject = result.ContentAs<JsonObject>();
                if (jsonObject is null)
                {
                    // Log or throw a specific error for debugging
                    throw new InvalidOperationException($"Failed to retrieve content for key: {key}");
                }

                // Map the JSON object to the data model
                var record = VectorStoreErrorHandler.RunModelConversion(
                    DatabaseName,
                    this.CollectionName,
                    OperationName,
                    () => this._mapper.MapFromStorageToDataModel(jsonObject, new() { IncludeVectors = options?.IncludeVectors ?? false })
                );

                if (record is null)
                {
                    throw new VectorStoreRecordMappingException($"Failed to map record for key: {key}");
                }

                yield return record;
            }
        }
    }


    public async Task DeleteAsync(string key, DeleteRecordOptions? options = null, CancellationToken cancellationToken = default)
    {
        await RunOperationAsync("Delete", async () =>
        {
            var removeOptions = new RemoveOptions();

            // Configure the options here if needed (currently null)
            await _collection.RemoveAsync(key, removeOptions).ConfigureAwait(false);
        });
    }

    // Todo: should you add RunOperationAsync?
    public async Task DeleteBatchAsync(IEnumerable<string> keys, DeleteRecordOptions? options = null, CancellationToken cancellationToken = default)
    {
        foreach (var key in keys)
        {
            await this._collection.RemoveAsync(key).ConfigureAwait(false);
        }
    }

    // Todo: verify the this._propertyReader.KeyPropertyName, will the key always be the first element, what if this changes
    public async Task<string> UpsertAsync(
        TRecord record,
        UpsertRecordOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        // Verify.NotNull(record);

        const string OperationName = "Upsert";

        // Convert the data model to the storage model
        var jsonObject = VectorStoreErrorHandler.RunModelConversion(
            DatabaseName,
            this.CollectionName,
            OperationName,
            () => this._mapper.MapFromDataToStorageModel(record));

        // Retrieve the key value from the JSON object
        if (!jsonObject.TryGetPropertyValue(this._propertyReader.KeyPropertyName, out var keyValue) || string.IsNullOrWhiteSpace(keyValue?.ToString()))
        {
            throw new VectorStoreOperationException($"Key property {this._propertyReader.KeyPropertyName} is not initialized.");
        }

        var key = keyValue.ToString()!;

        // Perform the upsert operation
        await this.RunOperationAsync(OperationName, async () =>
        {
            await this._collection.UpsertAsync(
                key,
                jsonObject,
                options: null // Add specific UpsertOptions if required
            ).ConfigureAwait(false);
        }).ConfigureAwait(false);

        return key;
    }

    /// <inheritdoc />
    public async IAsyncEnumerable<string> UpsertBatchAsync(
        IEnumerable<TRecord> records,
        UpsertRecordOptions? options = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        // Verify the records are not null
        // Verify.NotNull(records);

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
    
    //Todo: Check if initialize mapper match the standards comparing with others
    private IVectorStoreRecordMapper<TRecord, JsonObject> InitializeMapper(JsonSerializerOptions jsonSerializerOptions)
    {
        if (this._options.JsonDocumentCustomMapper is not null)
        {
            return this._options.JsonDocumentCustomMapper;
        }

        if (typeof(TRecord) == typeof(VectorStoreGenericDataModel<string>))
        {
            var mapper = new CouchbaseGenericDataModelMapper(this._propertyReader.Properties, this._storagePropertyNames, jsonSerializerOptions);
            return (mapper as IVectorStoreRecordMapper<TRecord, JsonObject>)!;
        }

        return new CouchbaseVectorStoreRecordMapper<TRecord>(
            this._storagePropertyNames,
            jsonSerializerOptions);
    }
}
