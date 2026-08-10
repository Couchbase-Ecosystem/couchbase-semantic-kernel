// Copyright (c) Microsoft. All rights reserved.

using Couchbase.ConformanceTests.Support;
using VectorData.ConformanceTests.ModelTests;
using VectorData.ConformanceTests.Support;
using Xunit;

namespace Couchbase.ConformanceTests.ModelTests;

public class CouchbaseDynamicModelTests(CouchbaseDynamicModelTests.Fixture fixture)
    : DynamicModelTests<string>(fixture), IClassFixture<CouchbaseDynamicModelTests.Fixture>
{
    public new class Fixture : DynamicModelTests<string>.Fixture
    {
        public override TestStore TestStore => CouchbaseTestStore.Instance;
    }
}
