#!/bin/sh
set -e

main() {
  if [ -z "$HOME" ]; then
    echo "error: HOME environment variable is not set." >&2
    exit 1
  fi

  for cmd in curl unzip; do
    if ! command -v "$cmd" >/dev/null 2>&1; then
      echo "error: '$cmd' is required to install rimdex." >&2
      exit 1
    fi
  done

  case "$(uname -s)" in
    Darwin) OS="osx" ;;
    Linux)  OS="linux" ;;
    *)
      echo "error: unsupported OS: $(uname -s)" >&2
      exit 1
      ;;
  esac

  case "$(uname -m)" in
    x86_64|amd64) ARCH="x64" ;;
    arm64|aarch64) ARCH="arm64" ;;
    *)
      echo "error: unsupported architecture: $(uname -m)" >&2
      exit 1
      ;;
  esac

  # Handle macOS architecture nuances and unsupported platforms
  if [ "$OS" = "osx" ] && [ "$ARCH" = "x64" ]; then
    # If running in a Rosetta translated shell on Apple Silicon, use native arm64 build
    if [ "$(sysctl -in hw.optional.arm64 2>/dev/null)" = "1" ]; then
      ARCH="arm64"
    else
      echo "error: macOS x64 (Intel) is currently not supported. rimdex provides Apple Silicon (arm64) builds." >&2
      exit 1
    fi
  fi

  if [ "$OS" = "linux" ] && [ "$ARCH" = "arm64" ]; then
    echo "error: Linux arm64 is currently not supported. rimdex provides Linux x64 builds." >&2
    exit 1
  fi

  ASSET="rimdex-${OS}-${ARCH}.zip"
  URL="https://github.com/realloon/rimdex/releases/latest/download/${ASSET}"
  INSTALL_DIR="$HOME/.local/bin"

  TMP_DIR="$(mktemp -d 2>/dev/null || mktemp -d -t 'rimdex')"
  if [ -z "$TMP_DIR" ] || [ ! -d "$TMP_DIR" ]; then
    echo "error: failed to create temporary directory." >&2
    exit 1
  fi
  trap 'rm -rf "$TMP_DIR"' EXIT INT TERM

  echo "Downloading ${ASSET}..."
  retries=3
  count=0
  downloaded=0
  while [ "$count" -lt "$retries" ]; do
    if curl -fsSL "$URL" -o "$TMP_DIR/$ASSET"; then
      downloaded=1
      break
    fi
    count=$((count + 1))
    if [ "$count" -lt "$retries" ]; then
      echo "Download failed, retrying ($count/$retries)..."
      sleep 1
    fi
  done

  if [ "$downloaded" -ne 1 ]; then
    echo "error: failed to download from $URL" >&2
    exit 1
  fi

  unzip -q -o "$TMP_DIR/$ASSET" -d "$TMP_DIR"
  if [ ! -f "$TMP_DIR/rimdex" ]; then
    echo "error: binary 'rimdex' not found in archive." >&2
    exit 1
  fi

  mkdir -p "$INSTALL_DIR"
  mv -f "$TMP_DIR/rimdex" "$INSTALL_DIR/rimdex"
  chmod +x "$INSTALL_DIR/rimdex"
  echo "Installed rimdex to $INSTALL_DIR/rimdex"

  case ":$PATH:" in
    *":$INSTALL_DIR:"*|*":$INSTALL_DIR/:"*) ;;
    *)
      SHELL_NAME="$(basename "${SHELL:-}")"
      PROFILE=""

      case "$SHELL_NAME" in
        zsh)
          PROFILE="$HOME/.zshrc"
          ;;
        bash)
          if [ "$OS" = "osx" ]; then
            if [ -f "$HOME/.bash_profile" ]; then
              PROFILE="$HOME/.bash_profile"
            elif [ -f "$HOME/.profile" ]; then
              PROFILE="$HOME/.profile"
            elif [ -f "$HOME/.bashrc" ]; then
              PROFILE="$HOME/.bashrc"
            else
              PROFILE="$HOME/.bash_profile"
            fi
          else
            PROFILE="$HOME/.bashrc"
          fi
          ;;
        fish)
          PROFILE="$HOME/.config/fish/config.fish"
          ;;
        *)
          PROFILE="$HOME/.profile"
          ;;
      esac

      if [ -n "$PROFILE" ]; then
        already_configured=0
        if [ -f "$PROFILE" ]; then
          if grep -qF "$INSTALL_DIR" "$PROFILE" 2>/dev/null || \
             grep -qF "\$HOME/.local/bin" "$PROFILE" 2>/dev/null || \
             grep -qF "~/.local/bin" "$PROFILE" 2>/dev/null; then
            already_configured=1
          fi
        fi

        if [ "$already_configured" -eq 0 ]; then
          mkdir -p "$(dirname "$PROFILE")"
          if [ "$SHELL_NAME" = "fish" ]; then
            printf "\nfish_add_path %s\n" "$INSTALL_DIR" >> "$PROFILE"
          else
            printf "\nexport PATH=\"\$HOME/.local/bin:\$PATH\"\n" >> "$PROFILE"
          fi
          echo "Added $INSTALL_DIR to PATH in $PROFILE"
        fi

        echo "Run this to update your current terminal session:"
        if [ "$SHELL_NAME" = "fish" ]; then
          echo "  fish_add_path $INSTALL_DIR"
        else
          echo "  export PATH=\"\$HOME/.local/bin:\$PATH\""
        fi
      fi
      ;;
  esac

  echo "Done."
}

main "$@"
