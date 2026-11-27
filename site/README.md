# Site

The documentation site, built with VitePress and published to GitHub Pages by `.github/workflows/site.yml`.

```bash
mise run site-dev        # serve the site at http://localhost:5173/SplashTheMusical/
mise run site-build      # build it into site/.vitepress/dist
mise run site-snapshot   # add a snapshot of the local L4 rasters to site/public/data
```

`scripts/prepare.mjs` writes `parity.md` from `docs/parity-report.md` before each build.

## Comparison data

The comparison section reads the data `tools/site/snapshot.mjs` writes. The site workflow keeps it in the ref
`refs/site-data`, which is not a branch, so a clone of the repository does not fetch it. On each push to main, CI's
`l4` job uploads the rasters and scores as the `comparison` artifact. The site workflow adds them to the data as a
snapshot, pushes the ref, copies the data to `public/data` and deploys the site.

| File | Holds |
|---|---|
| `snapshots.json` | One entry per snapshot: its id, commit, date, mermaid.js version and per type the outcome counts and mean score |
| `history/<type>.json` | The threshold of the type and per case its entries. A case gets an entry when its images, source, score or outcome change |
| `images/<hh>/<sha256>.png` | Each raster once, by the SHA-256 of its bytes |
| `sources/<hh>/<sha256>.txt` | Each case source once, by the SHA-256 of its bytes |
| `issues.json` | The open comparison issues per case, written at deploy time |

An entry holds `snapshot`, `outcome`, `score`, `oracle`, `stm` and `source`, the last three as hashes. A case that
leaves the corpus gets one entry with `removed`.
