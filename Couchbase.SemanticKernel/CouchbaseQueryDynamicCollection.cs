using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using Couchbase.KeyValue;

namespace Couchbase.SemanticKernel;

/// <summary>
/// Represents a collection of vector store records in a Couchbase database using SQL++ queries (BHIVE/COMPOSITE),
/// mapped to a dynamic <c>Dictionary&lt;string, object?&gt;</c>.
/// This collection uses Couchbase's Query API for vector search operations with BHIVE and COMPOSITE indexes.
/// </summary>
#pragma warning disable CA1711 // Identifiers should not have incorrect suffix
public sealed class CouchbaseQueryDynamicCollection : CouchbaseQueryCollection<object, Dictionary<string, object?>>
#pragma warning restore CA1711 // Identifiers should not have incorrect suffix
{
    /// <summary>
    /// Initializes a new instance of the <see cref="CouchbaseQueryDynamicCollection"/> class.
    /// </summary>
    /// <param name="scope"><see cref="IScope"/> that can be used to manage the collections in Couchbase.</param>
    /// <param name="name">The name of the collection.</param>
    /// <param name="options">Configuration options for this class.</param>
    /// <param name="indexType">The index type to use for vector operations.</param>
    public CouchbaseQueryDynamicCollection(IScope scope, string name, CouchbaseQueryCollectionOptions options, CouchbaseIndexType indexType = CouchbaseIndexType.Bhive)
        : base(
            scope,
            name,
            CreateDynamicOptions(options),
            indexType)
    {
    }

    /// <summary>
    /// Creates and validates dynamic options for query-based collections.
    /// </summary>
    /// <param name="options">The original query collection options.</param>
    /// <returns>Validated options for dynamic collections.</returns>
    /// <exception cref="ArgumentException">Thrown when Definition is null.</exception>
    private static CouchbaseQueryCollectionOptions CreateDynamicOptions(CouchbaseQueryCollectionOptions options)
    {
        if (options.Definition == null)
        {
            throw new ArgumentException("Definition is required for dynamic collections.", nameof(options));
        }

        // Create a new options instance with the dynamic model
        var dynamicOptions = new CouchbaseQueryCollectionOptions(options);
        
        // The collection will use the dynamic model builder through the model creation process
        return dynamicOptions;
    }
} 