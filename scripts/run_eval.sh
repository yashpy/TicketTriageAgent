#!/usr/bin/env bash
# Runs every ticket in eval/sample_tickets.json against the running API,
# prints predicted category vs expected, plus confidence and auto-resolve decision.
# Requires: curl, jq. Requires the API to be running first (dotnet run).

set -euo pipefail

API_URL="${API_URL:-http://localhost:5000}"
SAMPLE_FILE="$(dirname "$0")/../eval/sample_tickets.json"

echo "Running eval against $API_URL"
echo "-------------------------------------------------------------"
printf "%-30s %-18s %-18s %-6s %-14s\n" "SUBJECT" "EXPECTED" "PREDICTED" "MATCH" "STATUS"
echo "-------------------------------------------------------------"

pass=0
total=0

jq -c '.[]' "$SAMPLE_FILE" | while read -r row; do
  subject=$(echo "$row" | jq -r '.subject')
  body=$(echo "$row" | jq -r '.body')
  expected=$(echo "$row" | jq -r '.expected_category')

  response=$(curl -s -X POST "$API_URL/api/tickets" \
    -H "Content-Type: application/json" \
    -d "$(jq -n --arg s "$subject" --arg b "$body" '{subject: $s, body: $b}')")

  predicted=$(echo "$response" | jq -r '.category // "ERROR"')
  status=$(echo "$response" | jq -r '.status // "ERROR"')

  match="NO"
  if [ "$predicted" == "$expected" ]; then
    match="YES"
  fi

  printf "%-30s %-18s %-18s %-6s %-14s\n" "${subject:0:28}" "$expected" "$predicted" "$match" "$status"
done

echo "-------------------------------------------------------------"
echo "Done. For aggregate confidence/auto-resolve stats, hit: GET $API_URL/api/tickets/stats"
