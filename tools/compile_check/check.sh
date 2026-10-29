#!/bin/bash
# Compile check without the game: builds the event mod and J CLIENT against Unity 2021.3 reference assemblies (NuGet),
# Harmony (NuGet) and hand-written stubs of the Gorilla Tag / BepInEx / Photon / Input System members the code uses.
# Unity 6 renamed a few Rigidbody members; the copies compiled here use the 2021.3 names.
# Usage: tools/compile_check/check.sh   (needs the .NET 8 SDK). Prints the errors, or "OK" for each project.
set -e
HERE="$(cd "$(dirname "$0")" && pwd)"; REPO="$(cd "$HERE/../.." && pwd)"
copy() { # copy C# sources $1 -> $2, renaming Unity 6 members to their 2021.3 names
  rm -rf "$2"; mkdir -p "$2"
  (cd "$1" && find . -name '*.cs' -not -path './obj/*' -not -path './bin/*' -not -path './tools/*' -not -path './JClient/*' -not -path './PurpleGhostRemover/*') | while read -r f; do
    mkdir -p "$2/$(dirname "$f")"; sed 's/\blinearVelocity\b/velocity/g; s/\blinearDamping\b/drag/g; s/\bangularDamping\b/angularDrag/g' "$1/$f" > "$2/$f"; done
}
copy "$REPO" "$HERE/work/eventmod"
copy "$REPO/JClient" "$HERE/work/jclient"
build() { local out; out=$(cd "$HERE" && dotnet build "$1" --nologo -v q 2>&1 | grep -E ": error " | sed -E 's#.*/work/[a-z]+/##; s# \[/.*##' | sort -u); if [ -z "$out" ]; then echo "$1: OK"; else echo "$out"; return 1; fi; }
build stubs/stubs.csproj && build eventmod.csproj && build jclient.csproj
