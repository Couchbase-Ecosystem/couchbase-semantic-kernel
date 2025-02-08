using System.Text.Json.Serialization;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.VectorData;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Data;
using Microsoft.SemanticKernel.Embeddings;

namespace Couchbase.SemanticKernel.Playground;

#pragma warning disable CA2007 // Consider calling ConfigureAwait on the awaited task
#pragma warning disable SKEXP0001 // Some SK methods are still experimental

internal sealed class Program
{
    public static async Task Main(string[] args)
    {
        #pragma warning disable SKEXP0010 // Some SK methods are still experimental

        var builder = Host.CreateApplicationBuilder(args);
        
        // Add configuration from appsettings.json
        var couchbaseConfig = builder.Configuration.GetSection("Couchbase");

        // 1. Register AI and Kernel services
        var kernelBuilder = builder.Services.AddKernel();
        kernelBuilder.AddAzureOpenAIChatCompletion("gpt-4o", "https://my-service.openai.azure.com", "my_token");
        kernelBuilder.AddAzureOpenAITextEmbeddingGeneration("ada-002", "https://my-service.openai.azure.com", "my_token");

        // 2. Register text search and Couchbase Vector Store
        builder.Services.AddCouchbaseFtsVectorStoreRecordCollection<Hotel>(
            connectionString: couchbaseConfig["ConnectionString"],
            username: couchbaseConfig["Username"],
            password: couchbaseConfig["Password"],
            bucketName: couchbaseConfig["BucketName"],
            scopeName: couchbaseConfig["ScopeName"],
            collectionName: couchbaseConfig["CollectionName"],
            options: new CouchbaseFtsVectorStoreRecordCollectionOptions<Hotel>
            {
                IndexName = couchbaseConfig["IndexName"]
            });

        // 3. Build the host
        using var host = builder.Build();

        // 4. Resolve services
        var kernel = host.Services.GetRequiredService<Kernel>();
        var embeddings = host.Services.GetRequiredService<ITextEmbeddingGenerationService>();
        var vectorStoreCollection = host.Services.GetRequiredService<IVectorStoreRecordCollection<string, Hotel>>();

        // 5. Ensure the collection exists (create if needed)
        await vectorStoreCollection.CreateCollectionIfNotExistsAsync();

        // Dynamically calculate the file path for the CSV file
        var projectRoot = Directory.GetParent(AppContext.BaseDirectory)?.Parent?.Parent?.Parent?.FullName 
                          ?? throw new InvalidOperationException("Unable to determine the project root directory.");

        var csvFilePath = Path.Combine(projectRoot, "hotels.csv");

        if (!File.Exists(csvFilePath))
        {
            throw new FileNotFoundException($"The file 'hotels.csv' was not found at the path: {csvFilePath}");
        }

        Console.WriteLine($"Ingesting data from '{csvFilePath}'...");
        await IngestCsvAsync(embeddings, vectorStoreCollection, csvFilePath);
        Console.WriteLine("Data ingestion complete.");

        // 6. Prepare a question and embed it (vector) using your embedding service
        const string userQuestion = "Which hotels are great for nature lovers with hiking trails?";
        var embeddingResult = await embeddings.GenerateEmbeddingsAsync(new[] { userQuestion });
        var userEmbedding = embeddingResult[0];

        // 7. Customize your VectorSearchOptions
        var mySearchOptions = new VectorSearchOptions
        {
            Top = 5,
            Skip = 0,
            Filter = new VectorSearchFilter().EqualTo("HotelName", "Mountain View Inn")
        };

        // 8. Call VectorizedSearchAsync
        var vectorSearchResults = await vectorStoreCollection.VectorizedSearchAsync(
            userEmbedding,
            mySearchOptions
        );

        // 9. Display the results
        Console.WriteLine("Vector search matches:");
        await foreach (var result in vectorSearchResults.Results)
        {
            Console.WriteLine(
                $"Hotel ID: {result.Record.HotelId}, Name: {result.Record.HotelName}, Score: {result.Score}"
            );
        }
    }

    private static async Task IngestCsvAsync(
        ITextEmbeddingGenerationService embeddings,
        IVectorStoreRecordCollection<string, Hotel> vectorStoreCollection,
        string csvFilePath)
    {
        // If the file doesn't exist, this will throw an exception.
        if (!File.Exists(csvFilePath))
        {
            throw new FileNotFoundException($"File '{csvFilePath}' does not exist. Cannot ingest data.");
        }

        // CSV format: ID;HotelName;Description;ReferenceLink
        var lines = await File.ReadAllLinesAsync(csvFilePath);
        var splits = lines.Select(x => x.Split(';'));
        
        foreach (var chunk in splits.Chunk(25))
        {
            var textsToEmbed = chunk.Select(c => c[2]).ToArray();
            var embedResults = await embeddings.GenerateEmbeddingsAsync(textsToEmbed);

            for (var i = 0; i < chunk.Length; i++)
            {
                var fields = chunk[i];
                var doc = new Hotel
                {
                    HotelId = fields[0],
                    HotelName = fields[1],
                    Description = fields[2],
                    DescriptionEmbedding = embedResults[i],
                    ReferenceLink = fields[3]
                };
                await vectorStoreCollection.UpsertAsync(doc);
            }
        }
    }
}

public sealed record Hotel
{
    [VectorStoreRecordKey]
    [JsonPropertyName("hotelId")]
    public string HotelId { get; set; }

    [TextSearchResultName]
    [VectorStoreRecordData]
    [JsonPropertyName("hotelName")]
    public string HotelName { get; set; }

    [TextSearchResultValue]
    [VectorStoreRecordData]
    [JsonPropertyName("description")]
    public string Description { get; set; }

    [VectorStoreRecordVector(Dimensions: 1536, DistanceFunction.DotProductSimilarity)]
    [JsonPropertyName("descriptionEmbedding")]
    public ReadOnlyMemory<float> DescriptionEmbedding { get; set; }

    [TextSearchResultLink]
    [VectorStoreRecordData]
    [JsonPropertyName("referenceLink")]
    public string? ReferenceLink { get; set; }
}