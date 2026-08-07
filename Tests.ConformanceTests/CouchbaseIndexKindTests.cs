// Copyright (c) Microsoft. All rights reserved.

using Couchbase.ConformanceTests.Support;
using Microsoft.Extensions.VectorData;
using VectorData.ConformanceTests;
using VectorData.ConformanceTests.Support;
using Xunit;

namespace Couchbase.ConformanceTests;

public class CouchbaseIndexKindTests(CouchbaseIndexKindTests.Fixture fixture)
    : IndexKindTests<string>(fixture), IClassFixture<CouchbaseIndexKindTests.Fixture>
{
    // Couchbase's Hyperscale and Composite vector indexes are IVF-based and require existing
    // data to train centroids, so they cannot be created on an empty collection. Without an
    // index, Couchbase 7.6+ falls back to a sequential (brute-force) scan.
    public override Task Flat() => base.Flat();

    public new class Fixture : IndexKindTests<string>.Fixture
    {
        public override TestStore TestStore => CouchbaseTestStore.Instance;
    }
}
