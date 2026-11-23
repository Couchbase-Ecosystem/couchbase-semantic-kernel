using System;
using System.Linq;
using System.Text;
using Microsoft.Extensions.VectorData.ProviderServices;
using Newtonsoft.Json.Linq;

namespace Couchbase.VectorData;

/// <summary>
/// Shapes SQL++ queries issued by <see cref="CouchbaseQueryCollection{TKey,TRecord}"/> and converts
/// their result rows back into the storage document shape that <see cref="ICouchbaseMapper{TRecord}"/> expects.
/// </summary>
/// <remarks>
/// Every SQL++ read selects <c>META().id AS _id</c> plus the model's fields, and may add other
/// underscore-prefixed metadata aliases (for example <c>_distance</c>). This class owns both sides of
/// that contract so the SELECT list and the row decoding cannot drift apart.
/// </remarks>
internal static class CouchbaseQueryRowMapping
{
    /// <summary>The alias used for the document key in SQL++ SELECT lists.</summary>
    public const string IdAlias = "_id";

    /// <summary>
    /// Builds the SELECT list for reading records: the document key followed by every data property
    /// and, when requested, every vector property. Field names are backtick-escaped so SQL++ reserved
    /// words (e.g. Number, Type, Order) are valid storage names.
    /// </summary>
    /// <param name="model">The collection model.</param>
    /// <param name="includeVectors">Whether vector properties should be selected.</param>
    public static string BuildSelectList(CollectionModel model, bool includeVectors)
    {
        var fields = model.DataProperties.Select(p => $"`{p.StorageName}`");

        if (includeVectors)
        {
            fields = fields.Concat(model.VectorProperties.Select(p => $"`{p.StorageName}`"));
        }

        return string.Join(", ", new[] { $"META().id AS {IdAlias}" }.Concat(fields));
    }

    /// <summary>
    /// Casts a row returned by <c>IScope.QueryAsync&lt;dynamic&gt;</c> to a <see cref="JObject"/>.
    /// </summary>
    /// <exception cref="InvalidOperationException">The row is not a JSON object.</exception>
    public static JObject AsJObject(object? row)
        => row as JObject ?? throw new InvalidOperationException("Query result row is not a JObject.");

    /// <summary>
    /// Converts a SQL++ result row into the UTF-8 JSON storage document expected by the record mapper:
    /// underscore-prefixed metadata aliases are dropped and the document key is written back under the
    /// model's key storage name (Couchbase stores the key outside the document body).
    /// </summary>
    /// <param name="row">The result row.</param>
    /// <param name="keyStorageName">The storage name of the model's key property.</param>
    /// <returns>The storage document, or <see langword="null"/> when the row has no document key.</returns>
    public static byte[]? ToStorageDocument(JObject row, string keyStorageName)
    {
        var docId = row[IdAlias]?.Value<string>();
        if (string.IsNullOrEmpty(docId))
        {
            return null;
        }

        var document = new JObject();
        foreach (var property in row.Properties())
        {
            if (!property.Name.StartsWith("_", StringComparison.Ordinal))
            {
                document[property.Name] = property.Value;
            }
        }

        document[keyStorageName] = docId;

        return Encoding.UTF8.GetBytes(document.ToString(Newtonsoft.Json.Formatting.None));
    }
}
