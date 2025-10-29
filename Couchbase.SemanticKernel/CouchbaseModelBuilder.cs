using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Text.Json.Serialization;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.VectorData;
using Microsoft.Extensions.VectorData.ProviderServices;

namespace Couchbase.SemanticKernel;

/// <summary>
/// Customized Couchbase model builder that adds specialized configuration of property storage names
/// and validation for Couchbase-supported data types.
/// </summary>
internal class CouchbaseModelBuilder : CollectionModelBuilder
{
    internal const string SupportedVectorTypes =
        "ReadOnlyMemory<float>, " +
        "IEnumerable<float>, " +
        "IReadOnlyCollection<float>, " +
        "ICollection<float>, " +
        "IReadOnlyList<float>, " +
        "IList<float>, " +
        "Embedding<float>, " +
        "or float[]";

    private static readonly CollectionModelBuildingOptions s_validationOptions = new()
    {
        RequiresAtLeastOneVector = false,
        SupportsMultipleKeys = false,
        SupportsMultipleVectors = true,
        UsesExternalSerializer = true,
    };

    /// <summary>
    /// Initializes a new instance of the <see cref="CouchbaseModelBuilder"/> class.
    /// </summary>
    public CouchbaseModelBuilder() : base(s_validationOptions)
    {
    }

    /// <summary>
    /// Builds a dynamic collection model from the provided definition.
    /// </summary>
    /// <param name="definition">The collection definition.</param>
    /// <param name="defaultEmbeddingGenerator">The default embedding generator.</param>
    /// <returns>The built collection model.</returns>
    public override CollectionModel BuildDynamic(VectorStoreCollectionDefinition definition, IEmbeddingGenerator? defaultEmbeddingGenerator)
    {
        return base.BuildDynamic(definition, defaultEmbeddingGenerator);
    }

    /// <summary>
    /// Process the properties of the given type and customize storage names based on JsonPropertyName attributes.
    /// </summary>
    /// <param name="type">The type to process.</param>
    /// <param name="definition">The collection definition.</param>
    [RequiresUnreferencedCode("Traverses the CLR type's properties with reflection, so not compatible with trimming")]
    protected override void ProcessTypeProperties(Type type, VectorStoreCollectionDefinition? definition)
    {
        base.ProcessTypeProperties(type, definition);

        // Customize storage names based on JsonPropertyName attributes
        foreach (var property in this.Properties)
        {
            if (property.PropertyInfo?.GetCustomAttribute<JsonPropertyNameAttribute>() is { } jsonPropertyNameAttribute)
            {
                property.StorageName = jsonPropertyNameAttribute.Name;
            }
        }
    }

    /// <summary>
    /// Validates whether the given type is supported as a key property type.
    /// </summary>
    /// <param name="type">The type to validate.</param>
    /// <param name="supportedTypes">The supported types if validation fails.</param>
    /// <returns>True if the type is supported, false otherwise.</returns>
    protected override bool IsKeyPropertyTypeValid(Type type, [NotNullWhen(false)] out string? supportedTypes)
    {
        supportedTypes = "string";

        // Couchbase primarily supports string keys for document IDs
        return type == typeof(string);
    }

    /// <summary>
    /// Validates whether the given type is supported as a data property type.
    /// </summary>
    /// <param name="type">The type to validate.</param>
    /// <param name="supportedTypes">The supported types if validation fails.</param>
    /// <returns>True if the type is supported, false otherwise.</returns>
    protected override bool IsDataPropertyTypeValid(Type type, [NotNullWhen(false)] out string? supportedTypes)
    {
        supportedTypes = "string, int, long, double, float, bool, DateTime, DateTimeOffset, Guid, or arrays/lists of these types";

        if (Nullable.GetUnderlyingType(type) is Type underlyingType)
        {
            type = underlyingType;
        }

        return IsValidDataType(type)
            || (type.IsArray && IsValidDataType(type.GetElementType()!))
            || (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(List<>) && IsValidDataType(type.GenericTypeArguments[0]));

        static bool IsValidDataType(Type type)
            => type == typeof(bool) ||
                type == typeof(string) ||
                type == typeof(int) ||
                type == typeof(long) ||
                type == typeof(float) ||
                type == typeof(double) ||
                type == typeof(decimal) ||
                type == typeof(DateTime) ||
                type == typeof(DateTimeOffset) ||
                type == typeof(Guid);
    }

    /// <summary>
    /// Validates whether the given type is supported as a vector property type.
    /// </summary>
    /// <param name="type">The type to validate.</param>
    /// <param name="supportedTypes">The supported types if validation fails.</param>
    /// <returns>True if the type is supported, false otherwise.</returns>
    protected override bool IsVectorPropertyTypeValid(Type type, [NotNullWhen(false)] out string? supportedTypes)
        => IsVectorPropertyTypeValidCore(type, out supportedTypes);

    /// <summary>
    /// Internal method for validating vector property types.
    /// </summary>
    /// <param name="type">The type to validate.</param>
    /// <param name="supportedTypes">The supported types if validation fails.</param>
    /// <returns>True if the type is supported, false otherwise.</returns>
    internal static bool IsVectorPropertyTypeValidCore(Type type, [NotNullWhen(false)] out string? supportedTypes)
    {
        supportedTypes = SupportedVectorTypes;

        return type == typeof(ReadOnlyMemory<float>)
            || type == typeof(ReadOnlyMemory<float>?)
            || type == typeof(IEnumerable<float>)
            || type == typeof(IReadOnlyCollection<float>)
            || type == typeof(ICollection<float>)
            || type == typeof(IReadOnlyList<float>)
            || type == typeof(IList<float>)
            || type == typeof(Embedding<float>)
            || type == typeof(float[]);
    }
}