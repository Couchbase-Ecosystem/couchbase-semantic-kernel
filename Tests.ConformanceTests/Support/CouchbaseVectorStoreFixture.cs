// Copyright (c) Microsoft. All rights reserved.

using Microsoft.Extensions.VectorData;
using VectorData.ConformanceTests.Support;

namespace Couchbase.ConformanceTests.Support;

/// <summary>
/// Fixture for Couchbase vector store tests.
/// </summary>
public sealed class CouchbaseVectorStoreFixture : VectorStoreFixture
{
    public override TestStore TestStore => CouchbaseTestStore.Instance;
} 