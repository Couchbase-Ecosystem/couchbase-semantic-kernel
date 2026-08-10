// Copyright (c) Microsoft. All rights reserved.

using Couchbase.KeyValue;
using Couchbase.VectorData;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using VectorData.ConformanceTests;
using Xunit;

namespace Couchbase.ConformanceTests;

public class CouchbaseDependencyInjectionTests
    : DependencyInjectionTests<
        CouchbaseVectorStore,
        CouchbaseQueryCollection<string, DependencyInjectionTests<string>.Record>,
        string,
        DependencyInjectionTests<string>.Record>, IAsyncLifetime
{
    // This class has no IClassFixture, so nothing else starts the shared test store.
    // ScopeProvider below reads CouchbaseTestStore.Instance.Scope, which throws until
    // the store is started, so start/stop it here. The store is reference-counted, so
    // this is safe alongside other test classes using the same singleton.
    public Task InitializeAsync() => Support.CouchbaseTestStore.Instance.ReferenceCountingStartAsync();

    public Task DisposeAsync() => Support.CouchbaseTestStore.Instance.ReferenceCountingStopAsync();

    private const string ConnectionString = "couchbase://localhost";
    private const string Username = "Administrator";
    private const string Password = "password";
    private const string BucketName = "travel-sample";
    private const string ScopeName = "inventory";

    protected override void PopulateConfiguration(ConfigurationManager configuration, object? serviceKey = null)
        => configuration.AddInMemoryCollection(
        [
            new(CreateConfigKey("Couchbase", serviceKey, "ConnectionString"), ConnectionString),
            new(CreateConfigKey("Couchbase", serviceKey, "Username"), Username),
            new(CreateConfigKey("Couchbase", serviceKey, "Password"), Password),
            new(CreateConfigKey("Couchbase", serviceKey, "BucketName"), BucketName),
            new(CreateConfigKey("Couchbase", serviceKey, "ScopeName"), ScopeName),
        ]);

    private static IScope ScopeProvider(IServiceProvider sp) => CouchbaseTestStoreScope();

    private static IScope CouchbaseTestStoreScope() => Support.CouchbaseTestStore.Instance.Scope;

    public override IEnumerable<Func<IServiceCollection, object?, string, ServiceLifetime, IServiceCollection>> CollectionDelegates
    {
        get
        {
            // The Couchbase connector currently exposes no keyed collection registration overload,
            // so the non-keyed overload is used for both cases.
            yield return (services, serviceKey, name, lifetime) =>
                services.AddCouchbaseQueryCollection<string, Record>(name, ScopeProvider, lifetime: lifetime);
        }
    }

    public override IEnumerable<Func<IServiceCollection, object?, ServiceLifetime, IServiceCollection>> StoreDelegates
    {
        get
        {
            yield return (services, serviceKey, lifetime) => serviceKey is null
                ? services.AddCouchbaseVectorStore(
                    ConnectionString, Username, Password, BucketName, ScopeName, lifetime: lifetime)
                : services.AddKeyedCouchbaseVectorStore(
                    serviceKey, ConnectionString, Username, Password, BucketName, ScopeName, lifetime: lifetime);

            yield return (services, serviceKey, lifetime) => serviceKey is null
                ? services.AddCouchbaseVectorStore(ScopeProvider, lifetime: lifetime)
                : services.AddKeyedCouchbaseVectorStore(serviceKey, ScopeProvider, lifetime: lifetime);
        }
    }

    [Fact]
    public void ConnectionStringCantBeNullOrEmpty()
    {
        IServiceCollection services = new ServiceCollection();

        Assert.Throws<ArgumentNullException>(() => services.AddCouchbaseVectorStore(
            connectionString: null!, Username, Password, BucketName, ScopeName));
        Assert.Throws<ArgumentException>(() => services.AddCouchbaseVectorStore(
            connectionString: "", Username, Password, BucketName, ScopeName));
    }
}
