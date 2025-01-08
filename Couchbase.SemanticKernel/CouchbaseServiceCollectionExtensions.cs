using Couchbase.KeyValue;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.VectorData;

namespace Couchbase.SemanticKernel;

/// <summary>
/// Extensions methods to register Couchbase <see cref="IVectorStore"/> instances on an <see cref="IServiceCollection"/>.
/// </summary>
public static class CouchbaseServiceCollectionExtensions
{
    /// <summary>
    /// Register a Couchbase <see cref="IVectorStore"/> with the specified service ID
    /// and where the Couchbase <see cref="IScope"/> is retrieved from the dependency injection container.
    /// </summary>
    /// <param name="services">The <see cref="IServiceCollection"/> to register the <see cref="IVectorStore"/> on.</param>
    /// <param name="options">Optional options to further configure the <see cref="IVectorStore"/>.</param>
    /// <param name="serviceId">An optional service ID to use as the service key.</param>
    /// <returns>Service collection.</returns>
    public static IServiceCollection AddCouchbaseVectorStore(
        this IServiceCollection services,
        CouchbaseVectorStoreOptions? options = default,
        string? serviceId = default)
    {
        services.AddKeyedTransient<IVectorStore>(
            serviceId,
            (sp, _) =>
            {
                var scope = sp.GetRequiredService<IScope>();
                var selectedOptions = options ?? sp.GetService<CouchbaseVectorStoreOptions>();

                return new CouchbaseFtsVectorStore(scope, selectedOptions);
            });

        return services;
    }

    /// <summary>
    /// Register a Couchbase <see cref="IVectorStore"/> with the specified service ID
    /// and where the Couchbase <see cref="IScope"/> is constructed using the provided connection details.
    /// </summary>
    /// <param name="services">The <see cref="IServiceCollection"/> to register the <see cref="IVectorStore"/> on.</param>
    /// <param name="connectionString">Connection string required to connect to Couchbase.</param>
    /// <param name="username">Username required to connect to Couchbase.</param>
    /// <param name="password">Password required to connect to Couchbase.</param>
    /// <param name="bucketName">Bucket name for Couchbase.</param>
    /// <param name="scopeName">Scope name for Couchbase.</param>
    /// <param name="options">Optional options to further configure the <see cref="IVectorStore"/>.</param>
    /// <param name="serviceId">An optional service ID to use as the service key.</param>
    /// <returns>Service collection.</returns>
    public static IServiceCollection AddCouchbaseVectorStore(
        this IServiceCollection services,
        string connectionString,
        string username,
        string password,
        string bucketName,
        string scopeName,
        CouchbaseVectorStoreOptions? options = null,
        string? serviceId = null)
    {
        services.AddKeyedSingleton<IVectorStore>(
            serviceId,
            (sp, _) =>
            {
                var clusterOptions = new ClusterOptions
                {
                    ConnectionString = connectionString,
                    UserName = username,
                    Password = password
                };

                var cluster = Cluster.ConnectAsync(clusterOptions).GetAwaiter().GetResult();
                var bucket = cluster.BucketAsync(bucketName).GetAwaiter().GetResult();
                var scope = bucket.Scope(scopeName);
                var selectedOptions = options ?? sp.GetService<CouchbaseVectorStoreOptions>();

                return new CouchbaseFtsVectorStore(scope, selectedOptions);
            });

        return services;
    }

    /// <summary>
    /// Register a Couchbase <see cref="IVectorStoreRecordCollection{TKey, TRecord}"/> and <see cref="IVectorizedSearch{TRecord}"/>
    /// where the Couchbase <see cref="IScope"/> is retrieved from the dependency injection container.
    /// </summary>
    /// <typeparam name="TRecord">The type of the record.</typeparam>
    /// <param name="services">The <see cref="IServiceCollection"/> to register the <see cref="IVectorStoreRecordCollection{TKey, TRecord}"/> on.</param>
    /// <param name="collectionName">The name of the collection.</param>
    /// <param name="options">Optional options to further configure the <see cref="IVectorStoreRecordCollection{TKey, TRecord}"/>.</param>
    /// <param name="serviceId">An optional service id to use as the service key.</param>
    /// <returns>Service collection.</returns>
    public static IServiceCollection AddCouchbaseVectorStoreRecordCollection<TRecord>(
        this IServiceCollection services,
        string collectionName,
        CouchbaseVectorStoreRecordCollectionOptions<TRecord>? options = null,
        string? serviceId = null)
        where TRecord : class
    {
        services.AddKeyedTransient<IVectorStoreRecordCollection<string, TRecord>>(
            serviceId,
            (sp, _) =>
            {
                var scope = sp.GetRequiredService<IScope>();
                var selectedOptions = options ?? sp.GetService<CouchbaseVectorStoreRecordCollectionOptions<TRecord>>();

                return new CouchbaseVectorStoreRecordCollection<TRecord>(scope, collectionName, selectedOptions);
            });

        AddVectorizedSearch<TRecord>(services, serviceId);

        return services;
    }

    /// <summary>
    /// Register a Couchbase <see cref="IVectorStoreRecordCollection{TKey, TRecord}"/> and <see cref="IVectorizedSearch{TRecord}"/>
    /// where the Couchbase <see cref="IScope"/> is constructed using the provided connection details.
    /// </summary>
    /// <typeparam name="TRecord">The type of the record.</typeparam>
    /// <param name="services">The <see cref="IServiceCollection"/> to register the <see cref="IVectorStoreRecordCollection{TKey, TRecord}"/> on.</param>
    /// <param name="connectionString">Connection string required to connect to Couchbase.</param>
    /// <param name="username">Username required to connect to Couchbase.</param>
    /// <param name="password">Password required to connect to Couchbase.</param>
    /// <param name="bucketName">Bucket name for Couchbase.</param>
    /// <param name="scopeName">Scope name for Couchbase.</param>
    /// <param name="collectionName">The name of the collection.</param>
    /// <param name="options">Optional options to further configure the <see cref="IVectorStoreRecordCollection{TKey, TRecord}"/>.</param>
    /// <param name="serviceId">An optional service id to use as the service key.</param>
    /// <returns>Service collection.</returns>
    public static IServiceCollection AddCouchbaseVectorStoreRecordCollection<TRecord>(
        this IServiceCollection services,
        string connectionString,
        string username,
        string password,
        string bucketName,
        string scopeName,
        string collectionName,
        CouchbaseVectorStoreRecordCollectionOptions<TRecord>? options = null,
        string? serviceId = null)
        where TRecord : class
    {
        services.AddKeyedSingleton<IVectorStoreRecordCollection<string, TRecord>>(
            serviceId,
            (sp, _) =>
            {
                var clusterOptions = new ClusterOptions
                {
                    ConnectionString = connectionString,
                    UserName = username,
                    Password = password,
                };

                var cluster = Cluster.ConnectAsync(clusterOptions).GetAwaiter().GetResult();
                var bucket = cluster.BucketAsync(bucketName).GetAwaiter().GetResult();
                var scope = bucket.Scope(scopeName);
                var selectedOptions = options ?? sp.GetService<CouchbaseVectorStoreRecordCollectionOptions<TRecord>>();

                return new CouchbaseVectorStoreRecordCollection<TRecord>(scope, collectionName, selectedOptions);
            });

        AddVectorizedSearch<TRecord>(services, serviceId);

        return services;
    }

    /// <summary>
    /// Also register the <see cref="IVectorStoreRecordCollection{TKey, TRecord}"/> with the given <paramref name="serviceId"/> as a <see cref="IVectorizedSearch{TRecord}"/>.
    /// </summary>
    /// <typeparam name="TRecord">The type of the data model that the collection should contain.</typeparam>
    /// <param name="services">The service collection to register on.</param>
    /// <param name="serviceId">The service id that the registrations should use.</param>
    private static void AddVectorizedSearch<TRecord>(IServiceCollection services, string? serviceId)
        where TRecord : class
    {
        services.AddKeyedTransient<IVectorizedSearch<TRecord>>(
            serviceId,
            (sp, _) =>
            {
                return sp.GetRequiredKeyedService<IVectorStoreRecordCollection<string, TRecord>>(serviceId)
                    as IVectorizedSearch<TRecord>;
            });
    }
}
