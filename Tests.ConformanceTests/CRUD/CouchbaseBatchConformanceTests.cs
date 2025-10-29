// Copyright (c) Microsoft. All rights reserved.

using Couchbase.ConformanceTests.Support;
using VectorData.ConformanceTests.CRUD;
using Xunit;

namespace Couchbase.ConformanceTests.CRUD;

public class CouchbaseBatchConformanceTests(CouchbaseSimpleModelFixture fixture)
    : BatchConformanceTests<string>(fixture), IClassFixture<CouchbaseSimpleModelFixture>
{
}