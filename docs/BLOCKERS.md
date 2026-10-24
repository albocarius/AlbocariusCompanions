# BLOCKERS

## Intermittent dotnet CLR crash (infra, non-blocking)
- **Error:** `Fatal error. Internal CLR error. (0x80131506)` at `dotnet build`/`dotnet test` startup, ~10-15% of runs (dotnet SDK 10.0.113, Ubuntu 26.04, kernel 7.0). Same commands pass on rerun.
- **Tried:** `MSBUILDDISABLENODEREUSE=1` + `DOTNET_CLI_USE_MSBUILD_SERVER=0` (kept), `DOTNET_GCHeapHardLimit`, `DOTNET_EnableWriteXorExecute=0` — no change. No kernel log entries.
- **Ruled out:** memory pressure — with the local model server stopped (54 GB free) the rate was 5/15 crashes.
- **Best guess:** runtime bug in this distro build of dotnet 10.0.113 on Ubuntu 26.04 / kernel 7.0.
- **Workaround:** `orchestra/check_worker.sh` retries up to 3 times when this exact message appears. Worth trying Microsoft's own dotnet build (dotnet-install.sh) later.
