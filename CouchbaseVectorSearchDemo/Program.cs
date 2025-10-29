using System.Reflection;
using Couchbase;
using Couchbase.SemanticKernel;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.VectorData;
using OpenAI;
using OpenAI.Embeddings;
using EmbeddingGenerationOptions = Microsoft.Extensions.AI.EmbeddingGenerationOptions;

namespace CouchbaseVectorSearchDemo;

/// <summary>
/// Couchbase Vector Search Demo
/// 
/// This example demonstrates how to use CouchbaseQueryCollection with BHIVE index for vector search.
/// </summary>
public abstract class Program
{
    private static IConfigurationRoot? _configuration;
    private static IEmbeddingGenerator<string, Embedding<float>>? _embeddingGenerator;

    public static async Task Main(string[] args)
    {
        Console.WriteLine("Couchbase BHIVE Vector Search Demo");
        Console.WriteLine("====================================");

        try
        {
            // Setup configuration and services
            SetupConfiguration();
            SetupEmbeddingGenerator();

            // Step 1: Ingest data into Couchbase vector store
            await IngestDataIntoCouchbaseVectorStoreAsync();

            // Step 2: Create BHIVE index manually
            await CreateBhiveIndexAsync();

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
            Console.WriteLine("\nPress any key to exit...");
            Console.ReadKey();
        }
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

        // Use OpenAI client to create the embedding generator
        var embeddingClient = new OpenAIClient(openAiApiKey).GetEmbeddingClient(openAiModel);
        _embeddingGenerator = new OpenAiEmbeddingGeneratorWrapper(embeddingClient);
        Console.WriteLine($"Using OpenAI model: {openAiModel}");
    }

    /// <summary>
    /// Ingest data into Couchbase vector store
    /// </summary>
    private static async Task IngestDataIntoCouchbaseVectorStoreAsync()
    {
        Console.WriteLine("Step 1: Ingesting data into Couchbase vector store...");

        // Get Couchbase vector store collection
        var collection = await GetCouchbaseVectorStoreCollectionAsync();

        // Ingest data 
        await IngestDataIntoVectorStoreAsync(collection, _embeddingGenerator!);

        Console.WriteLine("Data ingestion completed");
    }

    /// <summary>
    /// Create BHIVE vector index manually after documents are inserted.
    /// </summary>
    private static async Task CreateBhiveIndexAsync()
    {
        Console.WriteLine("\nStep 2: Creating BHIVE vector index manually...");
        
        var connectionString = _configuration?["Couchbase:ConnectionString"];
        var username = _configuration?["Couchbase:Username"];
        var password = _configuration?["Couchbase:Password"];
        var bucketName = _configuration?["Couchbase:BucketName"];
        var scopeName = _configuration?["Couchbase:ScopeName"];
        var collectionName = _configuration?["Couchbase:CollectionName"];

        // Connect to Couchbase cluster
        var cluster = await Cluster.ConnectAsync(new ClusterOptions
        {
            ConnectionString = connectionString!,
            UserName = username!,
            Password = password!
        });

        var bucket = await cluster.BucketAsync(bucketName!);
        var scope = await bucket.ScopeAsync(scopeName!);

        // Create BHIVE index SQL
        var indexName = "bhive_glossary_index";
        var createIndexQuery = $@"
            CREATE VECTOR INDEX `{indexName}` 
            ON `{bucketName}`.`{scopeName}`.`{collectionName}` (DefinitionEmbedding VECTOR) 
            INCLUDE (Category, Term, Definition)
            USING GSI WITH {{
                ""dimension"": 1536,
                ""similarity"": ""cosine"", 
                ""description"": ""IVF,SQ8""
            }}";

        try
        {
            Console.WriteLine("Executing BHIVE index creation query...");
            await scope.QueryAsync<dynamic>(createIndexQuery);
            Console.WriteLine($"BHIVE vector index '{indexName}' created successfully!");
        }
        catch (Exception ex) when (ex.Message.Contains("already exists"))
        {
            Console.WriteLine($"BHIVE vector index '{indexName}' already exists.");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Failed to create BHIVE vector index: {ex.Message}");
            throw;
        }
    }

    /// <summary>
    /// Perform basic vector search - following SK pattern from Step2_Vector_Search.
    /// </summary>
    private static async Task SearchCouchbaseVectorStoreAsync()
    {
        Console.WriteLine("\nStep 3: Performing vector search...");

        var collection = await GetCouchbaseVectorStoreCollectionAsync();

        // Search the vector store using the same pattern as SK examples
        var searchResultItem = await SearchVectorStoreAsync(
            collection,
            "What is an Application Programming Interface?",
            _embeddingGenerator!);

        // Write the search result with its score to the console
        Console.WriteLine($"   Found: {searchResultItem.Record.Term}");
        Console.WriteLine($"   Definition: {searchResultItem.Record.Definition}");
        Console.WriteLine($"   Score: {searchResultItem.Score:F4}");
    }

    /// <summary>
    /// Perform filtered vector search - following SK pattern from Step2_Vector_Search.
    /// </summary>
    private static async Task SearchCouchbaseVectorStoreWithFilteringAsync()
    {
        Console.WriteLine("\nStep 4: Performing filtered vector search...");

        var collection = await GetCouchbaseVectorStoreCollectionAsync();

        // Generate an embedding from the search string
        var searchString = "How do I provide additional context to an LLM?";
        var searchVector = (await _embeddingGenerator!.GenerateAsync(searchString)).Vector;

        // Search the store with a filter and get the single most relevant result
        var searchResultItems = await collection.SearchAsync(
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
    /// Get Couchbase vector store collection with proper configuration.
    /// </summary>
    private static async Task<VectorStoreCollection<string, Glossary>> GetCouchbaseVectorStoreCollectionAsync()
    {
        var connectionString = _configuration!["Couchbase:ConnectionString"];
        var username = _configuration["Couchbase:Username"];
        var password = _configuration["Couchbase:Password"];
        var bucketName = _configuration["Couchbase:BucketName"];
        var scopeName = _configuration["Couchbase:ScopeName"];
        var collectionName = _configuration["Couchbase:CollectionName"];

        if (string.IsNullOrEmpty(connectionString) || string.IsNullOrEmpty(username) || string.IsNullOrEmpty(password) ||
            string.IsNullOrEmpty(bucketName) || string.IsNullOrEmpty(scopeName) || string.IsNullOrEmpty(collectionName))
        {
            throw new InvalidOperationException(
                "Couchbase configuration missing. Please set ConnectionString, Username, Password, BucketName, ScopeName, CollectionName in appsettings.json or user secrets.");
        }

        // Connect to Couchbase cluster
        var cluster = await Cluster.ConnectAsync(new ClusterOptions
        {
            ConnectionString = connectionString,
            UserName = username,
            Password = password
        });

        var bucket = await cluster.BucketAsync(bucketName);
        var scope = await bucket.ScopeAsync(scopeName);

        // Configure BHIVE index options
        var collectionOptions = new CouchbaseQueryCollectionOptions
        {
            IndexName = "bhive_glossary_index",
            SimilarityMetric = "cosine"
        };

        // Create vector store and get collection
        var vectorStore = new CouchbaseVectorStore(scope);
        var collection = vectorStore.GetCollection<string, Glossary>(collectionName, collectionOptions);

        return collection;
    }

    /// <summary>
    /// Ingest data into the given collection
    /// </summary>
    /// <param name="collection">The collection to ingest data into.</param>
    /// <param name="embeddingGenerator">The service to use for generating embeddings.</param>
    /// <returns>The keys of the upserted records.</returns>
    private static async Task IngestDataIntoVectorStoreAsync(VectorStoreCollection<string, Glossary> collection,
        IEmbeddingGenerator<string, Embedding<float>> embeddingGenerator)
    {
        // Create glossary entries and generate embeddings for them
        var glossaryEntries = CreateGlossaryEntries().ToList();
        var tasks = glossaryEntries.Select(entry => Task.Run(async () =>
        {
            entry.DefinitionEmbedding = (await embeddingGenerator.GenerateAsync(entry.Definition)).Vector;
        }));
        await Task.WhenAll(tasks);

        // Upsert the glossary entries into the collection and return their keys
        await collection.UpsertAsync(glossaryEntries);
    }

    /// <summary>
    /// Search the given collection for the most relevant result - following SK Step2_Vector_Search pattern.
    /// </summary>
    /// <param name="collection">The collection to search.</param>
    /// <param name="searchString">The string to search matches for.</param>
    /// <param name="embeddingGenerator">The service to generate embeddings with.</param>
    /// <returns>The top search result.</returns>
    private static async Task<VectorSearchResult<Glossary>?> SearchVectorStoreAsync(
        VectorStoreCollection<string, Glossary> collection, 
        string searchString, 
        IEmbeddingGenerator<string, Embedding<float>> embeddingGenerator)
    {
        // Generate an embedding from the search string
        var searchVector = (await embeddingGenerator.GenerateAsync(searchString)).Vector;

        // Search the store and get the single most relevant result
        var searchResultItems = await collection.SearchAsync(
            searchVector,
            top: 1).ToListAsync();
        return searchResultItems.FirstOrDefault();
    }

    /// <summary>
    /// Create some sample glossary entries - same as SK examples.
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

/// <summary>
/// Wrapper to adapt OpenAI EmbeddingClient to IEmbeddingGenerator interface.
/// Uses the GenerateEmbeddingsAsync method from EmbeddingClient.
/// </summary>
internal class OpenAiEmbeddingGeneratorWrapper(EmbeddingClient embeddingClient)
    : IEmbeddingGenerator<string, Embedding<float>>
{
    private readonly EmbeddingClient _embeddingClient = embeddingClient ?? throw new ArgumentNullException(nameof(embeddingClient));

    public async Task<GeneratedEmbeddings<Embedding<float>>> GenerateAsync(
        IEnumerable<string> values, 
        EmbeddingGenerationOptions? options = null, 
        CancellationToken cancellationToken = default)
    {
        var inputList = values.ToList();
        
        // Use the GenerateEmbeddingsAsync method from EmbeddingClient
        var response = await _embeddingClient.GenerateEmbeddingsAsync(inputList, cancellationToken: cancellationToken);
        
        // Convert OpenAIEmbeddingCollection to the expected format
        var embeddings = response.Value.Select(embedding => 
            new Embedding<float>(embedding.ToFloats())).ToList();

        return new GeneratedEmbeddings<Embedding<float>>(embeddings);
    }

    public void Dispose()
    {
        // Not implemented
    }

    public object? GetService(Type serviceType, object? serviceKey = null)
    {
        throw new NotImplementedException();
    }
}