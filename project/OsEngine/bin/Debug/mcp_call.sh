#!/bin/bash
# Usage: ./mcp_call.sh <method_name> [json_arguments]
# Example: ./mcp_call.sh wiki_securities_mapping_info '{"query":"Сбербанк","limit":10}'

API_KEY="osengine-mcp-default-key"
URL="http://localhost:6500/api/v1/mcp"

METHOD="$1"

if [ -z "$METHOD" ]; then
    echo "Usage: $0 <method_name> [json_arguments]"
    exit 1
fi

if [ -z "$2" ]; then
    ARGS="{}"
else
    ARGS="$2"
fi

RESPONSE=$(cat <<EOF | curl -s -H "X-Api-Key: $API_KEY" -H "Content-Type: application/json" -d @- "$URL"
{"jsonrpc":"2.0","method":"tools/call","params":{"name":"$METHOD","arguments":$ARGS},"id":1}
EOF
)

if command -v powershell >/dev/null 2>&1; then
    echo "$RESPONSE" | powershell -Command '$r = $input | ConvertFrom-Json; if ($r.result.Content.Count -gt 0 -and $r.result.Content[0].Type -eq "text") { $r.result.Content[0].Text | ConvertFrom-Json | ConvertTo-Json -Depth 10 } else { $r | ConvertTo-Json -Depth 10 }'
else
    echo "$RESPONSE"
fi
