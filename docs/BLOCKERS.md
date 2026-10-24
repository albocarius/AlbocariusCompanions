# BLOCKERS

## Intermittent dotnet CLR crash (infra) — RESOLVED 2026-10-24
- **Error:** `Fatal error. Internal CLR error. (0x80131506)` at `dotnet build`/`dotnet test` startup, ~10-15% of runs (dotnet SDK 10.0.113, Ubuntu 26.04, kernel 7.0). Same commands pass on rerun.
- **Tried:** `MSBUILDDISABLENODEREUSE=1` + `DOTNET_CLI_USE_MSBUILD_SERVER=0` (kept), `DOTNET_GCHeapHardLimit`, `DOTNET_EnableWriteXorExecute=0` — no change. No kernel log entries.
- **Ruled out:** memory pressure — with the local model server stopped (54 GB free) the rate was 5/15 crashes.
- **Best guess:** runtime bug in this distro build of dotnet 10.0.113 on Ubuntu 26.04 / kernel 7.0.
- **Workaround:** `orchestra/check_worker.sh` retries up to 3 times when this exact message appears. Worth trying Microsoft's own dotnet build (dotnet-install.sh) later.
- **Resolution:** Microsoft's SDK 10.0.402 installed with `dotnet-install.sh --channel 10.0 --install-dir ~/.dotnet-ms`: 0 crashes in 20 build+test runs. `orchestra/config.env` puts it first on PATH (`DOTNET_ROOT`). The 3x retry in check_worker.sh stays as a safety net.
