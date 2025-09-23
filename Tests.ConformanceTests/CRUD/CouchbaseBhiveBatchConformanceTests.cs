// Copyright (c) Microsoft. All rights reserved.

using Couchbase.ConformanceTests.Support;
using Couchbase.SemanticKernel;
using Microsoft.Extensions.VectorData;
using VectorData.ConformanceTests.CRUD;
using VectorData.ConformanceTests.Support;
using Xunit;

namespace Couchbase.ConformanceTests.CRUD;

public class CouchbaseBhiveBatchConformanceTests(CouchbaseBhiveBatchConformanceTests.Fixture fixture)
    : BatchConformanceTests<string>(fixture), IClassFixture<CouchbaseBhiveBatchConformanceTests.Fixture>
{
    public new class Fixture : SimpleModelFixture<string>
    {
        public override TestStore TestStore => CouchbaseTestStore.Instance;

        public override string CollectionName => "bhive_batch_tests";

        protected override VectorStoreCollection<string, VectorData.ConformanceTests.Models.SimpleRecord<string>> GetCollection()
        {
            var testStore = (CouchbaseTestStore)TestStore;
            var vectorStore = testStore.GetVectorStore(new CouchbaseVectorStoreOptions { IndexType = CouchbaseIndexType.Bhive });
            
            var queryOptions = new CouchbaseQueryCollectionOptions
            {
                IndexName = $"{CollectionName}_bhive_index",
                VectorDimensions = 3,
                SimilarityMetric = "COSINE",
                QuantizationSettings = "IVF,SQ8"
            };
            
            return vectorStore.GetCollection<string, VectorData.ConformanceTests.Models.SimpleRecord<string>>(CollectionName, queryOptions);
        }
    }
}
