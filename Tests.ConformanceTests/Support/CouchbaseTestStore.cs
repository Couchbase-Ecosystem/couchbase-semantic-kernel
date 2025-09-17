// Copyright (c) Microsoft. All rights reserved.

#pragma warning disable SKEXP0020 // Suppress experimental API warnings

using Couchbase.Core.Exceptions;
using Couchbase.KeyValue;
using Couchbase.SemanticKernel;
using Microsoft.Extensions.VectorData;
using VectorData.ConformanceTests.Support;

namespace Couchbase.ConformanceTests.Support;

#pragma warning disable CA1001 // Type owns disposable fields but is not disposable

/// <summary>
/// Test store for Couchbase conformance tests using a local Couchbase instance.
/// </summary>
internal sealed class CouchbaseTestStore : TestStore
{
    public static CouchbaseTestStore Instance { get; } = new();

    private ICluster? _cluster;
    private IBucket? _bucket;
    private IScope? _scope;

    private const string ConnectionString = "couchbases://cb.1-0nc9a4iqbyiqy7.cloud.couchbase.com";
    private const string Username = "Admin";
    private const string Password = "Admin@123";
    private const string BucketName = "travel-sample";
    private const string ScopeName = "inventory";
    
    // FTS Index name for tests - MUST exist before running tests
    public const string TestIndexName = "hotelIndex";

    public ICluster Cluster => _cluster ?? throw new InvalidOperationException("Not initialized");
    public IBucket Bucket => _bucket ?? throw new InvalidOperationException("Not initialized");
    public IScope Scope => _scope ?? throw new InvalidOperationException("Not initialized");

    public CouchbaseVectorStore GetVectorStore(CouchbaseVectorStoreOptions? options = null)
        => new(Scope, options ?? new CouchbaseVectorStoreOptions());

    /// <summary>
    /// Gets a test collection with IndexName configured for search operations.
    /// This ensures all test collections can perform vector and hybrid search.
    /// </summary>
    public VectorStoreCollection<TKey, TRecord> GetTestCollection<TKey, TRecord>(string collectionName)
        where TKey : notnull
        where TRecord : class
    {
        var options = new CouchbaseCollectionOptions
        {
            IndexName = TestIndexName
        };
        
        return new CouchbaseCollection<TKey, TRecord>(Scope, collectionName, options);
    }

    private CouchbaseTestStore()
    {
    }

    public override bool VectorsComparable => true;

    protected override async Task StartAsync()
    {
        try
        {
            // Connect to your local Couchbase cluster using static method
            _cluster = await Couchbase.Cluster.ConnectAsync(ConnectionString, Username, Password);
            
            // Try to get the bucket (create if it doesn't exist)
            try
            {
                _bucket = await _cluster.BucketAsync(BucketName);
            }
            catch (BucketNotFoundException)
            {
                // If bucket doesn't exist, you'll need to create it manually
                // or use the management API (requires additional setup)
                throw new InvalidOperationException(
                    $"Bucket '{BucketName}' not found. Please create it manually in Couchbase Web Console.");
            }
            
            _scope = _bucket.Scope(ScopeName);

            // Create the default vector store
            DefaultVectorStore = new CouchbaseVectorStore(_scope, new CouchbaseVectorStoreOptions());
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException(
                "Failed to connect to local Couchbase. Make sure Couchbase is running and accessible.", ex);
        }
    }

    protected override async Task StopAsync()
    {
        _cluster?.Dispose();
        await Task.CompletedTask;
    }
} 