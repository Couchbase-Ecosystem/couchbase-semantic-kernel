using System.Text.Json;
using System.Text.Json.Nodes;

namespace Couchbase.SemanticKernel.Connectors.Couchbase.UnitTests;

/// <summary>
/// Unit tests for <see cref="CouchbaseVectorStoreRecordMapper{TRecord}"/> class.
/// </summary>
public sealed class CouchbaseVectorStoreRecordMapperTests
{
    private readonly CouchbaseVectorStoreRecordMapper<CouchbaseHotelModel> _sut;

    public CouchbaseVectorStoreRecordMapperTests()
    {
        var storagePropertyNames = new Dictionary<string, string>
        {
            ["HotelId"] = "HotelId",
            ["HotelName"] = "HotelName",
            ["Tags"] = "Tags",
            ["DescriptionEmbedding"] = "description_embedding",
        };

        this._sut = new( storagePropertyNames, JsonSerializerOptions.Default);
    }

    [Fact]
    public void MapFromDataToStorageModelReturnsValidObject()
    {
        // Arrange
        var hotel = new CouchbaseHotelModel("key")
        {
            HotelName = "Test Name",
            Tags = new List<string> { "tag1", "tag2" },
            DescriptionEmbedding = new ReadOnlyMemory<float>(new float[] { 1f, 2f, 3f })
        };

        // Act
        var document = this._sut.MapFromDataToStorageModel(hotel);

        // Assert
        Assert.NotNull(document);

        Assert.Equal("key", document["HotelId"]!.GetValue<string>());
        Assert.Equal("Test Name", document["HotelName"]!.GetValue<string>());
        Assert.Equal(new List<string> { "tag1", "tag2" }, document["Tags"]!.AsArray().Select(l => l!.GetValue<string>()));
        Assert.Equal(new List<float> { 1f, 2f, 3f }, document["description_embedding"]!.AsArray().Select(l => l!.GetValue<float>()));
    }

    [Fact]
    public void MapFromStorageToDataModelReturnsValidObject()
    {
        // Arrange
        var document = new JsonObject
        {
            ["HotelId"] = "key",
            ["HotelName"] = "Test Name",
            ["Tags"] = new JsonArray(new List<string> { "tag1", "tag2" }.Select(l => JsonValue.Create(l)).ToArray()),
            ["description_embedding"] = new JsonArray(new List<float> { 1f, 2f, 3f }.Select(l => JsonValue.Create(l)).ToArray()),
        };

        // Act
        var hotel = this._sut.MapFromStorageToDataModel(document, new());

        // Assert
        Assert.NotNull(hotel);

        Assert.Equal("key", hotel.HotelId);
        Assert.Equal("Test Name", hotel.HotelName);
        Assert.Equal(new List<string> { "tag1", "tag2" }, hotel.Tags);
        Assert.True(new ReadOnlyMemory<float>(new float[] { 1f, 2f, 3f }).Span.SequenceEqual(hotel.DescriptionEmbedding!.Value.Span));
    }
}