# Neon Uprising — Project Setup Guide

This guide explains how to download the complete project, including Git LFS assets, and open it with the correct Unity version.

## Requirements

Install these before cloning the repository:

- [Git](https://git-scm.com/downloads)
- [Git LFS](https://git-lfs.com/)
- [Unity Hub](https://unity.com/download)
- Unity Editor **6000.5.0f1**

The Unity version must match the version recorded in `ProjectSettings/ProjectVersion.txt`. Opening the project with another Unity version may upgrade or reserialize assets and create unwanted changes.

## Recommended: fresh installation

Open PowerShell or Git Bash and enable Git LFS:

```powershell
git lfs install
```

Clone the branch that currently contains the latest project work:

```powershell
git clone --branch feat/start-village --single-branch https://github.com/mohamedazizhafhouf/avarra-uprising.git
cd avarra-uprising
```

Download and materialize all Git LFS assets:

```powershell
git lfs pull
git lfs checkout
```

Confirm that the checkout is complete and clean:

```powershell
git status
git lfs fsck
```

Expected results:

- `git status` reports `On branch feat/start-village` and a clean working tree.
- `git lfs fsck` reports `Git LFS fsck OK`.

Open Unity Hub, select **Add project from disk**, and choose the cloned `avarra-uprising` directory. Make sure Unity Hub opens it with **6000.5.0f1**. The first import can take several minutes.

## Fix an existing checkout

Close Unity before updating the repository. First, check whether there is uncommitted work:

```powershell
git status
```

Do not continue until personal changes have been committed or safely stashed. Then update the remote information:

```powershell
git lfs install
git fetch --all --prune
```

If `feat/start-village` does not exist locally yet:

```powershell
git switch --track origin/feat/start-village
```

If it already exists locally:

```powershell
git switch feat/start-village
git pull --ff-only
```

Download the binary assets:

```powershell
git lfs pull
git lfs checkout
git lfs fsck
```

If the Unity project still appears broken, close Unity and delete only the local generated caches:

```powershell
Remove-Item -LiteralPath .\Library -Recurse -Force -ErrorAction SilentlyContinue
Remove-Item -LiteralPath .\Temp -Recurse -Force -ErrorAction SilentlyContinue
Remove-Item -LiteralPath .\Logs -Recurse -Force -ErrorAction SilentlyContinue
```

Do not delete `Assets`, `Packages`, or `ProjectSettings`. Reopen the project and allow Unity to rebuild the cache.

## Detect missing Git LFS files

Models, textures, audio, fonts, and other large binary files are stored with Git LFS. If Git LFS was not active during checkout, Unity may report broken or unreadable assets.

Run:

```powershell
git lfs pull
git lfs checkout
git lfs fsck
```

A broken LFS checkout often leaves a three-line text file in place of a real asset. Its first line looks like this:

```text
version https://git-lfs.github.com/spec/v1
```

Do not edit or commit those pointer files manually. Let Git LFS replace them.

## Configure Unity Smart Merge

The repository marks scenes, prefabs, and other Unity YAML files for Unity Smart Merge. Each developer should configure the merge driver once from inside the repository:

```powershell
git config merge.unityyamlmerge.name "Unity SmartMerge"
git config merge.unityyamlmerge.driver '"C:/Program Files/Unity/Hub/Editor/6000.5.0f1/Editor/Data/Tools/UnityYAMLMerge.exe" merge -p "$BASE" "$REMOTE" "$LOCAL" "$MERGED"'
```

If Unity is installed elsewhere, replace the executable path with the location of `UnityYAMLMerge.exe` on that computer.

Smart Merge can help with text-serialized Unity files, but terrain data and other binary assets cannot be merged safely. Coordinate ownership before two people edit the same scene, prefab, or terrain.

## Daily collaboration workflow

Before starting work:

```powershell
git switch feat/start-village
git pull --ff-only
git lfs pull
```

Create a personal feature branch instead of committing directly to the shared branch:

```powershell
git switch -c feat/short-description
```

Before committing, close or save Unity and review the changed files:

```powershell
git status
git diff --stat
```

Commit and push the branch:

```powershell
git add Assets Packages ProjectSettings
git commit -m "feat: describe the change"
git push -u origin feat/short-description
```

Open a pull request and merge it only after the project has been tested.

## Files that belong in Git

Commit changes from these directories when they are part of the work:

- `Assets/`, including matching `.meta` files
- `Packages/manifest.json`
- `Packages/packages-lock.json`
- `ProjectSettings/`

Do not commit generated or personal directories such as:

- `Library/`
- `Temp/`
- `Logs/`
- `UserSettings/`
- `.vs/`
- generated `.csproj`, `.sln`, and `.slnx` files

The repository `.gitignore` already excludes these paths.

## Troubleshooting checklist

If the project differs between computers, check these in order:

1. Both developers are on the same Git branch and commit: `git branch --show-current` and `git rev-parse HEAD`.
2. `git status` is clean before opening Unity.
3. `git lfs fsck` succeeds and `git lfs pull` has completed.
4. Both developers use Unity **6000.5.0f1**.
5. Unity was closed while switching branches or pulling project files.
6. The local `Library` cache was rebuilt after major branch or package changes.
7. Only one developer edited a particular binary terrain asset at a time.

When reporting a problem, include the output of:

```powershell
git status
git branch --show-current
git rev-parse HEAD
git lfs fsck
```
