#!/bin/bash
# Resource Calculator (Avalonia) - one-line installer/updater (macOS)
# The SAME command installs on first run and safely UPDATES on subsequent runs:
# Usage: curl -sSL https://raw.githubusercontent.com/ajjs1ajjs/Calculator-servers/main/install_mac.sh | sudo bash

set -e

INSTALL_DIR="/Applications/Resource Calculator.app"
APP_BUNDLE="/Applications/Resource Calculator.app"
CALC_VERSION="${RESOURCE_CALCULATOR_VERSION:-latest}"
REPO="ajjs1ajjs/Calculator-servers"

if [ "$(uname)" != "Darwin" ]; then
    echo "ERROR: This installer is for macOS only."
    exit 1
fi

if [ "$(id -u)" -ne 0 ]; then
    echo "Please run as root (sudo ./install_mac.sh)"
    exit 1
fi

echo "=============================================="
echo "   Resource Calculator - Встановлення"
echo "=============================================="
echo ""

case "$(uname -m)" in
    x86_64|amd64) ARCH="x64" ;;
    aarch64|arm64) ARCH="arm64" ;;
    *) echo "ERROR: unsupported architecture: $(uname -m)"; exit 1 ;;
esac

BINARY_NAME="ITE.ResourceCalculator-macos-${ARCH}.tar.gz"

IS_UPDATE=0
if [ -d "$APP_BUNDLE" ]; then
    IS_UPDATE=1
fi
MODE=$(if [ "$IS_UPDATE" = "1" ]; then echo "Оновлення (update)"; else echo "Встановлення (install)"; fi)
echo "[INFO] Mode: $MODE"

if [ "$CALC_VERSION" = "latest" ]; then
    DOWNLOAD_URL="https://github.com/${REPO}/releases/latest/download/${BINARY_NAME}"
else
    DOWNLOAD_URL="https://github.com/${REPO}/releases/download/${CALC_VERSION}/${BINARY_NAME}"
fi

echo "[1/3] Downloading Resource Calculator ${CALC_VERSION}..."
TMP_ARCHIVE="$(mktemp)"
if command -v curl >/dev/null 2>&1; then
    curl -fsSL "$DOWNLOAD_URL" -o "$TMP_ARCHIVE" || { echo "ERROR: download failed"; rm -f "$TMP_ARCHIVE"; exit 1; }
elif command -v wget >/dev/null 2>&1; then
    wget -q -O "$TMP_ARCHIVE" "$DOWNLOAD_URL" || { echo "ERROR: download failed"; rm -f "$TMP_ARCHIVE"; exit 1; }
fi

echo "[2/3] Installing..."
rm -rf "$APP_BUNDLE" 2>/dev/null || true
mkdir -p "/Applications"

tar -xzf "$TMP_ARCHIVE" -C "/Applications"
rm -f "$TMP_ARCHIVE"

if [ ! -d "$APP_BUNDLE" ]; then
    echo "ERROR: App bundle not found after extraction"
    exit 1
fi

chmod +x "$APP_BUNDLE/Contents/MacOS/ITE.ResourceCalculator" 2>/dev/null || true

echo "[3/3] Done."
echo ""
echo "=============================================="
echo "   Resource Calculator $MODE complete!"
echo "=============================================="
echo ""
echo "Installed: $APP_BUNDLE"
echo ""
echo "Launch: open '$APP_BUNDLE'"
echo ""
