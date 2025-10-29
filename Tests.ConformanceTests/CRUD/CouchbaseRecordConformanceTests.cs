// Copyright (c) Microsoft. All rights reserved.

using Couchbase.ConformanceTests.Support;
using Couchbase.SemanticKernel;
using Microsoft.Extensions.VectorData;
using VectorData.ConformanceTests.CRUD;
using VectorData.ConformanceTests.Support;
using Xunit;

namespace Couchbase.ConformanceTests.CRUD;

public class CouchbaseRecordConformanceTests(CouchbaseSimpleModelFixture fixture)
    : RecordConformanceTests<string>(fixture), IClassFixture<CouchbaseSimpleModelFixture>
{
    
}