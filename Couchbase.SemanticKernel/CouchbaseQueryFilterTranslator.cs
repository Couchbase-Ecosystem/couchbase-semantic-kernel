using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using Microsoft.Extensions.VectorData;
using Microsoft.Extensions.VectorData.ProviderServices;
using Microsoft.Extensions.VectorData.ProviderServices.Filter;

namespace Couchbase.SemanticKernel;

/// <summary>
/// Translates vector store filters into SQL++ WHERE clause strings for Query-based vector search (BHIVE/COMPOSITE).
/// </summary>
internal sealed class CouchbaseQueryFilterTranslator
{
    private CollectionModel _model = null!;
    private ParameterExpression _recordParameter = null!;

    public string? Translate<TRecord>(Expression<Func<TRecord, bool>> filter, CollectionModel model)
    {
        if (filter == null)
        {
            return null;
        }

        _model = model;
        Debug.Assert(filter.Parameters.Count == 1);
        _recordParameter = filter.Parameters[0];

        var preprocessor = new FilterTranslationPreprocessor { SupportsParameterization = false };
        var body = preprocessor.Visit(filter.Body);
        return TranslateNode(body);
    }

    private string TranslateNode(Expression? node)
    {
        switch (node)
        {
            case null:
                throw new NotSupportedException("Null expression not supported in SQL filter.");

            case BinaryExpression be when be.NodeType == ExpressionType.Equal:
                return TranslateEquality(be.Left, be.Right, negated: false);
            case BinaryExpression be when be.NodeType == ExpressionType.NotEqual:
                return TranslateEquality(be.Left, be.Right, negated: true);

            case BinaryExpression be when be.NodeType is ExpressionType.GreaterThan or ExpressionType.GreaterThanOrEqual or ExpressionType.LessThan or ExpressionType.LessThanOrEqual:
                return TranslateComparison(be);

            case BinaryExpression be when be.NodeType == ExpressionType.AndAlso:
                return $"({TranslateNode(be.Left)} AND {TranslateNode(be.Right)})";
            case BinaryExpression be when be.NodeType == ExpressionType.OrElse:
                return $"({TranslateNode(be.Left)} OR {TranslateNode(be.Right)})";

            case UnaryExpression ue when ue.NodeType == ExpressionType.Not:
                return $"(NOT {TranslateNode(ue.Operand)})";

            // Handle converting non-nullable to nullable
            case UnaryExpression ue when ue.NodeType == ExpressionType.Convert && Nullable.GetUnderlyingType(ue.Type) == ue.Operand.Type:
                return TranslateNode(ue.Operand);

            // Special handling for bool property alone (r => r.Bool)
            case Expression e when e.Type == typeof(bool) && TryBindProperty(e, out var prop):
                return GenerateEquality(prop.StorageName!, true);

            case MethodCallExpression mc:
                return TranslateMethodCall(mc);
        }

        throw new NotSupportedException($"Unsupported expression node for SQL filter: {node.NodeType}");
    }

    private string TranslateEquality(Expression left, Expression right, bool negated)
    {
        if (TryBindProperty(left, out var property) && right is ConstantExpression { Value: var rightValue })
        {
            return GenerateEquality(property.StorageName!, rightValue, negated);
        }
        if (TryBindProperty(right, out property) && left is ConstantExpression { Value: var leftValue })
        {
            return GenerateEquality(property.StorageName!, leftValue, negated);
        }
        throw new NotSupportedException("Invalid equality/comparison in SQL filter.");
    }

    private string GenerateEquality(string storageName, object? value, bool negated = false)
    {
        if (value is null)
        {
            return negated ? $"({storageName} IS NOT NULL)" : $"({storageName} IS NULL)";
        }
        return negated
            ? $"({storageName} != {ToSqlLiteral(value)})"
            : $"({storageName} = {ToSqlLiteral(value)})";
    }

    private string TranslateComparison(BinaryExpression comparison)
    {
        if (!TryBindProperty(comparison.Left, out var property) || comparison.Right is not ConstantExpression { Value: var constant })
        {
            throw new NotSupportedException("Invalid comparison in SQL filter.");
        }

        var op = comparison.NodeType switch
        {
            ExpressionType.GreaterThan => ">",
            ExpressionType.GreaterThanOrEqual => ">=",
            ExpressionType.LessThan => "<",
            ExpressionType.LessThanOrEqual => "<=",
            _ => throw new NotSupportedException($"Unsupported comparison: {comparison.NodeType}")
        };

        return $"({property.StorageName} {op} {ToSqlLiteral(constant)})";
    }

    private string TranslateMethodCall(MethodCallExpression methodCall)
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

        throw new NotSupportedException($"Unsupported method for SQL filter: {methodCall.Method.DeclaringType?.Name}.{methodCall.Method.Name}.");
    }

    private string TranslateContains(Expression source, Expression item)
    {
        // Only support inline enumerables on the left and a bound property on the right
        if (TryExtractConstantEnumerable(source, out var elements) && TryBindProperty(item, out var property))
        {
            var literals = elements.Select(ToSqlLiteral).ToArray();
            return $"({property.StorageName} IN [{string.Join(", ", literals)}])";
        }

        // Or support property IN inline array when property is left and constant enumerable is right
        if (TryBindProperty(source, out property) && TryExtractConstantEnumerable(item, out elements))
        {
            var literals = elements.Select(ToSqlLiteral).ToArray();
            return $"({property.StorageName} IN [{string.Join(", ", literals)}])";
        }

        throw new NotSupportedException("Unsupported Contains usage in SQL filter.");
    }

    private static bool TryExtractConstantEnumerable(Expression expr, out IEnumerable<object?> elements)
    {
        if (expr is ConstantExpression { Value: IEnumerable enumerable })
        {
            var list = new List<object?>();
            foreach (var e in enumerable)
            {
                list.Add(e);
            }
            elements = list;
            return true;
        }
        elements = Array.Empty<object?>();
        return false;
    }

    private static string ToSqlLiteral(object? value)
    {
        if (value is null)
        {
            return "NULL";
        }

        switch (value)
        {
            case string s:
                return "'" + s.Replace("'", "''") + "'";
            case char c:
                return "'" + (c == '\'' ? "''" : c.ToString()) + "'";
            case bool b:
                return b ? "TRUE" : "FALSE";
            case sbyte or byte or short or ushort or int or uint or long or ulong:
                return Convert.ToString(value, CultureInfo.InvariantCulture)!;
            case float or double or decimal:
                return Convert.ToString(value, CultureInfo.InvariantCulture)!;
            case Guid g:
                return "'" + g.ToString() + "'";
            case DateTime dt:
                // ISO 8601
                return "'" + dt.ToString("o", CultureInfo.InvariantCulture) + "'";
            case DateTimeOffset dto:
                return "'" + dto.ToString("o", CultureInfo.InvariantCulture) + "'";
            default:
                return "'" + value.ToString()?.Replace("'", "''") + "'";
        }
    }

    private bool TryBindProperty(Expression expression, out PropertyModel? property)
    {
        var unwrapped = expression;
        while (unwrapped is UnaryExpression { NodeType: ExpressionType.Convert } convert)
        {
            unwrapped = convert.Operand;
        }

        string? modelName = null;

        if (unwrapped is MemberExpression memberExpression && memberExpression.Expression == _recordParameter)
        {
            modelName = memberExpression.Member.Name;
        }
        else if (unwrapped is MethodCallExpression methodCall &&
                 methodCall.Method.Name == "get_Item" &&
                 methodCall.Method.DeclaringType == typeof(Dictionary<string, object?>) &&
                 methodCall.Object == _recordParameter &&
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

        if (!_model.PropertyMap.TryGetValue(modelName, out property))
        {
            throw new InvalidOperationException($"Invalid filter field '{modelName}'.");
        }

        return true;
    }
}