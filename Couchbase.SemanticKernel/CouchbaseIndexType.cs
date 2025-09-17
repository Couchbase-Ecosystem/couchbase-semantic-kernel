namespace Couchbase.SemanticKernel;

/// <summary>
/// Represents the different types of indexes supported by Couchbase for vector operations.
/// </summary>
public enum CouchbaseIndexType
{
    /// <summary>
    /// Full-Text Search (FTS) index for vector and hybrid search operations.
    /// Uses Couchbase's Search API with vector field configuration.
    /// Best for: Hybrid search, text search, moderate scale.
    /// </summary>
    Search,

    /// <summary>
    /// BHIVE (Hyperscale Vector Index) for high-performance vector search using SQL++ queries.
    /// Optimized for high-dimensional vectors with advanced quantization support.
    /// Best for: Pure vector search, large scale, high performance.
    /// </summary>
    Bhive,

    /// <summary>
    /// COMPOSITE index with vector fields for traditional approach using SQL++ queries.
    /// Combines vector fields with scalar fields for complex filtering scenarios.
    /// Best for: Complex SQL filtering, mixed workloads.
    /// </summary>
    Composite
} 