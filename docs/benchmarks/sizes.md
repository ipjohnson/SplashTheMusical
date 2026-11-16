# Binary sizes

The size of the core assembly, release build, and of the native AOT `stm` CLI. CI fails a change that grows either
by more than 5% over the size here. `mise run size` checks, and `mise run size -- --accept` rewrites the rows of
the machine it runs on. CI checks on macos-latest, so the `stm` row CI reads is `osx-arm64`.

| File | RID | Bytes |
|---|---|---:|
| SplashTheMusical.dll | any | 477696 |
| stm | osx-arm64 | 1236080 |
