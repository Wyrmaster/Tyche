#!/bin/sh
# Runs the Rider/ReSharper code inspections headless and fails on any warning or error.
# The complexity limits are enforced by the build (see Directory.Build.props).
set -eu
cd "$(dirname "$0")"

report="$(mktemp -t inspectcode.XXXXXX.sarif)"
log="$(mktemp -t inspectcode.XXXXXX.log)"
trap 'rm -f "$report" "$log"' EXIT

dotnet tool restore > /dev/null
dotnet build --nologo -v quiet
dotnet jb inspectcode Tyche.slnx --output="$report" --format=Sarif --severity=WARNING --no-build > "$log" 2>&1 || { cat "$log"; exit 1; }

python3 - "$report" <<'PY'
import json, sys

results = json.load(open(sys.argv[1]))['runs'][0].get('results', [])
for result in results:
    location = result['locations'][0]['physicalLocation']
    print(f"{result['level']} {result['ruleId']} {location['artifactLocation']['uri']}:"
          f"{location['region'].get('startLine')} {result['message']['text']}")
if results:
    sys.exit(f'inspectcode: {len(results)} issue(s)')
print('inspectcode: no issues')
PY
