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

    private const string ConnectionString = "couchbase://localhost";
    private const string Username = "Administrator";
    private const string Password = "password";
    private const string BucketName = "travel-sample";
    private const string ScopeName = "inventory";

    public const string TestIndexName = "";

    public ICluster Cluster => _cluster ?? throw new InvalidOperationException("Not initialized");
    public IBucket Bucket => _bucket ?? throw new InvalidOperationException("Not initialized");
    public IScope Scope => _scope ?? throw new InvalidOperationException("Not initialized");

    public CouchbaseVectorStore GetVectorStore(CouchbaseVectorStoreOptions? options = null)
        => new(Scope, options ?? new CouchbaseVectorStoreOptions());

    private CouchbaseTestStore()
    {
    }

    public override bool VectorsComparable => true;

    protected override async Task StartAsync()
    {
        try
        {
            _cluster = await Couchbase.Cluster.ConnectAsync(ConnectionString, Username, Password);

            try
            {
                _bucket = await _cluster.BucketAsync(BucketName);
            }
            catch (BucketNotFoundException)
            {
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