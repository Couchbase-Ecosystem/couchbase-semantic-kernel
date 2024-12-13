using System.Reflection;
using Couchbase.KeyValue;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.VectorData;
using Moq;

namespace Couchbase.SemanticKernel.Connectors.Couchbase.UnitTests;

/// <summary>
/// Unit Tests for <see cref="CouchbaseServiceCollectionExtensions"/> class.
/// </summary>
public class CouchbaseServiceCollectionExtensionsTests
{
    private readonly IServiceCollection _serviceCollection = new ServiceCollection();

    [Fact]
    public void AddVectorStoreRegistersClass()
    {
        // Arrange
        var mockScope = Mock.Of<IScope>();
        this._serviceCollection.AddSingleton<IScope>(mockScope);

        // Act
        this._serviceCollection.AddCouchbaseVectorStore();

        var serviceProvider = this._serviceCollection.BuildServiceProvider();
        var vectorStore = serviceProvider.GetRequiredService<IVectorStore>();

        // Assert
        Assert.NotNull(vectorStore);
        Assert.IsType<CouchbaseFtsVectorStore>(vectorStore);
    }

    [Fact]
    public void AddVectorStoreWithConnectionStringRegistersClass()
    {
        // Arrange
        // Act
        this._serviceCollection.AddCouchbaseVectorStore("couchbases://cb.hlcup4o4jmjr55yf.cloud.couchbase.com", "Adminn", "Azad@12345", "vector-search-testing", "shared");

        var serviceProvider = this._serviceCollection.BuildServiceProvider();
        var vectorStore = serviceProvider.GetRequiredService<IVectorStore>();

        // Assert
        Assert.NotNull(vectorStore);
        Assert.IsType<CouchbaseFtsVectorStore>(vectorStore);

        var scope = (IScope)vectorStore.GetType().GetField("_scope", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(vectorStore)!;
        Assert.NotNull(scope);
    }

    [Fact]
    public void AddVectorStoreRecordCollectionWithConnectionStringRegistersClass()
    {
        // Act
        this._serviceCollection.AddCouchbaseVectorStoreRecordCollection<TestRecord>(
            "couchbases://cb.hlcup4o4jmjr55yf.cloud.couchbase.com",
            "Adminn",
            "Azad@12345",
            "vector-search-testing",
            "shared",
            "mistralai");

        // Assert
        this.AssertVectorStoreRecordCollectionCreated();
    }

    private void AssertVectorStoreRecordCollectionCreated()
    {
        var serviceProvider = this._serviceCollection.BuildServiceProvider();

        var collection = serviceProvider.GetRequiredService<IVectorStoreRecordCollection<string, TestRecord>>();
        Assert.NotNull(collection);
        Assert.IsType<CouchbaseVectorStoreRecordCollection<TestRecord>>(collection);

        var vectorizedSearch = serviceProvider.GetRequiredService<IVectorizedSearch<TestRecord>>();
        Assert.NotNull(vectorizedSearch);
        Assert.IsType<CouchbaseVectorStoreRecordCollection<TestRecord>>(vectorizedSearch);
    }

    private sealed class TestRecord
    {
        [VectorStoreRecordKey]
        public string Id { get; set; } = string.Empty;
    }
}