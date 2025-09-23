// Copyright (c) Microsoft. All rights reserved.

using Couchbase.SemanticKernel;
using VectorData.ConformanceTests.Support;
using Microsoft.Extensions.VectorData;

namespace Couchbase.ConformanceTests.Support;

/// <summary>
/// Fixture for Couchbase simple model tests with proper data seeding before index creation.
/// </summary>
public sealed class CouchbaseSimpleModelFixture : SimpleModelFixture<string>
{
    public override TestStore TestStore => CouchbaseTestStore.Instance;
    
    // Use fixed collection name instead of GUID for easier data management
    public override string CollectionName => "simple_model_tests";
    
    /// <summary>
    /// Override to use Couchbase Query collection with COMPOSITE index for query operations.
    /// </summary>
    protected override VectorStoreCollection<string, VectorData.ConformanceTests.Models.SimpleRecord<string>> GetCollection()
    {
        var testStore = (CouchbaseTestStore)TestStore;
        var vectorStore = testStore.GetVectorStore(new CouchbaseVectorStoreOptions { IndexType = CouchbaseIndexType.Composite });
        
        var queryOptions = new CouchbaseQueryCollectionOptions
        {
            IndexName = $"{CollectionName}_composite_index",
            VectorDimensions = 3, // SimpleRecord<string> has 3-dimensional vectors
            SimilarityMetric = "COSINE",
            QuantizationSettings = null // COMPOSITE doesn't use quantization
        };
        
        return vectorStore.GetCollection<string, VectorData.ConformanceTests.Models.SimpleRecord<string>>(CollectionName, queryOptions);
    }

    /// <summary>
    /// Override seeding to pre-seed minimal data before actual test data.
    /// </summary>
    protected override async Task SeedAsync()
    {
        // Step 1: Pre-seed minimal data first to allow vector index creation
        await this.PreSeedMinimalDataAsync();
        
        // Step 2: Now seed the actual test data
        await base.SeedAsync();
    }

    /// <summary>
    /// Pre-seed minimal data to allow vector index creation.
    /// </summary>
    private async Task PreSeedMinimalDataAsync()
    {
        var minimalData = new List<VectorData.ConformanceTests.Models.SimpleRecord<string>>
        {
            new()
            {
                Id = "pre_seed_1",
                Text = "Minimal data for vector index creation",
                Number = 0,
                Floats = new ReadOnlyMemory<float>(new float[] { 0.1f, 0.1f, 0.1f })
            }
        };

        // Use UpsertAsync (single method) instead of UpsertBatchAsync
        await this.Collection.UpsertAsync(minimalData);
        
        // Wait a moment for data to be available
        await Task.Delay(100);
    }
} 