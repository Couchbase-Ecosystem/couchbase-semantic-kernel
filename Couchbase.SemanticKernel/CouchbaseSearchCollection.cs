using System.Diagnostics.CodeAnalysis;
using System.Linq.Expressions;
using System.Runtime.CompilerServices;
using Couchbase.Core.IO.Transcoders;
using Couchbase.KeyValue;
using Couchbase.Search;
using Couchbase.Search.Queries.Compound;
using Couchbase.Search.Queries.Simple;
using Couchbase.Search.Queries.Vector;
using Couchbase.SemanticKernel.Diagnostics;
using Microsoft.Extensions.VectorData;
using Microsoft.Extensions.VectorData.ProviderServices;

namespace Couchbase.SemanticKernel;

/// <summary>
/// Service for storing and retrieving vector records using Couchbase FTS (Full-Text Search).
/// </summary>
/// <typeparam name="TKey">The data type of the record key.</typeparam>
/// <typeparam name="TRecord">The data model to use for adding, updating, and retrieving data from storage.</typeparam>
public class CouchbaseSearchCollection<TKey, TRecord> : CouchbaseCollectionBase<TKey, TRecord>
    where TKey : notnull
    where TRecord : class
{
    /// <summary>FTS index name to use for vector and hybrid search operations.</summary>
    private readonly string _vectorIndexName;

    /// <summary>Search-specific options for this collection.</summary>
    private readonly CouchbaseSearchCollectionOptions _searchOptions;

    /// <summary>
    /// Initializes a new instance of the <see cref="CouchbaseSearchCollection{TKey,TRecord}"/> class.
    /// </summary>
    /// <param name="scope"><see cref="IScope"/> that can be used to manage the collections in Couchbase.</param>
    /// <param name="name">The name of the collection.</param>
    /// <param name="options">Optional configuration options for this class.</param>
    public CouchbaseSearchCollection(
        IScope scope,
        string name,
        CouchbaseSearchCollectionOptions? options = null) : base(scope, name, options ?? new CouchbaseSearchCollectionOptions())
    {
        // Store the search-specific options
        _searchOptions = options ?? new CouchbaseSearchCollectionOptions();

        // Store the vector index name as a field
        _vectorIndexName = _searchOptions.IndexName ?? string.Empty;
    }

    /// <summary>
    /// Validates that the configured FTS index exists and is accessible.
    /// </summary>
    protected override async Task EnsureIndexExistsAsync(CancellationToken cancellationToken)
    {
        if (!string.IsNullOrEmpty(_vectorIndexName))
        {
            await ValidateFtsIndexExistsAsync(cancellationToken).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Validates that the configured FTS index exists and is accessible.
    /// </summary>
    private async Task ValidateFtsIndexExistsAsync(CancellationToken cancellationToken)
    {
        await RunOperationAsync("ValidateFtsIndex", async () =>
        {
            var searchIndexManager = _scope.Bucket.Cluster.SearchIndexes;

            try
            {
                var index = await searchIndexManager.GetIndexAsync(_vectorIndexName).ConfigureAwait(false);

                // Additional validation: ensure index is not in error state
                if (index == null)
                {
                    throw new InvalidOperationException(
                        $"FTS index '{_vectorIndexName}' exists but could not be retrieved. " +
                        "Please check index status and permissions.");
                }
            }
            catch (CouchbaseException ex) when (ex.Message.Contains("index not found") || ex.Message.Contains("not exist"))
            {
                throw new InvalidOperationException(
                    $"FTS index '{_vectorIndexName}' does not exist. " +
                    "Please create the FTS index with vector search enabled before using search operations.", ex);
            }
            catch (CouchbaseException ex)
            {
                throw new InvalidOperationException(
                    $"FTS index '{_vectorIndexName}' exists but is not accessible. " +
                    "Please check index status, permissions, and ensure the Search service is running.", ex);
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
        Verify.NotNull(searchValue);

        options ??= DefaultVectorSearchOptions;
        if (options.IncludeVectors && _model.EmbeddingGenerationRequired)
        {
            throw new NotSupportedException("IncludeVectors is not supported when embedding generation is configured.");
        }

        var vectorProperty = _model.GetVectorPropertyOrSingle(options);
        var searchVector = await GetSearchVectorAsync(searchValue, vectorProperty, cancellationToken).ConfigureAwait(false);

        // Build filter queries with support for both old and new filters
#pragma warning disable CS0618 // Type or member is obsolete
        var filter = options switch
        {
            { OldFilter: not null, Filter: not null } => throw new ArgumentException($"Either '{nameof(options.Filter)}' or '{nameof(options.OldFilter)}' can be specified, but not both."),
            { OldFilter: { } legacyFilter } => CouchbaseCollectionSearchMapping.BuildFromLegacyFilter(legacyFilter, _model),
            { Filter: { } newFilter } => new CouchbaseSearchFilterTranslator().Translate(newFilter, _model),
            _ => null
        };
#pragma warning restore CS0618 // Type or member is obsolete

        // Build the primary vector query
        var vectorQuery = VectorQuery.Create(
            vectorProperty.StorageName!,
            (float[])searchVector,
            new VectorQueryOptions
            {
                NumCandidates = _searchOptions.NumCandidates,
                Boost = _searchOptions.Boost
            });

        // Construct the final search request
        var searchRequest = new SearchRequest(
            // SearchQuery: filter,
            VectorSearch: VectorSearch.Create(vectorQuery)
        );

        // Validate that vector index name is configured
        if (string.IsNullOrEmpty(_vectorIndexName))
        {
            throw new InvalidOperationException(
                "Vector search requires an FTS index name. " +
                "Configure IndexName in CouchbaseSearchCollectionOptions.");
        }

        var searchResult = await RunOperationAsync("VectorSearch", () =>
            _scope.SearchAsync(
                _vectorIndexName,
                searchRequest,
                new SearchOptions()
                    .Limit(top)
                    .Skip(options.Skip)
            )).ConfigureAwait(false);

        // Map the search results to the target data model (TRecord)
        await foreach (var result in MapSearchResultsAsync(searchResult, options, cancellationToken))
        {
            yield return result;
        }
    }

    /// <inheritdoc />
    public override async IAsyncEnumerable<VectorSearchResult<TRecord>> HybridSearchAsync<TInput>(
        TInput searchValue,
        ICollection<string> keywords,
        int top,
        HybridSearchOptions<TRecord>? options = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        Verify.NotNull(keywords);

        // Resolve options
        options ??= DefaultKeywordVectorizedHybridSearchOptions;
        var vectorProperty = _model.GetVectorPropertyOrSingle<TRecord>(new() { VectorProperty = options.VectorProperty });
        var searchVector = await GetSearchVectorAsync(searchValue, vectorProperty, cancellationToken).ConfigureAwait(false);
        var textDataProperty = _model.GetFullTextDataPropertyOrSingle(options.AdditionalProperty);

        // Build filter queries with support for both old and new filters
#pragma warning disable CS0618 // Type or member is obsolete
        var filter = options switch
        {
            { OldFilter: not null, Filter: not null } => throw new ArgumentException($"Either '{nameof(options.Filter)}' or '{nameof(options.OldFilter)}' can be specified, but not both."),
            { OldFilter: { } legacyFilter } => CouchbaseCollectionSearchMapping.BuildFromLegacyFilter(legacyFilter, _model),
            { Filter: { } newFilter } => new CouchbaseSearchFilterTranslator().Translate(newFilter, _model),
            _ => null
        };
#pragma warning restore CS0618 // Type or member is obsolete

        // Build the primary vector query
        var vectorQuery = VectorQuery.Create(
            vectorProperty.StorageName!,
            (float[])searchVector,
            new VectorQueryOptions
            {
                NumCandidates = (uint?)Math.Max((int)Math.Ceiling(1.5f * top), 100),
                Boost = _searchOptions.Boost
            });

        var textQuery = new MatchQuery(string.Join(" ", keywords))
            .Field(textDataProperty.StorageName!);

        // Combine queries if filter exists
        ISearchQuery? finalQuery = textQuery;
        if (filter is not null)
        {
            var booleanQuery = new BooleanQuery();
            booleanQuery.Must(new ConjunctionQuery([filter]));
            booleanQuery.Should(new DisjunctionQuery([textQuery]));
            finalQuery = booleanQuery;
        }

        // Construct the final search request
        var searchRequest = new SearchRequest(
            SearchQuery: finalQuery,
            VectorSearch: VectorSearch.Create(vectorQuery)
        );

        // Validate that vector index name is configured
        if (string.IsNullOrEmpty(_vectorIndexName))
        {
            throw new InvalidOperationException(
                "Hybrid search requires an FTS index name. " +
                "Configure IndexName in CouchbaseSearchCollectionOptions.");
        }

        var searchResult = await RunOperationAsync("HybridSearch", () =>
            _scope.SearchAsync(
                _vectorIndexName,
                searchRequest,
                new SearchOptions()
                    .Limit(top)
                    .Skip(options.Skip)
            )).ConfigureAwait(false);

        // Map the search results to the target data model (TRecord)
        await foreach (var result in MapSearchResultsAsync(searchResult, new VectorSearchOptions<TRecord> { IncludeVectors = options.IncludeVectors }, cancellationToken))
        {
            yield return result;
        }
    }

    /// <inheritdoc />
    public override async IAsyncEnumerable<TRecord> GetAsync(
        Expression<Func<TRecord, bool>> filter,
        int top,
        FilteredRecordRetrievalOptions<TRecord>? options = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        // Build filter query
        var filterQuery = new CouchbaseSearchFilterTranslator().Translate(filter, _model);

        if (filterQuery == null)
        {
            yield break;
        }

        // Create search request with filter only (no vector search)
        var searchRequest = new SearchRequest(SearchQuery: filterQuery);

        // Validate that vector index name is configured for filtered search
        if (string.IsNullOrEmpty(_vectorIndexName))
        {
            throw new InvalidOperationException(
                "Filtered search requires an FTS index name. " +
                "Configure IndexName in CouchbaseSearchCollectionOptions.");
        }

        var searchResult = await RunOperationAsync("FilteredGet", () =>
            _scope.SearchAsync(
                _vectorIndexName,
                searchRequest,
                new SearchOptions()
                    .Limit(top)
                    .Skip(options?.Skip ?? 0)
            )).ConfigureAwait(false);

        // Map the search results to the target data model (TRecord)
        foreach (var hit in searchResult.Hits)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var docId = hit.Id;

            // Fetch the full document from KV by the doc ID
            var getResult = await _collection.GetAsync(docId,
                options => options.Transcoder(new RawJsonTranscoder())).ConfigureAwait(false);
            var docFromDb = getResult.ContentAs<byte[]>();

            // Map the document to the record
            var record = _mapper.MapFromStorageToDataModel(docFromDb, options?.IncludeVectors ?? false);

            yield return record;
        }
    }

    private async IAsyncEnumerable<VectorSearchResult<TRecord>> MapSearchResultsAsync(
       ISearchResult searchResult,
       VectorSearchOptions<TRecord> searchOptions,
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
            var getResult = await _collection.GetAsync(docId,
                options => options.Transcoder(new RawJsonTranscoder())).ConfigureAwait(false);
            var docFromDb = getResult.ContentAs<byte[]>();

            // Map the document to the record
            var record = _mapper.MapFromStorageToDataModel(docFromDb, searchOptions.IncludeVectors);

            yield return new VectorSearchResult<TRecord>(record, score);
        }
    }
}