// Copyright (c) Microsoft. All rights reserved.

using Couchbase.ConformanceTests.Support;
using VectorData.ConformanceTests.CRUD;
using VectorData.ConformanceTests.Support;
using Xunit;

namespace Couchbase.ConformanceTests.CRUD;

public class CouchbaseNoDataConformanceTests(CouchbaseNoDataConformanceTests.Fixture fixture)
    : NoDataConformanceTests<string>(fixture), IClassFixture<CouchbaseNoDataConformanceTests.Fixture>
{
    public new class Fixture : NoDataConformanceTests<string>.Fixture
    {
        public override TestStore TestStore => CouchbaseTestStore.Instance;
    }
} 