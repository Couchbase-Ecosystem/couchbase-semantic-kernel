// Copyright (c) Microsoft. All rights reserved.

using Couchbase.ConformanceTests.Support;
using VectorData.ConformanceTests.Support;
using VectorData.ConformanceTests.TypeTests;
using Xunit;

namespace Couchbase.ConformanceTests.TypeTests;

public class CouchbaseKeyTypeTests(CouchbaseKeyTypeTests.Fixture fixture)
    : KeyTypeTests(fixture), IClassFixture<CouchbaseKeyTypeTests.Fixture>
{
    // Couchbase document IDs are strings; no other key type is supported.
    [Fact]
    public virtual Task String() => this.Test<string>("foo", "bar");

    public new class Fixture : KeyTypeTests.Fixture
    {
        public override TestStore TestStore => CouchbaseTestStore.Instance;
    }
}
