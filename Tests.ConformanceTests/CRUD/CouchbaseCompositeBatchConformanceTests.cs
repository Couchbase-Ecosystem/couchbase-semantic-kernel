// Copyright (c) Microsoft. All rights reserved.

using Couchbase.ConformanceTests.Support;
using Couchbase.SemanticKernel;
using Microsoft.Extensions.VectorData;
using VectorData.ConformanceTests.CRUD;
using VectorData.ConformanceTests.Support;
using Xunit;

namespace Couchbase.ConformanceTests.CRUD;

public class CouchbaseCompositeBatchConformanceTests(CouchbaseCompositeBatchConformanceTests.Fixture fixture)
    : BatchConformanceTests<string>(fixture), IClassFixture<CouchbaseCompositeBatchConformanceTests.Fixture>
{
    public new class Fixture : SimpleModelFixture<string>
    {
        public override TestStore TestStore => CouchbaseTestStore.Instance;

        public override string CollectionName => "composite_batch_tests";

        protected override VectorStoreCollection<string, VectorData.ConformanceTests.Models.SimpleRecord<string>> GetCollection()
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

            return vectorStore.GetCollection<string, VectorData.ConformanceTests.Models.SimpleRecord<string>>(CollectionName, queryOptions);
        }
    }
}
