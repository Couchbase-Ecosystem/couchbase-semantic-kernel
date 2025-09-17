// Copyright (c) Microsoft. All rights reserved.

using Couchbase.ConformanceTests.Support;
using VectorData.ConformanceTests.Collections;
using VectorData.ConformanceTests.Models;
using Microsoft.Extensions.VectorData;
using Xunit;

namespace Couchbase.ConformanceTests.Collections;

public class CouchbaseCollectionConformanceTests(CouchbaseVectorStoreFixture fixture)
    : CollectionConformanceTests<string>(fixture), IClassFixture<CouchbaseVectorStoreFixture>
{
    //TODO: See if we can make these tests work differently
    public override string CollectionName => "collection_tests";
    
    /// <summary>
    /// Override to use Couchbase test collection with IndexName configured for search operations.
    /// </summary>
    public override VectorStoreCollection<string, SimpleRecord<string>> GetCollection()
        => ((CouchbaseTestStore)fixture.TestStore).GetTestCollection<string, SimpleRecord<string>>(CollectionName);
} 