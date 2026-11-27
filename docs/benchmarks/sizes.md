# Binary sizes

The size of the core assembly, release build, and of the native AOT `stm` CLI. CI fails a change that grows either
by more than 5% over the size here. `mise run size` checks, and `mise run size -- --accept` rewrites the rows of
the machine it runs on. CI checks on each runner of its build job, which reads the `stm` rows of `linux-x64`,
`osx-arm64` and `win-x64`.

| File | RID | Bytes |
|---|---|---:|
| SplashTheMusical.dll | any | 1279488 |
| stm | linux-x64 | 5213032 |
| stm | osx-arm64 | 4948592 |
| stm | win-x64 | 5298176 |
