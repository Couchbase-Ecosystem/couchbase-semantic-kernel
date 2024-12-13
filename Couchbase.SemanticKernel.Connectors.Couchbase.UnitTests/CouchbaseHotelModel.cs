using System.Text.Json.Serialization;
using Microsoft.Extensions.VectorData;

namespace Couchbase.SemanticKernel.Connectors.Couchbase.UnitTests;

/// <summary>
/// Represents a hotel model used in Couchbase vector search.
/// </summary>
public class CouchbaseHotelModel
{
    /// <summary>
    /// Initializes a new instance of the <see cref="CouchbaseHotelModel"/> class.
    /// </summary>
    /// <param name="hotelId">The unique hotel identifier.</param>
    public CouchbaseHotelModel(string hotelId)
    {
        this.HotelId = hotelId;
    }

    /// <summary>
    /// The unique identifier of the hotel record.
    /// </summary>
    [VectorStoreRecordKey]
    public string HotelId { get; init; }

    /// <summary>
    /// The name of the hotel.
    /// </summary>
    [VectorStoreRecordData(IsFilterable = true)]
    public string? HotelName { get; set; }

    /// <summary>
    /// The hotel code as an integer.
    /// </summary>
    [VectorStoreRecordData]
    public int HotelCode { get; set; }

    /// <summary>
    /// The rating of the hotel as a float.
    /// </summary>
    [VectorStoreRecordData]
    public float? HotelRating { get; set; }

    /// <summary>
    /// Indicates whether parking is included.
    /// </summary>
    [VectorStoreRecordData(StoragePropertyName = "parking_is_included")]
    public bool ParkingIncluded { get; set; }

    /// <summary>
    /// A list of tags associated with the hotel.
    /// </summary>
    [VectorStoreRecordData]
    public List<string> Tags { get; set; } = new();

    /// <summary>
    /// A brief description of the hotel.
    /// </summary>
    [VectorStoreRecordData]
    public string? Description { get; set; }

    /// <summary>
    /// The vector representation of the description for vector search.
    /// </summary>
    [JsonPropertyName("description_embedding")]
    [VectorStoreRecordVector(Dimensions: 4, DistanceFunction: DistanceFunction.CosineSimilarity, IndexKind: IndexKind.Flat)]
    public ReadOnlyMemory<float>? DescriptionEmbedding { get; set; }
}
