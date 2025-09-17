using Couchbase.Search;
using Couchbase.Search.Queries.Compound;
using Couchbase.Search.Queries.Range;
using Couchbase.Search.Queries.Simple;
using Microsoft.Extensions.VectorData;
using Microsoft.Extensions.VectorData.ProviderServices;

namespace Couchbase.SemanticKernel;

/// <summary>
/// Contains mapping helpers to use when searching for documents using Couchbase.
/// </summary>
internal static class CouchbaseCollectionSearchMapping
{
#pragma warning disable CS0618 // Type or member is obsolete

    /// <summary>
    /// Build Couchbase filter <see cref="ISearchQuery"/> from the provided <see cref="VectorSearchFilter"/>.
    /// </summary>
    /// <param name="filter">The legacy filter to convert.</param>
    /// <param name="model">The collection model containing property information.</param>
    /// <returns>The Couchbase filter queries</returns>
    /// <exception cref="InvalidOperationException">Thrown when property name specified in filter doesn't exist.</exception>
    /// <exception cref="NotSupportedException">Thrown when the provided filter type is unsupported.</exception>
    public static ISearchQuery? BuildFromLegacyFilter(VectorSearchFilter? filter, CollectionModel model)
    {
        if (filter?.FilterClauses is null)
        {
            return null;
        }

        var mustQueries = new List<ISearchQuery>();
        var shouldQueries = new List<ISearchQuery>();

        foreach (var clause in filter.FilterClauses)
        {
            switch (clause)
            {
                case EqualToFilterClause equalToClause:
                    if (model.PropertyMap.TryGetValue(equalToClause.FieldName, out var propertyModel))
                    {
                        var query = CreateQueryForType(propertyModel.StorageName!, equalToClause.Value);
                        mustQueries.Add(query);
                    }
                    else
                    {
                        throw new InvalidOperationException($"Invalid filter field: {equalToClause.FieldName}");
                    }
                    break;

                case AnyTagEqualToFilterClause anyTagClause:
                    if (model.PropertyMap.TryGetValue(anyTagClause.FieldName, out var tagPropertyModel))
                    {
                        foreach (var value in anyTagClause.Value)
                        {
                            var query = CreateQueryForType(tagPropertyModel.StorageName!, value);
                            shouldQueries.Add(query);
                        }
                    }
                    else
                    {
                        throw new InvalidOperationException($"Invalid filter field: {anyTagClause.FieldName}");
                    }
                    break;

                default:
                    throw new NotSupportedException($"Unsupported filter type: {clause.GetType().Name}");
            }
        }

        // Return null if no queries were created
        if (!mustQueries.Any() && !shouldQueries.Any())
        {
            return null;
        }

        // If we only have must queries, return a conjunction query
        if (mustQueries.Any() && !shouldQueries.Any())
        {
            return new ConjunctionQuery(mustQueries.ToArray());
        }

        // If we only have should queries, return a disjunction query
        if (!mustQueries.Any() && shouldQueries.Any())
        {
            return new DisjunctionQuery(shouldQueries.ToArray());
        }

        // If we have both, create a boolean query
        var booleanQuery = new BooleanQuery();

        if (mustQueries.Any())
        {
            booleanQuery.Must(new ConjunctionQuery(mustQueries.ToArray()));
        }

        if (shouldQueries.Any())
        {
            booleanQuery.Should(new DisjunctionQuery(shouldQueries.ToArray()));
        }

        return booleanQuery;
    }

    /// <summary>
    /// Creates a search query for the specified field and value.
    /// </summary>
    /// <param name="field">The field to query.</param>
    /// <param name="value">The value to match.</param>
    /// <returns>A search query based on the value type.</returns>
    /// <exception cref="NotSupportedException">Thrown if the value type is unsupported.</exception>
    private static ISearchQuery CreateQueryForType(string field, object value)
    {
        return value switch
        {
            null => throw new ArgumentNullException(nameof(value)),
            string stringValue => new TermQuery(stringValue).Field(field),
            int intValue => new NumericRangeQuery()
                .Field(field)
                .Min(intValue)
                .Max(intValue),
            long longValue => new NumericRangeQuery()
                .Field(field)
                .Min(longValue)
                .Max(longValue),
            float floatValue => new NumericRangeQuery()
                .Field(field)
                .Min(floatValue)
                .Max(floatValue),
            double doubleValue => new NumericRangeQuery()
                .Field(field)
                .Min(doubleValue)
                .Max(doubleValue),
            bool boolValue => new BooleanFieldQuery(boolValue).Field(field),
            _ => throw new NotSupportedException($"Unsupported data type: {value.GetType().Name}")
        };
    }
    
#pragma warning restore CS0618 // Type or member is obsolete

}