#!/usr/bin/env bash
set -euo pipefail

repo_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
output_dir="${1:-$repo_root/artifacts/migrations}"

mkdir -p "$output_dir"
dotnet ef migrations bundle \
  --project "$repo_root/src/Detara.Infrastructure/Detara.Infrastructure.csproj" \
  --startup-project "$repo_root/src/Detara.Api/Detara.Api.csproj" \
  --configuration Release \
  --target-runtime linux-x64 \
  --self-contained \
  --force \
  --output "$output_dir/detara-migrate"

sha256sum "$output_dir/detara-migrate" > "$output_dir/detara-migrate.sha256"
