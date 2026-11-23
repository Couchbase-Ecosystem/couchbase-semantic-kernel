using System.Text;
using Couchbase.VectorData;
using Microsoft.Extensions.VectorData;
using Microsoft.Extensions.VectorData.ProviderServices;
using Newtonsoft.Json.Linq;
using Xunit;

namespace Couchbase.ConformanceTests.UnitTests;

/// <summary>
/// Cluster-free tests for the SQL++ SELECT list / result-row contract used by CouchbaseQueryCollection.
/// </summary>
public class CouchbaseQueryRowMappingTests
{
    private static readonly CollectionModel s_model = new CouchbaseModelBuilder().BuildDynamic(
        new VectorStoreCollectionDefinition
        {
            Properties =
            [
                new VectorStoreKeyProperty("Key", typeof(string)),
                new VectorStoreDataProperty("Number", typeof(int)),
                new VectorStoreDataProperty("Text", typeof(string)),
                new VectorStoreVectorProperty("Embedding", typeof(ReadOnlyMemory<float>), 3),
            ],
        },
        defaultEmbeddingGenerator: null);

    [Fact]
    public void BuildSelectList_selects_key_alias_and_escaped_data_fields()
        => Assert.Equal(
            "META().id AS _id, `Number`, `Text`",
            CouchbaseQueryRowMapping.BuildSelectList(s_model, includeVectors: false));

    [Fact]
    public void BuildSelectList_adds_escaped_vector_fields_when_requested()
        => Assert.Equal(
            "META().id AS _id, `Number`, `Text`, `Embedding`",
            CouchbaseQueryRowMapping.BuildSelectList(s_model, includeVectors: true));

    [Fact]
    public void ToStorageDocument_strips_metadata_aliases_and_restores_key()
    {
        var row = JObject.Parse("""{"_id":"doc-1","_distance":0.25,"Number":7,"Text":"hello"}""");

        var bytes = CouchbaseQueryRowMapping.ToStorageDocument(row, "Key");

        Assert.NotNull(bytes);
        Assert.True(JToken.DeepEquals(
            JObject.Parse("""{"Number":7,"Text":"hello","Key":"doc-1"}"""),
            JObject.Parse(Encoding.UTF8.GetString(bytes!))));
    }

    [Fact]
    public void ToStorageDocument_round_trips_through_the_record_mapper()
    {
        var model = new CouchbaseModelBuilder().Build(typeof(Hotel), typeof(string), definition: null, defaultEmbeddingGenerator: null);
        var row = JObject.Parse("""{"_id":"doc-1","_distance":0.25,"Number":7,"Text":"hello","Embedding":[1,2,3]}""");

        var hotel = new CouchbaseMapper<string, Hotel>(model).MapFromStorageToDataModel(
            CouchbaseQueryRowMapping.ToStorageDocument(row, model.KeyProperty.StorageName), includeVectors: true);

        Assert.Equal("doc-1", hotel.Key);
        Assert.Equal(7, hotel.Number);
        Assert.Equal("hello", hotel.Text);
        Assert.Equal(new float[] { 1, 2, 3 }, hotel.Embedding.ToArray());
    }

    public sealed class Hotel
    {
        [VectorStoreKey]
        public string Key { get; set; } = string.Empty;

        [VectorStoreData]
        public int Number { get; set; }

        [VectorStoreData]
        public string Text { get; set; } = string.Empty;

        [VectorStoreVector(3)]
        public ReadOnlyMemory<float> Embedding { get; set; }
    }

    [Theory]
    [InlineData("""{"Number":7}""")]
    [InlineData("""{"_id":null,"Number":7}""")]
    [InlineData("""{"_id":"","Number":7}""")]
    public void ToStorageDocument_returns_null_when_row_has_no_key(string json)
        => Assert.Null(CouchbaseQueryRowMapping.ToStorageDocument(JObject.Parse(json), "Key"));

    [Fact]
    public void AsJObject_rejects_non_object_rows()
        => Assert.Throws<InvalidOperationException>(() => CouchbaseQueryRowMapping.AsJObject(new JArray()));
}
