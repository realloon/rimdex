#!/bin/sh
set -e

OS="$(uname -s | tr '[:upper:]' '[:lower:]')"
[ "$OS" = "darwin" ] && OS="osx"
ARCH="$(uname -m)"
[ "$ARCH" = "aarch64" ] && ARCH="arm64"
[ "$ARCH" = "x86_64" ] && ARCH="x64"

DEST="$HOME/.local/bin"
mkdir -p "$DEST"

TMP="$(mktemp -d)"
trap 'rm -rf "$TMP"' EXIT

curl -fsSL "https://github.com/realloon/rimdex/releases/latest/download/rimdex-${OS}-${ARCH}.zip" -o "$TMP/rimdex.zip"
unzip -q -o "$TMP/rimdex.zip" -d "$DEST"
chmod +x "$DEST/rimdex"

echo "Installed rimdex to $DEST/rimdex"
case ":$PATH:" in
  *":$DEST:"*) ;;
  *) echo "Add to PATH: export PATH=\"\$HOME/.local/bin:\$PATH\"" ;;
esac
