// Copyright (c) Microsoft. All rights reserved.

using Couchbase.ConformanceTests.Support;
using Couchbase.SemanticKernel;
using Microsoft.Extensions.VectorData;
using VectorData.ConformanceTests.CRUD;
using VectorData.ConformanceTests.Support;
using Xunit;

namespace Couchbase.ConformanceTests.CRUD;

public class CouchbaseCompositeDynamicDataModelConformanceTests(CouchbaseCompositeDynamicDataModelConformanceTests.Fixture fixture)
    : DynamicDataModelConformanceTests<object>(fixture), IClassFixture<CouchbaseCompositeDynamicDataModelConformanceTests.Fixture>
{
    public new class Fixture : DynamicDataModelFixture<object>
    {
        public override TestStore TestStore => CouchbaseTestStore.Instance;

        public override string CollectionName => "composite_dynamic_tests";

        protected override VectorStoreCollection<object, Dictionary<string, object?>> GetCollection()
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
            
            return vectorStore.GetQueryDynamicCollection(CollectionName, CreateRecordDefinition(), queryOptions);
        }
    }
}
