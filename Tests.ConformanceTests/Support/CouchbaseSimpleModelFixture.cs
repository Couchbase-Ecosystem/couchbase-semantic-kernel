// Copyright (c) Microsoft. All rights reserved.

using VectorData.ConformanceTests.Support;
using Microsoft.Extensions.VectorData;

namespace Couchbase.ConformanceTests.Support;

/// <summary>
/// Fixture for Couchbase simple model tests.
/// </summary>
public sealed class CouchbaseSimpleModelFixture : SimpleModelFixture<string>
{
    public override TestStore TestStore => CouchbaseTestStore.Instance;
    
    /// <summary>
    /// Override to use Couchbase test collection with IndexName configured for search operations.
    /// </summary>
    protected override VectorStoreCollection<string, VectorData.ConformanceTests.Models.SimpleRecord<string>> GetCollection()
        => ((CouchbaseTestStore)TestStore).GetTestCollection<string, VectorData.ConformanceTests.Models.SimpleRecord<string>>(CollectionName);
} 