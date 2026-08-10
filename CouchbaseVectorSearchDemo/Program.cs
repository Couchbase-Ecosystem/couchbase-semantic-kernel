using System.Reflection;
using Couchbase;
using Couchbase.VectorData;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.VectorData;
using OpenAI;

namespace CouchbaseVectorSearchDemo;

/// <summary>
/// Couchbase Vector Search Demo
///
/// This example demonstrates how to use CouchbaseQueryCollection with Hyperscale index for vector search.
/// </summary>
public abstract class Program
{
    private const string IndexName = "hyperscale_glossary_index";

    private static IConfigurationRoot? _configuration;
    private static IEmbeddingGenerator<string, Embedding<float>>? _embeddingGenerator;
    private static ICluster? _cluster;
    private static CouchbaseQueryCollection<string, Glossary>? _collection;

    private static IEmbeddingGenerator<string, Embedding<float>> EmbeddingGenerator
        => _embeddingGenerator ?? throw new InvalidOperationException("Embedding generator not initialized.");

    private static CouchbaseQueryCollection<string, Glossary> Collection
        => _collection ?? throw new InvalidOperationException("Not connected to Couchbase.");

    public static async Task Main(string[] args)
    {
        Console.WriteLine("Couchbase Hyperscale Vector Search Demo");
        Console.WriteLine("====================================");

        try
        {
            // Setup configuration and services
            SetupConfiguration();
            SetupEmbeddingGenerator();
            await ConnectToCouchbaseAsync();

            // Step 1: Ingest data into Couchbase vector store
            await IngestDataIntoCouchbaseVectorStoreAsync();

            // Step 2: Create Hyperscale index manually
            await CreateHyperscaleIndexAsync();

            // Step 3: Perform vector search
            await SearchCouchbaseVectorStoreAsync();

            // Step 4: Perform filtered vector search
            await SearchCouchbaseVectorStoreWithFilteringAsync();

            Console.WriteLine("\n Demo completed successfully!");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"\n Full error: {ex}");
        }
        finally
        {
            if (_cluster is not null)
            {
                await _cluster.DisposeAsync();
            }

            Console.WriteLine("\nPress any key to exit...");
            Console.ReadKey();
        }
    }

    /// <summary>
    /// Connects to the Couchbase cluster and creates the vector store collection, once, for reuse across all steps.
    /// </summary>
    private static async Task ConnectToCouchbaseAsync()
    {
        var connectionString = _configuration?["Couchbase:ConnectionString"];
        var username = _configuration?["Couchbase:Username"];
        var password = _configuration?["Couchbase:Password"];
        var bucketName = _configuration?["Couchbase:BucketName"];
        var scopeName = _configuration?["Couchbase:ScopeName"];
        var collectionName = _configuration?["Couchbase:CollectionName"];

        if (string.IsNullOrEmpty(connectionString) || string.IsNullOrEmpty(username) || string.IsNullOrEmpty(password) ||
            string.IsNullOrEmpty(bucketName) || string.IsNullOrEmpty(scopeName) || string.IsNullOrEmpty(collectionName))
        {
            throw new InvalidOperationException(
                "Couchbase configuration missing. Please set ConnectionString, Username, Password, BucketName, ScopeName, CollectionName in appsettings.json or user secrets.");
        }

        _cluster = await Cluster.ConnectAsync(connectionString, username, password);

        var bucket = await _cluster.BucketAsync(bucketName);
        var scope = await bucket.ScopeAsync(scopeName);

        var collectionOptions = new CouchbaseQueryCollectionOptions
        {
            IndexName = IndexName,
            SimilarityMetric = "cosine"
        };

        var vectorStore = new CouchbaseVectorStore(scope);
        _collection = (CouchbaseQueryCollection<string, Glossary>)vectorStore.GetCollection<string, Glossary>(collectionName, collectionOptions);
    }

    /// <summary>
    /// Setup configuration from appsettings.json, user secrets, and environment variables.
    /// </summary>
    private static void SetupConfiguration()
    {
        _configuration = new ConfigurationBuilder()
            .AddJsonFile("appsettings.Development.json", optional: true, reloadOnChange: true)
            .AddEnvironmentVariables()
            .AddUserSecrets(Assembly.GetExecutingAssembly())
            .Build();
    }

    /// <summary>
    /// Setup OpenAI embedding generator.
    /// </summary>
    private static void SetupEmbeddingGenerator()
    {
        var openAiApiKey = _configuration?["OpenAI:ApiKey"];
        var openAiModel = _configuration?["OpenAI:EmbeddingModel"] ?? "text-embedding-ada-002";

        if (string.IsNullOrEmpty(openAiApiKey))
        {
            throw new InvalidOperationException(
                "OpenAI API key missing. Please set OpenAI:ApiKey in appsettings.json or user secrets. ");
        }

        _embeddingGenerator = new OpenAIClient(openAiApiKey)
            .GetEmbeddingClient(openAiModel)
            .AsIEmbeddingGenerator();

        Console.WriteLine($"Using OpenAI model: {openAiModel}");
    }

    /// <summary>
    /// Ingest data into Couchbase vector store
    /// </summary>
    private static async Task IngestDataIntoCouchbaseVectorStoreAsync()
    {
        Console.WriteLine("Step 1: Ingesting data into Couchbase vector store...");

        var glossaryEntries = CreateGlossaryEntries().ToList();

        // Generate all embeddings in a single batched request.
        var embeddings = await EmbeddingGenerator.GenerateAsync(glossaryEntries.Select(entry => entry.Definition));
        for (var i = 0; i < glossaryEntries.Count; i++)
        {
            glossaryEntries[i].DefinitionEmbedding = embeddings[i].Vector;
        }

        await Collection.UpsertAsync(glossaryEntries);

        Console.WriteLine("Data ingestion completed");
    }

    /// <summary>
    /// Create Hyperscale vector index after documents are inserted.
    /// </summary>
    private static async Task CreateHyperscaleIndexAsync()
    {
        Console.WriteLine("\nStep 2: Creating Hyperscale vector index...");

        // The connector builds and runs the CREATE VECTOR INDEX statement, deriving the dimensions,
        // similarity metric and INCLUDE fields from the record model and collection options.
        try
        {
            Console.WriteLine("Executing Hyperscale index creation query...");
            await Collection.EnsureVectorIndexExistsAsync();
            Console.WriteLine($"Hyperscale vector index '{IndexName}' is ready.");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Failed to create Hyperscale vector index: {ex.Message}");
            throw;
        }
    }

    /// <summary>
    /// Perform basic vector search.
    /// </summary>
    private static async Task SearchCouchbaseVectorStoreAsync()
    {
        Console.WriteLine("\nStep 3: Performing vector search...");

        var searchVector = (await EmbeddingGenerator.GenerateAsync("What is an Application Programming Interface?")).Vector;

        var searchResultItem = (await Collection.SearchAsync(searchVector, top: 1).ToListAsync()).FirstOrDefault();

        Console.WriteLine($"   Found: {searchResultItem?.Record.Term}");
        Console.WriteLine($"   Definition: {searchResultItem?.Record.Definition}");
        Console.WriteLine($"   Score: {searchResultItem?.Score:F4}");
    }

    /// <summary>
    /// Perform filtered vector search.
    /// </summary>
    private static async Task SearchCouchbaseVectorStoreWithFilteringAsync()
    {
        Console.WriteLine("\nStep 4: Performing filtered vector search...");

        var searchVector = (await EmbeddingGenerator.GenerateAsync("How do I provide additional context to an LLM?")).Vector;

        // Search the store with a filter and get the single most relevant result
        var searchResultItems = await Collection.SearchAsync(
            searchVector,
            top: 1,
            new VectorSearchOptions<Glossary>
            {
                Filter = g => g.Category == "AI"
            }).ToListAsync();

        if (searchResultItems.Count != 0)
        {
            var result = searchResultItems.FirstOrDefault();
            Console.WriteLine($"   Found (AI category only): {result?.Record.Term}");
            Console.WriteLine($"   Definition: {result?.Record.Definition}");
            Console.WriteLine($"   Score: {result?.Score:F4}");
        }
        else
        {
            Console.WriteLine("No results found with AI category filter");
        }
    }

    /// <summary>
    /// Create some sample glossary entries.
    /// </summary>
    /// <returns>A list of sample glossary entries.</returns>
    private static IEnumerable<Glossary> CreateGlossaryEntries()
    {
        yield return new Glossary
        {
            Key = "1",
            Category = "Software",
            Term = "API",
            Definition = "Application Programming Interface. A set of rules and specifications that allow software components to communicate and exchange data."
        };

        yield return new Glossary
        {
            Key = "2",
            Category = "Software",
            Term = "SDK",
            Definition = "Software development kit. A set of libraries and tools that allow software developers to build software more easily."
        };

        yield return new Glossary
        {
            Key = "3",
            Category = "SK",
            Term = "Connectors",
            Definition = "Semantic Kernel Connectors allow software developers to integrate with various services providing AI capabilities, including LLM, AudioToText, TextToAudio, Embedding generation, etc."
        };

        yield return new Glossary
        {
            Key = "4",
            Category = "SK",
            Term = "Semantic Kernel",
            Definition = "Semantic Kernel is a set of libraries that allow software developers to more easily develop applications that make use of AI experiences."
        };

        yield return new Glossary
        {
            Key = "5",
            Category = "AI",
            Term = "RAG",
            Definition = "Retrieval Augmented Generation - a term that refers to the process of retrieving additional data to provide as context to an LLM to use when generating a response (completion) to a user's question (prompt)."
        };

        yield return new Glossary
        {
            Key = "6",
            Category = "AI",
            Term = "LLM",
            Definition = "Large language model. A type of artificial intelligence algorithm that is designed to understand and generate human language."
        };
    }
}
