# Agent instructions for `couchbase-semantic-kernel`

This repository is the Couchbase .NET Vector Store connector for Microsoft Semantic Kernel / `Microsoft.Extensions.VectorData`. Follow these repo-specific instructions in addition to the task body and any loaded Hermes skills.

## Dependency-upgrade scope

Dependency maintenance in this repo should be conservative but proactive. The goal is to stay current enough that upstream Semantic Kernel / `Microsoft.Extensions.VectorData` changes do not leave the connector broken, while avoiding broad preview-stack churn without validation.

1. Prioritize connector compatibility with the Microsoft vector-data abstractions used by this repo:
   - `Microsoft.Extensions.VectorData.Abstractions`
   - `Microsoft.Extensions.VectorData.ConformanceTests`
   - `Microsoft.Extensions.AI.Abstractions`
   - `Microsoft.Extensions.DependencyInjection.Abstractions`
   - demo-only `Microsoft.Extensions.AI.OpenAI`
2. Also check `CouchbaseNetClient`, xUnit, and `Microsoft.NET.Test.Sdk` when doing dependency work.
3. Prefer narrow, compatible version bumps. If the latest Microsoft/Semantic Kernel package breaks connector APIs, make the smallest connector update needed and document the breaking API in the PR.
4. Do not blindly chase every preview package. Explain intentionally skipped updates, especially when the skip avoids a known API break.
5. Keep `global.json` / target frameworks intentional. At the time this file was added, `global.json` pins .NET SDK `10.0.101` with `rollForward: latestFeature`; the library targets `netstandard2.0;net8.0;net10.0`, the demo targets `net8.0`, and conformance tests target `net10.0`.

## Upstream breakage watch

The user has heard that a recent latest-version update broke multiple integrations. Assume Semantic Kernel / `Microsoft.Extensions.VectorData` API changes are a likely source until proven otherwise.

When dependency updates touch the Microsoft vector-data/Semantic Kernel stack:

- Read upstream release notes or migration notes for breaking changes.
- Search the compile errors before making large rewrites; API renames and interface contract changes are common.
- Treat conformance-test compile/build failures as connector compatibility signals, not just test maintenance.
- Record which upstream package introduced the break and whether other vector-store connectors appear to have made similar fixes.

## Validation requirements

Automated checks are necessary but insufficient for dependency work.

Minimum local validation, when the required .NET SDK is available:

```bash
dotnet restore Couchbase.VectorData.sln
dotnet build Couchbase.VectorData.sln --configuration Release
dotnet test Tests.ConformanceTests/Tests.ConformanceTests.csproj --configuration Release
```

The CI workflow currently restores/builds a filtered conformance-test solution; the `dotnet test` step may be commented out. Do not treat CI build-only coverage as complete validation if local tests can run.

## README/manual validation

After dependency changes, manually test by following the README as closely as the environment permits:

1. Build the connector from source.
2. Run or adapt `CouchbaseVectorSearchDemo` against a verified Couchbase target.
3. Exercise the index modes documented in the README when the target supports them:
   - Search/FTS index path: Couchbase Server 7.6+ or Capella with Search Service enabled.
   - Hyperscale vector index path: Couchbase Server 8.0+ or Capella.
   - Composite vector index path: Couchbase Server 8.0+ or Capella.
4. Verify both basic vector-store usage and any hybrid/full-text search behavior affected by the dependency change.

The demo reads configuration from `appsettings.Development.json`, environment variables, and .NET user secrets. It requires Couchbase connection settings and an OpenAI API key/model for embeddings. Never commit real credentials or generated secret files. If OpenAI credentials or a suitable Couchbase version are unavailable, record the exact blocker and still run the nearest build/conformance validation.

## Couchbase target notes

- Verify the live Couchbase target before using it. On this machine the usual local default is `couchbase://localhost` with `Administrator` / `password`, but agents must not assume it is running or correctly initialized.
- Prefer Capella or Couchbase Server 8.0+ when validating Hyperscale/Composite vector index paths.
- If only Couchbase Server 7.6+ is available, validate the Search/FTS path and explicitly mark Hyperscale/Composite validation as blocked by server version.

## PR and task reporting

Dependency PRs should include:

- Packages updated and packages intentionally skipped.
- Any Semantic Kernel / `Microsoft.Extensions.VectorData` breaking changes encountered.
- Automated validation results with exact commands.
- Manual README/demo validation evidence, including Couchbase target/version, index mode(s), and any credential/version blockers.
- Release-process classification and any maintainer action needed.

Current owner routing note: Dex (`dex-the-ai`) is the temporary owner for dependency-maintenance routing; Dhiraj is no longer with the company.
