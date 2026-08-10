// Copyright (c) Microsoft. All rights reserved.

using Couchbase.ConformanceTests.Support;
using VectorData.ConformanceTests.Support;
using VectorData.ConformanceTests.TypeTests;
using Xunit;

namespace Couchbase.ConformanceTests.TypeTests;

public class CouchbaseDataTypeTests(CouchbaseDataTypeTests.Fixture fixture)
    : DataTypeTests<string, DataTypeTests<string>.DefaultRecord>(fixture), IClassFixture<CouchbaseDataTypeTests.Fixture>
{
    public new class Fixture : DataTypeTests<string, DataTypeTests<string>.DefaultRecord>.Fixture
    {
        public override TestStore TestStore => CouchbaseTestStore.Instance;

        // Types not accepted by CouchbaseModelBuilder.IsDataPropertyTypeValid.
        public override Type[] UnsupportedDefaultTypes { get; } =
        [
            typeof(byte),
            typeof(short),
            typeof(DateOnly),
            typeof(TimeOnly),
        ];
    }
}
