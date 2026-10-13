#!/bin/bash
# Headless playtest: compiles the real game scripts against a tiny fake Unity runtime
# (FakeUnity.cs) and lets a bot play the game, reporting state changes, flames lost,
# oil use and any exceptions. Used to balance the nights and catch bugs without the editor.
#
# Needs the .NET 8 SDK.  Usage:
#   Tools/Playtest/run.sh [good|idle|pause] [reaction,mouseSpeed,wobble]
# Example: SIMSEED=3 Tools/Playtest/run.sh good 0.35,12,0.45
set -e
HERE=$(cd "$(dirname "$0")" && pwd)
ROOT=$(cd "$HERE/../.." && pwd)
DOTNET_ROOT=$(dirname "$(readlink -f "$(command -v dotnet)")")
CSC=$(ls "$DOTNET_ROOT"/sdk/*/Roslyn/bincore/csc.dll | tail -1)
REF=$(ls -d "$DOTNET_ROOT"/packs/Microsoft.NETCore.App.Ref/8.*/ref/net8.0 | tail -1)
OUT="$HERE/bin"
mkdir -p "$OUT"
cp "$HERE/playtest.runtimeconfig.json" "$OUT/"
REFS=$(ls "$REF"/*.dll | sed 's/^/-r:/' | tr '\n' ' ')
SRC=$(find "$ROOT/Assets/Lanternlight/Scripts" -name '*.cs')
dotnet "$CSC" -nologo -noconfig -nostdlib -target:exe -langversion:9 -nowarn:CS0626 \
  -define:ENABLE_LEGACY_INPUT_MANAGER -out:"$OUT/playtest.dll" $REFS \
  "$HERE/FakeUnity.cs" "$HERE/Harness.cs" $SRC
dotnet "$OUT/playtest.dll" "$ROOT/Assets/Lanternlight/Resources" "$OUT/snapshots" "${1:-good}" ${2:-}
