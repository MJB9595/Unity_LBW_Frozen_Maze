#!/bin/bash
# MCP for Unity HTTP client helper.
# Usage:
#   ./mcp.sh init
#   ./mcp.sh call <method> '<json_params>'
#   ./mcp.sh tool <name> '<json_args>'
#   ./mcp.sh res  <uri>
URL="http://127.0.0.1:8080/mcp"
DIR="$(cd "$(dirname "$0")" && pwd)"
SID_FILE="$DIR/.session"
HDRS_FILE="$DIR/.last_headers"
HDR_ACCEPT='Accept: application/json, text/event-stream'
HDR_CT='Content-Type: application/json'

pick_response() {
  # Reads the last response body from $DIR/.last_body and prints the JSON-RPC
  # result/error frame, ignoring notification frames.
  python3 - "$DIR/.last_body" <<'PY'
import sys, json, pathlib
path = pathlib.Path(sys.argv[1])
buf = path.read_text(errors="replace") if path.exists() else ""
frames = []
current = []
for line in buf.splitlines():
    if line.startswith("data: "):
        current.append(line[6:])
    elif line == "" and current:
        frames.append("\n".join(current))
        current = []
if current:
    frames.append("\n".join(current))

chosen = None
for f in frames:
    try:
        obj = json.loads(f)
    except Exception:
        continue
    if isinstance(obj, dict) and ("result" in obj or "error" in obj):
        chosen = obj
        break

if chosen is None:
    for f in reversed(frames):
        try:
            chosen = json.loads(f)
            break
        except Exception:
            continue

if chosen is None:
    sys.stdout.write(buf)
else:
    print(json.dumps(chosen, indent=2, ensure_ascii=False))
PY
}

post() {
  local body="$1"
  local extra=()
  if [ -f "$SID_FILE" ]; then
    extra+=( -H "mcp-session-id: $(cat "$SID_FILE")" )
  fi
  local out
  out=$(curl -sS -m 60 -X POST "$URL" \
    -H "$HDR_CT" -H "$HDR_ACCEPT" "${extra[@]}" \
    -D "$HDRS_FILE" \
    -d "$body")
  echo "$out" > "$DIR/.last_body"
  echo "$out"
}

cmd="${1:-}"
case "$cmd" in
  init)
    rm -f "$SID_FILE"
    body='{"jsonrpc":"2.0","id":1,"method":"initialize","params":{"protocolVersion":"2024-11-05","capabilities":{},"clientInfo":{"name":"kiro","version":"1.0"}}}'
    raw=$(post "$body")
    # The init response carries the new session id in headers.
    sid=$(grep -i '^mcp-session-id:' "$HDRS_FILE" | awk '{print $2}' | tr -d '\r\n')
    if [ -n "$sid" ]; then echo -n "$sid" > "$SID_FILE"; fi
    echo "[session: $(cat "$SID_FILE" 2>/dev/null)]"
    # Save raw body for debugging.
    echo "$raw" > "$DIR/.last_body"
    pick_response
    # Send "initialized" notification per MCP spec, but don't let its empty
    # 202 response overwrite our last headers/body.
    curl -sS -m 10 -X POST "$URL" \
      -H "$HDR_CT" -H "$HDR_ACCEPT" \
      -H "mcp-session-id: $(cat "$SID_FILE")" \
      -d '{"jsonrpc":"2.0","method":"notifications/initialized"}' >/dev/null
    ;;
  call)
    method="${2:-}"; params="${3:-}"
    if [ -z "$params" ]; then
      body=$(printf '{"jsonrpc":"2.0","id":%d,"method":"%s"}' "$RANDOM" "$method")
    else
      body=$(printf '{"jsonrpc":"2.0","id":%d,"method":"%s","params":%s}' "$RANDOM" "$method" "$params")
    fi
    raw=$(post "$body")
    pick_response
    ;;
  tool)
    name="${2:-}"; args="${3:-}"
    # If args is empty, read from stdin (most robust against shell quoting).
    if [ -z "$args" ]; then
      args=$(cat)
    elif [[ "$args" == @* ]]; then
      argfile="${args:1}"
      args=$(cat "$argfile")
    fi
    params=$(MCP_NAME="$name" MCP_ARGS="$args" python3 -c "
import json,os
print(json.dumps({'name':os.environ['MCP_NAME'],'arguments':json.loads(os.environ['MCP_ARGS'])}))
")
    body=$(MCP_PARAMS="$params" python3 -c "
import json,os,random
print(json.dumps({'jsonrpc':'2.0','id':random.randint(1,2**31-1),'method':'tools/call','params':json.loads(os.environ['MCP_PARAMS'])}))
")
    raw=$(post "$body")
    pick_response
    ;;
  res)
    uri="${2:-}"
    params=$(python3 -c "import json,sys;print(json.dumps({'uri':sys.argv[1]}))" "$uri")
    body=$(printf '{"jsonrpc":"2.0","id":%d,"method":"resources/read","params":%s}' "$RANDOM" "$params")
    raw=$(post "$body")
    pick_response
    ;;
  list-resources)
    body=$(printf '{"jsonrpc":"2.0","id":%d,"method":"resources/list"}' "$RANDOM")
    raw=$(post "$body")
    pick_response
    ;;
  list-tools)
    body=$(printf '{"jsonrpc":"2.0","id":%d,"method":"tools/list"}' "$RANDOM")
    raw=$(post "$body")
    pick_response
    ;;
  *)
    echo "usage: $0 {init|call <method> [params]|tool <name> [args]|res <uri>}" >&2
    exit 2
    ;;
esac
