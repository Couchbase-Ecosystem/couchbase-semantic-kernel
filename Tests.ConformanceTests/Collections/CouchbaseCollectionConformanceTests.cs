// Copyright (c) Microsoft. All rights reserved.

using Couchbase.ConformanceTests.Support;
using Couchbase.SemanticKernel;
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
    /// Override to use Couchbase query collection with BHIVE index for collection operations.
    /// </summary>
    public override VectorStoreCollection<string, SimpleRecord<string>> GetCollection()
    {
        var testStore = (CouchbaseTestStore)fixture.TestStore;
        var vectorStore = testStore.GetVectorStore(new CouchbaseVectorStoreOptions { IndexType = CouchbaseIndexType.Bhive });
        
        var queryOptions = new CouchbaseQueryCollectionOptions
        {
            IndexName = $"{CollectionName}_bhive_index",
            VectorDimensions = 3, // SimpleRecord<string> has 3-dimensional vectors
            SimilarityMetric = "COSINE",
            QuantizationSettings = "IVF,SQ8" // BHIVE supports quantization
        };
        
        return vectorStore.GetCollection<string, SimpleRecord<string>>(CollectionName, queryOptions);
    }
} 