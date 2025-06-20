#!/usr/bin/env bash
set -euo pipefail

# Directory of this script
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"

# Install .NET 9 SDK if not already installed
if ! command -v dotnet >/dev/null || ! dotnet --list-sdks | grep -q '^9\.'; then
  echo "Installing .NET 9 SDK..."
  curl -SL https://dot.net/v1/dotnet-install.sh -o "$SCRIPT_DIR/dotnet-install.sh"
  chmod +x "$SCRIPT_DIR/dotnet-install.sh"
  "$SCRIPT_DIR/dotnet-install.sh" --channel 9.0 --install-dir "$HOME/.dotnet"
fi

# Ensure PATH includes the installed SDK
if ! grep -q 'export PATH="$HOME/.dotnet:$PATH"' "$HOME/.bashrc" 2>/dev/null; then
  echo 'export PATH="$HOME/.dotnet:$PATH"' >> "$HOME/.bashrc"
fi
export PATH="$HOME/.dotnet:$PATH"

# Ensure this script runs on new shell sessions
SETUP_SOURCE="source $SCRIPT_DIR/setup.sh"
if ! grep -Fq "$SETUP_SOURCE" "$HOME/.bashrc" 2>/dev/null; then
  echo "$SETUP_SOURCE" >> "$HOME/.bashrc"
fi

