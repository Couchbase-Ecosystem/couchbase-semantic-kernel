using System.Text.Json;

namespace Couchbase.SemanticKernel.Connectors.Couchbase.Data;

/// <summary>
/// Contains options for <see cref="VectorStoreRecordPropertyReader"/>.
/// </summary>
public class VectorStoreRecordPropertyReaderOptions
{
    /// <summary>
    /// Gets or sets a value indicating whether the connector/db supports multiple key properties.
    /// </summary>
    public bool SupportsMultipleKeys { get; set; } = false;
    
    /// <summary>
    /// Gets or sets a value indicating whether the connector/db supports multiple vector properties.
    /// </summary>
    public bool SupportsMultipleVectors { get; set; } = true;

    /// <summary>
    /// Gets or sets a value indicating whether the connector/db requires at least one vector property.
    /// </summary>
    public bool RequiresAtLeastOneVector { get; set; } = false;

    /// <summary>
    /// Gets or sets the json serializer options that the connector might be using for storage serialization.
    /// </summary>
    public JsonSerializerOptions? JsonSerializerOptions { get; set; }

}