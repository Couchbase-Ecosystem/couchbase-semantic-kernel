using Couchbase.KeyValue;
using Microsoft.Extensions.VectorData;
using Microsoft.SemanticKernel;

namespace Couchbase.SemanticKernel;

/// <summary>
/// Extension methods to register Couchbase <see cref="IVectorStore"/> instances on the <see cref="IKernelBuilder"/>.
/// </summary>
public static class CouchbaseKernelBuilderExtensions
{
    /// <summary>
    /// Registers a Couchbase <see cref="IVectorStore"/> with the specified service ID, retrieving the <see cref="IScope"/> from the DI container.
    /// </summary>
    /// <param name="builder">The builder to register the <see cref="IVectorStore"/> on.</param>
    /// <param name="options">Optional options to further configure the <see cref="IVectorStore"/>.</param>
    /// <param name="serviceId">An optional service ID to use as the service key.</param>
    /// <returns>The kernel builder.</returns>
    public static IKernelBuilder AddCouchbaseVectorStore(
        this IKernelBuilder builder,
        CouchbaseVectorStoreOptions? options = default,
        string? serviceId = default)
    {
        builder.Services.AddCouchbaseVectorStore(options, serviceId);
        return builder;
    }

    /// <summary>
    /// Register a Couchbase <see cref="IVectorStore"/> with the specified service ID
    /// and where the Couchbase <see cref="IScope"/> is constructed using the provided connection details.
    /// </summary>
    /// <param name="builder">The builder to register the <see cref="IVectorStore"/> on.</param>
    /// <param name="connectionString">Connection string required to connect to Couchbase.</param>
    /// <param name="username">Username required to connect to Couchbase.</param>
    /// <param name="password">Password required to connect to Couchbase.</param>
    /// <param name="bucketName">Bucket name for Couchbase.</param>
    /// <param name="scopeName">Scope name for Couchbase.</param>
    /// <param name="options">Optional options to further configure the <see cref="IVectorStore"/>.</param>
    /// <param name="serviceId">An optional service ID to use as the service key.</param>
    /// <returns>The kernel builder.</returns> 
    public static IKernelBuilder AddCouchbaseVectorStore(
        this IKernelBuilder builder,
        string connectionString,
        string username,
        string password,
        string bucketName,
        string scopeName,
        CouchbaseVectorStoreOptions? options = default,
        string? serviceId = default)
    {
        builder.Services.AddCouchbaseVectorStore(
            connectionString,
            username,
            password,
            bucketName,
            scopeName,
            options,
            serviceId);
        return builder;
    }

    /// <summary>
    /// Registers a Couchbase <see cref="IVectorStoreRecordCollection{TKey, TRecord}"/> with the specified service ID, retrieving the <see cref="IScope"/> from the DI container.
    /// </summary>
    /// <typeparam name="TRecord">The type of the record.</typeparam>
    /// <param name="builder">The builder to register the <see cref="IVectorStoreRecordCollection{TKey, TRecord}"/> on.</param>
    /// <param name="collectionName">The name of the collection.</param>
    /// <param name="options">Optional options to further configure the <see cref="IVectorStoreRecordCollection{TKey, TRecord}"/>.</param>
    /// <param name="serviceId">An optional service ID to use as the service key.</param>
    /// <returns>The kernel builder.</returns>
    public static IKernelBuilder AddCouchbaseFtsVectorStoreRecordCollection<TRecord>(
        this IKernelBuilder builder,
        string collectionName,
        CouchbaseFtsVectorStoreRecordCollectionOptions<TRecord>? options = default,
        string? serviceId = default)
    {
        builder.Services.AddCouchbaseFtsVectorStoreRecordCollection(collectionName, options, serviceId);
        return builder;
    }

    /// <summary>
    /// Register a Couchbase <see cref="IVectorStoreRecordCollection{TKey, TRecord}"/> and <see cref="IVectorizedSearch{TRecord}"/>
    /// where the Couchbase <see cref="IScope"/> is constructed using the provided connection details.    /// </summary>
    /// <typeparam name="TRecord">The type of the record.</typeparam>
    /// <param name="builder">The builder to register the <see cref="IVectorStore"/> on.</param>
    /// <param name="connectionString">Connection string required to connect to Couchbase.</param>
    /// <param name="username">Username required to connect to Couchbase.</param>
    /// <param name="password">Password required to connect to Couchbase.</param>
    /// <param name="bucketName">Bucket name for Couchbase.</param>
    /// <param name="scopeName">Scope name for Couchbase.</param>
    /// <param name="collectionName">The name of the collection.</param>
    /// <param name="options">Optional options to further configure the <see cref="IVectorStore"/>.</param>
    /// <param name="serviceId">An optional service ID to use as the service key.</param>
    public static IKernelBuilder AddCouchbaseFtsVectorStoreRecordCollection<TRecord>(
        this IKernelBuilder builder,
        string connectionString,
        string username,
        string password,
        string bucketName,
        string scopeName,
        string collectionName,
        CouchbaseFtsVectorStoreRecordCollectionOptions<TRecord>? options = default,
        string? serviceId = default)
    {
        builder.Services.AddCouchbaseFtsVectorStoreRecordCollection(
            connectionString,
            username,
            password,
            bucketName,
            scopeName,
            collectionName,
            options,
            serviceId);
        return builder;
    }
}