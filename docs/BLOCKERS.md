# BLOCKERS

## Intermittent dotnet CLR crash (infra, non-blocking)
- **Error:** `Fatal error. Internal CLR error. (0x80131506)` at `dotnet build`/`dotnet test` startup, ~10-15% of runs (dotnet SDK 10.0.113, Ubuntu 26.04, kernel 7.0). Same commands pass on rerun.
- **Tried:** `MSBUILDDISABLENODEREUSE=1` + `DOTNET_CLI_USE_MSBUILD_SERVER=0` (kept), `DOTNET_GCHeapHardLimit`, `DOTNET_EnableWriteXorExecute=0` — no change. No kernel log entries.
- **Best guess:** runtime bug in this distro build, possibly under memory pressure (local model server uses ~53 GB of 61 GB).
- **Workaround:** `orchestra/check_worker.sh` retries up to 3 times when this exact message appears. Worth trying Microsoft's own dotnet build (dotnet-install.sh) later.
