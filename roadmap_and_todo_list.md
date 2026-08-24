# AirVPN/Eddie — Roadmap & Contribution Tracker

**Repo**: github.com/AirVPN/Eddie (VPN Tunnel Desktop Edition — OpenVPN/WireGuard UI + CLI)
**Fork**: chrisdebian/Eddie — remote `origin`; upstream is remote `upstream`
**Licence**: GPLv3 | **Languages**: C# (.NET 4/7) + C++ (native elevated helpers)
**Onboarded**: 2026-08-24, via a self-sent "Claude Code" handoff email (see `project_airvpn_eddie_status` memory)
**Surveyed at**: commit `010e3391`, tag `v2.26.2` (2026-07-09) — re-verified 2026-08-24, still latest

## Ground rules (from the onboarding email, Chris's own standing rules for this repo)

- British English throughout — code comments, commit messages, PRs.
- No AI/LLM signal in commit messages or PR descriptions (external project).
- No PR submitted without local build + manual test first.
- Small, concrete, scoped PRs only — **no community-facing proposal documents** (the "bitmagnet
  lesson": a broad roadmap-proposal issue drew a hostile "AI slop" reaction there — see
  `project_bitmagnet_status`). Code first.
- No `CONTRIBUTING.md` exists yet — until Phase 6 adds one, treat "small scoped PR, tested
  locally" as the implicit norm.

## Fact-check corrections (2026-08-24, against a fresh clone + live GitHub/Repology checks)

The original roadmap was largely accurate — most claims (latest tag/commit, no CI, no governance
files, zero tests, `App.Checking`'s real role, tag-debt counts down to the exact per-file
breakdown, 39 issues/2 PRs) checked out exactly. Five corrections:

1. **Elevated-privilege project list was incomplete** — actual is 7 projects, not 5:
   `App.CLI.Linux.Elevated`, `App.CLI.Linux.Elevated.Service`, `App.CLI.MacOS.Elevated`,
   `App.CLI.MacOS.Elevated.Service`, `App.CLI.Windows.Elevated`,
   `App.CLI.Windows.Elevated.Service`, `Lib.CLI.Elevated`. The original claim's
   `App.Service.Windows.Elevated` doesn't exist under that name — real name is
   `App.CLI.Windows.Elevated.Service`, and the `.Linux.Elevated.Service`/`.MacOS.Elevated.Service`
   variants were missing entirely.
2. **AirVPN already has a published security policy**: https://airvpn.org/security_policy/,
   contact security@airvpn.org, PGP key published. Phase 2 below is corrected to "add SECURITY.md
   pointing to this" rather than drafting a new policy from scratch.
3. **nlohmann/json is ~5 years behind** — vendored header states 3.9.1 (2020), current upstream is
   3.12.0 (April 2025). Checked GitHub security advisories and the web for a CVE specifically
   requiring the bump; nothing found that isn't about *consumers* mishandling exceptions rather
   than a fixed-in-later-nlohmann-json bug. Treat as staleness/hygiene risk, not a proven
   vulnerability — don't oversell this in any PR/issue.
4. **Repology coverage was incomplete** — besides the AUR packages, there's a Pacstall listing
   (`eddie-ui-deb`, v2.24.6, outdated, Repology flags its upstream download link as dead) the
   original roadmap missed.
5. **Tag-naming inconsistency**: tags up to `2.24.6` have no `v` prefix (`2.21.8`, `2.24.6`), while
   `v2.26.2` onward does. The original roadmap's suggested regression-check command
   (`git log v2.21.8..v2.24.6`) would fail outright — correct form is `git log 2.21.8..2.24.6`.

**Not yet verified** (parked, both low-priority Phase 7 items):
- The actual 2.21.8→2.24.6 changelog diff for the reported perf/stuttering regression — unshallowing
  the full clone history timed out twice; needs a background/longer-timeout attempt before trusting
  either way.
- The AUR `eddie-ui` comment thread's nftables-dependency complaint — the AUR page is behind Anubis
  anti-bot protection, blocked automated fetch. Needs a manual look or a different fetch approach.

## Priority order

1. **Phase 2 — SECURITY.md** (corrected): quick, low-risk, high value — point to the existing
   AirVPN policy. Good low-friction first PR to gauge review responsiveness before anything larger.
2. **Phase 1 — CI foundation**: GitHub Actions build workflow (Linux/macOS/Windows) + CodeQL. Do
   this before Phase 4 (test coverage) so there's somewhere for tests to actually run, and before
   Phase 3's Dependabot wiring.
3. **Phase 5 — Tag debt**: start with TOCLEAN (14, low-risk deletions) as a second easy scoped PR;
   TOFIX (35, concentrated in `Engine.cs` and the elevated C++ helpers) pairs naturally with Phase 4.
4. **Phase 3 — Vendored dependency audit**: bump nlohmann/json, record versions for the other four
   unpinned vendored libs in a `DEPENDENCIES.md`.
5. **Phase 4 — Test coverage**: starting with `Lib.Core/Engine.cs` (C#, xUnit) and the two
   highest-tag-debt elevated C++ files (`ping.cpp`, `impl.cpp`).
6. **Phase 6 — Repository hygiene**: `CONTRIBUTING.md`, issue templates, README badges (version,
   licence, Repology — no CI badge until Phase 1 actually lands).
7. **Phase 7 — Packaging**: lowest priority, do after 1–2 land. Includes the two unverified items
   above.

## Phase 0 — Baseline (done, 2026-08-24)

- [x] Confirm latest tag/commit — `010e3391`, 2026-07-09, `v2.26.2`. Re-verified 2026-08-24, still
  current.
- [x] Confirm no CI/workflow directory — confirmed, `.github` doesn't exist.
- [x] Confirm no governance files — confirmed, none of CONTRIBUTING/SECURITY/CODE_OF_CONDUCT exist.
- [x] Confirm zero test projects — confirmed, no `.csproj` references a test framework, no
  test-named file/dir anywhere.
- [x] Confirm `App.Checking` is a build-prep script — confirmed, patches version/copyright headers
  ahead of release, not a test harness.

## Phase 1 — CI foundation

- [ ] GitHub Actions workflow building the CLI edition on Linux/macOS/Windows on every PR (mirrors
  the AUR PKGBUILD's msbuild approach)
- [ ] CodeQL analysis (free for public repos), covering both C# and C++ — highest-value single
  addition, since nothing today scans the elevated-privilege native helpers for memory-safety
  issues
- [ ] Dependabot config for the vendored dependency manifest (Phase 3) so stale headers get flagged
  automatically going forward
- [ ] Verify after merge: a workflow run actually appears under Actions, CodeQL produces a Security
  tab with results

## Phase 2 — Vulnerability disclosure path

- [ ] Add `SECURITY.md` at repo root linking to the existing https://airvpn.org/security_policy/
  policy (security@airvpn.org, PGP key published there) — do **not** draft a new policy, confirmed
  2026-08-24 one already exists

## Phase 3 — Vendored dependency audit

- [ ] Bump nlohmann/json from 3.9.1 (2020) to current (3.12.0, April 2025) — no specific CVE found
  requiring this, frame the PR as routine staleness hygiene, not a security fix
- [ ] PStreams, sha256.h, base64.h, yxml.h have no version pin or upstream source URL recorded
  in-tree — check each project's current upstream, record the pinned commit/version in a
  `DEPENDENCIES.md`
- [ ] Once versions are confirmed/bumped, wire into the Phase 1 Dependabot config if the vendoring
  approach allows it, otherwise document the manual re-check cadence

## Phase 4 — Test coverage, starting with the highest-risk code

- [ ] Add a test project (xUnit, for the net7 C# projects), starting with `Lib.Core/Engine.cs`
  (connection state machine — central, carries the most tag debt of any C# file)
- [ ] For the native C++ elevated helpers, evaluate Catch2 or GoogleTest scoped to the
  parsing/validation logic in `Lib.CLI.Elevated/src/ping.cpp` and
  `App.CLI.Linux.Elevated/src/impl.cpp` first — highest tag debt among the C++ files, run as root
- [ ] Add Coverlet (.NET) and wire a coverage report into the Phase 1 CI workflow, even starting
  from a low baseline

## Phase 5 — Tag debt (77 total at 2026-08-24 survey)

Tag definitions per `src/readme.coding.md`: TOTRANSLATE, TODO, TOOPTIMIZE, TOCLEAN, TOFIX, TOCHECK,
TOTEST, TOCONTINUE, WIP.

Counts confirmed 2026-08-24: TOFIX 35, TOCLEAN 14, TODO 10, WIP 8, TOOPTIMIZE 4, TOTRANSLATE 4,
TOCHECK 2, TOTEST 0, TOCONTINUE 0.

Top files: `src/Lib.Core/Engine.cs` (7), `src/App.CLI.Linux.Elevated/src/impl.cpp` (7),
`src/Lib.CLI.Elevated/src/ping.cpp` (6), `src/Lib.Core/ConnectionTypes/OpenVPN.cs` (5),
`src/Lib.Platform.Linux/Platform.cs` (4), `src/Lib.Core/ConfigBuilder/OpenVPN.cs` (4).

- [ ] Re-run the sweep before starting any tag-debt PR — counts drift
  (`for tag in TOTRANSLATE TODO TOOPTIMIZE TOCLEAN TOFIX TOCHECK TOTEST TOCONTINUE WIP; do echo -n
  "$tag: "; grep -rEn "// *${tag}\b" --include="*.cs" --include="*.cpp" --include="*.h"
  --include="*.mm" . | wc -l; done`)
- [ ] Prioritise TOFIX items in the elevated helpers and `Engine.cs` first — overlaps with Phase 4's
  test-coverage targets, fix-and-test can land in the same PR
- [ ] TOCLEAN items (14, deprecated code marked for deletion) — lower risk, good first scoped
  contribution
- [ ] Submit each fix as its own small PR referencing the specific tag and line

## Phase 6 — Repository hygiene / contribution funnel

- [ ] `CONTRIBUTING.md` covering: build instructions per `src/readme.coding.md`, the tag
  conventions, testing-before-PR expectations, and a note that this project has no documented
  AI-contribution policy today
- [ ] Minimal issue template distinguishing platform (Linux/macOS/Windows) and edition (CLI/UI)
- [ ] README badges: latest release/tag (shields.io), licence (GPLv3), Repology badge linking to
  https://repology.org/project/eddie-ui/versions — **no CI badge until Phase 1 actually lands**
- [ ] Link from top-level README.md to `src/readme.coding.md` — right now a new contributor has no
  way to discover the tag conventions without digging into `src/`
- [ ] Consider: short feature list, screenshot/UI preview, minimal "build from source" pointer

## Phase 7 — Packaging & distribution (lowest priority, after Phase 1–2)

- [ ] Re-verify Repology coverage before acting (checked 2026-08-24: AUR `eddie-ui`/`eddie-ui-git`
  up to date at v2.26.2; Pacstall `eddie-ui-deb` outdated at v2.24.6 with a dead upstream link per
  Repology; `eddie-cli`/`eddie-cli-git` on AUR only, up to date)
- [ ] AUR nftables-dependency complaint (Network Lock errors without nftables manually installed,
  not declared as a package dependency) — **not yet verified**, AUR comments page blocked by
  anti-bot protection during the 2026-08-24 check; needs a manual look
- [ ] 2.21.8→2.24.6 perceived perf/stuttering regression — **not yet verified**, full-history clone
  timed out twice during the 2026-08-24 check. Correct diff command is `git log 2.21.8..2.24.6
  --oneline` (no `v` prefix on either tag — see tag-naming correction above)
- [ ] Consider Flathub submission for the Linux UI edition, once CI (Phase 1) exists to build it
  reliably

## Technical Debt

Per project policy, only markers with a concrete contribution in mind are tracked in detail here —
external project, not batch-filing the maintainer's own legacy debt. Full counts are in Phase 5
above (77 total TOFIX/TOCLEAN/TODO/WIP/TOOPTIMIZE/TOTRANSLATE/TOCHECK markers, zero TOTEST/
TOCONTINUE). No unmarked stub/broken-interface implementations checked yet — do a
`NotImplementedException`/`throw new NotSupportedException` sweep before starting Phase 4's test
work, since that phase will surface any hiding there anyway.
