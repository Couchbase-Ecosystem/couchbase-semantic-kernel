﻿using System.Text.Json.Serialization;
using Couchbase.SemanticKernel.Connectors.Couchbase;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.VectorData;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Data;
using Microsoft.SemanticKernel.Embeddings;
using Microsoft.SemanticKernel.PromptTemplates.Handlebars;

namespace Couchbase.SemanticKernel.Playground;

#pragma warning disable CA2007 // Consider calling ConfigureAwait on the awaited task
#pragma warning disable SKEXP0001 // Some SK methods are still experimental

internal sealed class Program
{
    public static async Task Main(string[] args)
    {
        
        #pragma warning disable SKEXP0010 // Some SK methods are still experimental

        var builder = Host.CreateApplicationBuilder(args);
        
        // Register AI services.
        var kernelBuilder = builder.Services.AddKernel();
        kernelBuilder.AddAzureOpenAIChatCompletion("gpt-4o", "https://my-service.openai.azure.com", "my_token");
        kernelBuilder.AddAzureOpenAITextEmbeddingGeneration("ada-002", "https://my-service.openai.azure.com", "my_token");

        // Register text search service.
        kernelBuilder.AddVectorStoreTextSearch<Hotel>();
        
        // Register Couchbase Vector Store using provided extensions.
        builder.Services.AddCouchbaseVectorStoreRecordCollection<Hotel>(
            connectionString: "<your-couchbase-connection-string>",
            username: "<your-username>",
            password: "<your-password>",
            bucketName: "<your-bucket-name>",
            scopeName: "<your-scope-name>",
            collectionName: "<your-collection-name>",
            options: new CouchbaseVectorStoreRecordCollectionOptions<Hotel>
            {
                IndexName = "<your-index-name>"
            });

        // Build the host.
        using var host = builder.Build();
        
        // For demo purposes, we access the services directly without using a DI context.
        var kernel = host.Services.GetService<Kernel>()!;
        var embeddings = host.Services.GetService<ITextEmbeddingGenerationService>()!;
        var vectorStoreCollection = host.Services.GetService<IVectorStoreRecordCollection<string, Hotel>>()!;
        
        // Register search plugin.
        var textSearch = host.Services.GetService<VectorStoreTextSearch<Hotel>>()!;
        
        kernel.Plugins.Add(textSearch.CreateWithGetTextSearchResults("SearchPlugin"));
        
        // Crate collection and ingest a few demo records.
        await vectorStoreCollection.CreateCollectionIfNotExistsAsync();
        
        //CSV format: ID;Hotel Name;Description;Reference Link
        var hotels = (await File.ReadAllLinesAsync("hotels.csv"))
            .Select(x => x.Split(';'));
        
        foreach (var chunk in hotels.Chunk(25))
        {
            var descriptionEmbeddings = await embeddings.GenerateEmbeddingsAsync(chunk.Select(x => x[2]).ToArray());
            
            for (var i = 0; i < chunk.Length; ++i)
            {
                var hotel = chunk[i];
                await vectorStoreCollection.UpsertAsync(new Hotel
                {
                    HotelName = hotel[1],
                    HotelId = hotel[0],
                    Description = hotel[2],
                    DescriptionEmbedding = descriptionEmbeddings[i].ToArray(),
                    ReferenceLink = hotel[3]
                });
            }
        }
        
        // Invoke the LLM with a template that uses the search plugin to
        // 1. get related information to the user query from the vector store
        // 2. add the information to the LLM prompt.
        var response = await kernel.InvokePromptAsync(
            promptTemplate: """
                            Please use this information to answer the question:
                            {{#with (SearchPlugin-GetTextSearchResults question)}}
                              {{#each this}}
                                Name: {{Name}}
                                Value: {{Value}}
                                Source: {{Link}}
                                -----------------
                              {{/each}}
                            {{/with}}

                            Include the source of relevant information in the response.

                            Question: {{question}}
                            """,
            arguments: new KernelArguments
            {
                { "question", "Please show me all hotels that have a rooftop bar." },
            },
            templateFormat: "handlebars",
            promptTemplateFactory: new HandlebarsPromptTemplateFactory());

        Console.WriteLine(response.ToString());
    }
}

/// <summary>
/// Data model for storing a "hotel" with a name, a description, a  description embedding and an optional reference link.
/// </summary>
public sealed record Hotel
{
    [VectorStoreRecordKey]
    [JsonPropertyName("hotelId")]
    public required string HotelId { get; set; }

    [TextSearchResultName]
    [VectorStoreRecordData(IsFilterable = true)]
    [JsonPropertyName("hotelName")]
    public required string HotelName { get; set; }

    [TextSearchResultValue]
    [VectorStoreRecordData(IsFullTextSearchable = true)]
    [JsonPropertyName("description")]
    public required string Description { get; set; }

    [VectorStoreRecordVector(Dimensions: 1536, DistanceFunction.DotProductSimilarity)]
    [JsonPropertyName("descriptionEmbedding")]
    public float[] DescriptionEmbedding { get; set; }

    [TextSearchResultLink]
    [VectorStoreRecordData]
    [JsonPropertyName("referenceLink")]
    public string? ReferenceLink { get; set; }
}