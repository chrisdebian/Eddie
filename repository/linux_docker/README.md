# Linux multi-arch Docker build

Reproducible Debian Bookworm builder containers for the Linux packages
(`x64`, `arm64`, `arm32`). Bookworm is used as the base for a lower glibc floor
while still providing the GTK 3 / WebKitGTK 4.1 and GTK 4 / WebKitGTK 6.0 dev
stacks and .NET 10.

Everything needed is versioned here, so a build VM only needs Docker (plus
`qemu-user-static` + `binfmt_misc` for the emulated arm targets), a checkout of
this repository, and the local signing directory. Nothing under `/opt` other
than the signing secrets needs to be backed up.

## Files

- `Dockerfile.bookworm` — the builder image (toolchain, GTK 3/4 dev, .NET 10).
- `run-in-container.sh` — in-container runner: rsyncs the read-only source to a
  writable working copy, then runs the requested command from the repo root.
- `build-image.sh <x64|arm64|arm32>` — builds/tags `eddie-bookworm:<arch>`.
- `build.sh <x64|arm64|arm32>` — runs `build_all_linux_private.sh`
  (universal `u` line) inside the container, with signing + deploy by default.

## Host layout (defaults)

```
/opt/eddie-air                    read-only source checkout (git pull here)
/opt/eddie-work/bookworm-<arch>   writable rsync working copy (regenerable)
/opt/eddie-output/bookworm-<arch> collected artifacts (regenerable)
/opt/eddie-cache/nuget            persistent NuGet cache (regenerable)
/opt/eddie-signing                deploy + signing secrets (NOT in git, back up)
```

The source is mounted read-only; `run-build.sh` makes the writable copy with
`rsync`, so no container ever runs `git pull`.

## Signing directory

`build.sh` with deploy enabled (default) expects in `/opt/eddie-signing`:

- `eddie.website_deploy.key` — ssh key used by `linux_common/deploy.sh` (scp).
- `eddie_gpg_2026.key` — OpenPGP **secret** key for `apt@eddie.website`.
- `eddie_gpg_2026.passphrase` — passphrase for the secret key.

These are the only files to back up; they are static and must never be
committed. The key is imported into the container's throwaway gpg keyring at
run time (the container is `--rm`).

## Usage

```sh
# once per toolchain change (and first time per arch):
bash repository/linux_docker/build-image.sh x64

# build + sign + deploy the universal package set:
bash repository/linux_docker/build.sh x64

# local build only, no signing, no upload:
EDDIE_DEPLOY=0 bash repository/linux_docker/build.sh x64
```

Routine new release: `git pull` in `/opt/eddie-air`, then run `build.sh`.
Rebuild the image only when dependencies or the Dockerfile change.

## Notes

- AppImage is built on `x64` only; on `arm64`/`arm32` the AppImage step is a
  no-op (upstream linuxdeploy has no arm builds).
- `arm64`/`arm32` run under qemu-user emulation: slower, and the .NET publish
  flow is the part to watch.
- Deploy uploads to `deploy@eddie.website:/opt/repository/eddie/internal`.
