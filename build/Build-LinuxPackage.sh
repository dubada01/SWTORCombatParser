#!/usr/bin/env bash
set -euo pipefail

repo_dir="$(cd "$(dirname "$0")/.." && pwd)"
cd "$repo_dir"
version="$(dotnet msbuild SWTORCombatParser.csproj -nologo -getProperty:Version -p:RuntimeIdentifier=linux-x64)"
if [[ ! "$version" =~ ^[0-9]+\.[0-9]+\.2$ ]]; then
    echo "Linux versions must use major.minor.platform and end in .2: $version" >&2
    exit 1
fi
output_dir="${1:-$repo_dir/.codex-build-temp/linux-release-$version}"
mkdir -p "$output_dir"
output_dir="$(cd "$output_dir" && pwd)"
if [[ -n "$(ls -A "$output_dir")" ]]; then
    echo "Choose an empty output directory: $output_dir" >&2
    exit 1
fi
package_name="Orbs-$version-linux-x64"
package_dir="$output_dir/$package_name"

# The Linux startup probe imports the .NET OpenSSL shim directly. Keep the
# complete native runtime beside the app instead of hiding it in a single file.
dotnet publish SWTORCombatParser.csproj -c Release -r linux-x64 \
    --self-contained true -p:PublishSingleFile=false \
    -p:RuntimeFrameworkVersion="${ORBS_DOTNET_RUNTIME_VERSION:-8.0.33}" \
    -o "$package_dir"
test -s "$package_dir/libSystem.Security.Cryptography.Native.OpenSsl.so"
test -s "$package_dir/libSkiaSharp.so"
rm -f "$package_dir/Orbs.pdb"
cp PLATFORM-SUPPORT.txt "$package_dir/PLATFORM-SUPPORT.txt"
cat > "$package_dir/run.sh" <<'LAUNCHER'
#!/usr/bin/env sh
set -eu
app_dir=$(CDPATH= cd -- "$(dirname -- "$0")" && pwd)
cd "$app_dir"
exec "$app_dir/Orbs" "$@"
LAUNCHER
chmod 755 "$package_dir/run.sh" "$package_dir/Orbs"
cat > "$package_dir/README.txt" <<README
Orbs $version for Linux x64

Extract the complete folder and run ./run.sh as your normal desktop user.
No separate .NET installation is required. Requires a glibc-based x86_64
Linux desktop with X11 or XWayland. Keep all extracted files together.

Online features need OpenSSL 3. On Fedora 45 install openssl3-libs.
Use ./run.sh --offline for offline log review, overlays and timers.
Use ./run.sh --check-runtime to check the OpenSSL dependency.
For VM graphics problems: ORBS_SOFTWARE_RENDERING=1 ./run.sh
Optional timer audio needs your distribution's libVLC runtime/plugins.

Read PLATFORM-SUPPORT.txt for dependencies and desktop limitations.
Versions use major.minor.platform; Linux is always x.x.2.
README
COPYFILE_DISABLE=1 tar -czf "$output_dir/$package_name.tar.gz" -C "$output_dir" "$package_name"
(
    cd "$output_dir"
    if command -v sha256sum >/dev/null; then
        sha256sum "$package_name.tar.gz" > "$package_name.tar.gz.sha256"
    else
        shasum -a 256 "$package_name.tar.gz" > "$package_name.tar.gz.sha256"
    fi
)
echo "Linux release: $output_dir/$package_name.tar.gz"
