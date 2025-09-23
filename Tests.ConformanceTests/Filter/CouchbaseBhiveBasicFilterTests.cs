// Copyright (c) Microsoft. All rights reserved.

using Couchbase.ConformanceTests.Support;
using Couchbase.SemanticKernel;
using Microsoft.Extensions.VectorData;
using VectorData.ConformanceTests.Filter;
using VectorData.ConformanceTests.Support;
using Xunit;

namespace Couchbase.ConformanceTests.Filter;

public class CouchbaseBhiveBasicFilterTests(CouchbaseBhiveBasicFilterTests.Fixture fixture)
    : BasicFilterTests<string>(fixture), IClassFixture<CouchbaseBhiveBasicFilterTests.Fixture>
{
    public new class Fixture : BasicFilterTests<string>.Fixture
    {
        public override TestStore TestStore => CouchbaseTestStore.Instance;

        public override string CollectionName => "bhive_filter_tests";

        protected override VectorStoreCollection<string, FilterRecord> GetCollection()
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
            
            return vectorStore.GetCollection<string, FilterRecord>(CollectionName, queryOptions);
        }
    }
}
