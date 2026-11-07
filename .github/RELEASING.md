# CI and release operation

Three workflows run here:

- `validate.yml` on every pull request to `main` and every push to `main`: `lint` (`just lint-workflows`),
  `qualify` (`just qualify linux-x64`) and `release-controls` (`just release-controls`), run directly on a Linux
  x64 runner with the SDK from `global.json`. It has read-only repository access and no secrets.
- `release-please.yml` on pushes to `main`: keeps a release pull request that sets `version.txt` and `CHANGELOG.md`
  to the next version, and creates the GitHub release with its `vX.Y.Z` tag when that pull request is merged.
- `release.yml` on a published, non-draft, non-prerelease GitHub release: checks the tag, its commit, `main`
  ancestry, `version.txt` and a clean checkout; builds the three packages with `just release`, qualifies those
  archives with the release clone's own lanes, attaches them with their records and `SHA256SUMS` to the release,
  then pushes them to nuget.org with Trusted Publishing.

Release automation (`release-please.yml` and `release.yml`) runs only while the repository variable
`REZULTAT_RELEASE_AUTOMATION_ENABLED` is `true`. Validation always runs. The `canonical` job of `release.yml` refuses
NuGet publication in any repository other than `one-man-wolf-pack/Rezultat`, whatever that variable says.

## Settings the workflows need

| Setting | Value |
| --- | --- |
| GitHub App | The organization's release App installed on this repository with Contents, Pull requests and Issues read/write (release-please labels its pull requests). Its client ID is the variable `RELEASE_PLEASE_APP_CLIENT_ID`, its private key the secret `RELEASE_PLEASE_APP_PRIVATE_KEY`. On the organization's Free plan, organization variables and secrets reach workflows of public repositories only; while a repository is private, set both on the repository itself. The workflow narrows each token to this repository and those three permissions. |
| Actions | "Allow GitHub Actions to create and approve pull requests" is not needed: the App, not `GITHUB_TOKEN`, opens the release pull request. |
| Runners | The Blacksmith App installed on this repository; labels `blacksmith-2vcpu-ubuntu-2404`, `blacksmith-4vcpu-ubuntu-2404`, `blacksmith-8vcpu-ubuntu-2404` (also listed in `.github/actionlint.yaml`). |
| Merge method | Rebase merging, as now; release-please supports it. |
| Required checks on `main` | `lint`, `qualify` and `release-controls` from Validate. Branch protection or a ruleset needs the repository to be public or a paid plan. |
| Environment | `nuget-release`, without required reviewers or wait timers, with the secret `NUGET_USER` set to `zadazorg`: the nuget.org profile name, not an email address or the GitHub organization. |
| NuGet policy | See [NuGet Trusted Publishing policy](#nuget-trusted-publishing-policy). No API key is stored anywhere. |

### NuGet Trusted Publishing policy

Signed in to nuget.org as the profile `zadazorg`, create one Trusted Publishing policy with these fields:

| Field | Value |
| --- | --- |
| Policy package owner | `zadazorg` |
| Publisher | GitHub Actions |
| Owner | `one-man-wolf-pack` |
| Repository | `Rezultat` |
| Workflow file | `release.yml` (file name only) |
| Environment | `nuget-release` |
| Scopes | Push new packages and package versions only; not Unlist and not Stage |
| Packages | Exactly the three package IDs below, each on its own line |

```text
Rezultat
Rezultat.Testing.Xunit
Rezultat.Analyzers
```

nuget.org offers this policy form. Whether it saves with exactly these fields, whether `zadazorg` has the rights
the push needs and whether the token exchange works are not verified until the first real release. No API key,
other NuGet profile or organization is needed.

A policy created while the repository is private may start as temporarily active for 7 days; it becomes permanent
with the first successful publish. Restart its window on nuget.org if it lapses before the first release.
NuGet.org repository-signs the uploaded packages; no author-signing certificate is needed.

## First stable release, in order

1. Check the release source on `main`: the accepted API is in `PublicAPI.Shipped.txt` of `Rezultat` and
   `Rezultat.Testing.Xunit` (their `PublicAPI.Unshipped.txt` are empty), the READMEs describe the first stable
   `1.0.0`, and Validate is green. Leave `version.txt` to release-please.
2. Make the repository public, then require the three Validate checks on `main`.
3. Confirm the App and Blacksmith installations, create the environment with `NUGET_USER`, and create the NuGet
   policy with the fields above.
4. Set `REZULTAT_RELEASE_AUTOMATION_ENABLED` to `true`. The next push to `main` (or a re-run of the latest Release
   Please run) opens the release pull request. With no earlier release and an empty `.release-please-manifest.json`,
   it proposes `1.0.0` (`initial-version` in `release-please-config.json`), once; later versions follow the
   commits since the last release (`fix:` patch, `feat:` minor, `!` or `BREAKING CHANGE:` major).
5. Review the release pull request: `version.txt` is `1.0.0`, `CHANGELOG.md` lists the release. Merge it after
   Validate passes on it.
6. Watch Release: `qualify`, `attach`, `canonical` and `publish` must all pass. The GitHub release then holds the
   archives, their records and `SHA256SUMS`, and nuget.org the three packages and two symbol packages.
7. As an anonymous consumer, restore the packages, check `dotnet nuget verify --all` on the downloaded packages,
   compare their contents with the release assets (the repository signature changes the archive hash), and
   obtain symbols and Source Link sources.
8. For the next release, enable the SDK's package validation against the published `1.0.0`
   (`EnablePackageValidation` with `PackageValidationBaselineVersion` `1.0.0`).

Do not create test releases in this repository; rehearse in a separate private repository whose committed
`RepositoryUrl` names that repository.

## When something fails

Release runs are serialized per tag and never cancelled. A failed `qualify` or `attach` publishes nothing to
nuget.org; fix the source with a new commit and release a new version rather than moving the tag. `attach` never
replaces an existing asset. If `publish` fails part-way, look at what nuget.org actually holds for each package
ID and version before doing anything: a version that is already there cannot be pushed again, and the workflow does
not skip duplicates. Any continuation uses the release assets, whose hashes are in `SHA256SUMS`, never a rebuild.
