using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.VectorData.ProviderServices;

namespace Couchbase.SemanticKernel;

/// <summary>
/// Contains mapping helpers for creating Couchbase vector collections and queries.
/// </summary>
internal static class CouchbaseQueryCollectionCreateMapping
{
    /// <summary>
    /// Gets fields that should be included in BHIVE index INCLUDE clause.
    /// </summary>
    /// <param name="model">The collection model.</param>
    /// <returns>List of field names to include.</returns>
    public static List<string> GetBhiveIncludeFields(CollectionModel model)
    {
        return model.DataProperties
            .Where(p => p.IsIndexed || p.IsFullTextIndexed)
            .Select(p => p.StorageName)
            .ToList();
    }

    /// <summary>
    /// Gets fields that should be part of COMPOSITE index keys.
    /// </summary>
    /// <param name="model">The collection model.</param>
    /// <param name="customScalarKeys">Optional custom scalar keys.</param>
    /// <returns>List of field names for composite indexing.</returns>
    public static List<string> GetCompositeIndexFields(CollectionModel model, IEnumerable<string>? customScalarKeys = null)
    {
        if (customScalarKeys?.Any() == true)
        {
            return customScalarKeys.ToList();
        }

        return model.DataProperties
            .Where(p => p.IsIndexed || p.IsFullTextIndexed)
            .Select(p => p.StorageName)
            .ToList();
    }

    /// <summary>
    /// Builds index parameters for vector indexes.
    /// </summary>
    /// <param name="vectorProperty">The vector property.</param>
    /// <param name="options">The query collection options.</param>
    /// <returns>Dictionary of index parameters.</returns>
    public static Dictionary<string, object> BuildIndexParameters(VectorPropertyModel vectorProperty, CouchbaseQueryCollectionOptions options)
    {
        var dimensions = vectorProperty.Dimensions;

        var similarity = MapSimilarityMetric(options.SimilarityMetric);
        var description = options.QuantizationSettings ?? "IVF,SQ8";

        var indexParams = new Dictionary<string, object>
        {
            ["dimension"] = dimensions,
            ["similarity"] = similarity,
            ["description"] = description
        };

        if (options.CentroidsToProbe.HasValue)
        {
            indexParams["scan_nprobes"] = options.CentroidsToProbe.Value;
        }

        return indexParams;
    }

    /// <summary>
    /// Builds keyword search condition for hybrid search.
    /// </summary>
    /// <param name="model">The collection model.</param>
    /// <param name="keywords">Keywords to search for.</param>
    /// <returns>SQL WHERE clause for keyword search.</returns>
    public static string? BuildKeywordSearchCondition(CollectionModel model, ICollection<string>? keywords)
    {
        if (keywords?.Any() != true)
            return null;

        var textProperties = model.DataProperties.Where(p => p.IsFullTextIndexed).ToList();
        if (!textProperties.Any())
            return null;

        var textConditions = new List<string>();
        foreach (var textProperty in textProperties)
        {
            var keywordConditions = keywords.Select(keyword =>
                $"LOWER({textProperty.StorageName}) LIKE LOWER('%{keyword.Replace("'", "''")}%')");
            textConditions.Add($"({string.Join(" OR ", keywordConditions)})");
        }

        return $"({string.Join(" OR ", textConditions)})";
    }

    /// <summary>
    /// Formats a vector for SQL queries.
    /// </summary>
    /// <param name="vector">The vector to format.</param>
    /// <returns>Formatted vector string.</returns>
    public static string FormatVectorForSql(ReadOnlyMemory<float> vector)
    {
        var vectorArray = vector.ToArray();
        var vectorString = string.Join(",", vectorArray.Select(f => f.ToString("F6", System.Globalization.CultureInfo.InvariantCulture)));
        return $"[{vectorString}]";
    }

    /// <summary>
    /// Builds the WHERE clause for index creation if specified.
    /// </summary>
    /// <param name="options">The query collection options.</param>
    /// <returns>WHERE clause string or empty string if not specified.</returns>
    public static string BuildIndexWhereClause(CouchbaseQueryCollectionOptions options)
    {
        return !string.IsNullOrEmpty(options.IndexWhereClause)
            ? $"WHERE {options.IndexWhereClause}"
            : "";
    }

    /// <summary>
    /// Maps similarity metric strings to Couchbase values.
    /// </summary>
    public static string MapSimilarityMetric(string? similarityMetric)
    {
        return similarityMetric?.ToUpperInvariant() switch
        {
            "COSINE" => "cosine",
            "DOT_PRODUCT" => "dot",
            "EUCLIDEAN" or "L2" => "l2",
            "L2_SQUARED" or "EUCLIDEAN_SQUARED" => "l2_squared",
            _ => "dot"
        };
    }
}