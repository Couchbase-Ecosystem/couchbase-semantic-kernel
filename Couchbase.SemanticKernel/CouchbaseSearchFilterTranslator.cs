using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Linq.Expressions;
using Couchbase.Search;
using Couchbase.Search.Queries.Compound;
using Couchbase.Search.Queries.Range;
using Couchbase.Search.Queries.Simple;
using Microsoft.Extensions.VectorData.ProviderServices;
using Microsoft.Extensions.VectorData.ProviderServices.Filter;

namespace Couchbase.SemanticKernel;

/// <summary>
/// Translates vector store filter expressions to Couchbase search queries.
/// </summary>
internal class CouchbaseSearchFilterTranslator
{
    private CollectionModel _model = null!;
    private ParameterExpression _recordParameter = null!;

    /// <summary>
    /// Translates a lambda expression filter to a Couchbase search query.
    /// </summary>
    /// <typeparam name="TRecord">The record type.</typeparam>
    /// <param name="filter">The filter expression to translate.</param>
    /// <param name="model">The collection model containing property information.</param>
    /// <returns>A Couchbase search query, or null if no filter is provided.</returns>
    public ISearchQuery? Translate<TRecord>(Expression<Func<TRecord, bool>> filter, CollectionModel model)
    {
        if (filter == null)
        {
            return null;
        }

        _model = model;

        Debug.Assert(filter.Parameters.Count == 1);
        _recordParameter = filter.Parameters[0];

        var preprocessor = new FilterTranslationPreprocessor { SupportsParameterization = false };
        var preprocessedExpression = preprocessor.Visit(filter.Body);

        return Translate(preprocessedExpression);
    }

    private ISearchQuery Translate(Expression? node)
    {
        return node switch
        {
            BinaryExpression { NodeType: ExpressionType.Equal } equal => TranslateEqual(equal.Left, equal.Right),
            BinaryExpression { NodeType: ExpressionType.NotEqual } notEqual => TranslateEqual(notEqual.Left, notEqual.Right, negated: true),

            BinaryExpression
            {
                NodeType: ExpressionType.GreaterThan or ExpressionType.GreaterThanOrEqual or ExpressionType.LessThan or ExpressionType.LessThanOrEqual
            } comparison => TranslateComparison(comparison),

            BinaryExpression { NodeType: ExpressionType.AndAlso } andAlso => TranslateAndAlso(andAlso.Left, andAlso.Right),
            BinaryExpression { NodeType: ExpressionType.OrElse } orElse => TranslateOrElse(orElse.Left, orElse.Right),
            UnaryExpression { NodeType: ExpressionType.Not } not => TranslateNot(not.Operand),

            // Handle converting non-nullable to nullable
            UnaryExpression { NodeType: ExpressionType.Convert } convert when Nullable.GetUnderlyingType(convert.Type) == convert.Operand.Type
                => Translate(convert.Operand),

            // Special handling for bool constant as the filter expression (r => r.Bool)
            { } when node.Type == typeof(bool) && TryBindProperty(node, out var property) => GenerateEqual(property.StorageName, value: true),

            MethodCallExpression methodCall => TranslateMethodCall(methodCall),

#pragma warning disable CA1508
            _ => throw new NotSupportedException("Couchbase does not support the following NodeType in filters: " + node?.NodeType)
#pragma warning restore CA1508
        };
    }

    private ISearchQuery TranslateEqual(Expression left, Expression right, bool negated = false)
    {
        return TryBindProperty(left, out var property) && right is ConstantExpression { Value: var rightConstant }
            ? GenerateEqual(property.StorageName, rightConstant, negated)
            : TryBindProperty(right, out property) && left is ConstantExpression { Value: var leftConstant }
                ? GenerateEqual(property.StorageName, leftConstant, negated)
                : throw new NotSupportedException("Invalid equality/comparison.");
    }

    private static ISearchQuery GenerateEqual(string propertyStorageName, object? value, bool negated = false)
    {
        ISearchQuery coreQuery;

        if (value is null)
        {
            // For null checks, we need to create a query that matches documents where the field doesn't exist
            // In Couchbase FTS, we can use a wildcard query and then negate it
            coreQuery = new WildcardQuery("*").Field(propertyStorageName);
            // Since we want null (non-existent), we need to negate this
            negated = !negated;
        }
        else
        {
            //TODO: Check
            coreQuery = new TermQuery(value.ToString()).Field(propertyStorageName);
        }

        return negated
            ? new BooleanQuery().MustNot(coreQuery)
            : coreQuery;
    }

    private ISearchQuery TranslateComparison(BinaryExpression comparison)
    {
        return TryBindProperty(comparison.Left, out var property) && comparison.Right is ConstantExpression { Value: var rightConstant }
            ? GenerateComparison(comparison.NodeType, property.StorageName, rightConstant)
            : TryBindProperty(comparison.Right, out property) && comparison.Left is ConstantExpression { Value: var leftConstant }
                ? GenerateComparison(comparison.NodeType, property.StorageName, leftConstant)
                : throw new NotSupportedException("Comparison expression not supported by Couchbase.");
    }

    private static ISearchQuery GenerateComparison(ExpressionType nodeType, string propertyStorageName, object? value)
    {
        if (value == null)
        {
            throw new NotSupportedException("Cannot perform range comparison with null value.");
        }

        return nodeType switch
        {
            ExpressionType.GreaterThan => new NumericRangeQuery().Min((double)value, false).Field(propertyStorageName),
            ExpressionType.GreaterThanOrEqual => new NumericRangeQuery().Min((double)value, true).Field(propertyStorageName),
            ExpressionType.LessThan => new NumericRangeQuery().Max((double)value, false).Field(propertyStorageName),
            ExpressionType.LessThanOrEqual => new NumericRangeQuery().Max((double)value, true).Field(propertyStorageName),
            _ => throw new InvalidOperationException("Unreachable")
        };
    }

    private ISearchQuery TranslateAndAlso(Expression left, Expression right)
    {
        var leftFilter = Translate(left);
        var rightFilter = Translate(right);

        return new BooleanQuery()
            .Must(leftFilter)
            .Must(rightFilter);
    }

    private ISearchQuery TranslateOrElse(Expression left, Expression right)
    {
        var leftFilter = Translate(left);
        var rightFilter = Translate(right);

        return new BooleanQuery()
            .Should(leftFilter)
            .Should(rightFilter);
    }

    private ISearchQuery TranslateNot(Expression expression)
    {
        var filter = Translate(expression);

        return new BooleanQuery().MustNot(filter);
    }

    private ISearchQuery TranslateMethodCall(MethodCallExpression methodCall)
    {
        // Enumerable.Contains(source, item)
        if (methodCall.Method.Name == nameof(Enumerable.Contains) &&
            methodCall.Method.DeclaringType == typeof(Enumerable) &&
            methodCall.Arguments.Count == 2)
        {
            return TranslateContains(methodCall.Arguments[0], methodCall.Arguments[1]);
        }

        // List<T>.Contains(item)
        if (methodCall.Method.Name == nameof(Enumerable.Contains) &&
            methodCall.Method.DeclaringType?.IsGenericType == true &&
            methodCall.Method.DeclaringType.GetGenericTypeDefinition() == typeof(List<>) &&
            methodCall.Arguments.Count == 1)
        {
            return TranslateContains(methodCall.Object!, methodCall.Arguments[0]);
        }

        throw new NotSupportedException($"Unsupported method call: {methodCall.Method.DeclaringType?.Name}.{methodCall.Method.Name}.");
    }

    private ISearchQuery TranslateContains(Expression source, Expression item)
    {
        switch (source)
        {
            // Contains over field enumerable (array field contains value)
            case var _ when TryBindProperty(source, out var enumerableProperty):
                {
                    if (item is not ConstantExpression constant)
                    {
                        throw new NotSupportedException("Value must be a constant.");
                    }

                    return new TermQuery(constant.Value.ToString()).Field(enumerableProperty.StorageName);
                }

            // Contains over inline enumerable (value in array)
            case NewArrayExpression newArray:
                {
                    var elements = new object?[newArray.Expressions.Count];

                    for (var i = 0; i < newArray.Expressions.Count; i++)
                    {
                        if (newArray.Expressions[i] is not ConstantExpression { Value: var elementValue })
                        {
                            throw new NotSupportedException("Inline array elements must be constants.");
                        }

                        elements[i] = elementValue;
                    }

                    return ProcessInlineEnumerable(elements, item);
                }

            case ConstantExpression { Value: IEnumerable enumerable and not string }:
                {
                    return ProcessInlineEnumerable(enumerable, item);
                }

            default:
                throw new NotSupportedException("Unsupported Contains filter.");
        }

        ISearchQuery ProcessInlineEnumerable(IEnumerable elements, Expression item)
        {
            if (!TryBindProperty(item, out var property))
            {
                throw new NotSupportedException("Unsupported item type in Contains filter.");
            }

            // Create a boolean query with multiple should clauses (OR logic)
            var booleanQuery = new BooleanQuery();

            foreach (var element in elements)
            {
                var termQuery = new TermQuery(element.ToString()).Field(property.StorageName);
                booleanQuery.Should(termQuery);
            }

            return booleanQuery;
        }
    }

    private bool TryBindProperty(Expression expression, [NotNullWhen(true)] out PropertyModel? property)
    {
        var unwrappedExpression = expression;
        while (unwrappedExpression is UnaryExpression { NodeType: ExpressionType.Convert } convert)
        {
            unwrappedExpression = convert.Operand;
        }

        string? modelName = null;

        // Regular member access for strongly-typed POCO binding (e.g. r => r.SomeInt == 8)
        if (unwrappedExpression is MemberExpression memberExpression &&
            memberExpression.Expression == this._recordParameter)
        {
            modelName = memberExpression.Member.Name;
        }
        // Dictionary lookup for weakly-typed dynamic binding (e.g. r => r["SomeInt"] == 8)
        else if (unwrappedExpression is MethodCallExpression methodCall &&
                 methodCall.Method.Name == "get_Item" &&
                 methodCall.Method.DeclaringType == typeof(Dictionary<string, object?>) &&
                 methodCall.Object == this._recordParameter &&
                 methodCall.Arguments.Count == 1 &&
                 methodCall.Arguments[0] is ConstantExpression { Value: string keyName })
        {
            modelName = keyName;
        }

        if (modelName is null)
        {
            property = null;
            return false;
        }

        if (!this._model.PropertyMap.TryGetValue(modelName, out property))
        {
            throw new InvalidOperationException($"Property name '{modelName}' provided as part of the filter clause is not a valid property name.");
        }

        // Now that we have the property, go over all wrapping Convert nodes again to ensure that they're compatible with the property type
        unwrappedExpression = expression;
        while (unwrappedExpression is UnaryExpression { NodeType: ExpressionType.Convert } convert)
        {
            var convertType = Nullable.GetUnderlyingType(convert.Type) ?? convert.Type;
            if (convertType != property.Type && convertType != typeof(object))
            {
                throw new InvalidCastException($"Property '{property.ModelName}' is being cast to type '{convert.Type.Name}', but its configured type is '{property.Type.Name}'.");
            }

            unwrappedExpression = convert.Operand;
        }

        return true;
    }
}