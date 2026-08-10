// Copyright (c) Microsoft. All rights reserved.

using Couchbase.ConformanceTests.Support;
using Couchbase.VectorData;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.VectorData;
using VectorData.ConformanceTests;
using VectorData.ConformanceTests.Support;
using Xunit;

namespace Couchbase.ConformanceTests;

public class CouchbaseEmbeddingGenerationTests(
    CouchbaseEmbeddingGenerationTests.StringVectorFixture stringVectorFixture,
    CouchbaseEmbeddingGenerationTests.RomOfFloatVectorFixture romOfFloatVectorFixture)
    : EmbeddingGenerationTests<string>(stringVectorFixture, romOfFloatVectorFixture),
        IClassFixture<CouchbaseEmbeddingGenerationTests.StringVectorFixture>,
        IClassFixture<CouchbaseEmbeddingGenerationTests.RomOfFloatVectorFixture>
{
    public new class StringVectorFixture : EmbeddingGenerationTests<string>.StringVectorFixture
    {
        public override TestStore TestStore => CouchbaseTestStore.Instance;

        public override VectorStore CreateVectorStore(IEmbeddingGenerator? embeddingGenerator)
            => CouchbaseTestStore.Instance.GetVectorStore(new CouchbaseVectorStoreOptions { EmbeddingGenerator = embeddingGenerator });

        public override Func<IServiceCollection, IServiceCollection>[] DependencyInjectionStoreRegistrationDelegates =>
        [
            services => services
                .AddSingleton(CouchbaseTestStore.Instance.Scope)
                .AddCouchbaseVectorStore()
        ];

        public override Func<IServiceCollection, IServiceCollection>[] DependencyInjectionCollectionRegistrationDelegates =>
        [
            services => services
                .AddSingleton(CouchbaseTestStore.Instance.Scope)
                .AddCouchbaseQueryCollection<string, RecordWithAttributes>(this.CollectionName)
        ];
    }

    public new class RomOfFloatVectorFixture : EmbeddingGenerationTests<string>.RomOfFloatVectorFixture
    {
        public override TestStore TestStore => CouchbaseTestStore.Instance;

        public override VectorStore CreateVectorStore(IEmbeddingGenerator? embeddingGenerator)
            => CouchbaseTestStore.Instance.GetVectorStore(new CouchbaseVectorStoreOptions { EmbeddingGenerator = embeddingGenerator });

        public override Func<IServiceCollection, IServiceCollection>[] DependencyInjectionStoreRegistrationDelegates =>
        [
            services => services
                .AddSingleton(CouchbaseTestStore.Instance.Scope)
                .AddCouchbaseVectorStore()
        ];

        public override Func<IServiceCollection, IServiceCollection>[] DependencyInjectionCollectionRegistrationDelegates =>
        [
            services => services
                .AddSingleton(CouchbaseTestStore.Instance.Scope)
                .AddCouchbaseQueryCollection<string, RecordWithAttributes>(this.CollectionName)
        ];
    }
}
