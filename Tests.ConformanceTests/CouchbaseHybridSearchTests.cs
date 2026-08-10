// Copyright (c) Microsoft. All rights reserved.

using Couchbase.ConformanceTests.Support;
using VectorData.ConformanceTests;
using VectorData.ConformanceTests.Support;
using Xunit;

namespace Couchbase.ConformanceTests;

public class CouchbaseHybridSearchTests(
    CouchbaseHybridSearchTests.VectorAndStringFixture vectorAndStringFixture,
    CouchbaseHybridSearchTests.MultiTextFixture multiTextFixture)
    : HybridSearchTests<string>(vectorAndStringFixture, multiTextFixture),
        IClassFixture<CouchbaseHybridSearchTests.VectorAndStringFixture>,
        IClassFixture<CouchbaseHybridSearchTests.MultiTextFixture>
{
    public new class VectorAndStringFixture : HybridSearchTests<string>.VectorAndStringFixture
    {
        public override TestStore TestStore => CouchbaseTestStore.Instance;
    }

    public new class MultiTextFixture : HybridSearchTests<string>.MultiTextFixture
    {
        public override TestStore TestStore => CouchbaseTestStore.Instance;
    }
}
