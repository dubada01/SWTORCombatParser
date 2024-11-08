#!/bin/bash

# Check if a version argument is provided
if [ -z "$1" ]; then
  echo "No version provided. Exiting."
  exit 1
fi

# Define variables
VERSION=$1  # Get version from the argument
PUBLISH_DIR="./bin/Release/net8.0/osx-arm64/publish" # Adjust this if your publish directory is different
APP_NAME="Orbs"
MACOS_BUILD_DIR="./MacOSBuilds"
APP_DIR="$MACOS_BUILD_DIR/$APP_NAME.app"
ENTITLEMENTS_PLIST="$MACOS_BUILD_DIR/entitlements.plist"
DEVELOPER_ID="Developer ID Application: David Duba (GVWYC75SS7)"
ZIP_FILE="$APP_NAME.zip"
BUNDLE_IDENTIFIER="com.dubatech.Orbs"
MINIMUM_SYSTEM_VERSION="10.12"
ICON_FILE="./OrbsIcon.png" # Assuming OrbsIcon.png is next to the script

# Step 1: Build and Publish the .NET app
echo "Publishing .NET app..."
dotnet publish ./SWTORCombatParser.csproj -c Release -r osx-arm64 --self-contained -p:PublishSingleFile=true -p:Version=$VERSION

# Step 2: Create .app directory structure
echo "Creating .app structure..."
mkdir -p "$APP_DIR/Contents/MacOS"
mkdir -p "$APP_DIR/Contents/Resources" # Create Resources directory for the icon

# Copy the icon file into the Resources directory
if [ -f "$ICON_FILE" ]; then
  cp "$ICON_FILE" "$APP_DIR/Contents/Resources/OrbsIcon.png"
else
  echo "Icon file $ICON_FILE not found. Exiting."
  exit 1
fi

# Step 3: Move the published files into the app bundle
echo "Moving published files into app bundle..."
cp -R "$PUBLISH_DIR/"* "$APP_DIR/Contents/MacOS/"

# Step 4: Create Info.plist with dynamic version
echo "Creating Info.plist..."
cat > "$APP_DIR/Contents/Info.plist" <<EOL
<?xml version="1.0" encoding="UTF-8"?>
<!DOCTYPE plist PUBLIC "-//Apple//DTD PLIST 1.0//EN"
"https://www.apple.com/DTDs/PropertyList-1.0.dtd">
<plist version="1.0">
<dict>
    <key>CFBundleName</key>
    <string>$APP_NAME</string>
    <key>CFBundleDisplayName</key>
    <string>$APP_NAME</string>
    <key>CFBundleIdentifier</key>
    <string>$BUNDLE_IDENTIFIER</string>
    <key>CFBundleVersion</key>
    <string>$VERSION</string>
    <key>CFBundlePackageType</key>
    <string>APPL</string>
    <key>CFBundleExecutable</key>
    <string>$APP_NAME</string>
    <key>LSMinimumSystemVersion</key>
    <string>$MINIMUM_SYSTEM_VERSION</string>
    <key>CFBundleIconFile</key>
    <string>OrbsIcon</string> <!-- Reference the icon -->
</dict>
</plist>
EOL

# Step 5: Clear extended attributes
xattr -cr $APP_DIR

# Step 6: Sign the app binary with entitlements
echo "Signing the app binary..."
codesign --deep --force --verbose --options runtime --entitlements "$ENTITLEMENTS_PLIST" --sign "$DEVELOPER_ID" "$APP_DIR/Contents/MacOS/$APP_NAME"

# Step 7: Sign the entire app
echo "Signing the app bundle..."
codesign --deep --force --verbose --options runtime --entitlements "$ENTITLEMENTS_PLIST" --sign "$DEVELOPER_ID" "$APP_DIR"

# Step 8: Zip the app using ditto to preserve all necessary metadata
echo "Zipping the app..."
cd "$MACOS_BUILD_DIR" || exit
ditto -c -k --keepParent "$APP_NAME.app" "$ZIP_FILE"

# Step 9: Submit the app for notarization
echo "Submitting app for notarization..."
NOTARIZATION_INFO=$(xcrun notarytool submit "$ZIP_FILE" --keychain-profile "NotaryCredentials" --wait)
echo "$NOTARIZATION_INFO"

# Extract the ID from the output using awk
NOTARIZATION_ID=$(echo "$NOTARIZATION_INFO" | awk '/id:/{print $2; exit}')
echo "Notarization ID: $NOTARIZATION_ID"

# Step 10: Check if notarization was successful and staple the app
if [[ "$NOTARIZATION_INFO" == *"status: Accepted"* ]]; then
    echo "Notarization successful, stapling the app..."
    xcrun stapler staple "$APP_NAME.app"
else
    echo "Notarization failed, fetching logs..."
    xcrun notarytool log "$NOTARIZATION_ID" --keychain-profile "NotaryCredentials"
    exit 1
fi

echo "App creation, signing, and notarization complete!"
