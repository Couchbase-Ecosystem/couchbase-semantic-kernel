<img align="right" width="150" height="150" src="./Assets/logo.svg" alt="Couchbase Logo"/>

# Couchbase connector for Microsoft Semantic Kernel

Repository for `CouchbaseConnector.SemanticKernel` the official
Couchbase [Vector Store Connector](https://learn.microsoft.com/en-us/semantic-kernel/concepts/vector-store-connectors/?pivots=programming-language-csharp)
for
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

| Feature Area                          | Support                                                                                                           |
|---------------------------------------|-------------------------------------------------------------------------------------------------------------------|
| Collection maps to                    | Couchbase collection                                                                                              |
| Supported key property types          | string                                                                                                            |
| Supported data property types         | All types that are supported by System.Text.Json (either built-in or by using a custom converter)                 |
| Supported vector property types       | <ul><li>ReadOnlyMemory\<float\></li></ul>                                                                         |
| Supported index types                 | N/A                                                                                                               |
| Supported distance functions          | <ul><li>CosineSimilarity</li><li>DotProductSimilarity</li><li>EuclideanDistance</li></ul>                         |
| Supported filter clauses              | <ul><li>AnyTagEqualTo</li><li>EqualTo</li></ul>                                                                   |
| Supports multiple vectors in a record | Yes                                                                                                               |
| IsFilterable supported?               | No                                                                                                                |
| IsFullTextSearchable supported?       | No                                                                                                                |
| StoragePropertyName supported?        | No, use `JsonSerializerOptions` and `JsonPropertyNameAttribute` instead. [See here for more info.](#data-mapping) |

## Getting Started

### Setting up Couchbase

Setup a Couchbase Cluster ([Self-Managed](https://www.couchbase.com/downloads) or [Capella](https://www.couchbase.com/products/cloud)) running version 7.6+ with the [Search Service](https://docs.couchbase.com/server/current/search/search.html) enabled

For vector search, ensure you have a Vector Search Index configured.
For more information on creating a vector search index, please follow the [instructions](https://docs.couchbase.com/cloud/vector-search/create-vector-search-index-ui.html).

### Using the Couchbase Vector Store Connector

Add the Couchbase Vector Store connector NuGet package to your project.

```dotnetcli
dotnet add package CouchbaseConnector.SemanticKernel --prerelease
```

You can add the vector store to the dependency injection container available on the `KernelBuilder` or to
the `IServiceCollection` dependency injection container using extension methods provided by Semantic Kernel.

```csharp
using Microsoft.SemanticKernel;

// Using Kernel Builder.
var kernelBuilder = Kernel.CreateBuilder()
    .AddCouchbaseVectorStore(
        connectionString: "couchbases://your-cluster-address",
        username: "username",
        password: "password",
        bucketName: "bucket-name",
        scopeName: "scope-name");
```

```csharp
using Microsoft.Extensions.DependencyInjection;

// Using IServiceCollection with ASP.NET Core.
var builder = WebApplication.CreateBuilder(args);
builder.Services.AddCouchbaseVectorStore(
    connectionString: "couchbases://your-cluster-address",
    username: "username",
    password: "password",
    bucketName: "bucket-name",
    scopeName: "scope-name");
```

Extension methods that take no parameters are also provided. These require an instance of the `IScope` class to be
separately registered with the dependency injection container.

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

// Add Couchbase Vector Store
kernelBuilder.Services.AddCouchbaseVectorStore();
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

var collection = new CouchbaseFtsVectorStoreRecordCollection<Hotel>(
    scope,
    "hotelCollection");
```

## Data mapping

The Couchbase connector uses `System.Text.Json.JsonSerializer` for data mapping. Properties in the data model are serialized into a JSON object and mapped to Couchbase storage.

Use the `JsonPropertyName` attribute to map a property to a different name in Couchbase storage. Alternatively, you can configure `JsonSerializerOptions` for advanced customization.
```csharp
using Couchbase.SemanticKernel;
using Couchbase.KeyValue;
using System.Text.Json;

var jsonSerializerOptions = new JsonSerializerOptions
{
    PropertyNamingPolicy = JsonNamingPolicy.CamelCase
};

var options = new CouchbaseFtsVectorStoreRecordCollectionOptions<Hotel>
{
    JsonSerializerOptions = jsonSerializerOptions
};

var collection = new CouchbaseFtsVectorStoreRecordCollection<Hotel>(scope, "hotels", options);
```
Using the above custom `JsonSerializerOptions` which is using `CamelCase`, the following data model will be mapped to the below json.

```csharp
using System.Text.Json.Serialization;
using Microsoft.Extensions.VectorData;

public class Hotel
{
    [JsonPropertyName("hotelId")]
    [VectorStoreRecordKey]
    public string HotelId { get; set; }

    [JsonPropertyName("hotelName")]
    [VectorStoreRecordData]
    public string HotelName { get; set; }

    [JsonPropertyName("description")]
    [VectorStoreRecordData]
    public string Description { get; set; }

    [JsonPropertyName("descriptionEmbedding")]
    [VectorStoreRecordVector(Dimensions: 4, DistanceFunction.DotProductSimilarity)]
    public ReadOnlyMemory<float> DescriptionEmbedding { get; set; }
}
```

```json
{
  "hotelId": "h1",
  "hotelName": "Hotel Happy",
  "description": "A place where everyone can be happy",
  "descriptionEmbedding": [0.9, 0.1, 0.1, 0.1]
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
