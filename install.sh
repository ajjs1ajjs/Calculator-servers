#!/bin/bash
# Resource Calculator (Avalonia) - one-line installer/updater (Ubuntu / Debian)
# The SAME command installs on first run and safely UPDATES on subsequent runs:
# Usage: curl -sSL https://raw.githubusercontent.com/ajjs1ajjs/Calculator-servers/main/install.sh | sudo bash

set -e

INSTALL_DIR="/opt/resource-calculator"
APP_NAME="ITE.ResourceCalculator"
DESKTOP_FILE="/usr/share/applications/resource-calculator.desktop"
UM_VERSION="${RESOURCE_CALCULATOR_VERSION:-latest}"
REPO="ajjs1ajjs/Calculator-servers"

if [ "$(id -u)" -ne 0 ]; then
    echo "Please run as root (sudo ./install.sh)"
    exit 1
fi

if [ "$(uname)" != "Linux" ]; then
    echo "ERROR: This installer is for Linux only."
    exit 1
fi

case "$(uname -m)" in
    x86_64|amd64) ARCH="x64"; BINARY="ITE.ResourceCalculator" ;;
    aarch64|arm64) ARCH="arm64"; BINARY="ITE.ResourceCalculator" ;;
    *) echo "ERROR: unsupported architecture: $(uname -m)"; exit 1 ;;
esac

BINARY_NAME="ITE.ResourceCalculator-ubuntu-${ARCH}.tar.gz"

echo "=============================================="
echo "   Resource Calculator - Встановлення"
echo "=============================================="
echo ""

IS_UPDATE=0
if [ -f "$INSTALL_DIR/$BINARY" ] || [ -f "$DESKTOP_FILE" ]; then
    IS_UPDATE=1
fi

if [ "$IS_UPDATE" = "1" ]; then
    echo "[INFO] Update mode detected"
    MODE="Оновлення (update)"
else
    MODE="Встановлення (install)"
fi

if [ "$UM_VERSION" = "latest" ]; then
    DOWNLOAD_URL="https://github.com/${REPO}/releases/latest/download/${BINARY_NAME}"
    VERSION_URL="latest/download/"
else
    DOWNLOAD_URL="https://github.com/${REPO}/releases/download/${UM_VERSION}/${BINARY_NAME}"
    VERSION_URL="download/${UM_VERSION}/"
fi

echo "[1/3] Downloading Resource Calculator ${UM_VERSION}..."
TMP_ARCHIVE="$(mktemp)"
if command -v curl >/dev/null 2>&1; then
    curl -fsSL "$DOWNLOAD_URL" -o "$TMP_ARCHIVE" || { echo "ERROR: download failed"; rm -f "$TMP_ARCHIVE"; exit 1; }
elif command -v wget >/dev/null 2>&1; then
    wget -q -O "$TMP_ARCHIVE" "$DOWNLOAD_URL" || { echo "ERROR: download failed"; rm -f "$TMP_ARCHIVE"; exit 1; }
fi

echo "[2/3] Installing..."
mkdir -p "$INSTALL_DIR"
tar -xzf "$TMP_ARCHIVE" -C "$INSTALL_DIR"
rm -f "$TMP_ARCHIVE"

if [ ! -f "$INSTALL_DIR/$BINARY" ]; then
    echo "ERROR: binary not found after extraction"
    exit 1
fi
chmod +x "$INSTALL_DIR/$BINARY"

# Create desktop file
mkdir -p "$(dirname "$DESKTOP_FILE")"
cat > "$DESKTOP_FILE" <<EOF
[Desktop Entry]
Name=Resource Calculator
Comment=IT-Enterprise Resource Calculator
Exec=$INSTALL_DIR/$BINARY
Icon=$INSTALL_DIR/icon.png
Terminal=false
Type=Application
Categories=Office;Utility;
EOF

# Copy icon if exists
if [ -f "$INSTALL_DIR/icon.png" ]; then
    mkdir -p /usr/share/icons/hicolor/256x256/apps
    cp "$INSTALL_DIR/icon.png" /usr/share/icons/hicolor/256x256/apps/resource-calculator.png 2>/dev/null || true
    update-icon-cache hicolor 2>/dev/null || true
fi

echo "[3/3] Done."
echo ""
echo "=============================================="
echo "   Resource Calculator $MODE complete!"
echo "=============================================="
echo ""
echo "Installed: $INSTALL_DIR/$BINARY"
echo "Launch: $INSTALL_DIR/$BINARY"
echo ""
