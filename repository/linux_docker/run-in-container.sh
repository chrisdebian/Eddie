#!/bin/sh
# In-container runner. Makes a writable working copy of the read-only source
# checkout (so the build can write bin/obj/staging/artifacts), then execs the
# given command from the repository root. Mounted into the container, so it does
# not require the executable bit (invoke it as: sh run-in-container.sh <cmd...>).
set -eu

SRC="/src/eddie-air"
WORK="/work/eddie-air"

rsync -a --delete --exclude='.git/' "$SRC/" "$WORK/"
cd "$WORK"
exec "$@"
