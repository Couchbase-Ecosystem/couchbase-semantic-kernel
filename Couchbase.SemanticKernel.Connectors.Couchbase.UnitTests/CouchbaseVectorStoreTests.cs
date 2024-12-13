using Couchbase.KeyValue;
using Couchbase.Management.Collections;
using Microsoft.Extensions.VectorData;
using Moq;

namespace Couchbase.SemanticKernel.Connectors.Couchbase.UnitTests;

/// <summary>
/// Unit tests for <see cref="CouchbaseVectorStore"/> class.
/// </summary>
public sealed class CouchbaseVectorStoreTests
{
    private readonly Mock<IScope> _mockScope = new();

    [Fact]
    public void GetCollectionWithNotSupportedKeyThrowsException()
    {
        // Arrange
        var sut = new CouchbaseFtsVectorStore(this._mockScope.Object);

        // Act & Assert
        Assert.Throws<NotSupportedException>(() => sut.GetCollection<Guid, CouchbaseHotelModel>("collection"));
    }

    [Fact]
    public void GetCollectionWithSupportedKeyReturnsCollection()
    {
        // Arrange
        var sut = new CouchbaseFtsVectorStore(this._mockScope.Object);

        // Act
        var collectionWithStringKey = sut.GetCollection<string, CouchbaseHotelModel>("collection1");

        // Assert
        Assert.NotNull(collectionWithStringKey);
    }

    [Fact]
    public void GetCollectionWithFactoryReturnsCustomCollection()
    {
        // Arrange
        var mockFactory = new Mock<ICouchbaseVectorStoreRecordCollectionFactory>();
        var mockRecordCollection = new Mock<IVectorStoreRecordCollection<string, CouchbaseHotelModel>>();
    
        mockFactory
            .Setup(l => l.CreateVectorStoreRecordCollection<string, CouchbaseHotelModel>(
                this._mockScope.Object,
                "collection",
                It.IsAny<VectorStoreRecordDefinition>()))
            .Returns(mockRecordCollection.Object);
    
        var sut = new CouchbaseFtsVectorStore(
            this._mockScope.Object,
            new CouchbaseVectorStoreOptions { VectorStoreCollectionFactory = mockFactory.Object });
    
        // Act
        var collection = sut.GetCollection<string, CouchbaseHotelModel>("collection");
    
        // Assert
        Assert.Same(mockRecordCollection.Object, collection);
        mockFactory.Verify(l => l.CreateVectorStoreRecordCollection<string, CouchbaseHotelModel>(
            this._mockScope.Object,
            "collection",
            It.IsAny<VectorStoreRecordDefinition>()), Times.Once());
    }

    [Fact]
    public void GetCollectionWithoutFactoryReturnsDefaultCollection()
    {
        // Arrange
        var sut = new CouchbaseFtsVectorStore(this._mockScope.Object);
    
        // Act
        var collection = sut.GetCollection<string, CouchbaseHotelModel>("collection");
    
        // Assert
        Assert.NotNull(collection);
    }
    
    [Fact]
    public async Task ListCollectionNamesReturnsCollectionNamesAsync()
    {
        // Arrange
        var expectedCollectionNames = new List<string> { "collection-1", "collection-2", "collection-3" };

        // Create mock collections
        var mockCollections = expectedCollectionNames
            .Select(name => new CollectionSpec("scope-1", name))
            .ToList();

        // Create mock scope
        var mockScopeSpec = new ScopeSpec("scope-1") { Collections = mockCollections };

        // Mock the GetAllScopesAsync method to return the desired scope
        var mockBucket = new Mock<IBucket>();
        mockBucket
            .Setup(b => b.Collections.GetAllScopesAsync(It.IsAny<GetAllScopesOptions?>()))
            .ReturnsAsync(new List<ScopeSpec> { mockScopeSpec });

        var mockScope = new Mock<IScope>();
        mockScope.Setup(s => s.Bucket).Returns(mockBucket.Object);
        mockScope.Setup(s => s.Name).Returns("scope-1");

        var sut = new CouchbaseFtsVectorStore(mockScope.Object);

        // Act
        var actualCollectionNames = await sut.ListCollectionNamesAsync().ToListAsync();

        // Assert
        Assert.Equal(expectedCollectionNames, actualCollectionNames);
    }
}
