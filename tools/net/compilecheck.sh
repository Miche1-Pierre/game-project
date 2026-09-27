#!/usr/bin/env bash
# Out-of-editor compile check of the game's scripts, for the parallel netcode tracks (NETCODE_SLICE 13.2).
# Compiles the WORKING TREE of the checkout this script lives in (a git worktree works), in two passes:
#   runtime  = Assets/_Movers/Scripts
#   editor   = Assets/_Movers/Scripts + Assets/_Movers/Editor, with UNITY_EDITOR and UnityEditor.dll
# Package DLLs (LumaFlow, TargetIndicators, NGO, Transport, Services...) come from the MAIN checkout's
# Library/ScriptAssemblies, since a worktree has no Library. Safe to run concurrently (unique temp dir per run).
# Usage: bash tools/net/compilecheck.sh        Exit code: 0 when both passes have 0 errors.
set -u
U="${UNITY_DATA:-/c/Program Files/Unity/Hub/Editor/6000.6.0f1/Editor/Data}"
HERE="$(cd "$(dirname "$0")" && pwd)"
ROOT="$(git -C "$HERE" rev-parse --show-toplevel)" || { echo "not in a git checkout"; exit 2; }
COMMON="$(git -C "$HERE" rev-parse --path-format=absolute --git-common-dir)"
MAIN="$(dirname "$COMMON")"
SA="$MAIN/UnityProject/Library/ScriptAssemblies"
[ -d "$SA" ] || { echo "missing $SA (open the main checkout in Unity once)"; exit 2; }
SCRIPTS="$ROOT/UnityProject/Assets/_Movers/Scripts"
EDITOR="$ROOT/UnityProject/Assets/_Movers/Editor"
OUT="$(mktemp -d "${TMPDIR:-/tmp}/movers_cc_XXXXXX")"
trap 'rm -rf "$OUT"' EXIT
w() { cygpath -w "$1"; }

REFS="$OUT/refs.txt"; : > "$REFS"
for d in "$U"/Managed/UnityEngine/*.dll; do
  b=$(basename "$d"); case "$b" in *TestRunner*|UnityEditor.*) continue;; esac
  echo "-r:\"$(w "$d")\"" >> "$REFS"
done
echo "-r:\"$(w "$U/NetStandard/ref/2.1.0/netstandard.dll")\"" >> "$REFS"
for n in LumaFlow.Runtime TargetIndicators Unity.Netcode.Runtime Unity.Networking.Transport \
         Unity.Collections Unity.Burst Unity.Mathematics; do
  [ -f "$SA/$n.dll" ] && echo "-r:\"$(w "$SA/$n.dll")\"" >> "$REFS"
done
for d in "$SA"/Unity.Services.*.dll; do
  b=$(basename "$d"); case "$b" in *Editor*|*Deployment*) continue;; esac
  echo "-r:\"$(w "$d")\"" >> "$REFS"
done

EDREFS="$OUT/edrefs.txt"; : > "$EDREFS"
echo "-r:\"$(w "$U/Managed/UnityEditor.dll")\"" >> "$EDREFS"
for n in LumaFlow.Editor TargetIndicators.Editor; do
  [ -f "$SA/$n.dll" ] && echo "-r:\"$(w "$SA/$n.dll")\"" >> "$EDREFS"
done

find "$SCRIPTS" -name "*.cs" | sort | while read -r f; do echo "\"$(w "$f")\""; done > "$OUT/rt.list"
{ cat "$OUT/rt.list"; [ -d "$EDITOR" ] && find "$EDITOR" -name "*.cs" | sort | while read -r f; do echo "\"$(w "$f")\""; done; } > "$OUT/ed.list"

FAIL=0
run() {  # name define extraRefsFile list
  local RSP="$OUT/$1.rsp"
  { echo "-nologo"; echo "-nostdlib+"; echo "-target:library"; echo "-langversion:9.0"
    echo "-nowarn:0618,0414,0169,0649,0067,0219,0108,0114"
    [ -n "$2" ] && echo "-define:$2"
    echo "-out:\"$(w "$OUT/$1.dll")\""
    cat "$REFS"; [ -n "$3" ] && cat "$3"; cat "$4"; } > "$RSP"
  "$U/DotNetSdk/dotnet.exe" "$(w "$U/DotNetSdk/sdk/8.0.318/Roslyn/bincore/csc.dll")" "@$(w "$RSP")" > "$OUT/$1.log" 2>&1
  local code=$? errs
  errs=$(grep -c ' error ' "$OUT/$1.log")
  grep -E ' error ' "$OUT/$1.log" | sed 's#.*Assets.#Assets/#' | head -15
  echo "$1: EXIT=$code errors=$errs files=$(wc -l < "$4")"
  [ "$code" -ne 0 ] || [ "$errs" -ne 0 ] && FAIL=1
  return 0
}
echo "compilecheck: $ROOT"
run runtime "" "" "$OUT/rt.list"
run editor "UNITY_EDITOR" "$EDREFS" "$OUT/ed.list"
exit $FAIL
