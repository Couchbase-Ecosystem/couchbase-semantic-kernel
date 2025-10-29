// Copyright (c) Microsoft. All rights reserved.

using Couchbase.SemanticKernel;
using VectorData.ConformanceTests.Support;
using Microsoft.Extensions.VectorData;

namespace Couchbase.ConformanceTests.Support;

/// <summary>
/// Fixture for Couchbase simple model tests with proper data seeding before index creation.
/// </summary>
public sealed class CouchbaseSimpleModelFixture : SimpleModelFixture<string>
{
    public override TestStore TestStore => CouchbaseTestStore.Instance;
}