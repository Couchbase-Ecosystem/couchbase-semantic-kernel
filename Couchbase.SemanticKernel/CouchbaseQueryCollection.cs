using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Linq.Expressions;
using System.Runtime.CompilerServices;
using System.Text.Json;
using Couchbase.Core.IO.Transcoders;
using Couchbase.KeyValue;
using Couchbase.Query;
using Couchbase.SemanticKernel.Diagnostics;
using Microsoft.Extensions.VectorData;
using Microsoft.Extensions.VectorData.ProviderServices;

namespace Couchbase.SemanticKernel;

/// <summary>
/// Service for storing and retrieving vector records using Couchbase SQL++ queries (BHIVE and COMPOSITE indexes).
/// </summary>
/// <typeparam name="TKey">The data type of the record key.</typeparam>
/// <typeparam name="TRecord">The data model to use for adding, updating, and retrieving data from storage.</typeparam>
public class CouchbaseQueryCollection<TKey, TRecord> : CouchbaseCollectionBase<TKey, TRecord>
    where TKey : notnull
    where TRecord : class
{
    /// <summary>SQL filter translator for converting LINQ expressions to SQL++ WHERE clauses.</summary>
    private readonly CouchbaseQueryFilterTranslator _queryFilterTranslator;

    /// <summary>Query-specific options for this collection.</summary>
    private readonly CouchbaseQueryCollectionOptions _queryOptions;

    /// <summary>The index type to use for this collection.</summary>
    private readonly CouchbaseIndexType _indexType;

    /// <summary>
    /// Initializes a new instance of the <see cref="CouchbaseQueryCollection{TKey,TRecord}"/> class.
    /// </summary>
    /// <param name="scope"><see cref="IScope"/> that can be used to manage the collections in Couchbase.</param>
    /// <param name="name">The name of the collection.</param>
    /// <param name="options">Optional configuration options for this class.</param>
    /// <param name="indexType">The index type to use for vector operations.</param>
    public CouchbaseQueryCollection(
        IScope scope,
        string name,
        CouchbaseQueryCollectionOptions? options = null,
        CouchbaseIndexType indexType = CouchbaseIndexType.Bhive) : base(scope, name, options ?? new CouchbaseQueryCollectionOptions())
    {
        _queryFilterTranslator = new CouchbaseQueryFilterTranslator();
        _queryOptions = options ?? new CouchbaseQueryCollectionOptions();
        _indexType = indexType;
    }

    /// <summary>
    /// Ensures the appropriate index (BHIVE or COMPOSITE) exists for this collection.
    /// </summary>
    protected override async Task EnsureIndexExistsAsync(CancellationToken cancellationToken)
    {
        switch (_indexType)
        {
            case CouchbaseIndexType.Bhive:
            case CouchbaseIndexType.Composite:
                await CreateVectorIndexIfNotExistsAsync(_indexType, cancellationToken).ConfigureAwait(false);
                break;
            case CouchbaseIndexType.Search:
                throw new InvalidOperationException(
                    "Search index type is not supported for CouchbaseQueryCollection. " +
                    "Use CouchbaseSearchCollection instead.");
            default:
                throw new InvalidOperationException(
                    $"Unsupported index type '{_indexType}' for CouchbaseQueryCollection. " +
                    "Supported types are: BHIVE, COMPOSITE");
        }
    }
    
    /// <summary>
    /// Creates a vector index (BHIVE or COMPOSITE) if it doesn't already exist.
    /// </summary>
    /// <param name="indexType">The type of index to create (BHIVE or COMPOSITE).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    private async Task CreateVectorIndexIfNotExistsAsync(CouchbaseIndexType indexType, CancellationToken cancellationToken)
    {
        var operationName = indexType == CouchbaseIndexType.Bhive ? "CreateBhiveIndex" : "CreateCompositeIndex";
        
        await RunOperationAsync(operationName, async () =>
        {
            var vectorProperty = _model.VectorProperties.FirstOrDefault();
            if (vectorProperty == null)
            {
                throw new InvalidOperationException($"No vector property found for {indexType} index creation.");
            }

            // Extract common properties
            var indexName = _queryOptions.IndexName ?? $"{Name}_{indexType.ToString().ToLower()}_index";
            var bucketName = _scope.Bucket.Name;
            var scopeName = _scope.Name;
            var collectionName = Name;
            var vectorField = vectorProperty.StorageName;
            var dimensions = vectorProperty.Dimensions;

            // Build index parameters using the mapping class
            var indexParams = CouchbaseQueryCollectionCreateMapping.BuildIndexParameters(vectorProperty, _queryOptions);

            // Serialize index parameters to JSON properly
            var paramsString = JsonSerializer.Serialize(indexParams);

            // Build the WHERE clause for partial indexing
            var whereClause = CouchbaseQueryCollectionCreateMapping.BuildIndexWhereClause(_queryOptions);

            // Build the appropriate query based on index type
            string createIndexQuery;
            
            if (indexType == CouchbaseIndexType.Bhive)
            {
                // BHIVE: CREATE VECTOR INDEX with INCLUDE clause
                var includeFields = CouchbaseQueryCollectionCreateMapping.GetBhiveIncludeFields(_model);
                var includeClause = includeFields.Any() ? $"INCLUDE ({string.Join(", ", includeFields)})" : "";
                
                createIndexQuery = $@"
                    CREATE VECTOR INDEX `{indexName}` 
                    ON `{bucketName}`.`{scopeName}`.`{collectionName}` ({vectorField} VECTOR) 
                    {includeClause}
                    {whereClause}
                    USING GSI WITH {paramsString}";
            }
            else
            {
                // COMPOSITE: CREATE INDEX with vector + scalar fields
                var indexKeys = new List<string> { $"{vectorField} VECTOR" };
                
                // Add scalar fields for composite indexing (enables pre-filtering)
                var scalarFields = CouchbaseQueryCollectionCreateMapping.GetCompositeIndexFields(_model, _queryOptions.CompositeScalarKeys);
                if (scalarFields.Any())
                {
                    indexKeys.AddRange(scalarFields);
                }

                createIndexQuery = $@"
                    CREATE INDEX `{indexName}` 
                    ON `{bucketName}`.`{scopeName}`.`{collectionName}` ({string.Join(", ", indexKeys)})
                    {whereClause}
                    USING GSI WITH {paramsString}";
            }

            try
            {
                await _scope.Bucket.Cluster.QueryAsync<dynamic>(createIndexQuery).ConfigureAwait(false);
            }
            catch (CouchbaseException ex) when (ex.Message.Contains("already exists"))
            {
                // Index already exists, which is fine
            }
        }).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public override async IAsyncEnumerable<VectorSearchResult<TRecord>> SearchAsync<TInput>(
        TInput searchValue,
        int top,
        VectorSearchOptions<TRecord>? options = default,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var vectorProperty = _model.VectorProperties.FirstOrDefault();
        if (vectorProperty == null)
        {
            throw new InvalidOperationException("No vector property found for search.");
        }

        var searchVector = await GetSearchVectorAsync(searchValue, vectorProperty, cancellationToken).ConfigureAwait(false);
        
        var bucketName = _scope.Bucket.Name;
        var scopeName = _scope.Name;
        var collectionName = Name;
        var vectorField = vectorProperty.StorageName;

        // Get the similarity metric for the ANN_DISTANCE function
        var similarityMetric = CouchbaseQueryCollectionCreateMapping.MapSimilarityMetric(_queryOptions.SimilarityMetric);
        var formattedVector = CouchbaseQueryCollectionCreateMapping.FormatVectorForSql(searchVector.ToArray().AsMemory());
        
        // Build the SQL query for pure vector search (no WHERE clause)
        var selectClause = $"SELECT b.*, META(b).id AS _id, ANN_DISTANCE({vectorField}, {formattedVector}, '{similarityMetric}') AS _distance";
        var fromClause = $"FROM `{bucketName}`.`{scopeName}`.`{collectionName}` b";
        var orderByClause = $"ORDER BY _distance ASC";
        var limitClause = $"LIMIT {top} OFFSET {options?.Skip ?? 0}";

        var sqlQuery = $"{selectClause} {fromClause} {orderByClause} {limitClause}";

        var queryResult = await RunOperationAsync("VectorSearch", () =>
            _scope.Bucket.Cluster.QueryAsync<dynamic>(sqlQuery)).ConfigureAwait(false);

        await foreach (var row in queryResult.Rows)
        {
            cancellationToken.ThrowIfCancellationRequested();

            // Extract document content, ID, and distance from the query result
            var rowDict = row as IDictionary<string, object?> ?? 
                          throw new InvalidOperationException("Query result row is not a dictionary");
            
            if (!rowDict.TryGetValue("_id", out var idObj) || idObj?.ToString() is not string docId || string.IsNullOrEmpty(docId))
            {
                continue;
            }

            var distance = 0.0;
            if (rowDict.TryGetValue("_distance", out var distanceObj) && distanceObj != null)
            {
                distance = Convert.ToDouble(distanceObj);
            }

            // Remove metadata fields to get clean document content
            var cleanRow = rowDict.Where(kvp => !kvp.Key.StartsWith("_")).ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
            
            // Convert to JSON and then to TRecord
            var jsonBytes = JsonSerializer.SerializeToUtf8Bytes(cleanRow);
            var record = _mapper.MapFromStorageToDataModel(jsonBytes, options?.IncludeVectors ?? false);

            // Convert distance to score (lower distance = higher score)
            var score = distance > 0 ? 1.0 / (1.0 + distance) : 1.0;

            yield return new VectorSearchResult<TRecord>(record, score);
        }
    }

    /// <inheritdoc />
    public override IAsyncEnumerable<VectorSearchResult<TRecord>> HybridSearchAsync<TInput>(
        TInput searchValue,
        ICollection<string> keywords,
        int top,
        HybridSearchOptions<TRecord>? options = null,
        CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException("Hybrid search is not yet implemented for CouchbaseQueryCollection. Use CouchbaseSearchCollection for hybrid search capabilities.");
    }

    /// <inheritdoc />
    public override async IAsyncEnumerable<TRecord> GetAsync(
        Expression<Func<TRecord, bool>> filter,
        int top,
        FilteredRecordRetrievalOptions<TRecord>? options = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var whereClause = _queryFilterTranslator.Translate(filter, _model);
        if (string.IsNullOrEmpty(whereClause))
        {
            yield break;
        }

        var bucketName = _scope.Bucket.Name;
        var scopeName = _scope.Name;
        var collectionName = Name;
        
        var sqlQuery = $@"
            SELECT b.*, META(b).id AS _id
            FROM `{bucketName}`.`{scopeName}`.`{collectionName}` b
            WHERE {whereClause}
            LIMIT {top}
            OFFSET {options?.Skip ?? 0}";

        var queryResult = await RunOperationAsync("FilteredGet", () =>
            _scope.Bucket.Cluster.QueryAsync<dynamic>(sqlQuery)).ConfigureAwait(false);

        await foreach (var row in queryResult.Rows)
        {
            cancellationToken.ThrowIfCancellationRequested();

            // Extract document content and ID from the query result
            var rowDict = row as IDictionary<string, object?> ?? 
                          throw new InvalidOperationException("Query result row is not a dictionary");
            
            if (!rowDict.TryGetValue("_id", out var idObj) || idObj?.ToString() is not string docId || string.IsNullOrEmpty(docId))
            {
                continue;
            }

            // Remove metadata fields to get clean document content
            var cleanRow = rowDict.Where(kvp => !kvp.Key.StartsWith("_")).ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
            
            // Convert to JSON and then to TRecord
            var jsonBytes = System.Text.Json.JsonSerializer.SerializeToUtf8Bytes(cleanRow);
            var record = _mapper.MapFromStorageToDataModel(jsonBytes, options?.IncludeVectors ?? false);

            yield return record;
        }
    }
} 