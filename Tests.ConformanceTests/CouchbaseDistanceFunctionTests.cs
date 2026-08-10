// Copyright (c) Microsoft. All rights reserved.

using Couchbase.ConformanceTests.Support;
using VectorData.ConformanceTests;
using VectorData.ConformanceTests.Support;
using Xunit;

namespace Couchbase.ConformanceTests;

public class CouchbaseDistanceFunctionTests(CouchbaseDistanceFunctionTests.Fixture fixture)
    : DistanceFunctionTests<string>(fixture), IClassFixture<CouchbaseDistanceFunctionTests.Fixture>
{
    public new class Fixture : DistanceFunctionTests<string>.Fixture
    {
        public override TestStore TestStore => CouchbaseTestStore.Instance;
    }
}
