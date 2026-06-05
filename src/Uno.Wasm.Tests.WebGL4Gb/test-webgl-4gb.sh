#!/bin/bash

# Validates the emscripten GL_UNPACK_ROW_LENGTH backport (emscripten-core/emscripten#21980)
# is applied to the runtime-generated _framework/dotnet.native.*.js when an app opts in to a
# 4GB heap via `-s MAXIMUM_MEMORY=4GB`, and is NOT applied otherwise.
#
# The test app references WebGL from a native C file (webgl-force-link.c) so emscripten links
# its WebGL JS glue (library_webgl.js) into dotnet.native.js — otherwise a minimal app would
# not contain the code the backport patches and the assertions would be vacuous.
set -e

RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
NC='\033[0m'

PROJECT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
PROJECT_FILE="$PROJECT_DIR/Uno.Wasm.Tests.WebGL4Gb.csproj"

# Marker text emitted by the backport, and the original (unpatched) emscripten token it replaces.
PATCH_MARKER='GL.unpackRowLength = param'
PATCH_BRANCH='GL_UNPACK_ROW_LENGTH'
PATCH_ROWSIZE='(GL.unpackRowLength || width)'
ORIGINAL_ROWSIZE='var plainRowSize = width * sizePerPixel;'

echo "========================================="
echo "Testing 4GB WebGL fix (emscripten #21980 backport)"
echo "========================================="

find_native_js() {
    # $1 = wwwroot root; echoes the single dotnet.native.*.js path (excluding compressed variants).
    find "$1/_framework" -name "dotnet.native.*.js" ! -name "*.js.br" ! -name "*.js.gz" 2>/dev/null | head -1
}

count() { grep -F -c -- "$2" "$1" 2>/dev/null || true; }

assert_patched() {
    # $1 = native js path
    local njs="$1"
    if [ ! -f "$njs" ]; then
        echo -e "${RED}❌ FAIL: dotnet.native.*.js not found${NC}"; exit 1
    fi
    echo "  Inspecting: $njs"
    if [ "$(count "$njs" "$PATCH_MARKER")" -lt 1 ]; then
        echo -e "${RED}❌ FAIL: fix marker '$PATCH_MARKER' not present — the backport did not apply.${NC}"
        echo "  (If the runtime now ships emscripten >= 3.1.61, the token text may have changed; update the backport.)"
        exit 1
    fi
    if [ "$(count "$njs" "$PATCH_BRANCH")" -lt 1 ]; then
        echo -e "${RED}❌ FAIL: '$PATCH_BRANCH' branch not present.${NC}"; exit 1
    fi
    if [ "$(count "$njs" "$PATCH_ROWSIZE")" -lt 1 ]; then
        echo -e "${RED}❌ FAIL: row-size expression was not rewritten to use unpackRowLength.${NC}"; exit 1
    fi
    if [ "$(count "$njs" "$ORIGINAL_ROWSIZE")" -ne 0 ]; then
        echo -e "${RED}❌ FAIL: original unpatched row-size token is still present.${NC}"; exit 1
    fi
    echo -e "${GREEN}✓ Fix applied (marker, branch, row-size rewrite present; original token replaced)${NC}"
}

# ---------------------------------------------------------------------------
echo ""
echo "📦 Test 1: Build with MAXIMUM_MEMORY=4GB (expect fix applied)"
echo "----------------------------------------"
dotnet clean "$PROJECT_FILE" -c Release > /dev/null 2>&1 || true
rm -rf "$PROJECT_DIR/bin" "$PROJECT_DIR/obj"
dotnet build "$PROJECT_FILE" -c Release /m:1
BUILD_NJS="$(find_native_js "$PROJECT_DIR/bin/Release/net10.0/wwwroot")"
assert_patched "$BUILD_NJS"

# ---------------------------------------------------------------------------
echo ""
echo "📤 Test 2: Publish with MAXIMUM_MEMORY=4GB (expect fix applied + no stale compressed copies)"
echo "----------------------------------------"
dotnet publish "$PROJECT_FILE" -c Release /m:1
PUBLISH_NJS="$(find_native_js "$PROJECT_DIR/bin/Release/net10.0/publish/wwwroot")"
assert_patched "$PUBLISH_NJS"

if [ -f "$PUBLISH_NJS.br" ]; then
    echo -e "${RED}❌ FAIL: stale compressed copy exists: $PUBLISH_NJS.br${NC}"
    echo "  A content-negotiating server would serve the unpatched compressed bytes."
    exit 1
fi
if [ -f "$PUBLISH_NJS.gz" ]; then
    echo -e "${RED}❌ FAIL: stale compressed copy exists: $PUBLISH_NJS.gz${NC}"
    exit 1
fi
echo -e "${GREEN}✓ No stale .br/.gz copies of the patched dotnet.native.js${NC}"

# Output must remain valid JavaScript after the in-place edit.
# dotnet.native.js is an ES module (it uses `import.meta`), so it must be parsed as
# a module: `node --check` on a `.js` file treats it as CommonJS and rejects
# `import.meta`. Copy to a `.mjs` so Node selects the ESM parser by extension.
if command -v node > /dev/null 2>&1; then
    NJS_MODULE_CHECK="$(mktemp --suffix=.mjs)"
    cp "$PUBLISH_NJS" "$NJS_MODULE_CHECK"
    if node --check "$NJS_MODULE_CHECK"; then
        echo -e "${GREEN}✓ Patched dotnet.native.js parses as valid JavaScript (ES module)${NC}"
        rm -f "$NJS_MODULE_CHECK"
    else
        echo -e "${RED}❌ FAIL: patched dotnet.native.js is not valid JavaScript${NC}"
        rm -f "$NJS_MODULE_CHECK"; exit 1
    fi
fi

# ---------------------------------------------------------------------------
echo ""
echo "🚫 Test 3: Build WITHOUT the 4GB flag (-p:Enable4Gb=false) — expect NO patch"
echo "----------------------------------------"
rm -rf "$PROJECT_DIR/bin" "$PROJECT_DIR/obj"
dotnet build "$PROJECT_FILE" -c Release /m:1 -p:Enable4Gb=false
NOFLAG_NJS="$(find_native_js "$PROJECT_DIR/bin/Release/net10.0/wwwroot")"
if [ ! -f "$NOFLAG_NJS" ]; then
    echo -e "${RED}❌ FAIL: dotnet.native.*.js not found in no-flag build${NC}"; exit 1
fi
if [ "$(count "$NOFLAG_NJS" "$PATCH_MARKER")" -ne 0 ]; then
    echo -e "${RED}❌ FAIL: fix marker present without the 4GB flag — gating is broken.${NC}"; exit 1
fi
if [ "$(count "$NOFLAG_NJS" "$ORIGINAL_ROWSIZE")" -lt 1 ]; then
    echo -e "${RED}❌ FAIL: expected the original (unpatched) emscripten token in the no-flag build, but it is absent.${NC}"
    echo "  (The WebGL glue should still be linked via webgl-force-link.c — this points at a setup change.)"
    exit 1
fi
echo -e "${GREEN}✓ No-flag build is unpatched (gating works), and the WebGL glue is present${NC}"

echo ""
echo "========================================="
echo -e "${GREEN}✅ ALL TESTS PASSED${NC}"
echo "========================================="
exit 0
