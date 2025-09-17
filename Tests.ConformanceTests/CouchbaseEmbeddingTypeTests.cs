// Copyright (c) Microsoft. All rights reserved.

using Couchbase.ConformanceTests.Support;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.VectorData;
using VectorData.ConformanceTests;
using VectorData.ConformanceTests.Support;
using Xunit;

#pragma warning disable CA2000 // Dispose objects before losing scope

namespace Couchbase.ConformanceTests;

public class CouchbaseEmbeddingTypeTests(CouchbaseEmbeddingTypeTests.Fixture fixture)
    : EmbeddingTypeTests<string>(fixture), IClassFixture<CouchbaseEmbeddingTypeTests.Fixture>
{
    public new class Fixture : EmbeddingTypeTests<string>.Fixture
    {
        public override TestStore TestStore => CouchbaseTestStore.Instance;

        public override string CollectionName => "embedding_type_tests";
    }
} 