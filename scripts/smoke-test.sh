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

# True when one of the app's windows has the focus (not the launcher or another app).
app_in_front() { "$ADB" shell dumpsys window | grep -E "mCurrentFocus|mFocusedApp" | grep -q "$PKG"; }

# Waits up to $1 seconds for a UI dump with a line matching the awk program $2; prints its output.
wait_for_ui() {
    local until=$(( $(date +%s) + $1 )) found
    while [ "$(date +%s)" -lt "$until" ]; do
        found=$(dump_ui | awk -F'|' "$2")
        [ -n "$found" ] && { printf '%s\n' "$found"; return 0; }
        sleep 2
    done
    return 1
}

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
    # Count cards only on the Initiatives tab: Home also shows up to five "ending soon" cards.
    # The selected tab is the one whose label is the large one.
    cards=$(dump_ui | awk -F'|' -v x="$ix" '
        $4 ~ /navigation_bar_item_large_label_view/ { sel = ($5 - x < 0 ? x - $5 : $5 - x) < 100 }
        $2 ~ /\. .*\. / { n++ }
        END { print (sel ? n + 0 : 0) }')
    [ "$cards" -ge 2 ] && break
    sleep 5
done
[ "$cards" -ge 2 ] || fail "no initiatives listed after ${SYNC_TIMEOUT}s"
echo "first sync: $(( $(date +%s) - started ))s, $cards cards on screen"
check_log "list"

step "Open an initiative"
card=$(wait_for_ui 20 '$2 ~ /\. .*\. / { print $2 "|" $5 "|" $6; exit }') || fail "no card to open"
IFS="|" read -r _ cx cy <<< "$card"
"$ADB" shell input tap "$cx" "$cy"
# The details page is pushed on the tab, so its toolbar gets an up button (top left, clickable, with a description);
# the list, a tab root, has none. A slow device may take a while.
opened=0
for _ in $(seq 1 15); do
    sleep 2
    app_in_front || fail "the app left the screen opening an initiative"
    dump_ui | awk -F'|' '$1 == "true" && $2 != "" && $5 < 250 && $6 < 300 { f = 1 } END { exit !f }' && { opened=1; break; }
done
[ "$opened" = 1 ] || fail "the initiative did not open"
app_alive || fail "the app died opening an initiative"
check_log "details"
"$ADB" shell input keyevent KEYCODE_BACK
wait_for_ui 20 '$2 ~ /\. .*\. / { print; exit }' >/dev/null || fail "back did not return to the list"

for i in 2 3 0; do
    step "Tab $((i + 1))"
    read -r tx ty <<< "${TABS[$i]}"
    app_in_front || fail "the app is not in front before tab $((i + 1))"
    "$ADB" shell input tap "$tx" "$ty"; sleep 3
    app_alive || fail "the app died on tab $((i + 1))"
    check_log "tab $((i + 1))"
done

step "Rotate and come back"
"$ADB" shell settings put system accelerometer_rotation 0
"$ADB" shell settings put system user_rotation 1; sleep 3
"$ADB" shell settings put system user_rotation 0; sleep 3
{ app_alive && app_in_front; } || fail "the app died or left the screen on rotation"
check_log "rotation"

printf '\nPASS: smoke test\n'
