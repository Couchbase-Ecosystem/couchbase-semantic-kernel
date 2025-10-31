<img align="right" width="150px" height="150px" src="./Assets/logo.svg" alt="Couchbase Logo"/>

# Couchbase connector for Microsoft Semantic Kernel

Repository for `Couchbase.SemanticKernel` the official Couchbase [Vector Store Connector](https://learn.microsoft.com/en-us/semantic-kernel/concepts/vector-store-connectors/?pivots=programming-language-csharp) for
[Microsoft Semantic Kernel](https://learn.microsoft.com/en-us/semantic-kernel/overview/).

## Introduction

[Semantic Kernel](https://learn.microsoft.com/en-us/semantic-kernel/overview/) is an SDK that integrates Large Language
Models (LLMs) like OpenAI, Azure OpenAI, and Hugging Face with conventional programming languages like C#, Python, and
Java. Semantic Kernel achieves this by allowing you to define plugins that can be chained together in just a few lines
of code.

Semantic Kernel and .NET provides an abstraction for interacting with Vector Stores and a list of out-of-the-box
connectors that implement these abstractions. Features include creating, listing and deleting collections of records,
and uploading, retrieving and deleting records. The abstraction makes it easy to experiment with a free or locally
hosted Vector Store and then switch to a service when needing to scale up.

This repository contains the official Couchbase Vector Store Connector implementation for Semantic Kernel.

## Overview

The Couchbase Vector Store connector can be used to access and manage data in Couchbase. The connector has the
following characteristics.

| Feature Area                          | Support                                                                                                                                                                                                                              |
| ------------------------------------- | ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------ |
| Collection maps to                    | Couchbase collection + index                                                                                                                                                                                                                 |
| Supported key property types          | <ul><li>`string`</li></ul>                                                                                                                                                                                                           |
| Supported data property types         | All types that are supported by `System.Text.Json` (either built-in or by using a custom converter)                                                                                                                                  |
| Supported vector property types       | <ul><li>`ReadOnlyMemory<float>`</li><li>`Embedding<float>`</li><li>`float[]`</li></ul>                     |
| Supported distance functions          | <ul><li>CosineSimilarity</li><li>DotProductSimilarity</li><li>EuclideanDistance</li></ul>                                                                                                                                            |
| Supported filter clauses              | <ul><li>`AnyTagEqualTo`</li><li>`EqualTo`</li></ul>                                                                                                                                                                                  |
| Supports multiple vectors in a record | Yes                                                                                                                                                                                                                                  |
| IsIndexed supported?                  | Yes                                                                                                                                                                                                                                  |
| IsFullTextIndexed supported?          | Yes                                                                                                                                                                                                                                  |
| StoragePropertyName supported?        | No, use `JsonSerializerOptions` and `JsonPropertyNameAttribute` instead. [See here for more info.](#data-mapping)                                                                                                                    |
| HybridSearch supported?               | Yes                                                                                                                                                                                                                                  |

## Getting Started

### Setting up Couchbase

Setup a Couchbase Cluster ([Self-Managed](https://www.couchbase.com/downloads/?family=couchbase-server) or [Capella](https://www.couchbase.com/products/cloud))

**Version Requirements:**
- **Search (FTS) indexes**: Couchbase Server 7.6+ or Capella with [Search Service](https://docs.couchbase.com/server/current/search/search.html) enabled
- **Hyperscale and Composite indexes**: Couchbase Server 8.0+ or Capella


### Using the Couchbase Vector Store Connector

Add the Couchbase Vector Store connector NuGet package to your project.

```dotnetcli
dotnet add package CouchbaseConnector.SemanticKernel --prerelease
```

You can add the vector store to the dependency injection container available on the `KernelBuilder` or to the `IServiceCollection` dependency injection container using extension methods provided by Semantic Kernel.

```csharp
using Microsoft.SemanticKernel;
using Couchbase.SemanticKernel;

// Using Kernel Builder.
var kernelBuilder = Kernel
    .CreateBuilder()
    .AddCouchbaseVectorStore(
        connectionString: "couchbases://your-cluster-address",
        username: "username",
        password: "password",
        bucketName: "bucket-name",
        scopeName: "scope-name");
```

```csharp
using Microsoft.SemanticKernel;
using Couchbase.SemanticKernel;

// Using IServiceCollection with ASP.NET Core.
var builder = WebApplication.CreateBuilder(args);
builder.Services.AddCouchbaseVectorStore(
    connectionString: "couchbases://your-cluster-address",
    username: "username",
    password: "password",
    bucketName: "bucket-name",
    scopeName: "scope-name");
```

**Configuring Index Type**

The vector store defaults to using Hyperscale indexes. You can specify a different index type by passing `CouchbaseVectorStoreOptions`:

```csharp
using Microsoft.SemanticKernel;
using Couchbase.SemanticKernel;

var builder = WebApplication.CreateBuilder(args);

// Option 1: Use Hyperscale index 
builder.Services.AddCouchbaseVectorStore(
    connectionString: "couchbases://your-cluster-address",
    username: "username",
    password: "password",
    bucketName: "bucket-name",
    scopeName: "scope-name",
    options: new CouchbaseVectorStoreOptions 
    { 
        IndexType = CouchbaseIndexType.Hyperscale
    });

// Option 2: Use Search/FTS index
builder.Services.AddCouchbaseVectorStore(
    connectionString: "couchbases://your-cluster-address",
    username: "username",
    password: "password",
    bucketName: "bucket-name",
    scopeName: "scope-name",
    options: new CouchbaseVectorStoreOptions 
    { 
        IndexType = CouchbaseIndexType.Search
    });

// Option 3: Use Composite index
builder.Services.AddCouchbaseVectorStore(
    connectionString: "couchbases://your-cluster-address",
    username: "username",
    password: "password",
    bucketName: "bucket-name",
    scopeName: "scope-name",
    options: new CouchbaseVectorStoreOptions 
    { 
        IndexType = CouchbaseIndexType.Composite
    });
```

Extension methods that take no parameters are also provided. These require an instance of the `IScope` class to be separately registered with the dependency injection container.

```csharp
using Microsoft.Extensions.DependencyInjection;
using Microsoft.SemanticKernel;
using Couchbase;
using Couchbase.KeyValue;

// Using Kernel Builder.
var kernelBuilder = Kernel.CreateBuilder();
kernelBuilder.Services.AddSingleton<ICluster>(sp =>
{
    var clusterOptions = new ClusterOptions
    {
        ConnectionString = "couchbases://your-cluster-address",
        UserName = "username",
        Password = "password"
    };

    return Cluster.ConnectAsync(clusterOptions).GetAwaiter().GetResult();
});

kernelBuilder.Services.AddSingleton<IScope>(sp =>
{
    var cluster = sp.GetRequiredService<ICluster>();
    var bucket = cluster.BucketAsync("bucket-name").GetAwaiter().GetResult();
    return bucket.Scope("scope-name");
});

kernelBuilder.AddCouchbaseVectorStore();
```

```csharp
using Microsoft.Extensions.DependencyInjection;
using Microsoft.SemanticKernel;
using Couchbase.KeyValue;
using Couchbase;

// Using IServiceCollection with ASP.NET Core.
var builder = WebApplication.CreateBuilder(args);

builder.Services.AddSingleton<ICluster>(sp =>
{
    var clusterOptions = new ClusterOptions
    {
        ConnectionString = "couchbases://your-cluster-address",
        UserName = "username",
        Password = "password"
    };

    return Cluster.ConnectAsync(clusterOptions).GetAwaiter().GetResult();
});

builder.Services.AddSingleton<IScope>(sp =>
{
    var cluster = sp.GetRequiredService<ICluster>();
    var bucket = cluster.BucketAsync("bucket-name").GetAwaiter().GetResult();
    return bucket.Scope("scope-name");
});

// Add Couchbase Vector Store
builder.Services.AddCouchbaseVectorStore();
```

You can construct a Couchbase Vector Store instance directly.

```csharp
using Couchbase;
using Couchbase.KeyValue;
using Couchbase.SemanticKernel;

var clusterOptions = new ClusterOptions
{
    ConnectionString = "couchbases://your-cluster-address",
    UserName = "username",
    Password = "password"
};

var cluster = await Cluster.ConnectAsync(clusterOptions);
var bucket = await cluster.BucketAsync("bucket-name");
var scope = bucket.Scope("scope-name");

var vectorStore = new CouchbaseVectorStore(scope);
```

It is possible to construct a direct reference to a named collection.

### Using Search Collection (FTS Index)

For Full-Text Search indexes and hybrid search scenarios:

```csharp
using Couchbase.SemanticKernel;
using Couchbase;
using Couchbase.KeyValue;

var cluster = await Cluster.ConnectAsync(clusterOptions);
var bucket = await cluster.BucketAsync("bucket-name");
var scope = bucket.Scope("scope-name");

var collection = new CouchbaseSearchCollection<string, Hotel>(
    scope,
    "skhotels");
```

### Using Query Collection (Hyperscale or Composite Index)

For high-performance vector search with Hyperscale indexes:

```csharp
using Couchbase.SemanticKernel;
using Couchbase;
using Couchbase.KeyValue;

var cluster = await Cluster.ConnectAsync(clusterOptions);
var bucket = await cluster.BucketAsync("bucket-name");
var scope = bucket.Scope("scope-name");

// Using Hyperscale index (default)
var collection = new CouchbaseQueryCollection<string, Hotel>(
    scope,
    "skhotels",
    indexType: CouchbaseIndexType.Hyperscale);

// Or using Composite index
var collectionComposite = new CouchbaseQueryCollection<string, Hotel>(
    scope,
    "skhotels",
    indexType: CouchbaseIndexType.Composite);
```

### Index Type Comparison

Couchbase offers three types of indexes for vector search:

**Search (FTS) - Full-Text Search Index**
- Best for hybrid searches combining full-text search with vector similarity
- Allows semantic search alongside traditional keyword matching
- Supports geospatial searches in addition to vector and text
- **Use when:** You need to combine traditional keyword search with vector similarity search in the same query
- **Ideal for:** E-commerce product search, travel recommendations, content discovery with multiple search criteria
- **Requires:** Couchbase Server 7.6+ or Capella

**Hyperscale Vector Index**
- Best for pure vector searches - content discovery, recommendations, semantic search
- High performance with low memory footprint - designed to scale to billions of vectors
- Optimized for concurrent operations - supports simultaneous searches and inserts
- **Use when:** You primarily perform vector-only queries without complex scalar filtering
- **Ideal for:** Large-scale semantic search, recommendation systems, content discovery
- **Requires:** Couchbase Server 8.0+ or Capella

**Composite Vector Index**
- Best for filtered vector searches - combines vector search with scalar value filtering
- Efficient pre-filtering - scalar attributes reduce the vector comparison scope
- **Use when:** Your queries combine vector similarity with scalar filters that eliminate large portions of data
- **Ideal for:** Compliance-based filtering, user-specific searches, time-bounded queries
- **Requires:** Couchbase Server 8.0+ or Capella

**Choosing the Right Index Type:**
- Start with **Hyperscale** for pure vector searches and large datasets (scales to billions)
- Use **Search (FTS)** for hybrid search combining text and vectors
- Choose **Composite** when scalar filters significantly reduce your search space (works well for tens of millions to billions of vectors)

[Detailed comparison of vector index types](https://docs.couchbase.com/server/current/vector-index/use-vector-indexes.html)

## Data mapping

The Couchbase connector will use `System.Text.Json.JsonSerializer` to do mapping. Properties in the data model are serialized into a JSON object and stored as the document value in Couchbase.

Usage of the `JsonPropertyNameAttribute` is supported if a different storage name to the data model property name is required. It is also possible to use a custom `JsonSerializerOptions` instance with a customized property naming policy.

```csharp
using Couchbase.SemanticKernel;
using Couchbase.KeyValue;
using System.Text.Json;

var jsonSerializerOptions = new JsonSerializerOptions
{
    PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseUpper
};

var options = new CouchbaseQueryCollectionOptions
{
    JsonSerializerOptions = jsonSerializerOptions
};

var collection = new CouchbaseQueryCollection<string, Hotel>(scope, "skhotelsjson", options);
```

Since a naming policy of snake case upper was chosen, here is an example of how this data type will be stored in Couchbase. Also note the use of `JsonPropertyNameAttribute` on the `Description` property to further customize the storage naming.

```csharp
using System.Text.Json.Serialization;
using Microsoft.Extensions.VectorData;

public class Hotel
{
    [VectorStoreKey]
    public string HotelId { get; set; }

    [VectorStoreData(IsIndexed = true)]
    public string HotelName { get; set; }

    [JsonPropertyName("HOTEL_DESCRIPTION")]
    [VectorStoreData(IsFullTextIndexed = true)]
    public string Description { get; set; }

    [VectorStoreVector(Dimensions: 4, DistanceFunction.CosineSimilarity)]
    public ReadOnlyMemory<float>? DescriptionEmbedding { get; set; }
}
```

```json
{
  "_id" : "h1",
  "HOTEL_ID" : "h1",
  "HOTEL_NAME" : "Hotel Happy",
  "HOTEL_DESCRIPTION" : "A place where everyone can be happy.",
  "DESCRIPTION_EMBEDDING" : [
    0.9,
    0.1,
    0.1,
    0.1
  ]
}
```

## License

Couchbase connector for Microsoft Semantic Kernel is licensed under the Apache 2.0 license.
---

## 📢 Support Policy

We truly appreciate your interest in this project!  
This project is **community-maintained**, which means it's **not officially supported** by our support team.

If you need help, have found a bug, or want to contribute improvements, the best place to do that is right here — by [opening a GitHub issue](https://github.com/Couchbase-Ecosystem/couchbase-semantic-kernel/issues).  
Our support portal is unable to assist with requests related to this project, so we kindly ask that all inquiries stay within GitHub.

Your collaboration helps us all move forward together — thank you!
