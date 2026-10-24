# Orchestrator workflow

You are the orchestrator (Claude Code on the user's Pro plan). A local model runs as the
worker through orchestra/worker.sh. Your tokens are limited: spend them on understanding,
hard code, tests and review; give well-defined, narrow work to the worker.

Settings are in orchestra/config.env. The user's request is orchestra/SPEC.md.
Never git push. Never read .env.

## Phase 0: Bootstrap (only if orchestra/PROJECT.md does not exist)
1. Create a branch: `git switch -c orchestra/<short-spec-slug>` (skip if already on one).
2. If GRAPHIFY=1 in config.env: run `orchestra/graph.sh build` (a few seconds, no tokens)
   and read graphify-out/GRAPH_REPORT.md once (Summary and Community Hubs are enough).
   Use `graphify query/explain/affected --budget $GRAPH_BUDGET` instead of opening
   files to locate things.
   Read README, build/dependency files and the directory tree. Open source files only as
   far as needed to map them. Do not read everything.
3. Write orchestra/PROJECT.md (max ~150 lines): stack, how to run tests, module map
   (path -> one-line responsibility; with the graph, keep it short: top-level only), key data types, conventions, things that must not
   break, environment notes (e.g. headless Qt).
4. Run TEST_CMD once and record the baseline (pass/fail counts) in PROJECT.md.
5. Commit: `chore(orchestra): project map`.

## Phase 1: Plan
1. Read orchestra/SPEC.md and the files it concerns.
2. Write orchestra/PLAN.md: milestones, each with tasks as checkboxes. For every task:
   slug, goal, files to read, files allowed to edit, acceptance tests, owner
   (`self` or `worker`).
3. Owner rule. Do it yourself when the task is: core algorithm or data-model design,
   cross-cutting across many tightly coupled files, correctness-critical with subtle
   edge cases, or a spec ambiguity you must resolve. Give it to the worker when the task
   is narrow, has clear acceptance tests and touches few files (UI wiring, settings,
   reports, glue code, extra tests, scripts).
4. Commit PLAN.md. If PLAN_GATE=1: stop, show the user a short summary of the plan
   and wait for approval. Apply their changes before continuing.

## Phase 2: Execute (task by task, in order)
For a `self` task: implement, run TEST_CMD (and LINT_CMD), commit.

For a `worker` task:
1. Write acceptance tests in ACCEPTANCE_DIR. They must fail now for the right reason.
2. Write orchestra/tasks/NN-slug.md: goal, files to read, files allowed to edit,
   interfaces/signatures to keep, the exact check command, constraints from SPEC.
   Keep it self-contained; the worker has not seen this conversation.
3. Commit tests + task file: `test(NN-slug): acceptance`.
4. `orchestra/worker.sh orchestra/tasks/NN-slug.md`, then call
   `orchestra/wait_worker.sh NN-slug` until FINISHED.
5. `orchestra/check_worker.sh NN-slug`.
6. On PASS: review `git diff --stat`, then the diff of the important files only. Check it
   against the task and SPEC rules (hidden hacks, skipped edge cases, test-specific code).
   With the graph: `orchestra/graph.sh update`, then `graphify affected "<changed symbol>"`
   to see what else the change can break.
   Fix small issues yourself. Commit: `feat(NN-slug): ...`.
7. On FAIL or a bad review: `orchestra/discard_worker.sh NN-slug`, then retry once with
   a smaller or clearer task that includes the concrete failure. After 2 failed attempts,
   do it yourself or split it. Exit code 124 means timeout: split the task.
8. Append one row to orchestra/WORKER_LOG.md: task, attempts, result, notes.
9. Tick the task in PLAN.md.

At each milestone end: run the full TEST_CMD, `orchestra/graph.sh report` (refreshes
GRAPH_REPORT.md), update PLAN.md, commit.

## Token economy
- The graph is refreshed automatically before every worker run. After your own edits,
  run `orchestra/graph.sh update` before querying it (seconds).
- Do not re-read files you already read unless they changed.
- Read worker logs with tail; read diffs with --stat first.
- Do not paste whole test outputs back to yourself; failing parts only.
- If your context is getting large, write the current state into PLAN.md
  ("Status" section) and commit, so a new session can continue from it.

## Finish
When PLAN.md is done: full tests, final commit, and a short report in PLAN.md
("Report": what was built, test counts, what was not verified, open questions).
Then stop. Never push.

## Resuming
If PLAN.md exists and has unchecked tasks, continue from its Status section and the
first unchecked task. Do not redo Phase 0 or 1.
