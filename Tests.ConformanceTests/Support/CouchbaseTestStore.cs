// Copyright (c) Microsoft. All rights reserved.

using Couchbase.Core.Exceptions;
using Couchbase.KeyValue;
using Couchbase.VectorData;
using Microsoft.Extensions.VectorData;
using VectorData.ConformanceTests.Support;

namespace Couchbase.ConformanceTests.Support;

#pragma warning disable CA1001 // Type owns disposable fields but is not disposable

/// <summary>
/// Test store for Couchbase conformance tests, backed by a local Couchbase instance.
/// </summary>
internal sealed class CouchbaseTestStore : TestStore
{
    public static CouchbaseTestStore Instance { get; } = new();

    private ICluster? _cluster;
    private IBucket? _bucket;
    private IScope? _scope;

    private static string ConnectionString => Environment.GetEnvironmentVariable("COUCHBASE_CONNECTIONSTRING") ?? "couchbase://localhost";
    private static string Username => Environment.GetEnvironmentVariable("COUCHBASE_USERNAME") ?? "Administrator";
    private static string Password => Environment.GetEnvironmentVariable("COUCHBASE_PASSWORD") ?? "password";
    private static string BucketName => Environment.GetEnvironmentVariable("COUCHBASE_BUCKET") ?? "travel-sample";
    private static string ScopeName => Environment.GetEnvironmentVariable("COUCHBASE_SCOPE") ?? "inventory";

    public ICluster Cluster => _cluster ?? throw new InvalidOperationException("Not initialized");
    public IBucket Bucket => _bucket ?? throw new InvalidOperationException("Not initialized");
    public IScope Scope => _scope ?? throw new InvalidOperationException("Not initialized");

    public CouchbaseVectorStore GetVectorStore(CouchbaseVectorStoreOptions? options = null)
        => new(Scope, options ?? new CouchbaseVectorStoreOptions());

    private CouchbaseTestStore()
    {
    }

    public override bool VectorsComparable => true;

    // Couchbase only supports string document IDs.
    public override string DefaultDistanceFunction => DistanceFunction.CosineSimilarity;

    /// <summary>
    /// Couchbase collection names allow only [A-Za-z0-9_-%], must not start with '_' or '%',
    /// and are limited to 251 characters.
    /// </summary>
    public override string AdjustCollectionName(string name)
    {
        var sanitized = new string(name.Select(c => char.IsLetterOrDigit(c) || c == '_' || c == '-' ? c : '_').ToArray());

        if (sanitized.Length > 0 && (sanitized[0] == '_' || sanitized[0] == '%'))
        {
            sanitized = "c" + sanitized;
        }

        return sanitized.Length > 251 ? sanitized[..251] : sanitized;
    }

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
                    $"Bucket '{BucketName}' not found. Create it manually, or set COUCHBASE_BUCKET.");
            }

            _scope = _bucket.Scope(ScopeName);

            DefaultVectorStore = new CouchbaseVectorStore(_scope, new CouchbaseVectorStoreOptions());
        }
        catch (Exception ex) when (ex is not InvalidOperationException)
        {
            throw new InvalidOperationException(
                $"Failed to connect to Couchbase at '{ConnectionString}'. Ensure it is running and reachable.", ex);
        }
    }

    protected override Task StopAsync()
    {
        _cluster?.Dispose();
        return Task.CompletedTask;
    }
}
