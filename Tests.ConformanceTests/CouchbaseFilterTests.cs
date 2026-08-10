// Copyright (c) Microsoft. All rights reserved.

using Couchbase.ConformanceTests.Support;
using VectorData.ConformanceTests;
using VectorData.ConformanceTests.Support;
using Xunit;

namespace Couchbase.ConformanceTests;

public class CouchbaseFilterTests(CouchbaseFilterTests.Fixture fixture)
    : FilterTests<string>(fixture), IClassFixture<CouchbaseFilterTests.Fixture>
{
    public new class Fixture : FilterTests<string>.Fixture
    {
        public override TestStore TestStore => CouchbaseTestStore.Instance;
    }
}
