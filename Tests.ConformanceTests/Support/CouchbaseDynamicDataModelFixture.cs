// Copyright (c) Microsoft. All rights reserved.

using VectorData.ConformanceTests.Support;
using Microsoft.Extensions.VectorData;

namespace Couchbase.ConformanceTests.Support;

/// <summary>
/// Fixture for Couchbase dynamic data model tests.
/// </summary>
public sealed class CouchbaseDynamicDataModelFixture : DynamicDataModelFixture<object>
{
    public override TestStore TestStore => CouchbaseTestStore.Instance;
    
    /// <summary>
    /// Override to use Couchbase test collection with IndexName configured for search operations.
    /// </summary>
    protected override VectorStoreCollection<object, Dictionary<string, object?>> GetCollection()
        => ((CouchbaseTestStore)TestStore).GetTestCollection<object, Dictionary<string, object?>>(CollectionName);
} 