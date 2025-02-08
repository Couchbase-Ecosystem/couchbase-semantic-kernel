using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Couchbase.KeyValue;
using Couchbase.Management.Buckets;
using Couchbase.Management.Collections;
using Couchbase.Search;
using Microsoft.Extensions.VectorData;
using Moq;

namespace Couchbase.SemanticKernel.UnitTests;

public class CouchbaseVectorStoreRecordCollectionTests
{
    private readonly Mock<IScope> _mockScope = new();
    private readonly Mock<ICouchbaseCollection> _mockCollection = new();
    private readonly Mock<ICouchbaseCollectionManager> _mockCollectionManager = new();

    
    public CouchbaseVectorStoreRecordCollectionTests()
    {
        this._mockScope
            .Setup(l => l.Collection(It.IsAny<string>()))
            .Returns(this._mockCollection.Object);
    }

    [Fact]
    public void ConstructorForModelWithoutKeyThrowsException()
    {
        // Act & Assert
        var exception = Assert.Throws<ArgumentException>(() => new CouchbaseFtsVectorStoreRecordCollection<object>(this._mockScope.Object, "collection"));
        Assert.Contains("No key property found", exception.Message);
    }

    [Fact]
    public void ConstructorWithDeclarativeModelInitializesCollection()
    {
        // Act and Assert
        var collection = new CouchbaseFtsVectorStoreRecordCollection<CouchbaseHotelModel>(this._mockScope.Object, "collection");
        Assert.NotNull(collection);
    }

    [Fact]
    public void ConstructorWithImperativeModelInitializesCollection()
    {
        // Arrange
        var definition = new VectorStoreRecordDefinition
        {
            Properties = new List<VectorStoreRecordProperty>
            {
                new VectorStoreRecordKeyProperty("Id", typeof(string))
            }
        };
        
        // Act
        var collection = new CouchbaseFtsVectorStoreRecordCollection<TestModel>(
            this._mockScope.Object,
            "collection",
            new CouchbaseFtsVectorStoreRecordCollectionOptions<TestModel>
            {
                VectorStoreRecordDefinition = definition
            });

        // Assert
        Assert.NotNull(collection);
    }

    
    [Theory]
    [MemberData(nameof(CollectionExistsData))]
    public async Task CollectionExistsReturnsValidResultAsync(List<string> collections, string collectionName, bool expectedResult)
    {
        // Arrange
        var mockCollectionSpecs = collections
            .Select(name => new CollectionSpec("mockScope", name))
            .ToList();

        var mockScopeSpec = new ScopeSpec("mockScope") { Collections = mockCollectionSpecs };

        _mockScope.Setup(s => s.Name).Returns("mockScope");

        _mockCollectionManager
            .Setup(m => m.GetAllScopesAsync(It.IsAny<GetAllScopesOptions?>()))
            .ReturnsAsync(new List<ScopeSpec> { mockScopeSpec });

        _mockScope.Setup(s => s.Bucket.Collections).Returns(_mockCollectionManager.Object);

        var sut = new CouchbaseFtsVectorStoreRecordCollection<CouchbaseHotelModel>(_mockScope.Object, collectionName);

        // Act
        var actualResult = await sut.CollectionExistsAsync();

        // Assert
        Assert.Equal(expectedResult, actualResult);
    }
    
    [Theory]
    [InlineData(true, 0)] // Collection exists, no creation
    [InlineData(false, 1)] // Collection doesn't exist, creation needed
    public async Task CreateCollectionInvokesValidMethodsAsync(bool collectionExists, int expectedCreations)
    {
        // Arrange
        const string CollectionName = "test-collection";
        const string ScopeName = "test-scope";

        // Mock existing scopes and collections
        var mockScopeSpec = new ScopeSpec(ScopeName)
        {
            Collections = collectionExists
                ? new List<CollectionSpec> { new CollectionSpec(ScopeName, CollectionName) }
                : new List<CollectionSpec>() // No collection exists
        };
        
        // Mock the GetAllScopesAsync method
        _mockCollectionManager
            .Setup(m => m.GetAllScopesAsync(It.IsAny<GetAllScopesOptions?>()))
            .ReturnsAsync(new List<ScopeSpec> { mockScopeSpec });

        // Mock the CreateCollectionAsync method to verify invocation
        if (!collectionExists)
        {
            _mockCollectionManager
                .Setup(m => m.CreateCollectionAsync(
                    It.Is<CollectionSpec>(spec => spec.Name == CollectionName && spec.ScopeName == ScopeName),
                    It.IsAny<CreateCollectionOptions?>()))
                .Returns(Task.CompletedTask)
                .Verifiable();
        }

        _mockScope.Setup(s => s.Name).Returns(ScopeName);
        _mockScope.Setup(s => s.Bucket.Collections).Returns(_mockCollectionManager.Object);

        var sut = new CouchbaseFtsVectorStoreRecordCollection<CouchbaseHotelModel>(_mockScope.Object, CollectionName);

        // Act
        await sut.CreateCollectionAsync();

        // Assert
        _mockCollectionManager.Verify(
            m => m.CreateCollectionAsync(
                It.Is<CollectionSpec>(spec => spec.Name == CollectionName && spec.ScopeName == ScopeName),
                It.IsAny<CreateCollectionOptions?>()),
            Times.Exactly(expectedCreations));
    }
    
    [Fact]
    public async Task CreateCollectionUsesValidPropertiesAsync()
    {
        // Arrange
        const string CollectionName = "test-collection";
        const string ScopeName = "test-scope";

        var mockScopeSpec = new ScopeSpec(ScopeName)
        {
            Collections = new List<CollectionSpec>() // No collections initially
        };
        
        // Mock the GetAllScopesAsync method
        _mockCollectionManager
            .Setup(m => m.GetAllScopesAsync(It.IsAny<GetAllScopesOptions?>()))
            .ReturnsAsync(new List<ScopeSpec> { mockScopeSpec });

        // Mock the CreateCollectionAsync method to verify properties
        _mockCollectionManager
            .Setup(m => m.CreateCollectionAsync(
                It.Is<CollectionSpec>(spec =>
                    spec.Name == CollectionName &&
                    spec.ScopeName == ScopeName &&
                    spec.MaxExpiry == null), // Validate additional properties here if needed
                It.IsAny<CreateCollectionOptions?>())) // Pass CreateCollectionOptions? instead of CancellationToken
            .Returns(Task.CompletedTask)
            .Verifiable();

        var mockScope = new Mock<IScope>();
        mockScope.Setup(s => s.Name).Returns(ScopeName);
        mockScope.Setup(s => s.Bucket.Collections).Returns(_mockCollectionManager.Object);

        var sut = new CouchbaseFtsVectorStoreRecordCollection<CouchbaseHotelModel>(mockScope.Object, CollectionName);

        // Act
        await sut.CreateCollectionAsync();

        // Assert
        _mockCollectionManager.Verify(
            m => m.CreateCollectionAsync(
                It.Is<CollectionSpec>(spec =>
                    spec.Name == CollectionName &&
                    spec.ScopeName == ScopeName &&
                    spec.MaxExpiry == null),
                It.IsAny<CreateCollectionOptions?>()), // Verify CreateCollectionOptions? was used
            Times.Once());
    }
    
    [Theory]
    [MemberData(nameof(CreateCollectionIfNotExistsData))]
    public async Task CreateCollectionIfNotExistsInvokesValidMethodsAsync(List<string> collections, int actualCollectionCreations)
    {
        // Arrange
        const string CollectionName = "collection";
        const string ScopeName = "scope";

        // Mock the existing collections in the scope
        var mockScopeSpec = new ScopeSpec(ScopeName)
        {
            Collections = collections
                .Select(name => new CollectionSpec(ScopeName, name))
                .ToList()
        };
        
        // Mock GetAllScopesAsync to return the mocked scope and collections
        _mockCollectionManager
            .Setup(manager => manager.GetAllScopesAsync(It.IsAny<GetAllScopesOptions?>()))
            .ReturnsAsync(new List<ScopeSpec> { mockScopeSpec });

        // Mock CreateCollectionAsync to verify invocation
        if (!collections.Contains(CollectionName))
        {
            _mockCollectionManager
                .Setup(manager => manager.CreateCollectionAsync(
                    It.Is<CollectionSpec>(spec =>
                        spec.Name == CollectionName && spec.ScopeName == ScopeName),
                    It.IsAny<CreateCollectionOptions?>()))
                .Returns(Task.CompletedTask)
                .Verifiable();
        }

        _mockScope.Setup(scope => scope.Name).Returns(ScopeName);
        _mockScope.Setup(scope => scope.Bucket.Collections).Returns(_mockCollectionManager.Object);

        var sut = new CouchbaseFtsVectorStoreRecordCollection<CouchbaseHotelModel>(_mockScope.Object, CollectionName);

        // Act
        await sut.CreateCollectionIfNotExistsAsync();

        // Assert
        _mockCollectionManager.Verify(
            manager => manager.CreateCollectionAsync(
                It.Is<CollectionSpec>(spec =>
                    spec.Name == CollectionName && spec.ScopeName == ScopeName),
                It.IsAny<CreateCollectionOptions?>()),
            Times.Exactly(actualCollectionCreations));
    }

    [Fact]
    public async Task DeleteInvokesValidMethodsAsync()
    {
        // Arrange
        const string RecordKey = "key";
        const string CollectionName = "test-collection";
        
        _mockScope.Setup(s => s.Collection(It.IsAny<string>())).Returns(_mockCollection.Object);

        var sut = new CouchbaseFtsVectorStoreRecordCollection<CouchbaseHotelModel>(_mockScope.Object, CollectionName);

        // Act
        await sut.DeleteAsync(RecordKey);

        // Assert
        _mockCollection.Verify(
            c => c.RemoveAsync(
                It.Is<string>(key => key == RecordKey),
                It.IsAny<RemoveOptions>()), // Adjusted to remove options only
            Times.Once);
    }
    
    [Fact]
    public async Task DeleteBatchInvokesValidMethodsAsync()
    {
        // Arrange
        List<string> recordKeys = new() { "key1", "key2" };
        
        foreach (var key in recordKeys)
        {
            this._mockCollection
                .Setup(l => l.RemoveAsync(
                    key,
                    It.IsAny<RemoveOptions?>()))
                .Returns(Task.CompletedTask)
                .Verifiable();
        }

        this._mockScope
            .Setup(s => s.Collection(It.IsAny<string>()))
            .Returns(this._mockCollection.Object);

        var sut = new CouchbaseFtsVectorStoreRecordCollection<CouchbaseHotelModel>(this._mockScope.Object, "test-collection");

        // Act
        await sut.DeleteBatchAsync(recordKeys);

        // Assert
        foreach (var key in recordKeys)
        {
            this._mockCollection.Verify(
                l => l.RemoveAsync(
                    key,
                    It.IsAny<RemoveOptions?>()),
                Times.Once());
        }
    }
    
    [Fact]
    public async Task DeleteCollectionInvokesValidMethodsAsync()
    {
        // Arrange
        const string CollectionName = "test-collection";
        const string ScopeName = "test-scope";
        
        // Mock the DropCollectionAsync method
        _mockCollectionManager
            .Setup(manager => manager.DropCollectionAsync(
                It.Is<string>(name => name == ScopeName),
                It.Is<string>(name => name == CollectionName),
                It.IsAny<DropCollectionOptions?>()))
            .Returns(Task.CompletedTask)
            .Verifiable();

        // Mock IScope to return the mocked collection manager
        this._mockScope.Setup(scope => scope.Name).Returns(ScopeName);
        this._mockScope.Setup(scope => scope.Bucket.Collections).Returns(_mockCollectionManager.Object);

        // System Under Test (SUT)
        var sut = new CouchbaseFtsVectorStoreRecordCollection<CouchbaseHotelModel>(this._mockScope.Object, CollectionName);

        // Act
        await sut.DeleteCollectionAsync();

        // Assert
        _mockCollectionManager.Verify(
            manager => manager.DropCollectionAsync(
                It.Is<string>(name => name == ScopeName),
                It.Is<string>(name => name == CollectionName),
                It.IsAny<DropCollectionOptions?>()),
            Times.Once());
    }
    
    [Fact]
    public async Task GetReturnsValidRecordAsync()
    {
        // Arrange
        const string RecordKey = "key";
        const string ExpectedHotelName = "Test Name";

        // Create expected record
        var expectedRecord = new CouchbaseHotelModel(RecordKey) { HotelName = ExpectedHotelName };

        // Mock IGetResult
        var mockGetResult = new Mock<IGetResult>();
        mockGetResult
            .Setup(r => r.ContentAs<CouchbaseHotelModel>())
            .Returns(expectedRecord);

        _mockCollection
            .Setup(c => c.GetAsync(
                It.Is<string>(key => key == RecordKey),
                It.IsAny<GetOptions?>()))
            .ReturnsAsync(mockGetResult.Object);

        // Mock IScope
        var mockScope = new Mock<IScope>();
        mockScope
            .Setup(s => s.Collection(It.IsAny<string>()))
            .Returns(_mockCollection.Object);

        // Mock Mapper (updated to handle byte[] as output)
        var mockMapper = new Mock<IVectorStoreRecordMapper<CouchbaseHotelModel, byte[]>>();
        mockMapper
            .Setup(m => m.MapFromStorageToDataModel(
                It.IsAny<byte[]>(), 
                It.IsAny<StorageToDataModelMapperOptions>()))
            .Returns(expectedRecord); // Assuming you return the mapped record from byte[]

        // Create the SUT
        var sut = new CouchbaseFtsVectorStoreRecordCollection<CouchbaseHotelModel>(
            mockScope.Object, 
            "test-collection", 
            new CouchbaseFtsVectorStoreRecordCollectionOptions<CouchbaseHotelModel>
            {
                JsonDocumentCustomMapper = mockMapper.Object
            });

        // Act
        var result = await sut.GetAsync(RecordKey);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(RecordKey, result!.HotelId); 
        Assert.Equal(ExpectedHotelName, result.HotelName);

        // Verify expected calls
        _mockCollection.Verify(
            c => c.GetAsync(
                It.Is<string>(key => key == RecordKey),
                It.IsAny<GetOptions?>()),
            Times.Once());

        mockMapper.Verify(
            m => m.MapFromStorageToDataModel(
                It.IsAny<byte[]>(), // Adjusted to use byte[] as input type
                It.IsAny<StorageToDataModelMapperOptions>()),
            Times.Once());
    }

    [Fact]
    public async Task GetBatchReturnsValidRecordAsync()
    {
        // Arrange
        var expectedRecords = new List<CouchbaseHotelModel>
        {
            new("key1") { HotelName = "Test Name 1" },
            new("key2") { HotelName = "Test Name 2" },
            new("key3") { HotelName = "Test Name 3" }
        };

        // Mock JSON response as byte array
        var jsonResults = expectedRecords
            .Select(r => Encoding.UTF8.GetBytes(JsonSerializer.Serialize(r)))
            .ToList();

        var mockResult1 = new Mock<IGetResult>();
        mockResult1.Setup(r => r.ContentAs<byte[]>()).Returns(jsonResults[0]);

        var mockResult2 = new Mock<IGetResult>();
        mockResult2.Setup(r => r.ContentAs<byte[]>()).Returns(jsonResults[1]);

        var mockResult3 = new Mock<IGetResult>();
        mockResult3.Setup(r => r.ContentAs<byte[]>()).Returns(jsonResults[2]);

        // Setup individual GetAsync calls
        _mockCollection.Setup(c => c.GetAsync("key1", It.IsAny<GetOptions?>()))
            .ReturnsAsync(mockResult1.Object);

        _mockCollection.Setup(c => c.GetAsync("key2", It.IsAny<GetOptions?>()))
            .ReturnsAsync(mockResult2.Object);

        _mockCollection.Setup(c => c.GetAsync("key3", It.IsAny<GetOptions?>()))
            .ReturnsAsync(mockResult3.Object);

        _mockScope.Setup(s => s.Collection(It.IsAny<string>())).Returns(_mockCollection.Object);

        // Mock Mapper (adjusted for byte[] conversion)
        var mockMapper = new Mock<IVectorStoreRecordMapper<CouchbaseHotelModel, byte[]>>();
        mockMapper
            .Setup(m => m.MapFromStorageToDataModel(It.IsAny<byte[]>(), It.IsAny<StorageToDataModelMapperOptions>()))
            .Returns<byte[], StorageToDataModelMapperOptions>((b, _) =>
                JsonSerializer.Deserialize<CouchbaseHotelModel>(Encoding.UTF8.GetString(b))!);

        // Create the SUT
        var sut = new CouchbaseFtsVectorStoreRecordCollection<CouchbaseHotelModel>(
            _mockScope.Object,
            "test-collection",
            new CouchbaseFtsVectorStoreRecordCollectionOptions<CouchbaseHotelModel>
            {
                JsonDocumentCustomMapper = mockMapper.Object
            });

        // Act
        var results = await sut.GetBatchAsync(new[] { "key1", "key2", "key3" }).ToListAsync();

        // Assert
        Assert.NotNull(results);
        Assert.Equal(3, results.Count);

        Assert.Equal("key1", results[0].HotelId);
        Assert.Equal("Test Name 1", results[0].HotelName);

        Assert.Equal("key2", results[1].HotelId);
        Assert.Equal("Test Name 2", results[1].HotelName);

        Assert.Equal("key3", results[2].HotelId);
        Assert.Equal("Test Name 3", results[2].HotelName);

        // Verify correct calls to the collection
        _mockCollection.Verify(c => c.GetAsync("key1", It.IsAny<GetOptions?>()), Times.Once);
        _mockCollection.Verify(c => c.GetAsync("key2", It.IsAny<GetOptions?>()), Times.Once);
        _mockCollection.Verify(c => c.GetAsync("key3", It.IsAny<GetOptions?>()), Times.Once);

        // Verify that the mapper was used
        mockMapper.Verify(
            m => m.MapFromStorageToDataModel(It.IsAny<byte[]>(), It.IsAny<StorageToDataModelMapperOptions>()),
            Times.Exactly(3));
    }

    [Fact]
    public async Task GetWithCustomMapperWorksCorrectlyAsync()
    {
        // Arrange
        const string RecordKey = "key";
        var expectedHotel = new CouchbaseHotelModel(RecordKey) { HotelName = "Mapped Name" };

        // Mock JSON response as byte array
        var jsonData = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(expectedHotel));

        // Mock IGetResult for GetAsync
        var mockGetResult = new Mock<IGetResult>();
        mockGetResult
            .Setup(r => r.ContentAs<byte[]>())
            .Returns(jsonData);

        // Mock Couchbase collection
        _mockCollection
            .Setup(c => c.GetAsync(
                It.Is<string>(key => key == RecordKey),
                It.IsAny<GetOptions?>()))
            .ReturnsAsync(mockGetResult.Object);

        // Mock Scope
        _mockScope
            .Setup(s => s.Collection(It.IsAny<string>()))
            .Returns(_mockCollection.Object);

        // Mock Mapper
        var mockMapper = new Mock<IVectorStoreRecordMapper<CouchbaseHotelModel, byte[]>>();
        mockMapper
            .Setup(m => m.MapFromStorageToDataModel(
                It.IsAny<byte[]>(),
                It.IsAny<StorageToDataModelMapperOptions>()))
            .Returns<byte[], StorageToDataModelMapperOptions>((b, _) =>
                JsonSerializer.Deserialize<CouchbaseHotelModel>(Encoding.UTF8.GetString(b))!);

        // Create the System Under Test (SUT)
        var sut = new CouchbaseFtsVectorStoreRecordCollection<CouchbaseHotelModel>(
            _mockScope.Object,
            "test-collection",
            new CouchbaseFtsVectorStoreRecordCollectionOptions<CouchbaseHotelModel>
            {
                JsonDocumentCustomMapper = mockMapper.Object
            });

        // Act
        var result = await sut.GetAsync(RecordKey);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(RecordKey, result.HotelId);
        Assert.Equal("Mapped Name", result.HotelName);

        // Verify GetAsync was called correctly
        _mockCollection.Verify(
            c => c.GetAsync(
                It.Is<string>(key => key == RecordKey),
                It.IsAny<GetOptions?>()),
            Times.Once());

        // Verify that the mapper was used
        mockMapper.Verify(
            m => m.MapFromStorageToDataModel(It.IsAny<byte[]>(), It.IsAny<StorageToDataModelMapperOptions>()),
            Times.Once());
    }

    [Fact]
    public async Task UpsertReturnsRecordKeyAsync()
    {
        // Arrange
        var hotel = new CouchbaseHotelModel("key") { HotelName = "Test Name" };

        var mockMutationResult = new Mock<IMutationResult>();
        mockMutationResult
            .SetupGet(m => m.Cas)
            .Returns(12345UL); // Simulated CAS value

        _mockCollection
            .Setup(l => l.UpsertAsync(
                It.Is<string>(key => key == "key"),
                It.Is<CouchbaseHotelModel>(doc =>
                    doc.HotelId == "key" &&
                    doc.HotelName == "Test Name"),
                It.IsAny<UpsertOptions?>()))
            .ReturnsAsync(mockMutationResult.Object) // Return the mocked result
            .Verifiable();

        _mockScope
            .Setup(s => s.Collection(It.IsAny<string>()))
            .Returns(_mockCollection.Object);

        var sut = new CouchbaseFtsVectorStoreRecordCollection<CouchbaseHotelModel>(_mockScope.Object, "test-collection");

        // Act
        var result = await sut.UpsertAsync(hotel);

        // Assert
        Assert.Equal("key", result); // Ensures that the returned key is correct
    }

    [Fact]
    public async Task UpsertBatchReturnsRecordKeysAsync()
    {
        // Arrange
        var hotel1 = new CouchbaseHotelModel("key1") { HotelName = "Test Name 1" };
        var hotel2 = new CouchbaseHotelModel("key2") { HotelName = "Test Name 2" };
        var hotel3 = new CouchbaseHotelModel("key3") { HotelName = "Test Name 3" };

        var mockMutationResult = new Mock<IMutationResult>();

        // Set up the mock for each UpsertAsync call.
        _mockCollection
            .Setup(l => l.UpsertAsync(
                "key1",
                It.Is<byte[]>(b => ValidateHotelData(b, "key1", "Test Name 1")),
                It.IsAny<UpsertOptions?>()))
            .ReturnsAsync(mockMutationResult.Object);

        _mockCollection
            .Setup(l => l.UpsertAsync(
                "key2",
                It.Is<byte[]>(b => ValidateHotelData(b, "key2", "Test Name 2")),
                It.IsAny<UpsertOptions?>()))
            .ReturnsAsync(mockMutationResult.Object);

        _mockCollection
            .Setup(l => l.UpsertAsync(
                "key3",
                It.Is<byte[]>(b => ValidateHotelData(b, "key3", "Test Name 3")),
                It.IsAny<UpsertOptions?>()))
            .ReturnsAsync(mockMutationResult.Object);

        _mockScope
            .Setup(s => s.Collection(It.IsAny<string>()))
            .Returns(_mockCollection.Object);

        // Create the System Under Test (SUT)
        var sut = new CouchbaseFtsVectorStoreRecordCollection<CouchbaseHotelModel>(
            _mockScope.Object,
            "test-collection"
        );

        // Act
        var results = await sut.UpsertBatchAsync(new[] { hotel1, hotel2, hotel3 }).ToListAsync();

        // Assert
        Assert.NotNull(results);
        Assert.Equal(3, results.Count);
        Assert.Equal("key1", results[0]);
        Assert.Equal("key2", results[1]);
        Assert.Equal("key3", results[2]);

        // Optionally, verify that UpsertAsync was called exactly once per key.
        _mockCollection.Verify(
            l => l.UpsertAsync("key1", It.IsAny<byte[]>(), It.IsAny<UpsertOptions?>()),
            Times.Once);
        _mockCollection.Verify(
            l => l.UpsertAsync("key2", It.IsAny<byte[]>(), It.IsAny<UpsertOptions?>()),
            Times.Once);
        _mockCollection.Verify(
            l => l.UpsertAsync("key3", It.IsAny<byte[]>(), It.IsAny<UpsertOptions?>()),
            Times.Once);
    }

    private bool ValidateHotelData(byte[] b, string expectedHotelId, string expectedHotelName)
    {
        using var doc = JsonDocument.Parse(b);
        return doc.RootElement.GetProperty("HotelId").GetString() == expectedHotelId &&
               doc.RootElement.GetProperty("HotelName").GetString() == expectedHotelName;
    }

    [Fact]
    public async Task UpsertWithCustomMapperWorksCorrectlyAsync()
    {
        // Arrange
        var hotel = new CouchbaseHotelModel("key") { HotelName = "Test Name" };

        // Prepare the expected storage model as a byte array.
        var storageModel = JsonSerializer.SerializeToUtf8Bytes(hotel);

        // Mock custom mapper of type IVectorStoreRecordMapper<CouchbaseHotelModel, byte[]>
        var mockMapper = new Mock<IVectorStoreRecordMapper<CouchbaseHotelModel, byte[]>>();
        mockMapper
            .Setup(m => m.MapFromDataToStorageModel(It.IsAny<CouchbaseHotelModel>()))
            .Returns(storageModel);

        // Mock MutationResult for Upsert
        var mockMutationResult = new Mock<IMutationResult>();
        mockMutationResult.SetupGet(m => m.Cas).Returns(12345UL); // Simulate CAS value

        // Mock Couchbase collection to expect a call with key "key" and a byte[] matching storageModel.
        _mockCollection
            .Setup(l => l.UpsertAsync(
                "key",
                It.Is<byte[]>(b => b.SequenceEqual(storageModel)),
                It.IsAny<UpsertOptions?>()))
            .ReturnsAsync(mockMutationResult.Object)
            .Verifiable();

        // Mock Scope to return the collection mock.
        _mockScope
            .Setup(s => s.Collection(It.IsAny<string>()))
            .Returns(_mockCollection.Object);

        // Create the System Under Test (SUT) with the custom mapper provided.
        var sut = new CouchbaseFtsVectorStoreRecordCollection<CouchbaseHotelModel>(
            _mockScope.Object,
            "test-collection",
            new CouchbaseFtsVectorStoreRecordCollectionOptions<CouchbaseHotelModel>
            {
                JsonDocumentCustomMapper = mockMapper.Object
            });

        // Act
        var result = await sut.UpsertAsync(hotel);

        // Assert
        Assert.Equal("key", result);

        // Verify the correct upsert operation was called exactly once.
        _mockCollection.Verify(
            l => l.UpsertAsync(
                "key",
                It.Is<byte[]>(b => b.SequenceEqual(storageModel)),
                It.IsAny<UpsertOptions?>()),
            Times.Once());
    }

    [Fact]
    public async Task VectorizedSearchReturnsValidHotelRecordWithScoreAsync()
    {
        // Arrange
        const string RecordKey = "key";
        const string ExpectedHotelName = "Test Name";
        const double ExpectedScore = 0.99;

        // Mock search row
        var mockSearchRow = new Mock<ISearchQueryRow>();
        mockSearchRow
            .Setup(row => row.Id)
            .Returns(RecordKey);
        mockSearchRow
            .Setup(row => row.Score)
            .Returns(ExpectedScore);
        mockSearchRow
            .Setup(row => row.Fields)
            .Returns(new Dictionary<string, object>
            {
                { "HotelId", RecordKey },
                { "HotelName", ExpectedHotelName }
            });

        // Mock search result
        var mockSearchResult = new Mock<ISearchResult>();
        mockSearchResult
            .Setup(result => result.Hits)
            .Returns(new List<ISearchQueryRow> { mockSearchRow.Object });

        // Mock JSON response as byte array
        var expectedHotel = new CouchbaseHotelModel(RecordKey) { HotelName = ExpectedHotelName };
        var jsonData = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(expectedHotel));

        // Mock GetResult for KV retrieval
        var mockGetResult = new Mock<IGetResult>();
        mockGetResult
            .Setup(result => result.ContentAs<byte[]>())
            .Returns(jsonData);

        _mockCollection
            .Setup(collection => collection.GetAsync(It.Is<string>(key => key == RecordKey), It.IsAny<GetOptions>()))
            .ReturnsAsync(mockGetResult.Object);

        // Mock scope
        _mockScope
            .Setup(scope => scope.Collection(It.IsAny<string>()))
            .Returns(_mockCollection.Object);
        _mockScope
            .Setup(scope => scope.SearchAsync(
                It.IsAny<string>(),
                It.IsAny<SearchRequest>(),
                It.IsAny<SearchOptions>()))
            .ReturnsAsync(mockSearchResult.Object);

        // Mock Mapper
        var mockMapper = new Mock<IVectorStoreRecordMapper<CouchbaseHotelModel, byte[]>>();
        mockMapper
            .Setup(m => m.MapFromStorageToDataModel(It.IsAny<byte[]>(), It.IsAny<StorageToDataModelMapperOptions>()))
            .Returns<byte[], StorageToDataModelMapperOptions>((b, _) =>
                JsonSerializer.Deserialize<CouchbaseHotelModel>(Encoding.UTF8.GetString(b))!);

        // Collection options
        var collectionOptions = new CouchbaseFtsVectorStoreRecordCollectionOptions<CouchbaseHotelModel>
        {
            IndexName = "test-index",
            JsonDocumentCustomMapper = mockMapper.Object
        };

        // System Under Test (SUT)
        var sut = new CouchbaseFtsVectorStoreRecordCollection<CouchbaseHotelModel>(
            _mockScope.Object,
            "collection",
            collectionOptions);

        // Act
        var actual = await sut.VectorizedSearchAsync(new ReadOnlyMemory<float>(new float[] { 0.123f, 0.456f, -0.789f, 0.321f }));
        var results = await actual.Results.ToListAsync();
        var result = results.FirstOrDefault();

        // Assert
        Assert.NotNull(result);
        Assert.Equal(RecordKey, result.Record.HotelId);
        Assert.Equal(ExpectedHotelName, result.Record.HotelName);
        Assert.Equal(ExpectedScore, result.Score);

        // Verify GetAsync was called correctly
        _mockCollection.Verify(
            c => c.GetAsync(It.Is<string>(key => key == RecordKey), It.IsAny<GetOptions?>()),
            Times.Once());

        // Verify that the mapper was used
        mockMapper.Verify(
            m => m.MapFromStorageToDataModel(It.IsAny<byte[]>(), It.IsAny<StorageToDataModelMapperOptions>()),
            Times.Once());
    }

    
    [Fact]
    public async Task VectorizedSearchWithUnsupportedVectorTypeThrowsExceptionAsync()
    {
        // Arrange
        var collectionOptions = new CouchbaseFtsVectorStoreRecordCollectionOptions<CouchbaseHotelModel>
        {
            IndexName = "test-index"
        };

        var sut = new CouchbaseFtsVectorStoreRecordCollection<CouchbaseHotelModel>(
            _mockScope.Object,
            "collection",
            collectionOptions);

        // Act & Assert
        await Assert.ThrowsAsync<NotSupportedException>(async () =>
            await (await sut.VectorizedSearchAsync(new List<double> { 1, 2, 3 })).Results.ToListAsync());
    }

    public static TheoryData<List<string>, string, bool> CollectionExistsData => new()
    {
        { new List<string> { "collection1", "collection2" }, "collection2", true },
        { new List<string>(), "nonexistentCollection", false }
    };
    
    public static TheoryData<List<string>, int> CreateCollectionIfNotExistsData => new()
    {
        { new List<string> { "collection" }, 0 }, // Collection exists, no creation needed
        { new List<string>(), 1 } // Collection does not exist, creation needed
    };

    private sealed class TestModel
    {
        public string? Id { get; set; }

        public string? HotelName { get; set; }
    }
}