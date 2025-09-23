// Copyright (c) Microsoft. All rights reserved.

using Couchbase.ConformanceTests.Support;
using Couchbase.SemanticKernel;
using Microsoft.Extensions.VectorData;
using VectorData.ConformanceTests.CRUD;
using VectorData.ConformanceTests.Support;
using Xunit;

namespace Couchbase.ConformanceTests.CRUD;

public class CouchbaseBhiveDynamicDataModelConformanceTests(CouchbaseBhiveDynamicDataModelConformanceTests.Fixture fixture)
    : DynamicDataModelConformanceTests<object>(fixture), IClassFixture<CouchbaseBhiveDynamicDataModelConformanceTests.Fixture>
{
    public new class Fixture : DynamicDataModelFixture<object>
    {
        public override TestStore TestStore => CouchbaseTestStore.Instance;

        public override string CollectionName => "bhive_dynamic_tests";

        protected override VectorStoreCollection<object, Dictionary<string, object?>> GetCollection()
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
            
            return vectorStore.GetQueryDynamicCollection(CollectionName, CreateRecordDefinition(), queryOptions);
        }
    }
}
