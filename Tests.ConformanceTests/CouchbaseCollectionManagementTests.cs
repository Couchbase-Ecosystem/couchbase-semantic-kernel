// Copyright (c) Microsoft. All rights reserved.

using Couchbase.ConformanceTests.Support;
using VectorData.ConformanceTests;
using VectorData.ConformanceTests.Support;
using Xunit;

namespace Couchbase.ConformanceTests;

public class CouchbaseCollectionManagementTests(CouchbaseCollectionManagementTests.Fixture fixture)
    : CollectionManagementTests<string>(fixture), IClassFixture<CouchbaseCollectionManagementTests.Fixture>
{
    public class Fixture : VectorStoreFixture
    {
        public override TestStore TestStore => CouchbaseTestStore.Instance;
    }
}
