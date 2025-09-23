// Copyright (c) Microsoft. All rights reserved.

using Couchbase.ConformanceTests.Support;
using Couchbase.SemanticKernel;
using Microsoft.Extensions.VectorData;
using VectorData.ConformanceTests.CRUD;
using VectorData.ConformanceTests.Support;
using Xunit;

namespace Couchbase.ConformanceTests.CRUD;

public class CouchbaseRecordConformanceTests(CouchbaseRecordConformanceTests.Fixture fixture)
    : RecordConformanceTests<string>(fixture), IClassFixture<CouchbaseRecordConformanceTests.Fixture>
{
    public new class Fixture : SimpleModelFixture<string>
    {
        public override TestStore TestStore => CouchbaseTestStore.Instance;

        // Use fixed collection name instead of GUID for easier data management
        public override string CollectionName => "simple_model_tests";

        /// <summary>
        /// Override to use Couchbase Query collection with BHIVE index for query operations.
        /// Assumes collection already has data inserted.
        /// </summary>
        protected override VectorStoreCollection<string, VectorData.ConformanceTests.Models.SimpleRecord<string>> GetCollection()
        {
            var testStore = (CouchbaseTestStore)TestStore;
            var vectorStore = testStore.GetVectorStore(new CouchbaseVectorStoreOptions { IndexType = CouchbaseIndexType.Bhive });

            var queryOptions = new CouchbaseQueryCollectionOptions
            {
                IndexName = $"{CollectionName}_bhive_index",
                VectorDimensions = 3, // dimension: 3
                SimilarityMetric = "DOT_PRODUCT", // similarity: "dot" 
                QuantizationSettings = "IVF,SQ8", // description: "IVF,SQ8"
                CentroidsToProbe = 1 // scan_nprobes: 1
                // Note: train_list is not supported by the connector
            };

            return vectorStore.GetCollection<string, VectorData.ConformanceTests.Models.SimpleRecord<string>>(CollectionName, queryOptions);
        }


        /// <summary>
        /// Override seeding - assumes data is already in the collection.
        /// Just use the base implementation which seeds test data.
        /// </summary>
        protected override async Task SeedAsync()
        {
            // Use base seeding - assumes collection already has some data for index creation
            await base.SeedAsync();
        }
    }
}