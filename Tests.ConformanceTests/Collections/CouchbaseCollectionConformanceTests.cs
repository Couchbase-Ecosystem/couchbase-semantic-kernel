// Copyright (c) Microsoft. All rights reserved.

using Couchbase.ConformanceTests.Support;
using Couchbase.SemanticKernel;
using VectorData.ConformanceTests.Collections;
using VectorData.ConformanceTests.Models;
using Microsoft.Extensions.VectorData;
using Xunit;

namespace Couchbase.ConformanceTests.Collections;

public class CouchbaseCollectionConformanceTests(CouchbaseVectorStoreFixture fixture)
    : CollectionConformanceTests<string>(fixture), IClassFixture<CouchbaseVectorStoreFixture>
{
    public override string CollectionName => "collection_tests";
}