#!/usr/bin/env bash
# The deterministic gate behind gate-runner. Runs in the current checkout (any worktree):
#   epic-gate.sh <story|epic> <base> [--skip-mutation]
# Runs the 8 checks of docs/development.md#checks, the pending-model-changes check, the API
# client sync check, a scan for added suppressions, and Stryker on the touched backend
# projects (story: --since:<base>; epic: a full run). Logs go to stderr; stdout gets exactly one
# JSON object:
#   {pass, mode, base, head, checks:[{name,pass,seconds,tail}],
#    mutation:[{project,score,target,pass}], suppressions:[{file,line,text}]}
# Exit 0 when everything passes, 1 otherwise. --skip-mutation is for local smoke tests only.
set -euo pipefail

usage() {
  echo "usage: epic-gate.sh <story|epic> <base> [--skip-mutation]" >&2
  exit 2
}

mode=""
base=""
skip_mutation=0
for arg in "$@"; do
  case "$arg" in
    --skip-mutation) skip_mutation=1 ;;
    *)
      if [[ -z "$mode" ]]; then
        mode=$arg
      elif [[ -z "$base" ]]; then
        base=$arg
      else
        usage
      fi
      ;;
  esac
done
[[ "$mode" == story || "$mode" == epic ]] || usage
[[ -n "$base" ]] || usage

# stdout is reserved for the JSON result; everything else goes to stderr.
exec 3>&1 1>&2

top=$(git rev-parse --show-toplevel)
cd "$top"
export MSBUILDDISABLENODEREUSE=1

work=$(mktemp -d)
trap 'rm -rf "$work"' EXIT
: >"$work/checks.ndjson"
: >"$work/mutation.ndjson"
: >"$work/suppressions.tsv"

head=$(git rev-parse HEAD)
# Prefer the remote ref: a local main or epic branch may be stale.
if git rev-parse --verify -q "origin/$base" >/dev/null; then
  ref="origin/$base"
else
  ref=$base
fi

record() { # name pass seconds tail
  jq -nc --arg name "$1" --argjson pass "$2" --argjson seconds "$3" --arg tail "$4" \
    '{name:$name, pass:$pass, seconds:$seconds, tail:$tail}' >>"$work/checks.ndjson"
}

run_check() { # name command...
  local name=$1 start=$SECONDS status=0 tail_text
  shift
  echo "== $name" >&2
  "$@" >"$work/out.log" 2>&1 || status=$?
  tail_text=$(tail -n 15 "$work/out.log" | cut -c1-300)
  cat "$work/out.log" >&2
  if ((status == 0)); then
    record "$name" true $((SECONDS - start)) "$tail_text"
  else
    record "$name" false $((SECONDS - start)) "$tail_text"
  fi
}

if ! git rev-parse --verify -q "$ref" >/dev/null; then
  record "base ref" false 0 "cannot resolve base '$base'"
else
  run_check "pnpm lint" pnpm lint
  run_check "pnpm format:check" pnpm format:check
  run_check "pnpm typecheck" pnpm typecheck
  run_check "pnpm test" pnpm test
  run_check "pnpm build" pnpm build
  run_check "dotnet build" dotnet build backend/WattWise.slnx -nologo -v q -warnaserror
  run_check "dotnet format" dotnet format backend/WattWise.slnx --verify-no-changes
  run_check "dotnet test" dotnet test --solution backend/WattWise.slnx
  run_check "pending model changes" bash -c \
    'dotnet tool restore && dotnet ef migrations has-pending-model-changes --project backend/src/WattWise.Infrastructure'
  run_check "api client in sync" bash -c \
    'pnpm generate:api && git diff --exit-code -- backend/openapi packages/api-client'

  # Suppression scan: added lines in code files, not in the agent tooling or the docs.
  echo "== suppression scan" >&2
  start=$SECONDS
  git diff -U0 "$ref"...HEAD -- . ':(exclude).claude' ':(exclude)docs' |
    awk '
      /^\+\+\+ / { file = substr($0, 7); next }
      /^@@ / {
        match($0, /\+[0-9]+/)
        line = substr($0, RSTART + 1, RLENGTH - 1) + 0
        next
      }
      /^\+/ {
        text = substr($0, 2)
        code = (file ~ /\.(cs|ts|tsx|js|jsx|mjs|cjs)$/)
        hit = 0
        if (code && text ~ /#pragma warning disable|SuppressMessage|eslint-disable|Skip[[:space:]]*=|\.skip\(|\.only\(/) hit = 1
        if (file ~ /(^|\/)stryker-config\.json$/ && text ~ /"(high|low|break)"|thresholds/) hit = 1
        if (hit) printf "%s\t%d\t%s\n", file, line, text
        line++
      }
    ' >"$work/suppressions.tsv" || true
  count=$(wc -l <"$work/suppressions.tsv")
  if ((count == 0)); then
    record "suppression scan" true $((SECONDS - start)) "no added suppressions"
  else
    record "suppression scan" false $((SECONDS - start)) "$count added suppression(s) or skips"
  fi

  # Mutation testing on the touched backend source projects.
  if ((!skip_mutation)); then
    declare -A targets=([Domain]=88.46 [Application]=78.12 [Infrastructure]=57.42 [Api]=51.24)
    declare -A tests=(
      [Domain]="backend/tests/WattWise.Domain.Tests/WattWise.Domain.Tests.csproj"
      [Application]="backend/tests/WattWise.Application.Tests/WattWise.Application.Tests.csproj"
      [Infrastructure]="backend/tests/WattWise.Infrastructure.Tests/WattWise.Infrastructure.Tests.csproj backend/tests/WattWise.Infrastructure.IntegrationTests/WattWise.Infrastructure.IntegrationTests.csproj"
      [Api]="backend/tests/WattWise.Api.Tests/WattWise.Api.Tests.csproj"
    )
    dotnet tool restore >&2 || true
    for project in Domain Application Infrastructure Api; do
      git diff --name-only "$ref"...HEAD -- "backend/src/WattWise.$project" | grep -q . || continue
      echo "== stryker $project ($mode)" >&2
      out="backend/StrykerOutput/$project"
      rm -rf "$out"
      args=(-f backend/stryker-config.json -O "$out" -p "WattWise.$project.csproj")
      for t in ${tests[$project]}; do args+=(-tp "$t"); done
      [[ "$mode" != story ]] || args+=("--since:$ref")
      status=0
      dotnet stryker "${args[@]}" || status=$?
      report=$(find "$out" -name mutation-report.json -printf '%T@ %p\n' 2>/dev/null | sort -n | tail -n 1 | cut -d' ' -f2- || true)
      score=null
      if [[ -n "$report" ]]; then
        score=$(jq '
          [.files[].mutants[].status] as $s
          | ($s | map(select(. == "Killed" or . == "Timeout")) | length) as $k
          | ($s | map(select(. == "Survived" or . == "NoCoverage")) | length) as $x
          | if ($k + $x) == 0 then null else ((($k * 10000 / ($k + $x)) | round) / 100) end
        ' "$report")
      fi
      target=${targets[$project]}
      if [[ "$score" == null ]]; then
        # No mutants (nothing to mutate under --since) passes; a failed run with no report does not.
        if [[ -n "$report" || $status -eq 0 ]]; then pass=true; else pass=false; fi
      else
        pass=$(jq -n --argjson s "$score" --argjson t "$target" '$s >= $t')
      fi
      jq -nc --arg project "$project" --argjson score "$score" --argjson target "$target" --argjson pass "$pass" \
        '{project:$project, score:$score, target:$target, pass:$pass}' >>"$work/mutation.ndjson"
    done
  fi
fi

jq -n \
  --arg mode "$mode" --arg base "$base" --arg head "$head" \
  --slurpfile checks "$work/checks.ndjson" \
  --slurpfile mutation "$work/mutation.ndjson" \
  --rawfile supp "$work/suppressions.tsv" '
  ($supp | split("\n") | map(select(length > 0))
    | map(split("\t") | {file: .[0], line: (.[1] | tonumber), text: (.[2:] | join("\t"))})) as $suppressions
  | {
      pass: (($checks | all(.pass)) and ($mutation | all(.pass))),
      mode: $mode,
      base: $base,
      head: $head,
      checks: $checks,
      mutation: $mutation,
      suppressions: $suppressions
    }' >"$work/result.json"

cat "$work/result.json" >&3
[[ "$(jq -r .pass "$work/result.json")" == true ]]
