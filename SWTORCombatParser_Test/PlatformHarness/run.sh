#!/usr/bin/env bash
set -euo pipefail
cd "$(dirname "$0")"
dotnet build -v:q
export ORBS_PLATFORM_DLL="$PWD/bin/Debug/net8.0/PlatformHarness.dll"
mode="${1:-x11}"
if [[ "$mode" == "wayland" ]]; then
    export XDG_RUNTIME_DIR
    XDG_RUNTIME_DIR="$(mktemp -d)"
    export XDG_SESSION_TYPE=wayland KWIN_COMPOSE=Q
    export ORBS_PLATFORM_LOG="$XDG_RUNTIME_DIR/result.log"
    cat > "$XDG_RUNTIME_DIR/session.sh" <<'SESSION'
#!/bin/sh
exec dotnet "$ORBS_PLATFORM_DLL" > "$ORBS_PLATFORM_LOG" 2>&1
SESSION
    chmod +x "$XDG_RUNTIME_DIR/session.sh"
    timeout 30s dbus-run-session -- kwin_wayland --virtual --width 1280 --height 1024 --xwayland \
        --no-lockscreen --no-global-shortcuts --no-kactivities --exit-with-session "$XDG_RUNTIME_DIR/session.sh" \
        > "$XDG_RUNTIME_DIR/compositor.log" 2>&1
    cat "$ORBS_PLATFORM_LOG"
    # KWin doesn't propagate the child process exit code on all releases.
    grep -q '^ALL PLATFORM CHECKS PASSED$' "$ORBS_PLATFORM_LOG"
else
    unset WAYLAND_DISPLAY
    export XDG_SESSION_TYPE=x11
    case "$mode" in
        x11) xvfb-run -a dotnet "$ORBS_PLATFORM_DLL" ;;
        openbox) xvfb-run -a dbus-run-session -- bash -c 'openbox >/tmp/orbs-openbox.log 2>&1 & sleep 1; dotnet "$ORBS_PLATFORM_DLL"' ;;
        kwin) xvfb-run -a dbus-run-session -- bash -c 'export QT_X11_NO_MITSHM=1 KWIN_COMPOSE=N; kwin_x11 --replace >/tmp/orbs-kwin.log 2>&1 & sleep 2; dotnet "$ORBS_PLATFORM_DLL"' ;;
        *) echo "Usage: bash run.sh [x11|openbox|kwin|wayland]" >&2; exit 2 ;;
    esac
fi
