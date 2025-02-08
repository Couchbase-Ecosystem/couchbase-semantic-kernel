using System.Text.Json;
using Couchbase.SemanticKernel.Data;
using Microsoft.Extensions.VectorData;

namespace Couchbase.SemanticKernel.UnitTests;

/// <summary>
/// Unit tests for <see cref="CouchbaseFtsVectorStoreRecordMapper{TRecord}"/> class.
/// </summary>
public sealed class CouchbaseVectorStoreRecordMapperTests
{
    private readonly CouchbaseFtsVectorStoreRecordMapper<CouchbaseHotelModel> _sut;
    private readonly JsonSerializerOptions _jsonSerializerOptions;
    private readonly VectorStoreRecordPropertyReader _propertyReader;

    public CouchbaseVectorStoreRecordMapperTests()
    {
        _jsonSerializerOptions = new JsonSerializerOptions(JsonSerializerDefaults.General);
        _propertyReader = new VectorStoreRecordPropertyReader(typeof(CouchbaseHotelModel), null, null);
        _sut = new CouchbaseFtsVectorStoreRecordMapper<CouchbaseHotelModel>(_jsonSerializerOptions, _propertyReader);
    }

    [Fact]
    public void MapFromDataToStorageModelReturnsValidObject()
    {
        // Arrange
        var hotel = new CouchbaseHotelModel("key")
        {
            HotelName = "Test Name",
            HotelCode = 123,
            HotelRating = 4.5f,
            ParkingIncluded = true,
            Tags = new List<string> { "tag1", "tag2" },
            Description = "A beautiful hotel near the mountains",
            DescriptionEmbedding = new ReadOnlyMemory<float>(new float[] { 1f, 2f, 3f, 4f })
        };

        // Act
        var mappedBytes = _sut.MapFromDataToStorageModel(hotel);
        var mappedHotel = JsonSerializer.Deserialize<JsonElement>(mappedBytes, _jsonSerializerOptions);

        // Assert
        Assert.NotNull(mappedHotel);
        Assert.Equal("key", mappedHotel.GetProperty("HotelId").GetString());
        Assert.Equal("Test Name", mappedHotel.GetProperty("HotelName").GetString());
        Assert.Equal(123, mappedHotel.GetProperty("HotelCode").GetInt32());
        Assert.Equal(4.5f, mappedHotel.GetProperty("HotelRating").GetSingle());
        Assert.True(mappedHotel.GetProperty("ParkingIncluded").GetBoolean());
        Assert.Equal(new List<string> { "tag1", "tag2" }, mappedHotel.GetProperty("Tags").EnumerateArray().Select(e => e.GetString()).ToList());
        Assert.Equal("A beautiful hotel near the mountains", mappedHotel.GetProperty("Description").GetString());
        Assert.Equal(new float[] { 1f, 2f, 3f, 4f }, mappedHotel.GetProperty("description_embedding").EnumerateArray().Select(e => e.GetSingle()).ToArray());
    }

    [Fact]
    public void MapFromStorageToDataModelReturnsValidObject()
    {
        // Arrange
        var document = new CouchbaseHotelModel("key")
        {
            HotelName = "Test Name",
            HotelCode = 123,
            HotelRating = 4.5f,
            ParkingIncluded = true,
            Tags = new List<string> { "tag1", "tag2" },
            Description = "A beautiful hotel near the mountains",
            DescriptionEmbedding = new ReadOnlyMemory<float>(new float[] { 1f, 2f, 3f, 4f })
        };

        var storageBytes = _sut.MapFromDataToStorageModel(document);

        // Act
        var mappedHotel = _sut.MapFromStorageToDataModel(storageBytes, new StorageToDataModelMapperOptions { IncludeVectors = true });

        // Assert
        Assert.NotNull(mappedHotel);
        Assert.Equal("key", mappedHotel.HotelId);
        Assert.Equal("Test Name", mappedHotel.HotelName);
        Assert.Equal(123, mappedHotel.HotelCode);
        Assert.Equal(4.5f, mappedHotel.HotelRating);
        Assert.True(mappedHotel.ParkingIncluded);
        Assert.Equal(new List<string> { "tag1", "tag2" }, mappedHotel.Tags);
        Assert.Equal("A beautiful hotel near the mountains", mappedHotel.Description);
        Assert.NotNull(mappedHotel.DescriptionEmbedding);
        Assert.Equal(new float[] { 1f, 2f, 3f, 4f }, mappedHotel.DescriptionEmbedding.ToArray());
    }
}
