# Workflow: `dapsman prod offline` / `dapsman prod online`

Design notes for the offline/online pair. See `readme-cli-commands.md` for user-facing documentation.

## What the workflow does

`prod offline` replaces the project's `*.prod.caddy` file on the remote server with a config that
serves a static "temporarily offline" page (HTTP 503) and reloads Caddy. `prod online` copies the
original `*.prod.caddy` back and reloads again. The project's containers are untouched — this is a
front-door switch, not a shutdown, so the site can be worked on (restore, migration, incident
response) while visitors see a maintenance page instead of a broken one.

The offline content comes from one of two places, in order:

1. `_caddy_sites/<project>.offline.caddy`, if the project ships one — full manual control.
2. A generated 503 block, otherwise.

Both plans go through the standard plan/print/execute path, so `--dry-run` shows what would change.

## Generating the offline block

The generated block must cover **every domain the prod file serves**, or the uncovered domains fall
through to Caddy's default handling while the project is offline — the visitor gets a connection
error or a stray other site, not the maintenance page.

Two shapes have to be handled, and real projects use both:

```caddy
j-shirt.com, www.j-shirt.com, test.j-shirt.com {   # comma-separated addresses in one block
	reverse_proxy jshirt-site:8080
}

api.j-shirt.com {                                  # a second, separate site block
	reverse_proxy jshirt-api:8081
}
```

`ReadSiteAddresses` in `RemoteOfflineStatusPlanBuilder` walks the prod file tracking brace depth and
collects the address list from every line at depth 0 that ends in `{`. Depth tracking is what keeps
nested blocks (`basicauth { … }`, `handle { … }`) from being mistaken for site openers. All collected
addresses are de-duplicated and emitted as a single offline site block.

### The bug this replaced

The original implementation took the first non-comment line and split it on `' '` and `'{'`, keeping
only token zero. On `j-shirt.com, www.j-shirt.com {` that yields `j-shirt.com,` — a site address with
a trailing comma, which is invalid Caddyfile syntax and fails the reload — and on a multi-block file
it silently dropped every block after the first. Found during the July 2026 loccom incident (see
`postmortem-2026-07-loccom-compromise.md`), where offline mode was needed under time pressure and
would not load.

### Trade-off: redirects become 503s

A `www` block that only does `redir https://apex{uri} permanent` is folded into the same offline
block as the apex domain, so while offline the `www` host answers with the maintenance page rather
than redirecting. That is the intended behavior: during maintenance every hostname the project owns
should say the same thing, and preserving a redirect to a domain that is itself 503 adds a hop for
no benefit. A project that wants different behavior ships its own `.offline.caddy`.

## Limits

- Brace counting is textual. A prod file with an unbalanced `{` or `}` inside a quoted string could
  throw off depth tracking. Caddy placeholders (`{uri}`) are balanced and parse correctly.
- The generated page is not customizable beyond replacing it wholesale with `.offline.caddy`. If
  per-project branding becomes common, a template file under `daps/caddy/` would be the next step.
