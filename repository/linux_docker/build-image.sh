#!/usr/bin/env bash
# Build the Debian Bookworm builder image for one target architecture.
# Usage: build-image.sh <x64|arm64|arm32>
#
# Requires Docker plus qemu-user-static + binfmt_misc on the host for the
# emulated arm64/arm32 targets.
set -euo pipefail

ARCH="${1:-}"
case "$ARCH" in
  x64)   PLATFORM="linux/amd64"  ;;
  arm64) PLATFORM="linux/arm64"  ;;
  arm32) PLATFORM="linux/arm/v7" ;;
  *) echo "Usage: $0 <x64|arm64|arm32>" >&2; exit 1 ;;
esac

SCRIPTDIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"

docker build \
  --platform "$PLATFORM" \
  --build-arg BASE_PLATFORM="$PLATFORM" \
  --pull \
  -f "${SCRIPTDIR}/Dockerfile.bookworm" \
  -t "eddie-bookworm:${ARCH}" \
  "${SCRIPTDIR}"
