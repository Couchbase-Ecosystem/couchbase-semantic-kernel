// Copyright (c) Microsoft. All rights reserved.

using Couchbase.ConformanceTests.Support;
using VectorData.ConformanceTests.HybridSearch;
using VectorData.ConformanceTests.Support;
using Xunit;

namespace Couchbase.ConformanceTests.HybridSearch;

public class CouchbaseKeywordVectorizedHybridSearchTests(
    CouchbaseKeywordVectorizedHybridSearchTests.VectorAndStringFixture vectorAndStringFixture,
    CouchbaseKeywordVectorizedHybridSearchTests.MultiTextFixture multiTextFixture)
    : KeywordVectorizedHybridSearchComplianceTests<string>(vectorAndStringFixture, multiTextFixture),
        IClassFixture<CouchbaseKeywordVectorizedHybridSearchTests.VectorAndStringFixture>,
        IClassFixture<CouchbaseKeywordVectorizedHybridSearchTests.MultiTextFixture>
{
    public new class VectorAndStringFixture : KeywordVectorizedHybridSearchComplianceTests<string>.VectorAndStringFixture
    {
        public override TestStore TestStore => CouchbaseTestStore.Instance;

        public override string CollectionName => "keyword_hybrid_search" + GetUniqueCollectionName();
        protected override string IndexKind => Microsoft.Extensions.VectorData.IndexKind.Hnsw;
    }

    public new class MultiTextFixture : KeywordVectorizedHybridSearchComplianceTests<string>.MultiTextFixture
    {
        public override TestStore TestStore => CouchbaseTestStore.Instance;

        public override string CollectionName => "keyword_hybrid_search" + GetUniqueCollectionName();
        protected override string IndexKind => Microsoft.Extensions.VectorData.IndexKind.Hnsw;
    }
}