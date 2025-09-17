// Copyright (c) Microsoft. All rights reserved.

#pragma warning disable SKEXP0020 // Suppress experimental API warnings

using System.Globalization;
using Couchbase;
using Couchbase.KeyValue;
using Couchbase.SemanticKernel;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.VectorData;
using VectorData.ConformanceTests;
using VectorData.ConformanceTests.Models;
using Xunit;

namespace Couchbase.ConformanceTests;

public class CouchbaseDependencyInjectionTests
    : DependencyInjectionTests<CouchbaseVectorStore, VectorStoreCollection<string, SimpleRecord<string>>, string, SimpleRecord<string>>
{
    private const string ConnectionString = "couchbases://cb.1-0nc9a4iqbyiqy7.cloud.couchbase.com";
    private const string Username = "Admin";
    private const string Password = "Admin@123";
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

    private static async Task<IScope> ScopeProvider(IServiceProvider sp, object? serviceKey = null)
    {
        var connectionString = sp.GetRequiredService<IConfiguration>().GetRequiredSection(CreateConfigKey("Couchbase", serviceKey, "ConnectionString")).Value!;
        var username = sp.GetRequiredService<IConfiguration>().GetRequiredSection(CreateConfigKey("Couchbase", serviceKey, "Username")).Value!;
        var password = sp.GetRequiredService<IConfiguration>().GetRequiredSection(CreateConfigKey("Couchbase", serviceKey, "Password")).Value!;
        var bucketName = sp.GetRequiredService<IConfiguration>().GetRequiredSection(CreateConfigKey("Couchbase", serviceKey, "BucketName")).Value!;
        var scopeName = sp.GetRequiredService<IConfiguration>().GetRequiredSection(CreateConfigKey("Couchbase", serviceKey, "ScopeName")).Value!;

        var cluster = await Cluster.ConnectAsync(connectionString, username, password);
        var bucket = await cluster.BucketAsync(bucketName);
        return bucket.Scope(scopeName);
    }

    public override IEnumerable<Func<IServiceCollection, object?, string, ServiceLifetime, IServiceCollection>> CollectionDelegates
    {
        get
        {
            yield return (services, serviceKey, name, lifetime) => serviceKey is null
                ? services
                    .AddSingleton<IScope>(sp => ScopeProvider(sp).Result)
                    .AddCouchbaseCollection<string, SimpleRecord<string>>(name, lifetime: lifetime)
                : services
                    .AddSingleton<IScope>(sp => ScopeProvider(sp, serviceKey).Result)
                    .AddKeyedCouchbaseCollection<string, SimpleRecord<string>>(serviceKey, name, lifetime: lifetime);

            yield return (services, serviceKey, name, lifetime) => serviceKey is null
                ? services.AddCouchbaseCollection<string, SimpleRecord<string>>(
                    name, ConnectionString, Username, Password, BucketName, ScopeName, lifetime: lifetime)
                : services.AddKeyedCouchbaseCollection<string, SimpleRecord<string>>(
                    serviceKey, name, ConnectionString, Username, Password, BucketName, ScopeName, lifetime: lifetime);

            yield return (services, serviceKey, name, lifetime) => serviceKey is null
                ? services.AddCouchbaseCollection<string, SimpleRecord<string>>(
                    name, sp => ScopeProvider(sp).Result, lifetime: lifetime)
                : services.AddKeyedCouchbaseCollection<string, SimpleRecord<string>>(
                    serviceKey, name, sp => ScopeProvider(sp, serviceKey).Result, lifetime: lifetime);
        }
    }

    public override IEnumerable<Func<IServiceCollection, object?, ServiceLifetime, IServiceCollection>> StoreDelegates
    {
        get
        {
            yield return (services, serviceKey, lifetime) => serviceKey is null
                ? services.AddCouchbaseVectorStore(ConnectionString, Username, Password, BucketName, ScopeName, lifetime: lifetime)
                : services.AddKeyedCouchbaseVectorStore(serviceKey, ConnectionString, Username, Password, BucketName, ScopeName, lifetime: lifetime);

            yield return (services, serviceKey, lifetime) => serviceKey is null
                ? services.AddCouchbaseVectorStore(sp => ScopeProvider(sp).GetAwaiter().GetResult(), lifetime: lifetime)
                : services.AddKeyedCouchbaseVectorStore(serviceKey, sp => ScopeProvider(sp, serviceKey).GetAwaiter().GetResult(), lifetime: lifetime);

            yield return (services, serviceKey, lifetime) => serviceKey is null
                ? services
                    .AddSingleton<IScope>(sp => ScopeProvider(sp).Result)
                    .AddCouchbaseVectorStore(lifetime: lifetime)
                : services
                    .AddSingleton<IScope>(sp => ScopeProvider(sp, serviceKey).Result)
                    .AddKeyedCouchbaseVectorStore(serviceKey, lifetime: lifetime);
        }
    }

    [Fact]
    public void ConnectionStringCantBeNull()
    {
        IServiceCollection services = new ServiceCollection();

        Assert.Throws<ArgumentException>(() => services.AddCouchbaseVectorStore(connectionString: null!, "user", "pass", "bucket", "scope"));
        Assert.Throws<ArgumentException>(() => services.AddKeyedCouchbaseVectorStore(serviceKey: "notNull", connectionString: null!, "user", "pass", "bucket", "scope"));
        Assert.Throws<ArgumentException>(() => services.AddCouchbaseCollection<ulong, SimpleRecord<ulong>>(
            name: "notNull", connectionString: null!, "user", "pass", "bucket", "scope"));
        Assert.Throws<ArgumentException>(() => services.AddKeyedCouchbaseCollection<ulong, SimpleRecord<ulong>>(
            serviceKey: "notNull", name: "notNull", connectionString: null!, "user", "pass", "bucket", "scope"));
    }
} 