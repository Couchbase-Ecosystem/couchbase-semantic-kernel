// Copyright (c) Microsoft. All rights reserved.

using Couchbase.ConformanceTests.Support;
using VectorData.ConformanceTests.Filter;
using VectorData.ConformanceTests.Support;
using Xunit;

namespace Couchbase.ConformanceTests.Filter;

public class CouchbaseBasicFilterTests(CouchbaseBasicFilterTests.Fixture fixture)
    : BasicFilterTests<string>(fixture), IClassFixture<CouchbaseBasicFilterTests.Fixture>
{
    public new class Fixture : BasicFilterTests<string>.Fixture
    {
        public override TestStore TestStore => CouchbaseTestStore.Instance;

        public override string CollectionName => "filter_tests";
    }
} 