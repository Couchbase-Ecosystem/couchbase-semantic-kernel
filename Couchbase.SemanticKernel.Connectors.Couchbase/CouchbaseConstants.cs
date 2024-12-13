using Microsoft.Extensions.VectorData;

namespace Couchbase.SemanticKernel.Connectors.Couchbase;

/// <summary>
    /// Constants for Couchbase vector store implementation.
    /// </summary>
    internal static class CouchbaseConstants
    {
        /// <summary>
        /// Reserved key property name in Couchbase.
        /// </summary>
        internal const string ReservedKeyPropertyName = "id";

        /// <summary>
        /// Default bucket name for Couchbase operations.
        /// </summary>
        internal const string DefaultBucketName = "default";

        /// <summary>
        /// Default collection name for Couchbase operations.
        /// </summary>
        internal const string DefaultCollectionName = "_default";

        /// <summary>
        /// Default scope name for Couchbase operations.
        /// </summary>
        internal const string DefaultScopeName = "_default";

        /// <summary>
        /// Default index name for Couchbase vector operations.
        /// </summary>
        internal const string DefaultVectorIndexName = "vector_index";

        /// <summary>
        /// Default distance function for vector search.
        /// </summary>
        internal const string DefaultDistanceFunction = DistanceFunction.CosineSimilarity;

        /// <summary>
        /// A <see cref="HashSet{Type}"/> containing the supported key types.
        /// </summary>
        internal static readonly HashSet<Type> SupportedKeyTypes = new()
        {
            typeof(string)
        };

        /// <summary>
        /// A <see cref="HashSet{Type}"/> containing the supported data property types.
        /// </summary>
        internal static readonly HashSet<Type> SupportedDataTypes = new()
        {
            typeof(bool),
            typeof(bool?),
            typeof(string),
            typeof(int),
            typeof(int?),
            typeof(long),
            typeof(long?),
            typeof(float),
            typeof(float?),
            typeof(double),
            typeof(double?),
            typeof(decimal),
            typeof(decimal?),
            typeof(DateTime),
            typeof(DateTime?)
        };

        /// <summary>
        /// A <see cref="HashSet{Type}"/> containing the supported vector types.
        /// </summary>
        internal static readonly HashSet<Type> SupportedVectorTypes = new()
        {
            typeof(ReadOnlyMemory<float>),
            typeof(ReadOnlyMemory<float>?),
            typeof(ReadOnlyMemory<byte>),
            typeof(ReadOnlyMemory<byte>?)
        };
    }