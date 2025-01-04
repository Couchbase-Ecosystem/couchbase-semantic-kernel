using Couchbase.Search;
using Couchbase.Search.Queries.Compound;
using Couchbase.Search.Queries.Range;
using Couchbase.Search.Queries.Simple;
using Microsoft.Extensions.VectorData;

namespace Couchbase.SemanticKernel.Connectors.Couchbase;

/// <summary>
/// Contains mapping helpers to use when searching for documents using Couchbase.
/// </summary>
internal static class CouchbaseVectorStoreCollectionSearchMapping
{
    /// <summary>
    /// Build Couchbase filter <see cref="ISearchQuery"/> from the provided <see cref="VectorSearchFilter"/>.
    /// </summary>
    /// <param name="filter"></param>
    /// <param name="propertyMappings"></param>
    /// <returns>The Couchbase filter queries</returns>
    /// <exception cref="InvalidOperationException">Thrown when property name specified in filter doesn't exist.</exception>
    /// <exception cref="NotSupportedException">Thrown when the provided filter type is unsupported.</exception>
    public static ISearchQuery? BuildFilter(VectorSearchFilter? filter, Dictionary<string, string> propertyMappings)
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
                   if (propertyMappings.TryGetValue(equalToClause.FieldName, out var mappedField))
                   {
                       var query = CreateQueryForType(mappedField, equalToClause.Value);
                       mustQueries.Add(query);
                   }
                   else
                   {
                       throw new InvalidOperationException($"Invalid filter field: {equalToClause.FieldName}");
                   }
                   break;

               case AnyTagEqualToFilterClause anyTagClause:
                   if (propertyMappings.TryGetValue(anyTagClause.FieldName, out var tagField))
                   {
                       foreach (var value in anyTagClause.Value)
                       {
                           var query = CreateQueryForType(tagField, value);
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
           string stringValue => new TermQuery(stringValue).Field(field),
           int intValue => new NumericRangeQuery()
               .Field(field)
               .Min(intValue)
               .Max(intValue),
           float floatValue => new NumericRangeQuery()
               .Field(field)
               .Min(floatValue)
               .Max(floatValue),
           bool boolValue => new BooleanFieldQuery(boolValue).Field(field),
           _ => throw new NotSupportedException($"Unsupported data type: {value.GetType().Name}")
       };
   }
}