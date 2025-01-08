using System.Text.Json;
using Xunit;

namespace Couchbase.SemanticKernel.UnitTests;

/// <summary>
/// Unit tests for <see cref="CouchbaseVectorStoreRecordMapper{TRecord}"/> class.
/// </summary>
public sealed class CouchbaseVectorStoreRecordMapperTests
{
    private readonly CouchbaseVectorStoreRecordMapper<CouchbaseHotelModel> _sut;

    public CouchbaseVectorStoreRecordMapperTests()
    {
        this._sut = new(JsonSerializerOptions.Default);
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
            DescriptionEmbedding = new float[] { 1f, 2f, 3f, 4f }
        };

        // Act
        var mappedHotel = this._sut.MapFromDataToStorageModel(hotel);

        // Assert
        Assert.NotNull(mappedHotel);
        Assert.Equal("key", mappedHotel.HotelId);
        Assert.Equal("Test Name", mappedHotel.HotelName);
        Assert.Equal(123, mappedHotel.HotelCode);
        Assert.Equal(4.5f, mappedHotel.HotelRating);
        Assert.True(mappedHotel.ParkingIncluded);
        Assert.Equal(new List<string> { "tag1", "tag2" }, mappedHotel.Tags);
        Assert.Equal("A beautiful hotel near the mountains", mappedHotel.Description);
        Assert.True(hotel.DescriptionEmbedding.SequenceEqual(mappedHotel.DescriptionEmbedding));
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
            DescriptionEmbedding = new float[] { 1f, 2f, 3f, 4f }
        };

        // Act
        var mappedHotel = this._sut.MapFromStorageToDataModel(document, new());

        // Assert
        Assert.NotNull(mappedHotel);
        Assert.Equal("key", mappedHotel.HotelId);
        Assert.Equal("Test Name", mappedHotel.HotelName);
        Assert.Equal(123, mappedHotel.HotelCode);
        Assert.Equal(4.5f, mappedHotel.HotelRating);
        Assert.True(mappedHotel.ParkingIncluded);
        Assert.Equal(new List<string> { "tag1", "tag2" }, mappedHotel.Tags);
        Assert.Equal("A beautiful hotel near the mountains", mappedHotel.Description);
        Assert.Null(mappedHotel.DescriptionEmbedding);
    }
}