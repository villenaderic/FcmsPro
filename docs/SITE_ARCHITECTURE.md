# Website architecture

The website at <https://villenaderic.github.io/FcmsPro/> is **derived, never edited**
for releases. The only thing you do to ship a version is push a tag.

```
git tag vX.Y.Z && git push origin vX.Y.Z
        |
        v
"Build, Test, and Release"  (.github/workflows/build-and-release.yml)
   test -> build Win / macOS / Linux -> release -> appcast
   release job: notes from CHANGELOG.md, SHA256SUMS.txt attached
        |   workflow_run (completed, success, tag push)
        v
"Deploy website"            (.github/workflows/pages.yml)
   1. scripts/build-site-data.py   GitHub API (GITHUB_TOKEN) -> _build/data/*.json
   2. scripts/build-site.py        site/ + data + USER_GUIDE.md + CHANGELOG.md -> _site/
   3. verify, upload-pages-artifact, deploy-pages, smoke test
```

Nothing in this pipeline commits to the repository.

## Why `workflow_run` + cron (and not `release: published`)

Events created with the default `GITHUB_TOKEN` do not start other workflows. The
release is published *inside* the release workflow by `GITHUB_TOKEN`, so a
`release: published` or `push` trigger on the Pages workflow would never fire.
`workflow_run` is the documented exception: it fires when the release workflow
ends, i.e. after the `appcast` job, so the release, its four installers and
`SHA256SUMS.txt` all exist.

The 6-hourly cron makes the site self-healing if a trigger is ever missed.
`push` (to site files) and `workflow_dispatch` cover everything else.

`workflow_run` only runs workflows that exist on the **default branch**, so
`pages.yml` must be on `main` before the chain works.

## Where each fact on the site comes from

| Shown on the site | Source |
| --- | --- |
| Version, release date | Latest non-draft, non-prerelease GitHub Release |
| Download links, file sizes | Release assets, matched by extension (`.exe`, `.dmg`, `.deb`, `.AppImage`), never by file name |
| SHA-256 | Asset `digest` field, else `SHA256SUMS.txt` |
| "What's new" | Release body, which CI fills from the `## [X.Y.Z]` section of `CHANGELOG.md`; falls back to that section if the body is empty |
| Release history | Last 10 releases (pre-releases flagged, drafts hidden) |
| Changelog page | `CHANGELOG.md` parsed by `build-site-data.py` |
| Docs page | `docs/USER_GUIDE.md` rendered by `build-site.py` |

## Freshness layers (visitor side)

1. **Pre-rendered HTML.** Version and links are already in the page: works with
   JS off and for crawlers.
2. **`./data/release.json`** fetched with `cache: "no-cache"`; the DOM updates only
   if it is newer than what was pre-rendered.
3. **`api.github.com/.../releases/latest`**, cached 10 minutes in `sessionStorage`.
4. **Static fallback:** every button already points to
   `https://github.com/villenaderic/FcmsPro/releases/latest`.

No download link in `site/` may be a versioned file name. The workflow's
"Guard" step greps for that and fails the build.

## Safety rails

- `build-site-data.py` exits non-zero if the latest stable release has no `.exe`
  (same philosophy as `.github/generate_appcast.py`). The previous deployment
  stays live.
- A `workflow_run` deploy refuses to ship if `release.json` is not the tag that
  triggered it. The next cron run retries.
- Missing `CHANGELOG.md` section: the release job logs a warning and falls back
  to GitHub-generated notes. It does not fail the release.

## Things that must not change

- `docs/appcast.xml`: path and format. The shipped app's updater reads it from
  `raw.githubusercontent.com/.../main/docs/appcast.xml`.
- `.github/set-version.sh` and the five-job structure of the release workflow.

## Local development

```bash
python3 scripts/test-site-data.py
python3 scripts/test-extract-changelog.py

# offline build from a fixture, no network
RELEASE_JSON_FILE=scripts/fixtures/release-1.6.9.json \
  python3 scripts/build-site-data.py --out _build/data --changelog CHANGELOG.md
python3 scripts/build-site.py --src site --data _build/data \
  --guide docs/USER_GUIDE.md --changelog CHANGELOG.md \
  --site-url http://localhost:8000/ --out _site
python3 -m http.server -d _site 8000
```

`scripts/make-brand-assets.py` regenerates the icons and social card from
`docs/logo.png` (needs Pillow; outputs are committed).
