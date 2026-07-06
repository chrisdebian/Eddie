#!/usr/bin/env bash
# Build the full universal ('u' line) Linux package set for one architecture
# inside the Bookworm container, by running repository/build_all_linux_private.sh.
#
# Usage: build.sh <x64|arm64|arm32>
#
# Deploy + OpenPGP signing are enabled by default (EDDIE_DEPLOY=1): every
# artifact is uploaded to eddie.website and a detached .asc is produced and
# uploaded. Set EDDIE_DEPLOY=0 for a local build with no signing and no upload.
#
# Host paths (override via env):
#   EDDIE_SRC      default /opt/eddie-air         read-only source checkout
#   EDDIE_WORK     default /opt/eddie-work         writable rsync work base
#   EDDIE_OUTPUT   default /opt/eddie-output       collected artifacts base
#   EDDIE_NUGET    default /opt/eddie-cache/nuget  persistent NuGet cache
#   EDDIE_SIGNING  default /opt/eddie-signing      deploy key + gpg key + passphrase
#
# The signing dir (when deploying) must contain:
#   eddie.website_deploy.key      ssh key for scp upload
#   eddie_gpg_2026.key            OpenPGP secret key (apt@eddie.website)
#   eddie_gpg_2026.passphrase     passphrase for the secret key
set -euo pipefail

ARCH="${1:-}"
case "$ARCH" in
  x64)   PLATFORM="linux/amd64"  ;;
  arm64) PLATFORM="linux/arm64"  ;;
  arm32) PLATFORM="linux/arm/v7" ;;
  *) echo "Usage: $0 <x64|arm64|arm32>" >&2; exit 1 ;;
esac

SCRIPTDIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"

EDDIE_SRC="${EDDIE_SRC:-/opt/eddie-air}"
EDDIE_WORK="${EDDIE_WORK:-/opt/eddie-work}/bookworm-${ARCH}"
EDDIE_OUTPUT="${EDDIE_OUTPUT:-/opt/eddie-output}/bookworm-${ARCH}"
EDDIE_NUGET="${EDDIE_NUGET:-/opt/eddie-cache/nuget}"
EDDIE_SIGNING="${EDDIE_SIGNING:-/opt/eddie-signing}"
EDDIE_DEPLOY="${EDDIE_DEPLOY:-1}"
TAG="eddie-bookworm:${ARCH}"

mkdir -p "$EDDIE_WORK" "$EDDIE_OUTPUT" "$EDDIE_NUGET"

args=(
  --rm
  --platform "$PLATFORM"
  -e APPIMAGE_EXTRACT_AND_RUN=1
  -e USER=root
  -v "${EDDIE_SRC}:/src/eddie-air:ro"
  -v "${EDDIE_WORK}:/work"
  -v "${EDDIE_OUTPUT}:/out"
  -v "${EDDIE_NUGET}:/root/.nuget/packages"
  -v "${SCRIPTDIR}/run-in-container.sh:/usr/local/bin/run-eddie-build:ro"
)

if [ "$EDDIE_DEPLOY" = "1" ]; then
  args+=( -e EDDIESIGNINGDIR=/signing -v "${EDDIE_SIGNING}:/signing" )
  INNER='mkdir -p ~/.gnupg && chmod 700 ~/.gnupg && echo allow-loopback-pinentry > ~/.gnupg/gpg-agent.conf; gpgconf --kill gpg-agent >/dev/null 2>&1 || true; gpg --batch --import /signing/eddie_gpg_2026.key; gpg --list-secret-keys; gpg --list-secret-keys --with-colons | grep -q "^sec" || { echo NO_SECRET_KEY_ABORT; exit 1; }; mkdir -p ~/.ssh; ssh-keyscan -p 46333 eddie.website >> ~/.ssh/known_hosts 2>/dev/null || true; cd repository; bash build_all_linux_private.sh; cp -v files/* /out/ 2>/dev/null || true; ls -l /out'
else
  INNER='cd repository; bash build_all_linux_private.sh; cp -v files/* /out/ 2>/dev/null || true; ls -l /out'
fi

docker run "${args[@]}" "$TAG" sh /usr/local/bin/run-eddie-build sh -lc "$INNER"
