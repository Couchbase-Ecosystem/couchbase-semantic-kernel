// Copyright (c) Microsoft. All rights reserved.

using VectorData.ConformanceTests;

namespace Couchbase.ConformanceTests;

public class CouchbaseTestSuiteImplementationTests : TestSuiteImplementationTests
{
    protected override ICollection<Type> IgnoredTestBases { get; } = [];
}
