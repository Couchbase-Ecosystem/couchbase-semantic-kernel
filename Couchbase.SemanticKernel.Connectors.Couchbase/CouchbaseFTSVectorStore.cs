using System.Runtime.CompilerServices;
using Connectors.Memory.Couchbase;
using Connectors.Memory.Couchbase.Diagnostics;
using Couchbase.KeyValue;
using Couchbase.Management.Collections;
using Microsoft.Extensions.VectorData;

namespace Couchbase.SemanticKernel.Connectors.Couchbase;

/// <summary>
/// Class for accessing the list of collections in a Couchbase vector store.
/// </summary>
/// <remarks>
/// This class can be used with collections of any schema type but requires you to provide schema information when getting a collection.
/// </remarks>
public sealed class CouchbaseFtsVectorStore : IVectorStore
{
    /// <summary><see cref="IScope"/> that can be used to manage the collections in Couchbase.</summary>
    private readonly IScope _scope;

    /// <summary>Optional configuration options for this class.</summary>
    private readonly CouchbaseVectorStoreOptions _options;

    /// <summary>
    /// Initializes a new instance of the <see cref="CouchbaseFtsVectorStore"/> class.
    /// </summary>
    /// <param name="scope"><see cref="IScope"/> that can be used to manage the collections in Couchbase.</param>
    /// <param name="options">Optional configuration options for this class.</param>
    public CouchbaseFtsVectorStore(IScope scope, CouchbaseVectorStoreOptions? options = null)
    {
        Verify.NotNull(scope);

        this._scope = scope;
        this._options = options ?? new CouchbaseVectorStoreOptions();
    }

    /// <inheritdoc />
    public IVectorStoreRecordCollection<TKey, TRecord> GetCollection<TKey, TRecord>(
        string name,
        VectorStoreRecordDefinition? vectorStoreRecordDefinition = null)
        where TKey : notnull
    {
        if (this._options.VectorStoreCollectionFactory is not null)
        {
            return this._options.VectorStoreCollectionFactory.CreateVectorStoreRecordCollection<TKey, TRecord>(
                this._scope,
                name,
                vectorStoreRecordDefinition);
        }

        if (typeof(TKey) != typeof(string))
        {
            throw new NotSupportedException("Only string keys are supported.");
        }

        var recordCollection = new CouchbaseVectorStoreRecordCollection<TRecord>(
            this._scope,
            name,
            new() { VectorStoreRecordDefinition = vectorStoreRecordDefinition }) as IVectorStoreRecordCollection<TKey, TRecord>;

        return recordCollection!;
    }

    /// <inheritdoc />
    public async IAsyncEnumerable<string> ListCollectionNamesAsync([EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        // Create options and pass the cancellation token
        var options = new GetAllScopesOptions()
            .CancellationToken(cancellationToken);

        // Get all scopes in the bucket
        var scopes = await _scope.Bucket.Collections.GetAllScopesAsync(options).ConfigureAwait(false);

        // Find the current scope and yield its collection names
        foreach (var scope in scopes)
        {
            if (scope.Name == _scope.Name)
            {
                foreach (var collection in scope.Collections)
                {
                    yield return collection.Name;
                }
            }
        }
    }
}
