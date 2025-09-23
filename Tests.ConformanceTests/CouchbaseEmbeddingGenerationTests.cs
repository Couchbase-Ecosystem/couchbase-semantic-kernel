// Copyright (c) Microsoft. All rights reserved.

using System.Diagnostics.CodeAnalysis;
using Couchbase.ConformanceTests.Support;
using Couchbase.SemanticKernel;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.VectorData;
using VectorData.ConformanceTests;
using VectorData.ConformanceTests.Support;
using Xunit;

namespace Couchbase.ConformanceTests;

public class CouchbaseEmbeddingGenerationTests(CouchbaseEmbeddingGenerationTests.StringVectorFixture stringVectorFixture, CouchbaseEmbeddingGenerationTests.RomOfFloatVectorFixture romOfFloatVectorFixture)
    : EmbeddingGenerationTests<string>(stringVectorFixture, romOfFloatVectorFixture), IClassFixture<CouchbaseEmbeddingGenerationTests.StringVectorFixture>, IClassFixture<CouchbaseEmbeddingGenerationTests.RomOfFloatVectorFixture>
{
    public new class StringVectorFixture : EmbeddingGenerationTests<string>.StringVectorFixture
    {
        public override TestStore TestStore => CouchbaseTestStore.Instance;

        public override string CollectionName => "embedding_generation_tests";

        public override VectorStore CreateVectorStore(IEmbeddingGenerator? embeddingGenerator = null)
            => CouchbaseTestStore.Instance.GetVectorStore(new CouchbaseVectorStoreOptions { EmbeddingGenerator = embeddingGenerator });

        /// <summary>
        /// Override to use Couchbase test collection with IndexName configured for search operations.
        /// </summary>
        public override VectorStoreCollection<string, TRecord> GetCollection<TRecord>(
            VectorStore vectorStore,
            string collectionName,
            VectorStoreCollectionDefinition? recordDefinition = null)
            where TRecord : class
        {
            // Use the Couchbase-specific collection with IndexName configured
            var couchbaseVectorStore = (CouchbaseVectorStore)vectorStore;
            var options = new CouchbaseSearchCollectionOptions
            {
                IndexName = CouchbaseTestStore.TestIndexName,
                Definition = recordDefinition
            };
            
            return couchbaseVectorStore.GetCollection<string, TRecord>(collectionName, options);
        }

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
                .AddCouchbaseSearchCollection<string, RecordWithAttributes>(this.CollectionName)
        ];
    }

    public new class RomOfFloatVectorFixture : EmbeddingGenerationTests<string>.RomOfFloatVectorFixture
    {
        public override TestStore TestStore => CouchbaseTestStore.Instance;

        public override string CollectionName => "search_only_embedding_generation_tests";

        public override VectorStore CreateVectorStore(IEmbeddingGenerator? embeddingGenerator = null)
            => CouchbaseTestStore.Instance.GetVectorStore(new CouchbaseVectorStoreOptions { EmbeddingGenerator = embeddingGenerator });

        /// <summary>
        /// Override to use Couchbase test collection with IndexName configured for search operations.
        /// </summary>
        public override VectorStoreCollection<string, TRecord> GetCollection<TRecord>(
            VectorStore vectorStore,
            string collectionName,
            VectorStoreCollectionDefinition? recordDefinition = null)
            where TRecord : class
        {
            // Use the Couchbase-specific collection with IndexName configured
            var couchbaseVectorStore = (CouchbaseVectorStore)vectorStore;
            var options = new CouchbaseSearchCollectionOptions
            {
                IndexName = CouchbaseTestStore.TestIndexName,
                Definition = recordDefinition
            };
            
            return couchbaseVectorStore.GetCollection<string, TRecord>(collectionName, options);
        }

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
                .AddCouchbaseSearchCollection<string, RecordWithAttributes>(this.CollectionName)
        ];
    }
} 