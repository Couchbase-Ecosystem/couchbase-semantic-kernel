// Copyright (c) Microsoft. All rights reserved.

using Couchbase.ConformanceTests.Support;
using Couchbase.SemanticKernel;
using Microsoft.Extensions.VectorData;
using VectorData.ConformanceTests.Filter;
using VectorData.ConformanceTests.Support;
using Xunit;

namespace Couchbase.ConformanceTests.Filter;

public class CouchbaseCompositeBasicFilterTests(CouchbaseCompositeBasicFilterTests.Fixture fixture)
    : BasicFilterTests<string>(fixture), IClassFixture<CouchbaseCompositeBasicFilterTests.Fixture>
{
    public new class Fixture : BasicFilterTests<string>.Fixture
    {
        public override TestStore TestStore => CouchbaseTestStore.Instance;

        public override string CollectionName => "composite_filter_tests";

        protected override VectorStoreCollection<string, FilterRecord> GetCollection()
        {
            var testStore = (CouchbaseTestStore)TestStore;
            var vectorStore = testStore.GetVectorStore(new CouchbaseVectorStoreOptions { IndexType = CouchbaseIndexType.Composite });
            
            var queryOptions = new CouchbaseQueryCollectionOptions
            {
                IndexName = $"{CollectionName}_composite_index",
                VectorDimensions = 3,
                SimilarityMetric = "COSINE",
                QuantizationSettings = null // No quantization for composite
            };
            
            return vectorStore.GetCollection<string, FilterRecord>(CollectionName, queryOptions);
        }
    }
}
