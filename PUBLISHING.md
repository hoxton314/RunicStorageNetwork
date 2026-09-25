# Publishing to Thunderstore

The [Thunderstore release workflow](https://github.com/rerit33/RunicStorageNetwork/actions/workflows/thunderstore.yml) uploads a prepared release ZIP to **Rerit/RunicStorageNetwork** in the **Valheim** community. It uses the official Thunderstore CLI.

The Unity asset and plugin build remains local. GitHub validates and uploads the finished package; it does not rebuild the Unity models or download game assemblies. Ordinary pushes and pull requests do not publish anything.

## One-time setup

In Thunderstore, open **Settings → Teams → Rerit → Service Accounts** and create a publishing service account. Save its token as the GitHub repository Actions secret **TCLI_AUTH_TOKEN**. Do not put the token in source files, release notes or command arguments.

## Release procedure

1. Build and test the mod locally. Keep the source version, compiled DLL version and `manifest.json` version aligned. Update `CHANGELOG_EN.md` and `CHANGELOG.md`.
2. Commit and push the matching source. Create a tag such as `v0.5.3` on that commit and push the tag.
3. Create a **draft GitHub release** for that tag and attach `RunicStorageNetwork-0.5.3.zip`. Attach the ZIP before publishing the release.
4. In Actions, run **Thunderstore release → Run workflow**, entering the tag. This manual run validates the archive and CLI configuration but **never uploads to Thunderstore**, even when the secret is configured.
5. Once the checks and your in-game testing are complete, publish the GitHub release as a normal release. This triggers the upload automatically. Drafts and prereleases do not upload.

The archive must contain exactly:

```text
manifest.json
README.md
CHANGELOG.md
icon.png
plugins/RunicStorageNetwork/RunicStorageNetwork.dll
plugins/RunicStorageNetwork/Assets/rsn_core_windows
```

The validator checks the tag, source and compiled assembly versions, metadata, dependencies, documentation, icon dimensions and asset bundle header. Archive entries and duplicates are checked before reading files. These checks do not replace in-game tests.

For a local validation run:

```powershell
.\tools\ValidateRelease.ps1 -Package '.\dist\RunicStorageNetwork-0.5.3.zip' -Tag 'v0.5.3'
```

## Failed or repeated runs

Inspect the Actions log. A missing ZIP, mismatched version or stale documentation stops the upload. An already published Thunderstore version is skipped on retries. If an upload reports a connection error, check Thunderstore before retrying: it may have accepted the package before the response was lost.

Fix release assets while the GitHub release is still a draft, then repeat the manual validation. After publication, use a new version for package changes; do not move a published version tag. The workflow never overwrites or deletes an existing Thunderstore version.

## References

- [Official Thunderstore CLI](https://github.com/thunderstore-io/thunderstore-cli)
- [Thunderstore CLI authentication and prebuilt package publishing](https://github.com/thunderstore-io/thunderstore-cli/wiki)
- [GitHub release workflow events](https://docs.github.com/en/actions/reference/workflows-and-actions/events-that-trigger-workflows#release)
