# Releasing AsyncMonolith

Releases publish two packages, `AsyncMonolith` and `AsyncMonolith.TestHelpers`, at the same version, using
[NuGet trusted publishing](https://learn.microsoft.com/en-us/nuget/nuget-org/trusted-publishing): no API key is
stored anywhere. The [release workflow](.github/workflows/release.yml) does the work.

## One-off setup

- **nuget.org:** a trusted publishing policy for repository `Timmoth/AsyncMonolith`, workflow file `release.yml`,
  covering `AsyncMonolith*`. If the policy names an environment, it must be `release`.
- **GitHub secret `NUGET_USER`:** the nuget.org profile name (not the email address) that owns the policy.
- **GitHub environment `release`:** created on first use. Add required reviewers to it if releases should need an
  approval before publishing.
- The old `NUGET_API_KEY` secret is no longer used and can be deleted.

## Versioning

AsyncMonolith follows [Semantic Versioning](https://semver.org) from 11.0.0: breaking changes bump the major
version. The version lives in one place, `<VersionPrefix>` in [Directory.Build.props](Directory.Build.props).

## Each release

1. On `main`, set `<VersionPrefix>` to the new version.
2. Add a `## <version>` section to [docs/releases.md](docs/releases.md). It becomes the GitHub release notes, so
   say what changed, what breaks, and whether a migration is needed. Add a row to the table at the top.
3. Run the workflow manually ("Run workflow" on the Release workflow) for a dry run: it runs the full test suite,
   checks the release notes and packs, but doesn't publish. Download the `packages` artifact to inspect them.
4. Tag and push:

   ```bash
   git tag v11.0.0
   ```

   ```bash
   git push origin v11.0.0
   ```

   The tag must match `<VersionPrefix>`, or the workflow stops. It then runs the full test suite (including the
   integration tests against PostgreSQL, SQL Server, MySQL and MariaDB), packs, exchanges GitHub's OIDC token for
   a one-hour nuget.org key, pushes both packages and their symbols, and creates the GitHub release with the
   packages attached.
5. After the first release of a new major version, set `PackageValidationBaselineVersion` in
   [Directory.Build.targets](Directory.Build.targets) to it, so later releases are checked for accidental breaking
   changes.

If a push fails partway, rerun the failed job: pushing uses `--skip-duplicate`, so packages already published are
left alone.

## After 11.0.0

`AsyncMonolith.Ef`, `AsyncMonolith.PostgreSql`, `AsyncMonolith.MsSql`, `AsyncMonolith.MySql` and
`AsyncMonolith.MariaDb` are replaced by `AsyncMonolith`. On nuget.org, mark each as deprecated ("Legacy") with
`AsyncMonolith` as the alternate package, so their users are pointed at the upgrade.
