using System.Diagnostics.CodeAnalysis;
using Couchbase;
using Couchbase.KeyValue;
using Couchbase.SemanticKernel;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.VectorData;
using Couchbase.SemanticKernel.Diagnostics;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Extension methods to register Couchbase <see cref="VectorStore"/> instances on an <see cref="IServiceCollection"/>.
/// </summary>
public static class CouchbaseServiceCollectionExtensions
{
    private const string DynamicCodeMessage = "This method is incompatible with NativeAOT, consult the documentation for adding collections in a way that's compatible with NativeAOT.";
    private const string UnreferencedCodeMessage = "This method is incompatible with trimming, consult the documentation for adding collections in a way that's compatible with NativeAOT.";

    /// <summary>
    /// Registers a <see cref="CouchbaseVectorStore"/> as <see cref="VectorStore"/>
    /// with <see cref="IScope"/> retrieved from the dependency injection container.
    /// </summary>
    /// <inheritdoc cref="AddKeyedCouchbaseVectorStore(IServiceCollection, object?, Func{IServiceProvider, IScope}?, Func{IServiceProvider, CouchbaseVectorStoreOptions}?, ServiceLifetime)"/>
    [RequiresUnreferencedCode(DynamicCodeMessage)]
    [RequiresDynamicCode(UnreferencedCodeMessage)]
    public static IServiceCollection AddCouchbaseVectorStore(
        this IServiceCollection services,
        Func<IServiceProvider, IScope>? scopeProvider = default,
        Func<IServiceProvider, CouchbaseVectorStoreOptions>? optionsProvider = default,
        ServiceLifetime lifetime = ServiceLifetime.Singleton)
    {
        return AddKeyedCouchbaseVectorStore(services, serviceKey: null, scopeProvider, optionsProvider, lifetime);
    }

    /// <summary>
    /// Registers a keyed <see cref="CouchbaseVectorStore"/> as <see cref="VectorStore"/>
    /// with <see cref="IScope"/> returned by <paramref name="scopeProvider"/> or retrieved from the dependency injection
    /// container if <paramref name="scopeProvider"/> was not provided.
    /// </summary>
    /// <param name="services">The <see cref="IServiceCollection"/> to register the <see cref="CouchbaseVectorStore"/> on.</param>
    /// <param name="serviceKey">The key with which to associate the vector store.</param>
    /// <param name="scopeProvider">The <see cref="IScope"/> provider.</param>
    /// <param name="optionsProvider">Options provider to further configure the <see cref="CouchbaseVectorStore"/>.</param>
    /// <param name="lifetime">The service lifetime for the store. Defaults to <see cref="ServiceLifetime.Singleton"/>.</param>
    /// <returns>Service collection.</returns>
    [RequiresUnreferencedCode(DynamicCodeMessage)]
    [RequiresDynamicCode(UnreferencedCodeMessage)]
    public static IServiceCollection AddKeyedCouchbaseVectorStore(
        this IServiceCollection services,
        object? serviceKey,
        Func<IServiceProvider, IScope>? scopeProvider = default,
        Func<IServiceProvider, CouchbaseVectorStoreOptions>? optionsProvider = default,
        ServiceLifetime lifetime = ServiceLifetime.Singleton)
    {
        Verify.NotNull(services);

        services.Add(new ServiceDescriptor(typeof(CouchbaseVectorStore), serviceKey, (sp, _) =>
        {
            var scope = scopeProvider is null ? sp.GetRequiredService<IScope>() : scopeProvider(sp);
            var options = GetStoreOptions(sp, optionsProvider);

            return new CouchbaseVectorStore(scope, options);
        }, lifetime));

        services.Add(new ServiceDescriptor(typeof(VectorStore), serviceKey,
            static (sp, key) => sp.GetRequiredKeyedService<CouchbaseVectorStore>(key), lifetime));

        return services;
    }

    /// <summary>
    /// Registers a <see cref="CouchbaseVectorStore"/> as <see cref="VectorStore"/>
    /// using the provided connection details.
    /// </summary>
    /// <inheritdoc cref="AddKeyedCouchbaseVectorStore(IServiceCollection, object?, string, string, string, string, string, CouchbaseVectorStoreOptions?, ServiceLifetime)"/>
    [RequiresUnreferencedCode(DynamicCodeMessage)]
    [RequiresDynamicCode(UnreferencedCodeMessage)]
    public static IServiceCollection AddCouchbaseVectorStore(
        this IServiceCollection services,
        string connectionString,
        string username,
        string password,
        string bucketName,
        string scopeName,
        CouchbaseVectorStoreOptions? options = default,
        ServiceLifetime lifetime = ServiceLifetime.Singleton)
    {
        return AddKeyedCouchbaseVectorStore(services, serviceKey: null, connectionString, username, password, bucketName, scopeName, options, lifetime);
    }

    /// <summary>
    /// Registers a keyed <see cref="CouchbaseVectorStore"/> as <see cref="VectorStore"/>
    /// using the provided connection details.
    /// </summary>
    /// <param name="services">The <see cref="IServiceCollection"/> to register the <see cref="CouchbaseVectorStore"/> on.</param>
    /// <param name="serviceKey">The key with which to associate the vector store.</param>
    /// <param name="connectionString">Connection string required to connect to Couchbase.</param>
    /// <param name="username">Username required to connect to Couchbase.</param>
    /// <param name="password">Password required to connect to Couchbase.</param>
    /// <param name="bucketName">Bucket name for Couchbase.</param>
    /// <param name="scopeName">Scope name for Couchbase.</param>
    /// <param name="options">Options to further configure the <see cref="CouchbaseVectorStore"/>.</param>
    /// <param name="lifetime">The service lifetime for the store. Defaults to <see cref="ServiceLifetime.Singleton"/>.</param>
    /// <returns>Service collection.</returns>
    [RequiresUnreferencedCode(DynamicCodeMessage)]
    [RequiresDynamicCode(UnreferencedCodeMessage)]
    public static IServiceCollection AddKeyedCouchbaseVectorStore(
        this IServiceCollection services,
        object? serviceKey,
        string connectionString,
        string username,
        string password,
        string bucketName,
        string scopeName,
        CouchbaseVectorStoreOptions? options = default,
        ServiceLifetime lifetime = ServiceLifetime.Singleton)
    {
        Verify.NotNull(services);
        Verify.NotNullOrWhiteSpace(connectionString);
        Verify.NotNullOrWhiteSpace(username);
        Verify.NotNullOrWhiteSpace(password);
        Verify.NotNullOrWhiteSpace(bucketName);
        Verify.NotNullOrWhiteSpace(scopeName);

        return AddKeyedCouchbaseVectorStore(services, serviceKey, _ =>
        {
            var clusterOptions = new ClusterOptions
            {
                ConnectionString = connectionString,
                UserName = username,
                Password = password
            };

            var cluster = Cluster.ConnectAsync(clusterOptions).GetAwaiter().GetResult();
            var bucket = cluster.BucketAsync(bucketName).GetAwaiter().GetResult();
            return bucket.Scope(scopeName);
        }, _ => options!, lifetime);
    }

    /// <summary>
    /// Registers a <see cref="CouchbaseSearchCollection{TKey,TRecord}"/> as <see cref="VectorStoreCollection{TKey, TRecord}"/>
    /// with <see cref="IScope"/> retrieved from the dependency injection container.
    /// </summary>
    [RequiresUnreferencedCode(DynamicCodeMessage)]
    [RequiresDynamicCode(UnreferencedCodeMessage)]
    public static IServiceCollection AddCouchbaseSearchCollection<TKey, TRecord>(
        this IServiceCollection services,
        string name,
        Func<IServiceProvider, IScope>? scopeProvider = default,
        Func<IServiceProvider, CouchbaseSearchCollectionOptions>? optionsProvider = default,
        ServiceLifetime lifetime = ServiceLifetime.Singleton)
        where TKey : notnull
        where TRecord : class
    {
        return services.AddKeyedSingleton<VectorStoreCollection<TKey, TRecord>>(null, (sp, _) =>
        {
            var scope = scopeProvider?.Invoke(sp) ?? sp.GetRequiredService<IScope>();
            var options = optionsProvider?.Invoke(sp);
            return new CouchbaseSearchCollection<TKey, TRecord>(scope, name, options);
        });
    }

    /// <summary>
    /// Registers a <see cref="CouchbaseQueryCollection{TKey,TRecord}"/> as <see cref="VectorStoreCollection{TKey, TRecord}"/>
    /// with <see cref="IScope"/> retrieved from the dependency injection container.
    /// </summary>
    [RequiresUnreferencedCode(DynamicCodeMessage)]
    [RequiresDynamicCode(UnreferencedCodeMessage)]
    public static IServiceCollection AddCouchbaseQueryCollection<TKey, TRecord>(
        this IServiceCollection services,
        string name,
        Func<IServiceProvider, IScope>? scopeProvider = default,
        Func<IServiceProvider, CouchbaseQueryCollectionOptions>? optionsProvider = default,
        ServiceLifetime lifetime = ServiceLifetime.Singleton)
        where TKey : notnull
        where TRecord : class
    {
        return services.AddKeyedSingleton<VectorStoreCollection<TKey, TRecord>>(null, (sp, _) =>
        {
            var scope = scopeProvider?.Invoke(sp) ?? sp.GetRequiredService<IScope>();
            var options = optionsProvider?.Invoke(sp);
            return new CouchbaseQueryCollection<TKey, TRecord>(scope, name, options);
        });
    }

    /// <summary>
    /// Registers a <see cref="CouchbaseSearchDynamicCollection"/> as <see cref="VectorStoreCollection{TKey, TRecord}"/>
    /// with <see cref="IScope"/> retrieved from the dependency injection container.
    /// </summary>
    [RequiresUnreferencedCode(DynamicCodeMessage)]
    [RequiresDynamicCode(UnreferencedCodeMessage)]
    [Experimental("MEVD9001")]
    public static IServiceCollection AddCouchbaseSearchDynamicCollection(
        this IServiceCollection services,
        string name,
        Func<IServiceProvider, IScope>? scopeProvider = default,
        Func<IServiceProvider, CouchbaseSearchCollectionOptions>? optionsProvider = default,
        ServiceLifetime lifetime = ServiceLifetime.Singleton)
    {
        return services.AddKeyedSingleton<VectorStoreCollection<object, Dictionary<string, object?>>>(null, (sp, _) =>
        {
            var scope = scopeProvider?.Invoke(sp) ?? sp.GetRequiredService<IScope>();
            var options = optionsProvider?.Invoke(sp) ?? new CouchbaseSearchCollectionOptions();
            return new CouchbaseSearchDynamicCollection(scope, name, options);
        });
    }

    /// <summary>
    /// Registers a <see cref="CouchbaseQueryDynamicCollection"/> as <see cref="VectorStoreCollection{TKey, TRecord}"/>
    /// with <see cref="IScope"/> retrieved from the dependency injection container.
    /// </summary>
    [RequiresUnreferencedCode(DynamicCodeMessage)]
    [RequiresDynamicCode(UnreferencedCodeMessage)]
    [Experimental("MEVD9001")]
    public static IServiceCollection AddCouchbaseQueryDynamicCollection(
        this IServiceCollection services,
        string name,
        Func<IServiceProvider, IScope>? scopeProvider = default,
        Func<IServiceProvider, CouchbaseQueryCollectionOptions>? optionsProvider = default,
        CouchbaseIndexType indexType = CouchbaseIndexType.Bhive,
        ServiceLifetime lifetime = ServiceLifetime.Singleton)
    {
        return services.AddKeyedSingleton<VectorStoreCollection<object, Dictionary<string, object?>>>(null, (sp, _) =>
        {
            var scope = scopeProvider?.Invoke(sp) ?? sp.GetRequiredService<IScope>();
            var options = optionsProvider?.Invoke(sp) ?? new CouchbaseQueryCollectionOptions();
            return new CouchbaseQueryDynamicCollection(scope, name, options, indexType);
        });
    }

    private static CouchbaseVectorStoreOptions? GetStoreOptions(IServiceProvider sp, Func<IServiceProvider, CouchbaseVectorStoreOptions?>? optionsProvider)
    {
        var options = optionsProvider?.Invoke(sp);
        if (options?.EmbeddingGenerator is not null)
        {
            return options; // The user has provided everything, there is nothing to change.
        }

        var embeddingGenerator = sp.GetService<IEmbeddingGenerator>();
        return embeddingGenerator is null
            ? options // There is nothing to change.
            : new(options) { EmbeddingGenerator = embeddingGenerator }; // Create a brand new copy in order to avoid modifying the original options.
    }
}