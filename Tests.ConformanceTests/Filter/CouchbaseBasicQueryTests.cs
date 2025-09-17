// Copyright (c) Microsoft. All rights reserved.

using Couchbase.ConformanceTests.Support;
using VectorData.ConformanceTests.Filter;
using VectorData.ConformanceTests.Support;
using Xunit;

namespace Couchbase.ConformanceTests.Filter;

public class CouchbaseBasicQueryTests(CouchbaseBasicQueryTests.Fixture fixture)
    : BasicQueryTests<string>(fixture), IClassFixture<CouchbaseBasicQueryTests.Fixture>
{
    public new class Fixture : BasicQueryTests<string>.QueryFixture
    {
        public override TestStore TestStore => CouchbaseTestStore.Instance;

        public override string CollectionName => "query_tests";
    }
} 