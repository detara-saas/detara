#!/usr/bin/env bash
# Entrada .bak exclusivamente sintética, criada pelo Test-Production.ps1.
set -euo pipefail
[[ -f /.dockerenv && -f /fixtures/input.bak ]] || exit 2
age-keygen -o /fixtures/identity.agekey 2>/dev/null
recipient="$(age-keygen -y /fixtures/identity.agekey)"
gzip -c /fixtures/input.bak | age -r "$recipient" -o /fixtures/input.bak.gz.age
bash /repo/scripts/production/restore-test.sh --confirm-disposable /fixtures/input.bak.gz.age /fixtures/identity.agekey
