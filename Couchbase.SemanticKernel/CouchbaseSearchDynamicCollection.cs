using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using Couchbase.KeyValue;

namespace Couchbase.SemanticKernel;

/// <summary>
/// Represents a collection of vector store records in a Couchbase database using FTS (Full-Text Search),
/// mapped to a dynamic <c>Dictionary&lt;string, object?&gt;</c>.
/// This collection uses Couchbase's Search API for vector and hybrid search operations.
/// </summary>
#pragma warning disable CA1711 // Identifiers should not have incorrect suffix
[Experimental("MEVD9001")]
public sealed class CouchbaseSearchDynamicCollection : CouchbaseSearchCollection<object, Dictionary<string, object?>>
#pragma warning restore CA1711 // Identifiers should not have incorrect suffix
{
    /// <summary>
    /// Initializes a new instance of the <see cref="CouchbaseSearchDynamicCollection"/> class.
    /// </summary>
    /// <param name="scope"><see cref="IScope"/> that can be used to manage the collections in Couchbase.</param>
    /// <param name="name">The name of the collection.</param>
    /// <param name="options">Configuration options for this class.</param>
    public CouchbaseSearchDynamicCollection(IScope scope, string name, CouchbaseSearchCollectionOptions options)
        : base(
            scope,
            name,
            CreateDynamicOptions(options))
    {
    }

    /// <summary>
    /// Creates and validates dynamic options for search-based collections.
    /// </summary>
    /// <param name="options">The original search collection options.</param>
    /// <returns>Validated options for dynamic collections.</returns>
    /// <exception cref="ArgumentException">Thrown when Definition is null.</exception>
    private static CouchbaseSearchCollectionOptions CreateDynamicOptions(CouchbaseSearchCollectionOptions options)
    {
        if (options.Definition == null)
        {
            throw new ArgumentException("Definition is required for dynamic collections.", nameof(options));
        }

        // Create a new options instance with the dynamic model
        var dynamicOptions = new CouchbaseSearchCollectionOptions(options);

        // The collection will use the dynamic model builder through the model creation process
        return dynamicOptions;
    }
}