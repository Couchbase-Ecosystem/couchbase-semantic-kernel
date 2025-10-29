// Copyright (c) Microsoft. All rights reserved.

using Couchbase.SemanticKernel;
using VectorData.ConformanceTests.Support;
using Microsoft.Extensions.VectorData;

namespace Couchbase.ConformanceTests.Support;

/// <summary>
/// Fixture for Couchbase dynamic data model tests.
/// </summary>
public sealed class CouchbaseDynamicDataModelFixture : DynamicDataModelFixture<object>
{
    public override TestStore TestStore => CouchbaseTestStore.Instance;
}