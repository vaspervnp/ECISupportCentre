#!/usr/bin/env bash
# Smoke test on a connected emulator or device: installs an APK from scratch, starts it, waits for the first sync,
# opens every tab and an initiative, and fails on a crash, an ANR or an empty list. Independent of the device language.
#
#   scripts/smoke-test.sh path/to/app-Signed.apk
#
# Needs adb on PATH (or ADB=/path/to/adb) and python3 (or PYTHON=...). Uses the network of the device.
set -euo pipefail
export MSYS_NO_PATHCONV=1   # Git Bash on Windows: keep /data/... and /sdcard/... paths as they are

APK=${1:?usage: smoke-test.sh path/to/app.apk}
ADB=${ADB:-adb}
PYTHON=${PYTHON:-python3}
command -v "$PYTHON" >/dev/null 2>&1 || PYTHON=python
PKG=io.github.vaspervnp.ecisupportcentre
SYNC_TIMEOUT=${SYNC_TIMEOUT:-180}
WORK=$(mktemp -d)
trap 'rm -rf "$WORK"' EXIT

step() { printf '\n== %s\n' "$*"; }
fail() { printf 'FAIL: %s\n' "$*" >&2; "$ADB" exec-out screencap -p > smoke-test-failure.png 2>/dev/null || true; exit 1; }

# Dumps the UI and prints one line per node: "clickable|content-desc|text|resource-id|x|y" (x, y = centre).
dump_ui() {
    "$ADB" shell uiautomator dump /sdcard/smoke-ui.xml >/dev/null 2>&1 || true
    "$ADB" shell cat /sdcard/smoke-ui.xml > "$WORK/ui.xml" 2>/dev/null || true
    "$PYTHON" -c "$PARSE_UI" < "$WORK/ui.xml"
}
PARSE_UI=$(cat <<'PY'
import re, sys
xml = sys.stdin.buffer.read().decode('utf-8', 'replace')
for node in re.findall(r'<node [^>]*>', xml):
    attr = dict(re.findall(r'([\w-]+)="([^"]*)"', node))
    x1, y1, x2, y2 = map(int, re.findall(r'\d+', attr.get('bounds', '[0,0][0,0]')))
    print('|'.join([attr.get('clickable', ''), attr.get('content-desc', ''), attr.get('text', ''),
                    attr.get('resource-id', ''), str((x1 + x2) // 2), str((y1 + y2) // 2)]))
PY
)

app_alive() { [ -n "$("$ADB" shell pidof "$PKG" | tr -d '\r')" ]; }

check_log() {
    if "$ADB" logcat -d | grep -E "FATAL EXCEPTION|ANR in $PKG|Abort message" | grep -q -E "$PKG|FATAL|Abort"; then
        "$ADB" logcat -d | grep -E -A15 "FATAL EXCEPTION|ANR in $PKG|Abort message" | head -40 >&2
        fail "crash or ANR in logcat ($1)"
    fi
}

tab_centres() { dump_ui | awk -F'|' '$4 ~ /navigation_bar_item_icon_container/ { print $5, $6 }'; }

step "Install $APK"
"$ADB" wait-for-device
"$ADB" uninstall "$PKG" >/dev/null 2>&1 || true
"$ADB" install "$APK" >/dev/null
"$ADB" shell pm grant "$PKG" android.permission.POST_NOTIFICATIONS 2>/dev/null || true
"$ADB" logcat -c

step "Start and wait for the first sync (up to ${SYNC_TIMEOUT}s)"
"$ADB" shell monkey -p "$PKG" -c android.intent.category.LAUNCHER 1 >/dev/null 2>&1
started=$(date +%s)
mapfile -t TABS < <(for _ in $(seq 1 60); do t=$(tab_centres); [ "$(printf '%s\n' "$t" | grep -c .)" -ge 4 ] && { printf '%s\n' "$t"; break; }; sleep 2; done)
[ "${#TABS[@]}" -ge 4 ] || fail "the tab bar did not appear"
echo "tabs at: ${TABS[*]}"

# Initiatives tab: cards are nodes whose description joins title, phase and supporters with ". ".
read -r ix iy <<< "${TABS[1]}"
cards=0
while [ $(( $(date +%s) - started )) -lt "$SYNC_TIMEOUT" ]; do
    "$ADB" shell input tap "$ix" "$iy"; sleep 3
    app_alive || fail "the app is not running"
    cards=$(dump_ui | awk -F'|' '$2 ~ /\. .*\. / { n++ } END { print n + 0 }')
    [ "$cards" -ge 2 ] && break
    sleep 5
done
[ "$cards" -ge 2 ] || fail "no initiatives listed after ${SYNC_TIMEOUT}s"
echo "first sync: $(( $(date +%s) - started ))s, $cards cards on screen"
check_log "list"

step "Open an initiative"
read -r cx cy < <(dump_ui | awk -F'|' '$2 ~ /\. .*\. / { print $5, $6; exit }')
"$ADB" shell input tap "$cx" "$cy"; sleep 4
app_alive || fail "the app died opening an initiative"
check_log "details"
"$ADB" shell input keyevent KEYCODE_BACK; sleep 2

for i in 2 3 0; do
    step "Tab $((i + 1))"
    read -r tx ty <<< "${TABS[$i]}"
    "$ADB" shell input tap "$tx" "$ty"; sleep 3
    app_alive || fail "the app died on tab $((i + 1))"
    check_log "tab $((i + 1))"
done

step "Rotate and come back"
"$ADB" shell settings put system accelerometer_rotation 0
"$ADB" shell settings put system user_rotation 1; sleep 3
"$ADB" shell settings put system user_rotation 0; sleep 3
app_alive || fail "the app died on rotation"
check_log "rotation"

printf '\nPASS: smoke test\n'
