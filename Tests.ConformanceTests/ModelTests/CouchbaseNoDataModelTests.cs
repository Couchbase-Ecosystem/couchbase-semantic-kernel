// Copyright (c) Microsoft. All rights reserved.

using Couchbase.ConformanceTests.Support;
using VectorData.ConformanceTests.ModelTests;
using VectorData.ConformanceTests.Support;
using Xunit;

namespace Couchbase.ConformanceTests.ModelTests;

public class CouchbaseNoDataModelTests(CouchbaseNoDataModelTests.Fixture fixture)
    : NoDataModelTests<string>(fixture), IClassFixture<CouchbaseNoDataModelTests.Fixture>
{
    public new class Fixture : NoDataModelTests<string>.Fixture
    {
        public override TestStore TestStore => CouchbaseTestStore.Instance;
    }
}
