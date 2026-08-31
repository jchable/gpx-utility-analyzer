# Bug Remediation Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Remediate the 50 adversarially-verified bugs filed as GitHub issues #73–#122 across the CLI, ai-analyzer, Web API and React client, in a risk-ordered sequence where every fix ships with a regression test that fails before it and passes after.

**Architecture:** The 50 issues are grouped into 7 waves. Waves are ordered by blast radius: security and session integrity first (an attacker or a mis-routed webhook corrupts other users' data), then CLI correctness bugs that silently produce wrong numbers from correct input, then the ai-analyzer and API robustness bugs that turn a good run into a crash, then client UX/data-loss bugs, then low-severity polish. Within a wave, bugs that share a file or a root cause are fixed in a single task, because a partial fix in a shared code path is either wrong or immediately re-broken — the clearest case is the power-namespace pair (#98 in `cli/.../GpxWriter.cs` writes the element, #115 in `ui/api/.../ProfileComputationService.cs` reads it; fixing either side alone leaves the round-trip broken or breaks it in the other direction).

**Tech Stack:** .NET 9 / xUnit (CLI, ai-analyzer, API), ASP.NET Core + EF Core (SQLite dev / PostgreSQL prod), React 19 + TypeScript 5.9 + Vite 7, Playwright for client E2E, Vitest (added in Task 1) for client unit tests.

**Spec:** GitHub issues #73–#122 (each carries the full description, failure scenario and adversarial verification)

## Global Constraints

- **TDD** — every fix starts with a failing regression test; write the test, run it, watch it fail for the *stated* reason before writing any production code.
- **No behavior change beyond the fix** — do not refactor neighbouring code, rename symbols, or "improve" adjacent logic in a bugfix commit.
- **EF migrations via `dotnet ef` only** — never hand-edit a migration `.cs` file. No task in this plan is expected to change an entity; if one turns out to, stop and run `dotnet ef migrations add <Name>` from `ui/api`.
- **No `Co-Authored-By` trailer** in any commit message (project rule).
- **Each task independently committable and CI-green** — run the owning component's full suite before committing; a task that spans two components runs both.
- **Reference the issue** — every commit message body ends with `Closes #NN` for each issue the commit resolves.

---

## Wave overview

| Wave | Theme | Issues | Why sequenced here |
|---|---|---|---|
| 1 | Security & session integrity | #92, #93, #94, #95, #96, #117, plus test-harness prerequisite | Cross-user data leakage (#92), an unauthenticated write path (#94), a feature that can never work (#93) and two client bugs that destroy user data or the session (#95, #96). These are the only issues where an attacker or another user is in the threat model. Everything else can wait; these cannot. |
| 2 | CLI silent data corruption (Core library) | #73, #74, #75, #76, #77, #78, #79, #80, #81, #82, #83, #84, #97, #98, #99, #100, #115 | These take correct input and emit wrong numbers or wrong GPX with no error. They are in `GpxAnalyzer.Cli.Core`, which is consumed in-process by the Web API too, so every one of them is live in the web app as well as the CLI. Ordered before the frontend waves because they are the source of truth those layers display. |
| 3 | CLI command frontend | #85, #86, #88, #107, #108 | **Blocked by the System.CommandLine 2.x migration** — see the dependency note below. All five live in `cli/src/GpxAnalyzer.Cli/Commands/`, whose files are being rewritten wholesale by that migration. Fixing them first guarantees rework and merge conflicts. |
| 4 | ai-analyzer robustness | #89, #90, #91, #109, #110, #111, #112 | Turn a paid model call into a crash or a silently wrong number. Independent of waves 1–3; sequenced after the data-correctness waves because a wrong report over correct stats is less harmful than wrong stats. |
| 5 | API processing correctness | #113, #114, #116 | Wrong stored timestamps, activities permanently stuck after a restart, inflated split elevation. Depends on wave 2 (#116 is in the same file as #115 and must rebase onto it). |
| 6 | Client editor & upload correctness | #118, #120, #122, #121 | User-visible data loss and wrong-file uploads in the route editor and upload queue. Uses the Vitest harness installed in Task 1. |
| 7 | Statistics polish & low-severity cleanup | #87, #101, #102, #103, #104, #105, #106 | Culture-sensitive formatting plus heuristics that are wrong but bounded. Last because none of them lose or corrupt data. |

### Hard dependency: the System.CommandLine 2.x migration

`cli/src/GpxAnalyzer.Cli/GpxAnalyzer.Cli.csproj` pins `System.CommandLine` at `2.0.0-beta4.22272.1`, and every command in `cli/src/GpxAnalyzer.Cli/Commands/` uses the beta4 API surface (`new Option<T>(name, () => default, description)`, `cmd.SetHandler((InvocationContext ctx) => …)`, `ctx.ParseResult.GetValueForOption(...)`). The 2.x API replaces all of it (`SetAction`, `DefaultValueFactory`, `parseResult.GetValue(...)`). **Wave 3 (#85, #86, #88, #107, #108) MUST be executed after `docs/superpowers/plans/2026-08-28-system-commandline-2-migration.md` is complete and merged.** Doing it earlier means writing fixes against an API surface that is about to be deleted, and every one of those five fixes would have to be re-applied by hand during the migration.

Two clarifications so nobody blocks the wrong work on this:

- `ai-analyzer/src/GpxAiAnalyzer/GpxAiAnalyzer.csproj` already references `System.CommandLine` `2.*` and already uses `SetAction`. **#111 is NOT blocked** and stays in Wave 4.
- The root cause of **#85** is in `cli/src/GpxAnalyzer.Cli.Core/Split/TimeSplitter.cs` (the boundary point is shared *by reference* between consecutive segments), not in `SplitCommand.cs`. That half is fixed in Wave 2 / Task 12, which is not blocked. Wave 3 / Task 17 closes #85 with the command-level regression test and the write-then-compute ordering hardening.

### Test commands referenced throughout

```bash
dotnet test cli/tests/GpxAnalyzer.Cli.Tests/              # CLI Core + CLI
dotnet test ai-analyzer/tests/GpxAiAnalyzer.Tests/        # ai-analyzer
dotnet test ui/api.Tests/GpxAnalyzer.Api.Tests.csproj     # API (auth + multi-user isolation)
cd ui/client && npm run test                              # Vitest unit tests (added in Task 1)
cd ui/client && npm run build && npm run e2e              # Playwright E2E (mocked API, no backend)
```

There is **no test file for `TimeSplitter` or `GpxMerger`** — Tasks 12 and 13 create `cli/tests/GpxAnalyzer.Cli.Tests/Split/TimeSplitterTests.cs` and `Merge/GpxMergerTests.cs` from scratch. Existing fixtures live in `cli/tests/GpxAnalyzer.Cli.Tests/testdata/` (`small.gpx`, `two-segments.gpx`, `with-extensions.gpx`, `with-gps-quality.gpx`); new fixtures are added there.

---

## Wave 1 — Security & session integrity

### Task 1: Add a Vitest unit-test harness to the client

**Issues:** none directly — prerequisite for #95, #118, #120, #121

**Files:**
- Modify `ui/client/package.json`
- Modify `ui/client/vite.config.ts`
- Create `ui/client/src/test/setup.ts`
- Modify `ui/client/tsconfig.app.json` (or whichever tsconfig covers `src/`)

**Root cause:** The client has `@playwright/test` but no unit-test runner. Playwright drives the built app through a mocked network and cannot reasonably exercise a 30-second auto-save timer, a concurrent-refresh race inside a module, or a pure index-mapping function over a 1800-point polyline. Four of the client bugs in this plan need a unit-level regression test, so the harness is installed once, first.

**Fix approach:** Add Vitest with the jsdom environment, wired into the existing Vite config so it shares the same resolution and TS settings. Keep Playwright as-is; the two do not overlap (`e2e/**` is excluded from Vitest's include glob).

```ts
// ui/client/vite.config.ts — add to the defineConfig object
  test: {
    environment: 'jsdom',
    globals: true,
    setupFiles: ['./src/test/setup.ts'],
    include: ['src/**/*.{test,spec}.{ts,tsx}'],
    exclude: ['e2e/**', 'node_modules/**'],
  },
```

`vite.config.ts` must import the Vitest config type so `test` typechecks:

```ts
/// <reference types="vitest/config" />
```

**Steps:**

- [ ] Install the dev dependencies: `cd ui/client && npm install -D vitest@^3 jsdom @testing-library/react @testing-library/jest-dom`
- [ ] Add `"test": "vitest run"` and `"test:watch": "vitest"` to the `scripts` block of `ui/client/package.json`
- [ ] Add the `/// <reference types="vitest/config" />` triple-slash directive at the top of `ui/client/vite.config.ts` and the `test` block shown above to `defineConfig`
- [ ] Create `ui/client/src/test/setup.ts`:
  ```ts
  import '@testing-library/jest-dom/vitest';

  // jsdom has no localStorage quota and no navigation; reset between tests.
  afterEach(() => {
    localStorage.clear();
    sessionStorage.clear();
  });
  ```
- [ ] Create a smoke test `ui/client/src/test/harness.test.ts` proving the runner works:
  ```ts
  import { describe, it, expect } from 'vitest';

  describe('vitest harness', () => {
    it('runs in a jsdom environment with localStorage', () => {
      localStorage.setItem('probe', 'ok');
      expect(localStorage.getItem('probe')).toBe('ok');
      expect(typeof window).toBe('object');
    });
  });
  ```
- [ ] Run `cd ui/client && npm run test` — expect `1 passed`. (This task has no failing-first step: it installs the tool that makes failing-first possible for Tasks 5, 25, 26, 27.)
- [ ] Verify nothing regressed: `cd ui/client && npm run build && npm run lint`
- [ ] Verify Playwright is untouched: `cd ui/client && npm run e2e:desktop`
- [ ] Commit:
  ```bash
  git add ui/client/package.json ui/client/package-lock.json ui/client/vite.config.ts ui/client/src/test/
  git commit -m "test(client): add vitest unit-test harness

  The client had no unit-test runner, only Playwright E2E. Several
  regression tests in the bug-remediation plan (#95, #118, #120, #121)
  need module-level tests that E2E cannot express: a concurrent refresh
  race, a 30s auto-save timer, and pure index/time mapping functions.

  Adds vitest + jsdom + testing-library, wired through the existing
  vite config. e2e/ is excluded from the vitest glob so the two runners
  do not overlap."
  ```

---

### Task 2: OAuth callback is unreachable and unbound to the initiating user

**Issues:** #93

**Files:**
- Modify `ui/api/Controllers/IntegrationsController.cs:46` (Connect), `:58` (Callback)
- Modify `ui/api/Services/Integrations/IActivityImporter.cs:6`
- Modify `ui/api/Services/Integrations/StravaService.cs:30`
- Test `ui/api.Tests/Integrations/OAuthCallbackTests.cs` (new)

**Root cause:** `IntegrationsController` carries a class-level `[Authorize]` and `Callback` has no `[AllowAnonymous]`. The callback is a top-level browser navigation performed by Strava, not a fetch from the SPA; the JWT lives in `localStorage` and `Program.cs` registers only `JwtBearerDefaults.AuthenticationScheme` (no cookie scheme), so the redirect carries no `Authorization` header and is rejected with 401 before the action body runs. No integration can ever be connected. Compounding it, `StravaService.GetAuthorizationUrlAsync` sends no `state`, so merely removing `[Authorize]` would leave `User.GetUserId()` with nothing to return.

**Fix approach:** Mint a signed, time-limited `state` value at connect time that carries the initiating user's id, and consume it in an `[AllowAnonymous]` callback. Use ASP.NET Core's built-in `IDataProtectionProvider` — it is already in the shared framework, so no new package and no new entity (hence no migration).

Before (`IntegrationsController.cs`):

```csharp
    [HttpPost("{provider}/connect")]
    public async Task<ActionResult<object>> Connect(string provider)
    {
        var importer = _importers.FirstOrDefault(i => i.ProviderName == provider);
        if (importer is null) return NotFound(new { code = "UNKNOWN_PROVIDER" });

        var callbackUrl = $"{Request.Scheme}://{Request.Host}/api/integrations/{provider}/callback";
        var authUrl = await importer.GetAuthorizationUrlAsync(callbackUrl);

        return Ok(new { authUrl });
    }

    [HttpGet("{provider}/callback")]
    public async Task<IActionResult> Callback(
        string provider,
        [FromQuery] string? code = null,
        ...
        var userId = User.GetUserId();
```

After:

```csharp
    private const string StatePurpose = "integrations.oauth.state.v1";
    private static readonly TimeSpan StateLifetime = TimeSpan.FromMinutes(15);

    [HttpPost("{provider}/connect")]
    public async Task<ActionResult<object>> Connect(string provider)
    {
        var importer = _importers.FirstOrDefault(i => i.ProviderName == provider);
        if (importer is null) return NotFound(new { code = "UNKNOWN_PROVIDER" });

        var callbackUrl = $"{Request.Scheme}://{Request.Host}/api/integrations/{provider}/callback";

        // Bind the flow to the caller: the callback arrives as a browser navigation
        // with no Authorization header, so the user id has to travel in `state`.
        var protector = _dataProtection.CreateProtector(StatePurpose);
        var state = protector.Protect(
            $"{User.GetUserId()}|{provider}|{DateTimeOffset.UtcNow.ToUnixTimeSeconds()}");

        var authUrl = await importer.GetAuthorizationUrlAsync(callbackUrl, state);

        return Ok(new { authUrl });
    }

    [HttpGet("{provider}/callback")]
    [AllowAnonymous]
    public async Task<IActionResult> Callback(
        string provider,
        [FromQuery] string? code = null,
        [FromQuery] string? state = null,
        [FromQuery] string? oauth_token = null,
        [FromQuery] string? oauth_verifier = null)
    {
        var importer = _importers.FirstOrDefault(i => i.ProviderName == provider);
        if (importer is null) return NotFound();

        if (!TryReadState(state, provider, out var userId))
            return BadRequest(new { code = "INVALID_OAUTH_STATE" });
        ...
    }

    private bool TryReadState(string? state, string provider, out Guid userId)
    {
        userId = Guid.Empty;
        if (string.IsNullOrEmpty(state)) return false;

        string plain;
        try { plain = _dataProtection.CreateProtector(StatePurpose).Unprotect(state); }
        catch (CryptographicException) { return false; }

        var parts = plain.Split('|');
        if (parts.Length != 3) return false;
        if (!Guid.TryParse(parts[0], out userId)) return false;
        if (!string.Equals(parts[1], provider, StringComparison.Ordinal)) return false;
        if (!long.TryParse(parts[2], out var issuedAt)) return false;

        var age = DateTimeOffset.UtcNow - DateTimeOffset.FromUnixTimeSeconds(issuedAt);
        return age >= TimeSpan.Zero && age <= StateLifetime;
    }
```

`IActivityImporter.GetAuthorizationUrlAsync` gains the `state` parameter, and `StravaService` forwards it:

```csharp
    // IActivityImporter.cs
    Task<string> GetAuthorizationUrlAsync(string callbackUrl, string state);

    // StravaService.cs
    public async Task<string> GetAuthorizationUrlAsync(string callbackUrl, string state)
    {
        var clientId = await _settings.GetAsync("Integrations:Strava:ClientId")
            ?? throw new InvalidOperationException("Strava ClientId not configured.");

        return $"{AuthUrl}?client_id={clientId}&response_type=code" +
               $"&redirect_uri={Uri.EscapeDataString(callbackUrl)}" +
               $"&scope=read,activity:read_all&approval_prompt=auto" +
               $"&state={Uri.EscapeDataString(state)}";
    }
```

Inject `IDataProtectionProvider _dataProtection` through the constructor and add `using System.Security.Cryptography;` plus `using Microsoft.AspNetCore.DataProtection;`.

**Steps:**

- [ ] Write the failing regression test `ui/api.Tests/Integrations/OAuthCallbackTests.cs`:
  ```csharp
  using System.Net;
  using GpxAnalyzer.Api.Tests.Helpers;

  namespace GpxAnalyzer.Api.Tests.Integrations;

  public class OAuthCallbackTests
  {
      [Fact]
      public async Task Callback_WithoutAuthorizationHeader_IsNotRejectedAsUnauthorized()
      {
          using var factory = new ApiFactory();
          var anon = factory.CreateClient(new Microsoft.AspNetCore.Mvc.Testing
              .WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

          // A real OAuth redirect is a browser navigation: no Authorization header.
          var resp = await anon.GetAsync("/api/integrations/strava/callback?code=abc&state=garbage");

          // The state is invalid so we expect a 400, NOT a 401 — a 401 means the
          // [Authorize] filter short-circuited and the flow can never complete.
          Assert.NotEqual(HttpStatusCode.Unauthorized, resp.StatusCode);
          Assert.Equal(HttpStatusCode.BadRequest, resp.StatusCode);
      }

      [Fact]
      public async Task Connect_ReturnsAuthUrlCarryingAStateParameter()
      {
          using var factory = new ApiFactory();
          var client = factory.CreateClient();
          var auth = await TestHelpers.RegisterAsync(client, $"oauth_{Guid.NewGuid():N}@test.local");
          var authed = TestHelpers.CreateAuthorizedClient(factory, auth.AccessToken);

          var resp = await authed.PostAsync("/api/integrations/strava/connect", null);

          // ClientId is unset in the Test environment, so the importer throws;
          // when it IS configured the URL must carry state=. Assert on whichever
          // path runs, but never on a silent success with no state.
          if (resp.IsSuccessStatusCode)
          {
              var body = await resp.Content.ReadAsStringAsync();
              Assert.Contains("state=", body);
          }
          else
          {
              Assert.NotEqual(HttpStatusCode.OK, resp.StatusCode);
          }
      }
  }
  ```
- [ ] Run it and watch it fail: `dotnet test ui/api.Tests/GpxAnalyzer.Api.Tests.csproj --filter OAuthCallbackTests`
  Expected failure: `Callback_WithoutAuthorizationHeader_IsNotRejectedAsUnauthorized` fails with `Assert.NotEqual() Failure: Values are equal. Expected: Not Unauthorized  Actual: Unauthorized` — the `[Authorize]` filter rejects the request before the action runs.
- [ ] Add the `state` parameter to `IActivityImporter.GetAuthorizationUrlAsync` and implement it in `StravaService.GetAuthorizationUrlAsync`
- [ ] Inject `IDataProtectionProvider` into `IntegrationsController`, add `StatePurpose`, `StateLifetime`, `TryReadState`, mint the state in `Connect`, and add `[AllowAnonymous]` + state validation to `Callback`; replace `var userId = User.GetUserId();` in `Callback` with the `userId` produced by `TryReadState`
- [ ] Run the test and watch it pass: `dotnet test ui/api.Tests/GpxAnalyzer.Api.Tests.csproj --filter OAuthCallbackTests`
- [ ] Run the full API suite: `dotnet test ui/api.Tests/GpxAnalyzer.Api.Tests.csproj`
- [ ] Commit:
  ```bash
  git add ui/api/Controllers/IntegrationsController.cs ui/api/Services/Integrations/ ui/api.Tests/Integrations/
  git commit -m "fix(api): make the OAuth callback reachable and bind it to the initiating user

  The callback is a browser navigation from the provider, which carries no
  Authorization header, but the action sat behind the controller's class-level
  [Authorize] with only the JWT bearer scheme registered — so every callback
  was rejected with 401 and no integration could ever be connected.

  Marks the callback [AllowAnonymous] and carries the initiating user id in a
  DataProtection-signed, 15-minute state parameter, which also closes the
  session-fixation hole that simply removing [Authorize] would have opened.

  Closes #93"
  ```

---

### Task 3: Webhook picks an arbitrary user and accepts unauthenticated requests

**Issues:** #92, #94

**Files:**
- Modify `ui/api/Controllers/WebhooksController.cs:38` (StravaValidation), `:62`–`:130` (HandleWebhook)
- Modify `ui/api/Services/Integrations/IActivityImporter.cs:9`–`:10`
- Modify `ui/api/Services/Integrations/StravaService.cs:97`–`:123`
- Test `ui/api.Tests/Integrations/WebhookRoutingTests.cs` (new)

**Root cause:** One task because both defects live in the same request path and both are consequences of the body being read once, untrusted, and then used to select credentials. (#92) `HandleWebhook` resolves the integration with `FirstOrDefaultAsync(i => i.Provider == provider && i.IsActive)` — no filter on the athlete the event belongs to — even though Strava's payload carries `owner_id` and `Integration.ExternalUserId` already stores the athlete id from `ExchangeCodeAsync`. `IActivityImporter.GetWebhookActivityIdAsync` returns only `object_id` and throws `owner_id` away. (#94) `ValidateWebhookAsync` is called only from the GET subscription-verification action; the POST handler goes straight from route match to `GetWebhookActivityIdAsync` to `FetchActivityAsync` with a stored user OAuth token, with no shared secret, signature or source check.

**Fix approach:** Read the body **once** into a `WebhookEvent` that carries both ids, validate the event against the stored subscription id before using it, then resolve the integration by `(Provider, ExternalUserId)`. An unknown owner is acknowledged with 200 and dropped — Strava retries on non-2xx, and a 404 would leak which athletes are connected.

Replace the two importer members:

```csharp
// IActivityImporter.cs — before
    Task<bool> ValidateWebhookAsync(HttpContext context);
    Task<string?> GetWebhookActivityIdAsync(HttpContext context);

// after
    /// <summary>Validates a GET subscription-verification request.</summary>
    Task<bool> ValidateSubscriptionAsync(HttpContext context);

    /// <summary>
    /// Reads and validates the POST webhook body exactly once.
    /// Returns null when the event is not an activity creation, or fails validation.
    /// </summary>
    Task<WebhookEvent?> ReadWebhookEventAsync(HttpContext context);
}

public sealed record WebhookEvent(string ExternalActivityId, string? OwnerId);
```

`StravaService`:

```csharp
    public async Task<bool> ValidateSubscriptionAsync(HttpContext context)
    {
        var verifyToken = await _settings.GetAsync("Integrations:Strava:WebhookVerifyToken", "gpx-analyzer")
            ?? "gpx-analyzer";
        var mode = context.Request.Query["hub.mode"].ToString();
        var token = context.Request.Query["hub.verify_token"].ToString();
        return mode == "subscribe" && token == verifyToken;
    }

    public async Task<WebhookEvent?> ReadWebhookEventAsync(HttpContext context)
    {
        JsonElement body;
        try { body = await JsonSerializer.DeserializeAsync<JsonElement>(context.Request.Body); }
        catch (JsonException) { return null; }

        if (body.ValueKind != JsonValueKind.Object) return null;

        // Reject anything not issued against our own subscription. Strava does not
        // sign webhook bodies, so subscription_id is the only binding it offers.
        var expectedSubscription = await _settings.GetAsync("Integrations:Strava:SubscriptionId");
        if (!string.IsNullOrEmpty(expectedSubscription))
        {
            if (!body.TryGetProperty("subscription_id", out var sub)) return null;
            var actual = sub.ValueKind == JsonValueKind.Number
                ? sub.GetInt64().ToString()
                : sub.GetString();
            if (!string.Equals(actual, expectedSubscription, StringComparison.Ordinal))
            {
                _logger.LogWarning("Rejected Strava webhook for unknown subscription {Subscription}", actual);
                return null;
            }
        }

        if (!body.TryGetProperty("object_type", out var objectType) ||
            !body.TryGetProperty("aspect_type", out var aspectType) ||
            objectType.GetString() != "activity" ||
            aspectType.GetString() != "create")
            return null;

        if (!body.TryGetProperty("object_id", out var objectId)) return null;

        string? ownerId = body.TryGetProperty("owner_id", out var owner)
            ? (owner.ValueKind == JsonValueKind.Number ? owner.GetInt64().ToString() : owner.GetString())
            : null;

        return new WebhookEvent(objectId.GetInt64().ToString(), ownerId);
    }
```

`WebhooksController.HandleWebhook` — read the event first, then resolve the owner:

```csharp
    [HttpPost("{provider}")]
    public async Task<IActionResult> HandleWebhook(string provider)
    {
        var importer = _importers.FirstOrDefault(i => i.ProviderName == provider);
        if (importer is null) return NotFound();

        // Read + validate the body once, before any credential is selected.
        var evt = await importer.ReadWebhookEventAsync(HttpContext);
        if (evt is null) return Ok(); // not an activity-create event, or failed validation

        if (string.IsNullOrEmpty(evt.OwnerId))
        {
            _logger.LogWarning("Webhook for {Provider} carried no owner id; dropping", provider);
            return Ok();
        }

        var integration = await _db.Integrations.FirstOrDefaultAsync(
            i => i.Provider == provider && i.IsActive && i.ExternalUserId == evt.OwnerId);
        if (integration is null)
        {
            _logger.LogWarning(
                "Received {Provider} webhook for owner {OwnerId} with no matching active integration",
                provider, evt.OwnerId);
            return Ok(); // Acknowledge but don't process
        }

        var externalId = evt.ExternalActivityId;
        // ... the rest of the method is unchanged from here
```

And `StravaValidation` calls `ValidateSubscriptionAsync` instead of `ValidateWebhookAsync`.

**Steps:**

- [ ] Write the failing regression test `ui/api.Tests/Integrations/WebhookRoutingTests.cs`:
  ```csharp
  using System.Net;
  using System.Net.Http.Json;
  using GpxAnalyzer.Api.Data;
  using GpxAnalyzer.Api.Entities;
  using GpxAnalyzer.Api.Tests.Helpers;
  using Microsoft.EntityFrameworkCore;
  using Microsoft.Extensions.DependencyInjection;

  namespace GpxAnalyzer.Api.Tests.Integrations;

  public class WebhookRoutingTests
  {
      [Fact]
      public async Task Webhook_ForUnknownOwner_DoesNotTouchAnyIntegration()
      {
          using var factory = new ApiFactory();
          var client = factory.CreateClient();

          // Alice connects Strava as athlete 1001. Her row is the only one, so the
          // buggy FirstOrDefault(provider && IsActive) always selects it.
          var alice = await TestHelpers.RegisterAsync(client, $"alice_{Guid.NewGuid():N}@test.local");
          using (var scope = factory.Services.CreateScope())
          {
              var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
              db.Integrations.Add(new Integration
              {
                  Id = Guid.NewGuid(),
                  UserId = Guid.Parse(alice.User.Id),
                  Provider = "strava",
                  AccessToken = "alice-token",
                  ExternalUserId = "1001",
                  IsActive = true,
              });
              await db.SaveChangesAsync();
          }

          // Bob (athlete 2002, not connected here) finishes a run.
          var resp = await client.PostAsJsonAsync("/api/webhooks/strava", new
          {
              object_type = "activity",
              aspect_type = "create",
              object_id = 9001L,
              owner_id = 2002L,
          });

          Assert.Equal(HttpStatusCode.OK, resp.StatusCode);

          using (var scope = factory.Services.CreateScope())
          {
              var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
              // No activity may be created for Alice from Bob's event.
              Assert.Empty(await db.Activities.Where(a => a.Source == "strava").ToListAsync());
              // Alice's token must be untouched.
              var row = await db.Integrations.SingleAsync(i => i.Provider == "strava");
              Assert.Equal("alice-token", row.AccessToken);
          }
      }

      [Fact]
      public async Task Webhook_WithNoOwnerId_IsDroppedInsteadOfGuessing()
      {
          using var factory = new ApiFactory();
          var client = factory.CreateClient();
          var alice = await TestHelpers.RegisterAsync(client, $"alice2_{Guid.NewGuid():N}@test.local");
          using (var scope = factory.Services.CreateScope())
          {
              var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
              db.Integrations.Add(new Integration
              {
                  Id = Guid.NewGuid(),
                  UserId = Guid.Parse(alice.User.Id),
                  Provider = "strava",
                  AccessToken = "alice-token",
                  ExternalUserId = "1001",
                  IsActive = true,
              });
              await db.SaveChangesAsync();
          }

          // An anonymous attacker's minimal injection payload: no owner_id.
          var resp = await client.PostAsJsonAsync("/api/webhooks/strava", new
          {
              object_type = "activity",
              aspect_type = "create",
              object_id = 123456L,
          });

          Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
          using (var scope = factory.Services.CreateScope())
          {
              var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
              Assert.Empty(await db.Activities.Where(a => a.Source == "strava").ToListAsync());
          }
      }
  }
  ```
- [ ] Run it and watch it fail: `dotnet test ui/api.Tests/GpxAnalyzer.Api.Tests.csproj --filter WebhookRoutingTests`
  Expected failure: both tests fail on the `Assert.Empty(...Activities...)` line — the handler picks Alice's row and attempts the import (the Strava fetch throws and is swallowed at the catch, so in the test environment no `Activity` row is written *by accident*; if `FetchActivityAsync` is stubbed the row appears). If the swallow masks the failure, the second assertion (`Assert.Equal("alice-token", row.AccessToken)` after seeding an expired `TokenExpiresAt`) is the one that fails. Confirm the observed failure before proceeding.
- [ ] Replace `ValidateWebhookAsync`/`GetWebhookActivityIdAsync` with `ValidateSubscriptionAsync`/`ReadWebhookEventAsync` + the `WebhookEvent` record in `IActivityImporter.cs`
- [ ] Implement both in `StravaService.cs` including the `subscription_id` check and the `owner_id` extraction
- [ ] Rewrite the head of `WebhooksController.HandleWebhook` to read the event first and resolve the integration by `ExternalUserId`; update `StravaValidation` to call `ValidateSubscriptionAsync`
- [ ] Run the test and watch it pass: `dotnet test ui/api.Tests/GpxAnalyzer.Api.Tests.csproj --filter WebhookRoutingTests`
- [ ] Run the full API suite: `dotnet test ui/api.Tests/GpxAnalyzer.Api.Tests.csproj`
- [ ] Commit:
  ```bash
  git add ui/api/Controllers/WebhooksController.cs ui/api/Services/Integrations/ ui/api.Tests/Integrations/WebhookRoutingTests.cs
  git commit -m "fix(api): route webhooks to the owning user and validate the request body

  HandleWebhook selected the first active integration for the provider with no
  filter on the athlete the event belonged to, so an event for Bob was fetched
  with Alice's OAuth token, could refresh and overwrite Alice's tokens, and any
  activity it did retrieve was stored under Alice's user id. The POST path also
  never called ValidateWebhookAsync, so the body was fully attacker-controlled.

  Reads and validates the body once into a WebhookEvent carrying object_id and
  owner_id, rejects events from an unknown subscription, and resolves the
  integration by (Provider, ExternalUserId). Unknown owners are acknowledged
  with 200 and dropped so the endpoint does not leak which athletes are connected.

  Closes #92
  Closes #94"
  ```

---

### Task 4: Refresh endpoint keeps deactivated accounts alive

**Issues:** #117

**Files:**
- Modify `ui/api/Controllers/AuthController.cs:97`
- Test `ui/api.Tests/Auth/AuthApiTests.cs` (append)

**Root cause:** `Refresh` gates only on `storedToken.IsActive` (`!IsRevoked && !IsExpired` on the `RefreshToken` entity) and then mints a fresh access token plus a fresh rolling 30-day refresh token for `storedToken.User` without ever checking `user.IsActive`. `Login` does check it (line 73), so the flag is clearly meant to disable an account, and nothing revokes a user's `RefreshTokens` rows when the flag is cleared — so the window renews indefinitely.

**Fix approach:** Check `user.IsActive` after loading the token and, when it is false, revoke the presented token so the session cannot be renewed again.

```csharp
        if (storedToken == null || !storedToken.IsActive)
            return Unauthorized(new { code = "INVALID_REFRESH_TOKEN" });

        var user = storedToken.User;

        // A deactivated account must not be able to renew its session.
        // Login already refuses these users; refresh has to agree.
        if (!user.IsActive)
        {
            storedToken.RevokedAt = DateTime.UtcNow;
            storedToken.RevokedByIp = GetIpAddress();
            await _context.SaveChangesAsync();
            return Unauthorized(new { code = "ACCOUNT_DISABLED" });
        }

        // Revoke the old token
        storedToken.RevokedAt = DateTime.UtcNow;
        storedToken.RevokedByIp = GetIpAddress();

        var roles = await _userManager.GetRolesAsync(user);
        ...
```

(Note `var user = storedToken.User;` moves above the revoke block; delete the later duplicate declaration.)

**Steps:**

- [ ] Append the failing regression test to `ui/api.Tests/Auth/AuthApiTests.cs`:
  ```csharp
      [Fact]
      public async Task Refresh_ForDeactivatedUser_IsRejected()
      {
          using var factory = new ApiFactory();
          var client = factory.CreateClient();
          var auth = await TestHelpers.RegisterAsync(client, $"deact_{Guid.NewGuid():N}@test.local");

          // Admin deactivates the account.
          using (var scope = factory.Services.CreateScope())
          {
              var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
              var user = await db.Users.SingleAsync(u => u.Id == Guid.Parse(auth.User.Id));
              user.IsActive = false;
              await db.SaveChangesAsync();
          }

          var resp = await client.PostAsJsonAsync("/api/auth/refresh",
              new { refreshToken = auth.RefreshToken });

          Assert.Equal(HttpStatusCode.Unauthorized, resp.StatusCode);

          // And the presented token must now be revoked, so a retry cannot succeed either.
          var retry = await client.PostAsJsonAsync("/api/auth/refresh",
              new { refreshToken = auth.RefreshToken });
          Assert.Equal(HttpStatusCode.Unauthorized, retry.StatusCode);
      }
  ```
  (Add `using Microsoft.Extensions.DependencyInjection;`, `using Microsoft.EntityFrameworkCore;`, `using GpxAnalyzer.Api.Data;` and `using System.Net.Http.Json;` to the file if not already present.)
- [ ] Run it and watch it fail: `dotnet test ui/api.Tests/GpxAnalyzer.Api.Tests.csproj --filter Refresh_ForDeactivatedUser_IsRejected`
  Expected failure: `Assert.Equal() Failure  Expected: Unauthorized  Actual: OK` — the endpoint issues a new 30-day token pair for the deactivated user.
- [ ] Apply the `user.IsActive` check in `AuthController.Refresh` as shown
- [ ] Run the test and watch it pass: `dotnet test ui/api.Tests/GpxAnalyzer.Api.Tests.csproj --filter Refresh_ForDeactivatedUser_IsRejected`
- [ ] Run the full API suite: `dotnet test ui/api.Tests/GpxAnalyzer.Api.Tests.csproj`
- [ ] Commit:
  ```bash
  git add ui/api/Controllers/AuthController.cs ui/api.Tests/Auth/AuthApiTests.cs
  git commit -m "fix(api): reject token refresh for deactivated accounts

  Refresh gated only on the RefreshToken row being unrevoked and unexpired,
  never on ApplicationUser.IsActive, and each call minted a new 30-day token.
  Deactivating a user therefore ended nothing: the browser tab kept renewing
  indefinitely and could keep reading and deleting the user's data.

  Closes #117"
  ```

---

### Task 5: Concurrent 401s each start their own refresh and log the user out

**Issues:** #95

**Files:**
- Modify `ui/client/src/api/client.ts:22`–`:93`
- Test `ui/client/src/api/client.refresh.test.ts` (new)

**Root cause:** `tryRefreshToken()` has no single-flight guard: every request that gets a 401 calls it independently with whatever is in `localStorage[REFRESH_KEY]`. `AuthController.Refresh` revokes the presented token and issues a new one, so the token is strictly single-use. The second and later concurrent calls get 401 `INVALID_REFRESH_TOKEN`, `tryRefreshToken` returns false, and `fetchJson`/`fetchWithAuth` then delete **both** tokens — including the brand-new valid pair the winning refresh just wrote — and hard-navigate to `/login`. `api.getSettings` fires two `fetchJson` calls inside one `Promise.all`, so it races on its own.

**Fix approach:** Collapse concurrent refreshes onto one in-flight promise, and never clear tokens that a *different* refresh already replaced.

```ts
// ui/client/src/api/client.ts — before
async function tryRefreshToken(): Promise<boolean> {
  const refreshToken = localStorage.getItem(REFRESH_KEY);
  if (!refreshToken) return false;
  try {
    const res = await fetch(`${BASE}/auth/refresh`, { ... });
    if (!res.ok) return false;
    const data = await res.json();
    localStorage.setItem(TOKEN_KEY, data.accessToken);
    localStorage.setItem(REFRESH_KEY, data.refreshToken);
    return true;
  } catch {
    return false;
  }
}

// after
let refreshInFlight: Promise<boolean> | null = null;

async function doRefresh(refreshToken: string): Promise<boolean> {
  try {
    const res = await fetch(`${BASE}/auth/refresh`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ refreshToken }),
    });
    if (!res.ok) return false;
    const data = await res.json();
    localStorage.setItem(TOKEN_KEY, data.accessToken);
    localStorage.setItem(REFRESH_KEY, data.refreshToken);
    return true;
  } catch {
    return false;
  }
}

/**
 * Single-flight token refresh. The API rotates refresh tokens (each is
 * single-use), so parallel 401s must share one refresh, not race for it.
 */
export async function tryRefreshToken(): Promise<boolean> {
  if (refreshInFlight) return refreshInFlight;

  const refreshToken = localStorage.getItem(REFRESH_KEY);
  if (!refreshToken) return false;

  const p = doRefresh(refreshToken);
  refreshInFlight = p;
  void p.finally(() => {
    if (refreshInFlight === p) refreshInFlight = null;
  });
  return p;
}

/** Test-only: drops any in-flight refresh so tests start from a clean slate. */
export function __resetRefreshStateForTests(): void {
  refreshInFlight = null;
}
```

Both call sites also stop wiping a pair they no longer own. Extract the shared logout so the guard exists in one place:

```ts
function forceLogout(attemptedToken: string | null): never {
  // Only clear if nobody else has already rotated us onto a fresh pair.
  if (localStorage.getItem(REFRESH_KEY) === attemptedToken) {
    localStorage.removeItem(TOKEN_KEY);
    localStorage.removeItem(REFRESH_KEY);
    window.location.href = '/login';
  }
  throw new Error('UNAUTHORIZED');
}
```

and in `fetchJson` / `fetchWithAuth`:

```ts
  if (res.status === 401) {
    const attempted = localStorage.getItem(REFRESH_KEY);
    const refreshed = await tryRefreshToken();
    if (refreshed) {
      const retryHeaders = { ...allHeaders(), ...init?.headers };
      res = await fetch(`${BASE}${url}`, { cache: 'no-cache', ...init, headers: retryHeaders });
    } else {
      forceLogout(attempted);
    }
  }
```

**Steps:**

- [ ] Write the failing regression test `ui/client/src/api/client.refresh.test.ts`:
  ```ts
  import { describe, it, expect, beforeEach, vi, afterEach } from 'vitest';
  import { tryRefreshToken, __resetRefreshStateForTests } from './client';

  describe('tryRefreshToken single-flight', () => {
    beforeEach(() => {
      localStorage.clear();
      __resetRefreshStateForTests();
      localStorage.setItem('gpx_access_token', 'stale-access');
      localStorage.setItem('gpx_refresh_token', 'refresh-1');
    });

    afterEach(() => {
      vi.restoreAllMocks();
    });

    it('issues exactly one refresh request for five concurrent callers', async () => {
      let calls = 0;
      vi.stubGlobal(
        'fetch',
        vi.fn(async (url: string) => {
          expect(url).toContain('/auth/refresh');
          calls += 1;
          // The API rotates single-use refresh tokens: only the first
          // presentation of refresh-1 succeeds.
          if (calls > 1) {
            return new Response(JSON.stringify({ code: 'INVALID_REFRESH_TOKEN' }), { status: 401 });
          }
          return new Response(
            JSON.stringify({ accessToken: 'fresh-access', refreshToken: 'refresh-2' }),
            { status: 200, headers: { 'Content-Type': 'application/json' } },
          );
        }),
      );

      const results = await Promise.all([
        tryRefreshToken(), tryRefreshToken(), tryRefreshToken(),
        tryRefreshToken(), tryRefreshToken(),
      ]);

      expect(calls).toBe(1);
      expect(results).toEqual([true, true, true, true, true]);
      expect(localStorage.getItem('gpx_access_token')).toBe('fresh-access');
      expect(localStorage.getItem('gpx_refresh_token')).toBe('refresh-2');
    });

    it('allows a new refresh after the in-flight one settles', async () => {
      let calls = 0;
      vi.stubGlobal(
        'fetch',
        vi.fn(async () => {
          calls += 1;
          return new Response(
            JSON.stringify({ accessToken: `access-${calls}`, refreshToken: `refresh-${calls + 1}` }),
            { status: 200, headers: { 'Content-Type': 'application/json' } },
          );
        }),
      );

      expect(await tryRefreshToken()).toBe(true);
      expect(await tryRefreshToken()).toBe(true);
      expect(calls).toBe(2);
    });
  });
  ```
- [ ] Run it and watch it fail: `cd ui/client && npx vitest run src/api/client.refresh.test.ts`
  Expected failure: the import fails first — `tryRefreshToken` and `__resetRefreshStateForTests` are not exported from `client.ts` (`SyntaxError: The requested module './client' does not provide an export named 'tryRefreshToken'`). Export them (no logic change), re-run, and the real failure appears: `expected 1 to be 5` on `expect(calls).toBe(1)` — five callers, five refresh requests, four of them 401. Record that second failure before fixing.
- [ ] Implement the single-flight `tryRefreshToken` + `doRefresh` + `__resetRefreshStateForTests` in `client.ts`
- [ ] Add `forceLogout(attempted)` and use it from both `fetchJson` and `fetchWithAuth` in place of the inline token-wipe blocks
- [ ] Run the test and watch it pass: `cd ui/client && npx vitest run src/api/client.refresh.test.ts`
- [ ] Run the client checks: `cd ui/client && npm run test && npm run lint && npm run build`
- [ ] Run E2E to confirm the auth flow still works end-to-end: `cd ui/client && npm run e2e`
- [ ] Commit:
  ```bash
  git add ui/client/src/api/client.ts ui/client/src/api/client.refresh.test.ts
  git commit -m "fix(client): make token refresh single-flight

  Every 401 called tryRefreshToken independently with the same stored refresh
  token, but the API rotates refresh tokens and each is single-use. The first
  call succeeded and stored a new pair; the losers got INVALID_REFRESH_TOKEN,
  wiped both tokens — including the fresh pair — and hard-navigated to /login.
  api.getSettings alone triggers this with its two parallel fetchJson calls.

  Collapses concurrent refreshes onto one in-flight promise, and only clears
  tokens when the refresh token being abandoned is still the stored one.

  Closes #95"
  ```

---

### Task 6: Partial race-plan payload sent to a full-replace PUT wipes half the plan

**Issues:** #96

**Files:**
- Modify `ui/client/src/types/race-plan.ts:255` (add `toRacePlanUpdateRequest`)
- Modify `ui/client/src/pages/RacePlanDetailPage.tsx:74` (`handleComputeTimes`), `:291` (`PlanMetaForm.handleSave`)
- Test `ui/client/e2e/race-plan.spec.ts` (new)

**Root cause:** `handleComputeTimes` builds a `RacePlanUpdateRequest` with only `name`, `activityType`, `status`, `performanceCoefficient`; `PlanMetaForm.handleSave` adds only `raceDate`, `startTime`, `targetTimeSeconds`. But `RacePlanService.UpdateAsync` (`ui/api/Services/RacePlanService.cs:186`) is a full replace — it unconditionally assigns `Description`, `RaceDate`, `StartLatitude`, `StartLongitude`, `TargetTimeBSeconds`, `TargetTimeCSeconds`, `SweatRateMLPerHour`, and sets `plan.StartTime = null` when `dto.StartTime` is empty. Every omitted field deserializes to null on `RacePlanUpdateDto` and is persisted as null.

**Fix approach:** Never hand-build a partial update body. Add one helper that projects the loaded `RacePlanDetail` onto a complete `RacePlanUpdateRequest`, and apply only the intended deltas on top.

```ts
// ui/client/src/types/race-plan.ts — append next to the interface
/**
 * Projects a loaded plan onto a COMPLETE update request.
 * The API's PUT is a full replace: any field omitted here is persisted as null.
 * Always build the body with this helper and pass only the fields you changed.
 */
export function toRacePlanUpdateRequest(
  plan: RacePlanDetail,
  overrides: Partial<RacePlanUpdateRequest> = {},
): RacePlanUpdateRequest {
  return {
    name: plan.name,
    description: plan.description ?? undefined,
    activityType: plan.activityType,
    status: plan.status,
    raceDate: plan.raceDate,
    startTime: plan.startTime,
    startLatitude: plan.startLatitude,
    startLongitude: plan.startLongitude,
    targetTimeSeconds: plan.targetTimeSeconds,
    targetTimeBSeconds: plan.targetTimeBSeconds,
    targetTimeCSeconds: plan.targetTimeCSeconds,
    performanceCoefficient: plan.performanceCoefficient,
    sweatRateMLPerHour: plan.sweatRateMLPerHour,
    equipment: plan.equipment,
    ...overrides,
  };
}
```

Call sites become:

```ts
// handleComputeTimes (RacePlanDetailPage.tsx:74)
      const req = toRacePlanUpdateRequest(plan, { performanceCoefficient: localCoeff });
      await updatePlan.mutateAsync({ id: plan.id, data: req });

// PlanMetaForm.handleSave (RacePlanDetailPage.tsx:291)
  async function handleSave() {
    await updatePlan.mutateAsync({
      id: plan!.id,
      data: toRacePlanUpdateRequest(plan!, {
        status: form.status,
        raceDate: form.raceDate || null,
        startTime: form.startTime || null,
        targetTimeSeconds: form.targetTimeSeconds,
      }),
    });
    setEditing(false);
  }
```

`PlanMetaForm` must receive the full `plan` object (it already does — it dereferences `plan!.name`, `plan!.activityType`, `plan!.performanceCoefficient`).

**Steps:**

- [ ] Add a `race-plan-detail.json` fixture under `ui/client/e2e/fixtures/` populated with every field: `raceDate`, `startTime`, `startLatitude`, `startLongitude`, `targetTimeSeconds`, `targetTimeBSeconds`, `targetTimeCSeconds`, `sweatRateMLPerHour`, `description`, `equipment`, plus `id`, `name`, `activityType`, `status`, `performanceCoefficient` and the stats fields required by `RacePlanDetail`
- [ ] Add race-plan routes to `ui/client/e2e/helpers/mock-api.ts` (`**/api/race-plans/*` GET → the fixture, PUT → echo the body, `**/api/race-plans/*/compute-times` POST → the fixture), following the existing `**/api/routes/route-1` pattern
- [ ] Write the failing regression test `ui/client/e2e/race-plan.spec.ts`:
  ```ts
  import { test, expect } from '@playwright/test';
  import { mockAllApi } from './helpers/mock-api';

  test.describe('race plan detail', () => {
    test.beforeEach(async ({ page }) => {
      await mockAllApi(page);
    });

    test('compute-times PUT carries the whole plan, not a 4-field payload', async ({ page }) => {
      const puts: Record<string, unknown>[] = [];
      await page.route('**/api/race-plans/plan-1', async (route) => {
        if (route.request().method() === 'PUT') {
          puts.push(route.request().postDataJSON());
        }
        await route.fallback();
      });

      await page.goto('/race-plans/plan-1');
      await page.getByRole('button', { name: /compute times/i }).click();
      await expect.poll(() => puts.length).toBeGreaterThan(0);

      const body = puts[0];
      // The API's PUT is a full replace: anything missing here is nulled server-side.
      expect(body).toHaveProperty('raceDate', '2026-06-06');
      expect(body).toHaveProperty('startTime', '04:00');
      expect(body).toHaveProperty('startLatitude');
      expect(body.startLatitude).not.toBeNull();
      expect(body).toHaveProperty('startLongitude');
      expect(body.startLongitude).not.toBeNull();
      expect(body).toHaveProperty('targetTimeBSeconds');
      expect(body.targetTimeBSeconds).not.toBeNull();
      expect(body).toHaveProperty('targetTimeCSeconds');
      expect(body.targetTimeCSeconds).not.toBeNull();
      expect(body).toHaveProperty('sweatRateMLPerHour', 700);
    });
  });
  ```
  (Adjust the plan id, the button accessible name and the route path to whatever `RacePlanDetailPage` and its router entry actually use — read them from `ui/client/src/pages/RacePlanDetailPage.tsx` and the router config before writing the selectors.)
- [ ] Run it and watch it fail: `cd ui/client && npm run build && npx playwright test e2e/race-plan.spec.ts --project=desktop`
  Expected failure: `expect(received).toHaveProperty('raceDate', '2026-06-06')` fails with `Received path: []` — the PUT body has only `name`, `activityType`, `status`, `performanceCoefficient`.
- [ ] Add `toRacePlanUpdateRequest` to `ui/client/src/types/race-plan.ts`
- [ ] Rewrite `handleComputeTimes` and `PlanMetaForm.handleSave` to use it
- [ ] Grep for any other partial builder: `grep -rn "RacePlanUpdateRequest" ui/client/src` — every construction site must go through the helper (`NutritionPlanner.handleSweatRateChange` already sends a complete object; convert it to the helper too for consistency)
- [ ] Run the test and watch it pass: `cd ui/client && npm run build && npx playwright test e2e/race-plan.spec.ts --project=desktop`
- [ ] Run the client checks: `cd ui/client && npm run lint && npm run build && npm run e2e`
- [ ] Commit:
  ```bash
  git add ui/client/src/types/race-plan.ts ui/client/src/pages/RacePlanDetailPage.tsx ui/client/e2e/
  git commit -m "fix(client): send the complete plan on every race-plan PUT

  RacePlanService.UpdateAsync is a full replace, but handleComputeTimes sent a
  4-field body and PlanMetaForm.handleSave a 7-field one. Every omitted field
  deserialized to null on the DTO and was persisted: dragging the performance
  slider nulled the race date, start time, start coordinates, the B/C objectives
  and the sweat rate, silently killing the day/night bands and the nutrition plan.

  Adds toRacePlanUpdateRequest(plan, overrides), which projects the loaded plan
  onto a complete request, and routes every call site through it.

  Closes #96"
  ```

---

### Task 7: Route export opens an API URL in a new tab with no bearer token

**Issues:** #119

**Files:**
- Modify `ui/client/src/api/routes-client.ts:105` (`getExportUrl` → `exportRoute`)
- Modify `ui/client/src/components/editor/ExportModal.tsx:19`
- Test `ui/client/e2e/editor.spec.ts` (append)

**Root cause:** `handleExport` does `window.open('/api/routes/{id}/export/{format}', '_blank')`. Auth is bearer-only: the JWT lives in `localStorage` and is attached per request by `authHeaders()`; `Program.cs` registers only `JwtBearerDefaults.AuthenticationScheme` with no cookie scheme, and `RoutesController` is `[Authorize]` with no `[AllowAnonymous]` on the export actions. A top-level browser navigation sends no `Authorization` header, so every export returns 401 and the user gets a blank tab. `api.downloadGpx` in `client.ts` already does this correctly via `fetchWithAuth` + a blob URL.

**Fix approach:** Mirror `api.downloadGpx`: fetch with auth headers, turn the response into a blob URL, click a synthetic anchor.

```ts
// ui/client/src/api/routes-client.ts — replace getExportUrl
  /**
   * Downloads a route export. Auth is bearer-only, so this must go through
   * fetch + a blob URL: a top-level navigation carries no Authorization header.
   */
  exportRoute: async (
    id: string,
    format: 'gpx' | 'geojson' | 'kml',
    filename?: string,
  ): Promise<void> => {
    const res = await fetch(`${BASE}/routes/${id}/export/${format}`, { headers: allHeaders() });
    if (!res.ok) throw new Error(`Export failed: ${res.status}`);

    const blob = await res.blob();
    const url = URL.createObjectURL(blob);
    const a = document.createElement('a');
    a.href = url;
    a.download = filename ?? `route-${id}.${format}`;
    a.click();
    URL.revokeObjectURL(url);
  },
```

```tsx
// ui/client/src/components/editor/ExportModal.tsx
  const handleExport = async (format: 'gpx' | 'geojson' | 'kml') => {
    try {
      await routesApi.exportRoute(routeId, format);
    } catch (err) {
      console.error('Route export failed:', err);
    } finally {
      onClose();
    }
  };
```

**Steps:**

- [ ] Write the failing regression test — append to `ui/client/e2e/editor.spec.ts`:
  ```ts
  test('route export sends the bearer token instead of opening a bare tab', async ({ page }) => {
    await mockAllApi(page);

    const exportRequests: { url: string; auth: string | undefined }[] = [];
    await page.route('**/api/routes/*/export/*', async (route) => {
      exportRequests.push({
        url: route.request().url(),
        auth: route.request().headers()['authorization'],
      });
      await route.fulfill({
        status: 200,
        contentType: 'application/gpx+xml',
        body: '<gpx version="1.1"></gpx>',
      });
    });

    const popups: string[] = [];
    page.on('popup', (p) => popups.push(p.url()));

    await page.goto('/routes/route-1/edit');
    await page.getByRole('button', { name: /export/i }).first().click();
    await page.getByRole('button', { name: /gpx/i }).click();

    await expect.poll(() => exportRequests.length).toBe(1);
    expect(exportRequests[0].auth).toMatch(/^Bearer /);
    expect(popups).toHaveLength(0);
  });
  ```
  (Read `ui/client/e2e/editor.spec.ts` first for the existing navigation/setup pattern and the real editor route; reuse them rather than the placeholders above where they differ.)
- [ ] Run it and watch it fail: `cd ui/client && npm run build && npx playwright test e2e/editor.spec.ts --project=desktop -g "bearer token"`
  Expected failure: `expect(popups).toHaveLength(0)` fails with `Expected length: 0  Received length: 1` — `window.open` fired a popup — and/or `exportRequests[0].auth` is `undefined` because the navigation carried no header.
- [ ] Replace `getExportUrl` with `exportRoute` in `routes-client.ts`
- [ ] Rewrite `ExportModal.handleExport` to await `routesApi.exportRoute`
- [ ] Check for other callers: `grep -rn "getExportUrl" ui/client/src` must return nothing
- [ ] Run the test and watch it pass: `cd ui/client && npm run build && npx playwright test e2e/editor.spec.ts --project=desktop -g "bearer token"`
- [ ] Run the client checks: `cd ui/client && npm run lint && npm run build && npm run e2e`
- [ ] Commit:
  ```bash
  git add ui/client/src/api/routes-client.ts ui/client/src/components/editor/ExportModal.tsx ui/client/e2e/editor.spec.ts
  git commit -m "fix(client): download route exports with the bearer token

  handleExport called window.open on the API URL. Auth is bearer-only with no
  cookie scheme registered, so a top-level navigation carries no Authorization
  header: every export format returned 401 and the user got a blank tab with no
  error surfaced. api.downloadGpx already had the correct fetch + blob pattern.

  Replaces getExportUrl with exportRoute, which fetches with auth headers and
  saves via a blob URL.

  Closes #119"
  ```

---

## Wave 2 — CLI silent data corruption (`GpxAnalyzer.Cli.Core`)

Everything in this wave is in the shared Core library, which the Web API consumes in-process through `GpxAnalysisService`. Every fix here lands in both the CLI and the web app at once. None of it is blocked by the System.CommandLine migration.

### Task 8: One bad early point deletes the rest of the track

**Issues:** #77

**Files:**
- Modify `cli/src/GpxAnalyzer.Cli.Core/Stats/GpsFilter.cs:23`
- Test `cli/tests/GpxAnalyzer.Cli.Tests/Stats/GpsFilterTests.cs` (append)

**Root cause:** `FilterOutliers` keeps `points[0]` unconditionally as the anchor and measures every later point's speed against `filtered[^1]`. When a point is rejected the anchor does not move, so if the *anchor itself* is the outlier every subsequent point is measured against the bogus position and rejected too. There is no anchor validation and no bound on how many points one bad anchor can eat. `GpxParser.ParsePoint` makes it trivially reachable: a `<trkpt>` missing `lat`/`lon` parses as `0.0`, and cheap receivers commonly emit a first fix at (0,0). `MaxReasonableSpeed` is not opt-in — both `SharedFlags` and the API's `BuildConfig` default it to the preset value (hiking 4.0 m/s).

**Fix approach:** Bound the damage: after a run of consecutive rejections, re-anchor on the rejected point. A GPS jump is one or two bad points; a wrong anchor produces an unbroken rejection run. Re-anchoring after `MaxConsecutiveRejections` converts "the whole track vanishes" into "at most N points are dropped".

```csharp
// cli/src/GpxAnalyzer.Cli.Core/Stats/GpsFilter.cs — after
public static class GpsFilter
{
    /// <summary>
    /// After this many consecutive rejections we assume the anchor itself is the
    /// outlier (a bad first fix at 0,0 is the common case) and re-anchor onto the
    /// current point rather than deleting the remainder of the track.
    /// </summary>
    public const int MaxConsecutiveRejections = 3;

    public static (List<TrackPoint> Filtered, int Removed) FilterOutliers(
        List<TrackPoint> points, double maxSpeed)
    {
        if (maxSpeed <= 0 || points.Count <= 1)
            return (points, 0);

        var filtered = new List<TrackPoint>(points.Count) { points[0] };
        int removed = 0;
        int consecutiveRejections = 0;

        for (int i = 1; i < points.Count; i++)
        {
            var anchor = filtered[^1];
            double dt = (points[i].Time - anchor.Time).TotalSeconds;

            if (dt <= 0)
            {
                filtered.Add(points[i]);
                consecutiveRejections = 0;
                continue;
            }

            double dist = DistanceCalculator.Haversine(
                anchor.Lat, anchor.Lon, points[i].Lat, points[i].Lon);
            double speed = dist / dt;

            if (speed > maxSpeed)
            {
                consecutiveRejections++;

                if (consecutiveRejections >= MaxConsecutiveRejections)
                {
                    // The anchor, not the stream of points, is the outlier.
                    // Drop the anchor and restart from here.
                    filtered.RemoveAt(filtered.Count - 1);
                    removed++;                            // the discarded anchor
                    removed -= consecutiveRejections - 1; // un-count points rejected against it
                    filtered.Add(points[i]);
                    consecutiveRejections = 0;
                    continue;
                }

                removed++;
                continue;
            }

            filtered.Add(points[i]);
            consecutiveRejections = 0;
        }

        return (filtered, removed);
    }
}
```

Re-anchoring only removes the anchor itself; the points rejected against it were rejected for the wrong reason, so their count is subtracted back out of `removed`. They are not re-inserted (they are already past), which is acceptable: with `MaxConsecutiveRejections = 3` at most two real points are lost, versus the entire track today.

**Steps:**

- [ ] Append the failing regression test to `cli/tests/GpxAnalyzer.Cli.Tests/Stats/GpsFilterTests.cs`:
  ```csharp
      [Fact]
      public void FilterOutliers_BadFirstFixAtZeroZero_DoesNotDeleteTheWholeTrack()
      {
          var t0 = DateTime.Parse("2024-01-01T10:00:00Z").ToUniversalTime();
          var points = new List<TrackPoint>
          {
              // Cheap receiver's first fix: lat/lon attributes missing -> parsed as 0,0
              new() { Lat = 0.0, Lon = 0.0, Time = t0 },
          };
          // 50 real points near 48.0/2.0, one per second, ~7 m apart
          for (int i = 1; i <= 50; i++)
              points.Add(new TrackPoint
              {
                  Lat = 48.0 + i * 0.00006,
                  Lon = 2.0,
                  Time = t0.AddSeconds(i),
              });

          var (filtered, removed) = GpsFilter.FilterOutliers(points, 4.0); // hiking preset

          // The single bad anchor must not eat the activity.
          Assert.True(filtered.Count >= 45,
              $"expected the real track to survive, kept only {filtered.Count} of 51");
          Assert.DoesNotContain(filtered, p => p.Lat == 0.0 && p.Lon == 0.0);
          Assert.True(removed <= 6, $"expected a handful of removals, got {removed}");
      }

      [Fact]
      public void FilterOutliers_SingleMidTrackSpike_StillRemovesOnlyTheSpike()
      {
          var t0 = DateTime.Parse("2024-01-01T10:00:00Z").ToUniversalTime();
          var points = new List<TrackPoint>();
          for (int i = 0; i < 20; i++)
              points.Add(new TrackPoint { Lat = 48.0 + i * 0.00006, Lon = 2.0, Time = t0.AddSeconds(i) });
          // One teleport at index 10
          points[10] = new TrackPoint { Lat = 49.5, Lon = 3.5, Time = t0.AddSeconds(10) };

          var (filtered, removed) = GpsFilter.FilterOutliers(points, 4.0);

          Assert.Equal(1, removed);
          Assert.Equal(19, filtered.Count);
      }
  ```
- [ ] Run it and watch it fail: `dotnet test cli/tests/GpxAnalyzer.Cli.Tests/ --filter GpsFilterTests`
  Expected failure: `FilterOutliers_BadFirstFixAtZeroZero_DoesNotDeleteTheWholeTrack` fails with `expected the real track to survive, kept only 1 of 51` — the (0,0) anchor never advances, so all 50 real points are rejected.
- [ ] Implement the re-anchoring in `GpsFilter.FilterOutliers`
- [ ] Run the tests and watch both pass: `dotnet test cli/tests/GpxAnalyzer.Cli.Tests/ --filter GpsFilterTests`
- [ ] Run the full CLI suite: `dotnet test cli/tests/GpxAnalyzer.Cli.Tests/`
- [ ] Commit:
  ```bash
  git add cli/src/GpxAnalyzer.Cli.Core/Stats/GpsFilter.cs cli/tests/GpxAnalyzer.Cli.Tests/Stats/GpsFilterTests.cs
  git commit -m "fix(cli): re-anchor the GPS outlier filter after consecutive rejections

  FilterOutliers keeps points[0] as the anchor unconditionally and never advances
  it on rejection. When the anchor is itself the outlier — a first fix at (0,0),
  which GpxParser produces for any trkpt missing lat/lon — every later point is
  measured against the bogus position and dropped. With the default hiking preset
  (4 m/s) a 5,000-point track collapses to one point: zero distance, zero
  elevation, zero stops, with no error.

  After three consecutive rejections the anchor is discarded and the current point
  becomes the new anchor, bounding the loss to at most two real points.

  Closes #77"
  ```

---

### Task 9: `total_distance_3d_m` re-adds the jumps that 2D distance excludes

**Issues:** #78

**Files:**
- Modify `cli/src/GpxAnalyzer.Cli.Core/Stats/ComputePipeline.cs:64`
- Modify `cli/src/GpxAnalyzer.Cli.Core/Anomaly/AnomalyCorrector.cs:118` (same expression, kept in sync)
- Test `cli/tests/GpxAnalyzer.Cli.Tests/Stats/ComputePipelineTests.cs` (append)

**Root cause:** Steps 6–7 sum two distances in one loop from two different sources. `s.TotalDistance` accumulates `points[i].DistFromPrev`, which `SpeedCalculator.EnrichPoints` deliberately sets to 0 when `dt > ElevationSmoother.GapThreshold` (10 min) and which `ClampSpeeds` zeroes for over-speed segments. `s.TotalDistance3D` instead calls `DistanceCalculator.Distance3D` on the raw lat/lon/ele of every consecutive pair, re-adding exactly the teleport jumps steps 4–5 just excluded. Both values are user-visible: `total_distance_3d_m` in the JSON contract, `Total Distance (3D)` in the text report, and `GpxStats.TotalDistance3dM` fed to the AI analyzer.

**Fix approach:** Derive the 3D distance from the same segment set the 2D distance uses — a segment with `DistFromPrev == 0` was excluded on purpose and contributes nothing in 3D either. Combine the retained horizontal distance with the vertical delta by Pythagoras, which is what `Distance3D` does internally.

```csharp
        // Step 6-7: Distance
        // 3D must be derived from the SAME segments as 2D: EnrichPoints zeroes
        // DistFromPrev across recording gaps and ClampSpeeds zeroes it for
        // over-speed segments, and a segment excluded from 2D is not a real
        // segment in 3D either.
        for (int i = 1; i < points.Count; i++)
        {
            double horizontal = points[i].DistFromPrev;
            s.TotalDistance += horizontal;

            if (horizontal <= 0) continue;

            double dEle = points[i].Ele - points[i - 1].Ele;
            s.TotalDistance3D += Math.Sqrt(horizontal * horizontal + dEle * dEle);
        }
```

Apply the identical expression in `AnomalyCorrector.RecalculateStats` (lines 115–121) so the two code paths cannot drift.

**Steps:**

- [ ] Append the failing regression test to `cli/tests/GpxAnalyzer.Cli.Tests/Stats/ComputePipelineTests.cs`:
  ```csharp
      [Fact]
      public void Compute_TrackWithRecordingGap_DoesNotAddTheGapJumpTo3dDistance()
      {
          var t0 = DateTime.Parse("2024-01-01T10:00:00Z").ToUniversalTime();
          var points = new List<TrackPoint>();

          // Leg 1: 10 points near Paris, 1 s apart, flat
          for (int i = 0; i < 10; i++)
              points.Add(new TrackPoint { Lat = 48.85 + i * 0.0001, Lon = 2.35, Ele = 35, Time = t0.AddSeconds(i) });

          // Device off for 3 h, then leg 2 near Lyon (~390 km away)
          var t1 = t0.AddHours(3);
          for (int i = 0; i < 10; i++)
              points.Add(new TrackPoint { Lat = 45.75 + i * 0.0001, Lon = 4.85, Ele = 170, Time = t1.AddSeconds(i) });

          var cfg = new ComputeConfig
          {
              StopConfig = StopDetector.Presets[StopDetector.PresetHiking],
              SmoothingLevel = "none",
              TrackSmoothing = "none",
              ElevationCfg = new ElevationConfig(),
              BiometricsCfg = new BiometricsConfig(),
              MaxReasonableSpeed = 0,   // isolate the gap behaviour from outlier filtering
          };

          var (summary, _) = ComputePipeline.Compute(points, 1, cfg);

          // The 3 h gap is excluded from 2D by EnrichPoints; 3D must exclude it too.
          Assert.True(summary.TotalDistance < 5_000,
              $"2D distance should exclude the gap, got {summary.TotalDistance:F0} m");
          Assert.True(summary.TotalDistance3D < 5_000,
              $"3D distance must exclude the same gap, got {summary.TotalDistance3D:F0} m");

          // And the invariant that motivated the field: 3D is the same path plus
          // vertical, so it can never fall below 2D nor rise wildly above it.
          Assert.InRange(summary.TotalDistance3D, summary.TotalDistance, summary.TotalDistance * 1.1 + 1);
      }
  ```
  Check the exact `ComputeConfig` initialiser names against `cli/src/GpxAnalyzer.Cli.Core/Stats/ComputeConfig.cs` and against the existing tests in the same file before running — reuse whatever config helper those tests already have.
- [ ] Run it and watch it fail: `dotnet test cli/tests/GpxAnalyzer.Cli.Tests/ --filter Compute_TrackWithRecordingGap`
  Expected failure: `3D distance must exclude the same gap, got 391000 m` (approximately) — the 3D loop re-adds the full Paris→Lyon great-circle jump.
- [ ] Rewrite the step 6–7 loop in `ComputePipeline.Compute` as shown
- [ ] Apply the same expression in `AnomalyCorrector.RecalculateStats` lines 115–121
- [ ] Run the test and watch it pass: `dotnet test cli/tests/GpxAnalyzer.Cli.Tests/ --filter Compute_TrackWithRecordingGap`
- [ ] Run the full CLI suite: `dotnet test cli/tests/GpxAnalyzer.Cli.Tests/`
- [ ] Commit:
  ```bash
  git add cli/src/GpxAnalyzer.Cli.Core/Stats/ComputePipeline.cs cli/src/GpxAnalyzer.Cli.Core/Anomaly/AnomalyCorrector.cs cli/tests/GpxAnalyzer.Cli.Tests/Stats/ComputePipelineTests.cs
  git commit -m "fix(cli): derive 3D distance from the same segments as 2D

  TotalDistance sums DistFromPrev, which EnrichPoints zeroes across recording
  gaps over 10 min and ClampSpeeds zeroes for over-speed segments. TotalDistance3D
  re-derived the distance from raw coordinates, re-adding exactly those excluded
  jumps: two 10 km legs recorded either side of a 3 h drive reported 20 km in 2D
  and 420 km in 3D, in the JSON contract, the text report and the AI prompt.

  3D is now horizontal (post-gap, post-clamp) combined with the vertical delta,
  so a segment excluded from 2D contributes nothing to 3D either.

  Closes #78"
  ```

---

### Task 10: `--fix-anomalies` corrects the data but not the numbers

**Issues:** #79, #81, #82, #83, #84

**Files:**
- Modify `cli/src/GpxAnalyzer.Cli.Core/Anomaly/AnomalyCorrector.cs:49` (`RecalculateStats`), `:96` (frozen-speed estimate), `:118` (re-summing), `:142` (`CorrectGpsFrozen`)
- Modify `cli/src/GpxAnalyzer.Cli.Core/Stats/ComputePipeline.cs:118`–`:122`
- Modify `cli/src/GpxAnalyzer.Cli.Core/Gpx/TrackPoint.cs` (add `Clone()`)
- Test `cli/tests/GpxAnalyzer.Cli.Tests/Anomaly/AnomalyCorrectorTests.cs` (append)

**Root cause:** All five issues share one defect surface — the post-correction recompute is a hand-maintained subset of the pipeline — plus one aliasing bug in the same file. Per the coupling rule they are one task, delivered as four commits.

- **#79** Step 17 calls `ApplyCorrections` (which mutates `Ele`, `Lat`/`Lon`, `Time`, and nulls `HeartRate`) then `RecalculateStats`, which refreshes only `TotalDistance`, `TotalDistance3D`, `Speed`, `PointsPerKm`. `s.Elevation` (step 8), `s.Stops`/`StoppedTime`/`MovingTime` (steps 10–11), `s.Biometrics` (step 14) and `s.Effort` (step 15) keep pre-correction values, so the exported GPX and the reported statistics describe different tracks.
- **#81** `RecalculateStats` re-runs `EnrichPoints` but never re-applies `ClampSpeeds` — it cannot, its signature has no access to `MaxReasonableSpeed`. `SpeedCalculatorTests.cs:145` documents the requirement verbatim ("this is why RecalculateStats must re-clamp") and `ComputePipelineIntegrationTests.cs:17` names it "the 699 km/h bug"; the fixture no longer exercises the path, so that regression test passes while the defect is live.
- **#82** `var p0 = start > 0 ? points[start - 1] : points[start]` stores a *reference* (`TrackPoint` is a mutable class). When `StartIndex == 0` the loop's first write overwrites `p0` itself, so every later iteration interpolates from a moved anchor.
- **#83** `avgSpeed2 = healthyDist / max(0, MovingTime - totalFrozenDuration)` is by construction ≥ `avgSpeed1`, so `Math.Min` always picks `avgSpeed1` — except in the degenerate case the clamp creates: when `MovingTime <= totalFrozenDuration` the denominator hits 0, `avgSpeed2` becomes 0, and the `Min` yields 0, so the frozen section is credited zero distance while `correction_applied` reports true.
- **#84** `Ele` and `HeartRate` are mutated but `s.Elevation` and `s.Biometrics` are never recomputed, while `s.TotalDistance3D` *is* re-summed from the corrected `Ele` — one report mixing corrected and uncorrected values derived from the same field.

**Fix approach:** Stop hand-maintaining a subset. Move the recompute into `ComputePipeline`, which already holds the full `ComputeConfig`, and re-run the affected pipeline stages in order. `AnomalyCorrector` keeps only the part that genuinely cannot be re-derived — the frozen-section distance estimate.

```csharp
// ComputePipeline.cs — before
            if (cfg.FixAnomalies && s.AnomalyReport.TotalCount > 0)
            {
                s.AnomalyReport = AnomalyCorrector.ApplyCorrections(points, s.AnomalyReport);
                AnomalyCorrector.RecalculateStats(points, s);
            }

// after
            if (cfg.FixAnomalies && s.AnomalyReport.TotalCount > 0)
            {
                s.AnomalyReport = AnomalyCorrector.ApplyCorrections(points, s.AnomalyReport);

                // Corrections mutate Ele, Lat/Lon, Time and HeartRate, so every
                // stage downstream of those fields has to run again. Re-running
                // the stages is the only way to keep the exported GPX and the
                // reported numbers describing the same track.
                SpeedCalculator.EnrichPoints(points);
                SpeedCalculator.ClampSpeeds(points, cfg.MaxReasonableSpeed);
                AnomalyCorrector.ApplyFrozenSectionDistances(points, s);

                s.TotalDistance = 0;
                s.TotalDistance3D = 0;
                for (int i = 1; i < points.Count; i++)
                {
                    double horizontal = points[i].DistFromPrev;
                    s.TotalDistance += horizontal;
                    if (horizontal <= 0) continue;
                    double dEle = points[i].Ele - points[i - 1].Ele;
                    s.TotalDistance3D += Math.Sqrt(horizontal * horizontal + dEle * dEle);
                }

                s.Elevation = ElevationCalculator.ComputeWithAlgo(points, elevCfg);

                s.StartTime = points[0].Time;
                s.EndTime = points[^1].Time;
                s.TotalTime = s.EndTime - s.StartTime;

                s.Stops = StopDetector.DetectStops(points, cfg.StopConfig);
                s.StopCount = s.Stops.Count;
                s.TotalStopTime = StopDetector.TotalStopTime(s.Stops);
                s.LongestStop = StopDetector.LongestStop(s.Stops);
                s.AvgStopDuration = StopDetector.AvgStopDuration(s.Stops);
                s.StoppedTime = s.TotalStopTime;
                s.MovingTime = s.TotalTime - s.StoppedTime;
                if (s.MovingTime < TimeSpan.Zero) s.MovingTime = TimeSpan.Zero;

                s.Speed = SpeedCalculator.ComputeSpeed(s.TotalDistance, s.TotalTime, s.MovingTime);
                s.Speed.MaxSpeed = SpeedCalculator.MaxSpeedFromPoints(points);

                if (s.TotalDistance > 0)
                    s.PointsPerKm = points.Count / (s.TotalDistance / 1000);

                s.Biometrics = BiometricsCalculator.Compute(points, cfg.BiometricsCfg);
                s.Effort = EffortCalculator.ComputeAll(points, s);
            }
```

`elevCfg` is already a local in `Compute` (built at step 8) and stays in scope.

`RecalculateStats` is replaced by the narrower `ApplyFrozenSectionDistances`, keeping its frozen-range logic with the #83 fix:

```csharp
    /// <summary>
    /// Overrides DistFromPrev inside corrected GPS-frozen sections. Linear lat/lon
    /// interpolation cannot recover loop-course distance, so the distance is
    /// estimated from the post-correction average moving speed.
    /// </summary>
    public static void ApplyFrozenSectionDistances(List<TrackPoint> points, Summary s)
    {
        if (s.AnomalyReport is null) return;

        var frozenRanges = new List<(int Start, int End, double Duration)>();
        foreach (var a in s.AnomalyReport.Anomalies)
        {
            if (a.Type == AnomalyType.GpsFrozen && a.WasCorrected && a.TimeImpactS > 0)
                frozenRanges.Add((a.StartIndex, a.EndIndex, a.TimeImpactS));
        }
        if (frozenRanges.Count == 0) return;

        var frozenIndices = new HashSet<int>();
        foreach (var (start, end, _) in frozenRanges)
            for (int i = start; i <= end; i++)
                frozenIndices.Add(i);

        double healthyDist = 0;
        for (int i = 1; i < points.Count; i++)
            if (!frozenIndices.Contains(i))
                healthyDist += points[i].DistFromPrev;

        double totalFrozenDuration = frozenRanges.Sum(r => r.Duration);
        double nonFrozenMovingS = s.MovingTime.TotalSeconds - totalFrozenDuration;

        // MovingTime may or may not already exclude the frozen sections (a frozen
        // section usually reads as a stop). Prefer the variant that excludes them;
        // fall back to plain MovingTime when the subtraction is not meaningful,
        // instead of collapsing the estimate to zero.
        double avgMovingSpeed =
            nonFrozenMovingS > 0 ? healthyDist / nonFrozenMovingS
          : s.MovingTime.TotalSeconds > 0 ? healthyDist / s.MovingTime.TotalSeconds
          : 0;

        foreach (var (start, end, duration) in frozenRanges)
        {
            double estimatedDist = avgMovingSpeed * duration;
            int count = end - start + 1;
            double distPerPoint = estimatedDist / count;
            for (int i = start; i <= end && i < points.Count; i++)
                points[i].DistFromPrev = distPerPoint;
        }
    }
```

The #82 aliasing fix in `CorrectGpsFrozen` snapshots the anchors instead of holding references:

```csharp
        // TrackPoint is a mutable class, so the fallback aliased the very object
        // the loop overwrites on its first iteration when start == 0. Snapshot
        // the coordinates before interpolating.
        var anchor = start > 0 ? points[start - 1] : points[start];
        double lat0 = anchor.Lat, lon0 = anchor.Lon;

        var tail = end < points.Count - 1 ? points[end + 1] : points[end];
        double lat1 = tail.Lat, lon1 = tail.Lon;

        int count = end - start + 1;

        for (int i = start; i <= end; i++)
        {
            double t = (double)(i - start + 1) / (count + 1);
            points[i].Lat = lat0 + (lat1 - lat0) * t;
            points[i].Lon = lon0 + (lon1 - lon0) * t;
        }
```

`TrackPoint.Clone()` is added here because Task 12 needs it and it belongs with the mutable-aliasing fix:

```csharp
    /// <summary>Shallow copy. TrackPoint is mutable and is aliased across pipeline
    /// stages and split boundaries; use this wherever a stage must not observe
    /// another stage's in-place mutations.</summary>
    public TrackPoint Clone() => (TrackPoint)MemberwiseClone();
```

**Steps:**

- [ ] Add `Clone()` to `cli/src/GpxAnalyzer.Cli.Core/Gpx/TrackPoint.cs` (consumed by this task and Task 12)
- [ ] Add a `BuildFixAnomaliesConfig(double maxReasonableSpeed)` private helper to `AnomalyCorrectorTests` returning a `ComputeConfig` with `FixAnomalies = true`, `AnomalyConfig = AnomalyConfig.Default()`, `StopConfig = StopDetector.Presets[StopDetector.PresetHiking]`, `SmoothingLevel = "none"`, `TrackSmoothing = "none"`, `DemSource = null`, `ElevationCfg = new ElevationConfig()`, `BiometricsCfg = new BiometricsConfig()` and the given `MaxReasonableSpeed`. Match the exact property names in `ComputeConfig.cs`.
- [ ] Append the four failing regression tests to `cli/tests/GpxAnalyzer.Cli.Tests/Anomaly/AnomalyCorrectorTests.cs`:
  ```csharp
      // ── #81: the recompute must re-clamp ─────────────────────────────────
      [Fact]
      public void FixAnomalies_BackwardTimestamp_DoesNotReportAnImpossibleMaxSpeed()
      {
          var t0 = DateTime.Parse("2024-01-01T10:00:00Z").ToUniversalTime();
          var points = new List<TrackPoint>
          {
              new() { Lat = 48.0000, Lon = 2.0, Ele = 100, Time = t0 },
              // Backward timestamp, ~100 m further along
              new() { Lat = 48.0009, Lon = 2.0, Ele = 100, Time = t0.AddSeconds(-60) },
              new() { Lat = 48.0010, Lon = 2.0, Ele = 100, Time = t0.AddSeconds(30) },
          };
          for (int i = 3; i < 30; i++)
              points.Add(new TrackPoint
              {
                  Lat = 48.0010 + (i - 2) * 0.00003, Lon = 2.0, Ele = 100,
                  Time = t0.AddSeconds(30 + i),
              });

          var cfg = BuildFixAnomaliesConfig(maxReasonableSpeed: 4.0); // hiking
          var (summary, _) = ComputePipeline.Compute(points, 1, cfg);

          // CorrectBackwardTime sets p1.Time = p0.Time + 1s -> 100 m in 1 s.
          // Without a re-clamp that lands in speed.max as 360 km/h for a hike.
          Assert.True(summary.Speed.MaxSpeed <= 4.0,
              $"max speed should stay clamped at the hiking threshold, got {summary.Speed.MaxSpeed * 3.6:F0} km/h");
      }

      // ── #79 + #84: elevation and biometrics must follow the corrections ──
      [Fact]
      public void FixAnomalies_ElevationSpikeAndHrDropout_RecomputesElevationAndBiometrics()
      {
          var t0 = DateTime.Parse("2024-01-01T10:00:00Z").ToUniversalTime();
          var points = new List<TrackPoint>();
          for (int i = 0; i < 30; i++)
              points.Add(new TrackPoint
              {
                  Lat = 48.0 + i * 0.00003, Lon = 2.0, Ele = 100,
                  Time = t0.AddSeconds(i), HeartRate = 140,
              });

          points[10].Ele = 950;                                   // barometric spike, +850 m
          for (int i = 20; i < 25; i++) points[i].HeartRate = 250; // HR dropout run

          var cfg = BuildFixAnomaliesConfig(maxReasonableSpeed: 4.0);
          var (summary, processed) = ComputePipeline.Compute(points, 1, cfg);

          // The corrector rewrote the elevations and nulled the HR samples...
          Assert.True(processed[10].Ele < 500, "the spike should have been interpolated away");
          Assert.DoesNotContain(processed, p => p.HeartRate == 250);

          // ...so the reported numbers must reflect the corrected data.
          Assert.True(summary.Elevation.Gain < 100,
              $"elevation gain still includes the corrected spike: {summary.Elevation.Gain:F0} m");
          Assert.NotNull(summary.Biometrics.HeartRate);
          Assert.True(summary.Biometrics.HeartRate!.Max < 250,
              $"heart rate max still reports the nulled dropout: {summary.Biometrics.HeartRate.Max}");
      }

      // ── #82: a frozen run starting at index 0 must ramp linearly ─────────
      [Fact]
      public void CorrectGpsFrozen_RunStartingAtIndexZero_ProducesEvenlySpacedPoints()
      {
          var t0 = DateTime.Parse("2024-01-01T10:00:00Z").ToUniversalTime();
          var points = new List<TrackPoint>();
          for (int i = 0; i < 5; i++)
              points.Add(new TrackPoint { Lat = 48.0000, Lon = 2.0, Time = t0.AddSeconds(i), Cadence = 80 });
          points.Add(new TrackPoint { Lat = 48.0050, Lon = 2.0, Time = t0.AddSeconds(5), Cadence = 80 });

          var report = new AnomalyReport
          {
              Anomalies =
              [
                  new TrackAnomaly
                  {
                      Type = AnomalyType.GpsFrozen,
                      Severity = AnomalySeverity.Warning,
                      Category = AnomalyCategory.Position,
                      StartIndex = 0, EndIndex = 4,
                      StartTime = t0, EndTime = t0.AddSeconds(4),
                      TimeImpactS = 4,
                  },
              ],
          };

          AnomalyCorrector.ApplyCorrections(points, report);

          // Five points interpolated between 48.0000 and 48.0050 must be evenly
          // spaced: every consecutive delta identical to within floating error.
          var deltas = new List<double>();
          for (int i = 1; i <= 4; i++) deltas.Add(points[i].Lat - points[i - 1].Lat);
          foreach (var d in deltas)
              Assert.Equal(deltas[0], d, 8);
      }

      // ── #83: a freeze longer than moving time must not zero the estimate ─
      [Fact]
      public void ApplyFrozenSectionDistances_FrozenLongerThanMovingTime_StillEstimatesDistance()
      {
          var t0 = DateTime.Parse("2024-01-01T10:00:00Z").ToUniversalTime();
          var points = new List<TrackPoint>();
          for (int i = 0; i < 10; i++)
              points.Add(new TrackPoint { Lat = 48.0 + i * 0.0009, Lon = 2.0, Time = t0.AddSeconds(i * 30) });

          // Frozen indices 4..7 (900 s of impact) with only 300 s of moving time
          var s = new Summary
          {
              MovingTime = TimeSpan.FromSeconds(300),
              AnomalyReport = new AnomalyReport
              {
                  Anomalies =
                  [
                      new TrackAnomaly
                      {
                          Type = AnomalyType.GpsFrozen,
                          StartIndex = 4, EndIndex = 7,
                          TimeImpactS = 900, WasCorrected = true,
                      },
                  ],
              },
          };
          SpeedCalculator.EnrichPoints(points);

          AnomalyCorrector.ApplyFrozenSectionDistances(points, s);

          double frozenDist = 0;
          for (int i = 4; i <= 7; i++) frozenDist += points[i].DistFromPrev;
          Assert.True(frozenDist > 0,
              "the frozen section must receive an estimated distance, not zero");
      }
  ```
- [ ] Run them and watch them fail: `dotnet test cli/tests/GpxAnalyzer.Cli.Tests/ --filter AnomalyCorrectorTests`
  Expected failures — confirm all four before touching production code:
  - `max speed should stay clamped at the hiking threshold, got 360 km/h` (#81)
  - `elevation gain still includes the corrected spike: ~850 m` and `heart rate max still reports the nulled dropout: 250` (#79/#84)
  - `Assert.Equal() Failure  Expected: 0.00083…  Actual: 0.00139…` on the second delta (#82)
  - `the frozen section must receive an estimated distance, not zero` (#83)
  The `ApplyFrozenSectionDistances` test will not compile until the method exists; add an empty `public static void ApplyFrozenSectionDistances(List<TrackPoint> points, Summary s) { }` stub first so the failure is an assertion failure, not a build error.
- [ ] **Commit 1 (#82):** fix the aliasing in `CorrectGpsFrozen`, confirm `CorrectGpsFrozen_RunStartingAtIndexZero_ProducesEvenlySpacedPoints` passes, run `dotnet test cli/tests/GpxAnalyzer.Cli.Tests/`, then:
  ```bash
  git add cli/src/GpxAnalyzer.Cli.Core/Anomaly/AnomalyCorrector.cs cli/src/GpxAnalyzer.Cli.Core/Gpx/TrackPoint.cs cli/tests/GpxAnalyzer.Cli.Tests/Anomaly/AnomalyCorrectorTests.cs
  git commit -m "fix(cli): stop aliasing the interpolation anchor in CorrectGpsFrozen

  TrackPoint is a mutable class, so 'var p0 = start > 0 ? points[start-1] :
  points[start]' stores a reference. When a frozen run starts at index 0 — which
  PositionAnomalyDetector.DetectGpsFrozen reaches, it scans from i = 0 — the
  loop's first write overwrites p0 itself and every later point interpolates from
  a moved anchor, displacing them by up to ~60 m in the exported GPX.

  Snapshots the anchor coordinates before the loop. Also adds TrackPoint.Clone().

  Closes #82"
  ```
- [ ] **Commit 2 (#83):** replace `RecalculateStats` with `ApplyFrozenSectionDistances` including the fallback, confirm the #83 test passes, run the full CLI suite, then:
  ```bash
  git add cli/src/GpxAnalyzer.Cli.Core/Anomaly/AnomalyCorrector.cs cli/tests/GpxAnalyzer.Cli.Tests/Anomaly/AnomalyCorrectorTests.cs
  git commit -m "fix(cli): do not collapse the frozen-section distance estimate to zero

  avgSpeed2 = healthyDist / max(0, MovingTime - frozenDuration) is by construction
  never below avgSpeed1, so Math.Min always selected avgSpeed1 — except in the one
  case the clamp created: when the freeze is longer than MovingTime the denominator
  hits 0, avgSpeed2 becomes 0 and the Min yields 0. A 20 min run whose GPS froze
  for 15 min reported correction_applied = true with the ~2.5 km covered during
  the freeze still missing.

  Prefers the frozen-excluded denominator and falls back to plain MovingTime
  instead of degenerating.

  Closes #83"
  ```
- [ ] **Commit 3 (#81):** move the recompute into `ComputePipeline` with the `ClampSpeeds` call, confirm the #81 test passes, run the full CLI suite, then:
  ```bash
  git add cli/src/GpxAnalyzer.Cli.Core/Stats/ComputePipeline.cs cli/src/GpxAnalyzer.Cli.Core/Anomaly/AnomalyCorrector.cs cli/tests/GpxAnalyzer.Cli.Tests/
  git commit -m "fix(cli): re-clamp speeds after anomaly corrections

  RecalculateStats re-ran EnrichPoints, which unconditionally recomputes
  DistFromPrev and CalcSpeed, but never re-applied ClampSpeeds — it could not, its
  signature had no access to MaxReasonableSpeed. Every value pipeline step 5 had
  zeroed came back: a corrected backward timestamp produced 100 m in 1 s and the
  JSON reported speed.max = 360 km/h for a hike, and each outlier jump was
  re-added to total_distance. SpeedCalculatorTests.cs:145 documents this exact
  requirement; the integration fixture had stopped exercising the path.

  Moves the post-correction recompute into ComputePipeline, which holds the full
  ComputeConfig, and re-clamps there.

  Closes #81"
  ```
- [ ] **Commit 4 (#79 + #84):** extend the recompute to elevation, time, stops, biometrics and effort, confirm the remaining tests pass, run the full CLI suite, then:
  ```bash
  git add cli/src/GpxAnalyzer.Cli.Core/Stats/ComputePipeline.cs cli/tests/GpxAnalyzer.Cli.Tests/Anomaly/AnomalyCorrectorTests.cs
  git commit -m "fix(cli): recompute every stage affected by anomaly corrections

  ApplyCorrections mutates Ele, Lat/Lon, Time and HeartRate, but only distance,
  speed and points-per-km were refreshed. Elevation, stops, moving time,
  biometrics and effort kept their pre-correction values, while TotalDistance3D
  WAS re-summed from the corrected elevations — so one report mixed corrected and
  uncorrected values derived from the same field, and the exported GPX described
  a different track from the JSON beside it.

  Re-runs the affected pipeline stages in order after ApplyCorrections.

  Closes #79
  Closes #84"
  ```

---

### Task 11: Recording gaps are rejected as stops, so the whole pause counts as moving time

**Issues:** #80

**Files:**
- Modify `cli/src/GpxAnalyzer.Cli.Core/Stats/StopDetector.cs:168` (`BuildStop`)
- Test `cli/tests/GpxAnalyzer.Cli.Tests/Stats/StopDetectorTests.cs` (append)

**Root cause:** `EnrichPoints` sets `CalcSpeed = 0` for the first point after a gap longer than `ElevationSmoother.GapThreshold` (10 min), which makes `DetectStops` open a stop candidate spanning exactly that gap. `BuildStop` then applies the `MaxDistance` test to `Haversine(points[startIdx], points[endIdx-1])` — but across a paused interval that displacement is movement during *unrecorded* time, not GPS jitter at a standstill. The preset limits are jitter tolerances (hiking 30 m, trail 50 m, cycling 100 m), so any real pause where the user moved more than a few tens of metres returns null. The gap then contributes nothing to `TotalStopTime`, and `s.MovingTime = s.TotalTime - s.StoppedTime` charges the entire pause to moving time. It is all-or-nothing, decided by a metric that is meaningless over a gap.

**Fix approach:** The `MaxDistance` jitter test is only meaningful when the interval was actually recorded. Skip it for a candidate whose span contains a recording gap.

```csharp
    private static Stop? BuildStop(List<TrackPoint> points, int startIdx, int endIdx, StopConfig cfg)
    {
        int count = endIdx - startIdx;
        if (count < 2)
            return null;

        var duration = points[endIdx - 1].Time - points[startIdx].Time;
        if (duration < cfg.MinDuration)
            return null;

        // Reject if the person actually moved too far — but only when the interval
        // was recorded. Across a recording gap the displacement is movement during
        // unrecorded time, not jitter at a standstill, and the preset limits
        // (30-100 m) are jitter tolerances. Applying them there discards the pause
        // entirely and charges all of it to moving time.
        if (cfg.MaxDistance > 0 && !SpansRecordingGap(points, startIdx, endIdx - 1))
        {
            double dist = DistanceCalculator.Haversine(
                points[startIdx].Lat, points[startIdx].Lon,
                points[endIdx - 1].Lat, points[endIdx - 1].Lon);
            if (dist > cfg.MaxDistance)
                return null;
        }

        // ... centroid computation unchanged
    }

    /// <summary>
    /// True when any interval inside [startIdx, endIdx] exceeds the pipeline's
    /// recording-gap threshold — i.e. the device stopped logging.
    /// </summary>
    private static bool SpansRecordingGap(List<TrackPoint> points, int startIdx, int endIdx)
    {
        for (int i = startIdx + 1; i <= endIdx && i < points.Count; i++)
            if (points[i].Time - points[i - 1].Time > Elevation.ElevationSmoother.GapThreshold)
                return true;
        return false;
    }
```

**Steps:**

- [ ] Append the failing regression test to `cli/tests/GpxAnalyzer.Cli.Tests/Stats/StopDetectorTests.cs`:
  ```csharp
      [Fact]
      public void DetectStops_AutoPauseGap_CountsAsAStopEvenWhenTheUserMoved()
      {
          var t0 = DateTime.Parse("2024-01-01T10:00:00Z").ToUniversalTime();
          var points = new List<TrackPoint>();

          // 10 min of hiking, one point every 30 s
          for (int i = 0; i < 20; i++)
              points.Add(new TrackPoint { Lat = 48.0 + i * 0.0002, Lon = 2.0, Time = t0.AddSeconds(i * 30) });

          // Watch auto-pauses for 45 min; the hiker resumes ~50 m from where they stopped
          var resume = points[^1].Time.AddMinutes(45);
          double lastLat = points[^1].Lat;
          points.Add(new TrackPoint { Lat = lastLat + 0.00045, Lon = 2.0, Time = resume });

          // ...then hikes on for another 10 min
          for (int i = 1; i < 20; i++)
              points.Add(new TrackPoint
              {
                  Lat = lastLat + 0.00045 + i * 0.0002, Lon = 2.0,
                  Time = resume.AddSeconds(i * 30),
              });

          SpeedCalculator.EnrichPoints(points);
          var stops = StopDetector.DetectStops(points, StopDetector.Presets[StopDetector.PresetHiking]);

          Assert.NotEmpty(stops);
          var total = StopDetector.TotalStopTime(stops);
          Assert.True(total >= TimeSpan.FromMinutes(40),
              $"the 45 min auto-pause should be counted as stopped time, got {total}");
      }

      [Fact]
      public void DetectStops_RecordedStandstillBeyondMaxDistance_IsStillRejected()
      {
          var t0 = DateTime.Parse("2024-01-01T10:00:00Z").ToUniversalTime();
          var points = new List<TrackPoint>();

          // 5 min of continuously recorded very slow movement covering ~200 m,
          // well past the hiking MaxDistance of 30 m: not a stop.
          for (int i = 0; i < 60; i++)
              points.Add(new TrackPoint { Lat = 48.0 + i * 0.00003, Lon = 2.0, Time = t0.AddSeconds(i * 5) });

          SpeedCalculator.EnrichPoints(points);
          var stops = StopDetector.DetectStops(points, StopDetector.Presets[StopDetector.PresetHiking]);

          Assert.Empty(stops);
      }
  ```
- [ ] Run them and watch the first fail: `dotnet test cli/tests/GpxAnalyzer.Cli.Tests/ --filter StopDetectorTests`
  Expected failure: `Assert.NotEmpty() Failure: Collection was empty` — `BuildStop` returns null because the 50 m displacement exceeds the hiking `MaxDistance` of 30 m. The second test must pass both before and after; it guards against over-correcting.
- [ ] Add `SpansRecordingGap` and gate the `MaxDistance` test on it in `BuildStop`
- [ ] Run the tests and watch both pass: `dotnet test cli/tests/GpxAnalyzer.Cli.Tests/ --filter StopDetectorTests`
- [ ] Run the full CLI suite: `dotnet test cli/tests/GpxAnalyzer.Cli.Tests/`
- [ ] Commit:
  ```bash
  git add cli/src/GpxAnalyzer.Cli.Core/Stats/StopDetector.cs cli/tests/GpxAnalyzer.Cli.Tests/Stats/StopDetectorTests.cs
  git commit -m "fix(cli): do not apply the jitter distance test across recording gaps

  EnrichPoints zeroes CalcSpeed after a gap over 10 min, which opens a stop
  candidate spanning the gap. BuildStop then measured the start-to-end
  displacement against MaxDistance — a tolerance tuned for GPS jitter at a
  standstill (30-100 m) — but across an unrecorded interval that displacement is
  real movement. Any auto-pause where the user walked more than a few tens of
  metres was discarded: stop_count 0, stopped_time 0, and the whole pause charged
  to moving time, understating avg_moving_speed by about a third on a 2 h hike.

  Skips the MaxDistance test when the candidate spans a recording gap; a
  continuously recorded slow section is still rejected as before.

  Closes #80"
  ```

---

### Task 12: `split` emits hundreds of thousands of bogus segments and shares points by reference

**Issues:** #73, #99 (and the root-cause half of #85 — see the dependency note; #85 itself is closed in Task 17)

**Files:**
- Modify `cli/src/GpxAnalyzer.Cli.Core/Split/TimeSplitter.cs:15`–`:67`
- Test `cli/tests/GpxAnalyzer.Cli.Tests/Split/TimeSplitterTests.cs` (**new file — there is no test for `TimeSplitter` today**)

**Root cause:** Three defects in one 50-line method, all in the bucket-advance `while` loop.

- **#73** `baseTime = points[0].Time` is taken verbatim. A `<trkpt>` with no `<time>` child yields `DateTime.MinValue` (`GpxParser.cs:59`), so the window starts in year 0001. The `while (p.Time >= segEnd)` loop then walks forward one `interval` at a time until it reaches the first real timestamp, and on every iteration `currentPoints.Count > 0` (it holds the duplicated boundary point) so it appends a fresh single-point segment. Verified by execution: three points, the first untimed, default 24 h interval → **738,886 segments**; at `--interval 30m` it is ~35.5 million and the process OOMs.
- **#99** The same branch fires on every catch-up pass even for a legitimate gap. Verified: two points three hours apart on a 1 h interval returns 4 segments, three of which hold the same single point over a window in which nothing was recorded.
- **#85's root cause** `currentPoints = [lastPoint]` duplicates the boundary point **by reference**, so segment *i*'s last `TrackPoint` and segment *i+1*'s first `TrackPoint` are the same mutable object. `SplitCommand` writes segment *i*, then runs `ComputePipeline.Compute` on it, which mutates `Ele` (DEM + smoothing) and `Lat`/`Lon` (track smoothing) in place — and segment *i+1* is serialized on the next iteration, after those mutations.

**Fix approach:** Anchor the window on the first *usable* timestamp, only emit a segment when it holds points that were actually recorded in that window, and clone the boundary point.

```csharp
// cli/src/GpxAnalyzer.Cli.Core/Split/TimeSplitter.cs — after
public static class TimeSplitter
{
    public static List<TimeSegment> ByTime(List<TrackPoint> points, TimeSpan interval)
    {
        if (points.Count == 0)
            throw new InvalidOperationException("No points to split");
        if (interval <= TimeSpan.Zero)
            throw new ArgumentException("Interval must be positive", nameof(interval));

        var segments = new List<TimeSegment>();

        // A trkpt with no <time> parses as DateTime.MinValue. Anchoring the bucket
        // window there makes the catch-up loop walk two millennia one interval at
        // a time, emitting a junk segment per iteration. Anchor on the first
        // timestamp that is actually usable instead.
        var baseTime = points.FirstOrDefault(p => p.Time > DateTime.MinValue)?.Time
                       ?? points[0].Time;

        int segIndex = 0;
        var currentPoints = new List<TrackPoint>();
        var segStart = baseTime;
        var segEnd = baseTime + interval;

        foreach (var p in points)
        {
            while (p.Time >= segEnd)
            {
                // Only emit a bucket that actually holds recorded points. After a
                // flush, currentPoints holds nothing but the duplicated boundary
                // point, so a multi-interval recording gap would otherwise emit one
                // junk single-point segment per interval it spans.
                if (currentPoints.Count > 1)
                {
                    var lastPoint = currentPoints[^1];
                    segments.Add(new TimeSegment
                    {
                        Index = segIndex,
                        StartTime = segStart,
                        EndTime = segEnd,
                        Points = new List<TrackPoint>(currentPoints)
                    });
                    segIndex++;

                    // Clone: consumers (SplitCommand) run ComputePipeline per
                    // segment, which mutates Ele/Lat/Lon in place. Sharing the
                    // boundary object writes one segment's smoothed values into
                    // the neighbouring segment's exported GPX.
                    currentPoints = [lastPoint.Clone()];
                }

                segStart = segEnd;
                segEnd = segStart + interval;
            }

            currentPoints.Add(p);
        }

        if (currentPoints.Count > 0)
        {
            segments.Add(new TimeSegment
            {
                Index = segIndex,
                StartTime = segStart,
                EndTime = segEnd,
                Points = currentPoints
            });
        }

        return segments;
    }
}
```

`currentPoints.Count > 1` is the fix for #99: after a flush the list holds exactly the one cloned boundary point, so a bucket that received no new point is skipped, and the window still advances. The untimed leading point is carried into the first segment rather than driving the window.

`TrackPoint.Clone()` is added in Task 10; this task depends on it.

**Steps:**

- [ ] Create the new test file `cli/tests/GpxAnalyzer.Cli.Tests/Split/TimeSplitterTests.cs`:
  ```csharp
  using GpxAnalyzer.Cli.Core.Gpx;
  using GpxAnalyzer.Cli.Core.Split;

  namespace GpxAnalyzer.Cli.Tests.Split;

  public class TimeSplitterTests
  {
      private static TrackPoint P(double lat, DateTime t) =>
          new() { Lat = lat, Lon = 2.0, Ele = 100, Time = t };

      [Fact]
      public void ByTime_UntimedFirstPoint_DoesNotExplodeIntoBogusSegments()
      {
          var t0 = DateTime.Parse("2024-01-01T10:00:00Z").ToUniversalTime();
          var points = new List<TrackPoint>
          {
              new() { Lat = 48.0, Lon = 2.0, Ele = 100 },   // no <time> -> DateTime.MinValue
              P(48.001, t0),
              P(48.002, t0.AddMinutes(1)),
          };

          var segments = TimeSplitter.ByTime(points, TimeSpan.FromHours(24));

          // Before the fix this returns 738,886 segments and ~700 MB of allocations.
          Assert.Single(segments);
          Assert.Equal(3, segments[0].Points.Count);
      }

      [Fact]
      public void ByTime_RecordingGapLongerThanInterval_DoesNotEmitDuplicateOnePointSegments()
      {
          var t0 = DateTime.Parse("2024-01-01T10:00:00Z").ToUniversalTime();
          var points = new List<TrackPoint> { P(48.0, t0), P(48.01, t0.AddHours(3)) };

          var segments = TimeSplitter.ByTime(points, TimeSpan.FromHours(1));

          // Before the fix: 4 segments, three of them holding the same single point.
          Assert.All(segments, s => Assert.True(s.Points.Count >= 2,
              $"segment {s.Index} holds only {s.Points.Count} point(s)"));
          Assert.True(segments.Count <= 2, $"expected at most 2 segments, got {segments.Count}");
      }

      [Fact]
      public void ByTime_BoundaryPointIsCloned_SoMutatingOneSegmentDoesNotAffectTheNext()
      {
          var t0 = DateTime.Parse("2024-01-01T10:00:00Z").ToUniversalTime();
          var points = new List<TrackPoint>();
          for (int i = 0; i < 6; i++) points.Add(P(48.0 + i * 0.001, t0.AddMinutes(i * 20)));

          var segments = TimeSplitter.ByTime(points, TimeSpan.FromHours(1));
          Assert.True(segments.Count >= 2, "fixture must produce at least two segments");

          // SplitCommand writes segment i, then runs ComputePipeline on it, which
          // mutates Ele in place. That must not reach segment i+1's first point.
          var tail = segments[0].Points[^1];
          var head = segments[1].Points[0];
          Assert.NotSame(tail, head);

          tail.Ele = 9999;
          Assert.NotEqual(9999, head.Ele);
      }

      [Fact]
      public void ByTime_NormalMultiDayTrack_SplitsOnePerDay()
      {
          var t0 = DateTime.Parse("2024-01-01T08:00:00Z").ToUniversalTime();
          var points = new List<TrackPoint>();
          for (int day = 0; day < 3; day++)
              for (int i = 0; i < 10; i++)
                  points.Add(P(48.0 + i * 0.001, t0.AddDays(day).AddMinutes(i * 10)));

          var segments = TimeSplitter.ByTime(points, TimeSpan.FromHours(24));

          Assert.Equal(3, segments.Count);
          Assert.All(segments, s => Assert.True(s.Points.Count >= 10));
      }
  }
  ```
- [ ] Run them and watch them fail: `dotnet test cli/tests/GpxAnalyzer.Cli.Tests/ --filter TimeSplitterTests`
  Expected failures: `Assert.Single() Failure  Collection: [...]  Actual count: 738886` (#73, may take ~250 ms and several hundred MB); `segment 0 holds only 1 point(s)` (#99); `Assert.NotSame() Failure` (the boundary-reference half of #85). The fourth test should already pass and guards the happy path.
- [ ] Rewrite `TimeSplitter.ByTime` as shown (requires `TrackPoint.Clone()` from Task 10)
- [ ] Run the tests and watch all four pass: `dotnet test cli/tests/GpxAnalyzer.Cli.Tests/ --filter TimeSplitterTests`
- [ ] Run the full CLI suite: `dotnet test cli/tests/GpxAnalyzer.Cli.Tests/`
- [ ] Commit:
  ```bash
  git add cli/src/GpxAnalyzer.Cli.Core/Split/TimeSplitter.cs cli/tests/GpxAnalyzer.Cli.Tests/Split/
  git commit -m "fix(cli): anchor split buckets on a real timestamp and clone the boundary point

  baseTime was points[0].Time taken verbatim. A trkpt with no <time> parses as
  DateTime.MinValue, so the bucket window started in year 0001 and the catch-up
  loop walked two millennia one interval at a time, appending a single-point
  segment on every iteration: 738,886 segments for a three-point file at the
  default 24h interval, ~35.5 million (and an OOM) at 30m.

  The same branch also fired on every catch-up pass across a legitimate recording
  gap, emitting one junk single-point segment per interval spanned, and the
  boundary point was duplicated by reference so ComputePipeline's in-place Ele
  and Lat/Lon mutations on one segment leaked into the next segment's export.

  Anchors on the first usable timestamp, only emits buckets holding recorded
  points, and clones the boundary point.

  Closes #73
  Closes #99"
  ```

---

### Task 13: `merge` scrambles the geometry of points sharing a timestamp

**Issues:** #74

**Files:**
- Modify `cli/src/GpxAnalyzer.Cli.Core/Merge/GpxMerger.cs:14`
- Test `cli/tests/GpxAnalyzer.Cli.Tests/Merge/GpxMergerTests.cs` (**new file — there is no test for `GpxMerger` today**)

**Root cause:** `List<T>.Sort` is an introsort and is documented as unstable, so points comparing equal on `Time` are reordered arbitrarily. Every point parsed from a `<trkpt>` with no `<time>` child gets `DateTime.MinValue`, so an entire untimed track is one giant block of equal keys. `merge` enables this path by default (`--sort` defaults to `true`), and the reordered list is what gets written and analyzed. Verified by execution: 20 untimed points with `Lat` 1..20 merged with one timed point come back as `1,19,18,17,16,15,14,13,12,20,11,9,8,7,6,5,4,3,2,10,100`.

**Fix approach:** Use a stable sort. LINQ's `OrderBy` is documented as stable, which preserves both the within-file order of untimed points and the file order of the documents they came from.

```csharp
// cli/src/GpxAnalyzer.Cli.Core/Merge/GpxMerger.cs — before
        if (sortByTime)
            allPoints.Sort((a, b) => a.Time.CompareTo(b.Time));

// after
        if (sortByTime)
            // List<T>.Sort is an introsort and is explicitly unstable. Untimed
            // trkpts all parse to DateTime.MinValue, so a course/route GPX is one
            // block of equal keys and introsort shuffles its geometry into a
            // zig-zag. OrderBy is documented stable.
            allPoints = [.. allPoints.OrderBy(p => p.Time)];
```

`allPoints` must lose its `var`-inferred readonly-ness — it is already a local `List<TrackPoint>`, so reassignment compiles as-is.

**Steps:**

- [ ] Create the new test file `cli/tests/GpxAnalyzer.Cli.Tests/Merge/GpxMergerTests.cs`:
  ```csharp
  using GpxAnalyzer.Cli.Core.Gpx;
  using GpxAnalyzer.Cli.Core.Merge;

  namespace GpxAnalyzer.Cli.Tests.Merge;

  public class GpxMergerTests
  {
      private static GpxDocument DocOf(params TrackPoint[] points) => new()
      {
          Version = "1.1",
          Creator = "test",
          Tracks = [new GpxTrack { Name = "t", Segments = [new GpxSegment { Points = [.. points] }] }],
      };

      [Fact]
      public void Merge_UntimedCourse_PreservesPointOrder()
      {
          // A Komoot / Garmin course export: no <time> on any trkpt, so every
          // point parses to DateTime.MinValue — one block of equal sort keys.
          var course = new List<TrackPoint>();
          for (int i = 1; i <= 20; i++)
              course.Add(new TrackPoint { Lat = i, Lon = 2.0, Ele = 100 });

          var timed = new TrackPoint
          {
              Lat = 100, Lon = 2.0, Ele = 100,
              Time = DateTime.Parse("2024-01-01T10:00:00Z").ToUniversalTime(),
          };

          var merged = GpxMerger.Merge([DocOf([.. course]), DocOf(timed)], sortByTime: true);
          var lats = merged.AllPoints().Select(p => p.Lat).ToList();

          // Untimed points keep their input order; the timed point sorts after them.
          Assert.Equal(Enumerable.Range(1, 20).Select(i => (double)i).Append(100).ToList(), lats);
      }

      [Fact]
      public void Merge_PointsSharingOneSecond_KeepTheirRecordedOrder()
      {
          var t = DateTime.Parse("2024-01-01T10:00:00Z").ToUniversalTime();
          var a = new List<TrackPoint>();
          for (int i = 0; i < 10; i++)
              a.Add(new TrackPoint { Lat = 48.0 + i * 0.0001, Lon = 2.0, Time = t });

          var merged = GpxMerger.Merge([DocOf([.. a])], sortByTime: true);
          var lats = merged.AllPoints().Select(p => p.Lat).ToList();

          Assert.Equal(a.Select(p => p.Lat).ToList(), lats);
      }

      [Fact]
      public void Merge_TimedTracks_StillInterleavesByTime()
      {
          var t = DateTime.Parse("2024-01-01T10:00:00Z").ToUniversalTime();
          var morning = DocOf(
              new TrackPoint { Lat = 1, Lon = 2.0, Time = t },
              new TrackPoint { Lat = 3, Lon = 2.0, Time = t.AddMinutes(20) });
          var midday = DocOf(
              new TrackPoint { Lat = 2, Lon = 2.0, Time = t.AddMinutes(10) },
              new TrackPoint { Lat = 4, Lon = 2.0, Time = t.AddMinutes(30) });

          var merged = GpxMerger.Merge([morning, midday], sortByTime: true);

          Assert.Equal([1d, 2d, 3d, 4d], merged.AllPoints().Select(p => p.Lat).ToList());
      }
  }
  ```
  Check `GpxDocument`/`GpxTrack`/`GpxSegment` initialiser names and the `AllPoints()` signature against `cli/src/GpxAnalyzer.Cli.Core/Gpx/GpxDocument.cs` before running.
- [ ] Run them and watch the first two fail: `dotnet test cli/tests/GpxAnalyzer.Cli.Tests/ --filter GpxMergerTests`
  Expected failure: `Assert.Equal() Failure  Expected: [1, 2, 3, …]  Actual: [1, 19, 18, 17, …]` — introsort shuffled the equal-key block. The third test must pass before and after.
- [ ] Replace `allPoints.Sort(...)` with the stable `OrderBy` projection
- [ ] Run the tests and watch all three pass: `dotnet test cli/tests/GpxAnalyzer.Cli.Tests/ --filter GpxMergerTests`
- [ ] Run the full CLI suite: `dotnet test cli/tests/GpxAnalyzer.Cli.Tests/`
- [ ] Commit:
  ```bash
  git add cli/src/GpxAnalyzer.Cli.Core/Merge/GpxMerger.cs cli/tests/GpxAnalyzer.Cli.Tests/Merge/
  git commit -m "fix(cli): sort merged trackpoints stably

  List<T>.Sort is an introsort and is documented unstable. Every trkpt with no
  <time> parses to DateTime.MinValue, so an entire course/route GPX (Komoot,
  Garmin Connect course export, RideWithGPS) is one block of equal keys and gets
  shuffled: 20 points in order 1..20 came back as 1,19,18,17,…,2,10. merge sorts
  by default, so the emitted track was a zig-zag whose computed distance was many
  times the real one. The same reordering hits any device logging several points
  inside one second.

  Uses the documented-stable OrderBy instead.

  Closes #74"
  ```

---

### Task 14: `Path.GetDirectoryName` returns empty, not null, for a bare filename

**Issues:** #75, #76

**Files:**
- Modify `cli/src/GpxAnalyzer.Cli.Core/Gpx/GpxWriter.cs:71`, `:77`
- Modify `cli/src/GpxAnalyzer.Cli.Core/Input/FileResolver.cs:33`
- Test `cli/tests/GpxAnalyzer.Cli.Tests/Gpx/GpxWriterTests.cs` (new), `cli/tests/GpxAnalyzer.Cli.Tests/Input/FileResolverTests.cs` (new)

**Root cause:** One root cause in two files: `Path.GetDirectoryName` returns `string.Empty`, not `null`, when the argument has no directory component, so the `?? "."` fallback never fires.

- **#75** `Directory.CreateDirectory("")` throws `ArgumentException: The value cannot be an empty string. (Parameter 'path')`. `merge` uses the default `--output merged.gpx`, so `GpxWriter.Write` throws, `MergeCommand`'s catch swallows it into an error line, and the command returns exit code 0 with no output file. `merge` is unusable unless the user passes a path containing a separator. The same defect is in `WriteEnriched`.
- **#76** `Directory.Exists("")` returns `false`, so `ResolveArg` falls through to `return []` and `ResolveFiles` throws `No GPX files found in arguments: *.gpx`. This matters on Windows specifically: PowerShell and cmd.exe do not expand globs, so the literal pattern reaches the CLI — exactly the case this branch exists for.

**Fix approach:** Treat an empty directory component as the current directory in both places.

```csharp
// GpxWriter.cs
    public static void Write(string path, List<TrackPoint> points, string trackName)
    {
        EnsureDirectory(path);
        WriteToFile(path, points, trackName, enrich: false);
    }

    public static void WriteEnriched(string path, List<TrackPoint> points, string trackName)
    {
        EnsureDirectory(path);
        WriteToFile(path, points, trackName, enrich: true);
    }

    /// <summary>
    /// Creates the output directory when the path has one. Path.GetDirectoryName
    /// returns string.Empty (not null) for a bare filename, so a "?? \".\"" fallback
    /// never fires and CreateDirectory("") throws.
    /// </summary>
    private static void EnsureDirectory(string path)
    {
        var dir = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(dir))
            Directory.CreateDirectory(dir);
    }

// FileResolver.cs
        if (arg.Contains('*') || arg.Contains('?') || arg.Contains('['))
        {
            // GetDirectoryName returns string.Empty (not null) for a bare pattern
            // such as "*.gpx", which PowerShell and cmd.exe pass through unexpanded.
            var dirPart = Path.GetDirectoryName(arg);
            string dir = string.IsNullOrEmpty(dirPart) ? "." : dirPart;
            string pattern = Path.GetFileName(arg);
            if (Directory.Exists(dir))
            {
                var matches = Directory.GetFiles(dir, pattern);
                return FilterGpx(matches);
            }
            return [];
        }
```

**Steps:**

- [ ] Create `cli/tests/GpxAnalyzer.Cli.Tests/Gpx/GpxWriterTests.cs`:
  ```csharp
  using GpxAnalyzer.Cli.Core.Gpx;

  namespace GpxAnalyzer.Cli.Tests.Gpx;

  public class GpxWriterTests
  {
      private static List<TrackPoint> SamplePoints()
      {
          var t0 = DateTime.Parse("2024-01-02T10:04:05Z").ToUniversalTime();
          return
          [
              new() { Lat = 48.0, Lon = 2.0, Ele = 100, Time = t0 },
              new() { Lat = 48.001, Lon = 2.0, Ele = 110, Time = t0.AddSeconds(30) },
          ];
      }

      [Fact]
      public void Write_BareFilename_DoesNotThrow()
      {
          var tmp = Directory.CreateTempSubdirectory();
          var previous = Directory.GetCurrentDirectory();
          try
          {
              Directory.SetCurrentDirectory(tmp.FullName);

              // This is exactly what `merge` does with its default --output merged.gpx.
              GpxWriter.Write("merged.gpx", SamplePoints(), "merged");

              Assert.True(File.Exists(Path.Combine(tmp.FullName, "merged.gpx")));
          }
          finally
          {
              Directory.SetCurrentDirectory(previous);
              tmp.Delete(recursive: true);
          }
      }

      [Fact]
      public void WriteEnriched_BareFilename_DoesNotThrow()
      {
          var tmp = Directory.CreateTempSubdirectory();
          var previous = Directory.GetCurrentDirectory();
          try
          {
              Directory.SetCurrentDirectory(tmp.FullName);
              GpxWriter.WriteEnriched("enriched.gpx", SamplePoints(), "enriched");
              Assert.True(File.Exists(Path.Combine(tmp.FullName, "enriched.gpx")));
          }
          finally
          {
              Directory.SetCurrentDirectory(previous);
              tmp.Delete(recursive: true);
          }
      }

      [Fact]
      public void Write_PathWithDirectory_StillCreatesIt()
      {
          var tmp = Directory.CreateTempSubdirectory();
          try
          {
              var outPath = Path.Combine(tmp.FullName, "nested", "out.gpx");
              GpxWriter.Write(outPath, SamplePoints(), "out");
              Assert.True(File.Exists(outPath));
          }
          finally { tmp.Delete(recursive: true); }
      }
  }
  ```
- [ ] Create `cli/tests/GpxAnalyzer.Cli.Tests/Input/FileResolverTests.cs`:
  ```csharp
  using GpxAnalyzer.Cli.Core.Input;

  namespace GpxAnalyzer.Cli.Tests.Input;

  public class FileResolverTests
  {
      [Fact]
      public void ResolveFiles_BareGlobInCurrentDirectory_FindsTheFiles()
      {
          var tmp = Directory.CreateTempSubdirectory();
          var previous = Directory.GetCurrentDirectory();
          try
          {
              File.WriteAllText(Path.Combine(tmp.FullName, "a.gpx"), "<gpx/>");
              File.WriteAllText(Path.Combine(tmp.FullName, "b.gpx"), "<gpx/>");
              File.WriteAllText(Path.Combine(tmp.FullName, "notes.txt"), "x");
              Directory.SetCurrentDirectory(tmp.FullName);

              // PowerShell and cmd.exe do not expand globs, so this literal
              // pattern is what actually reaches the CLI on Windows.
              var files = FileResolver.ResolveFiles(["*.gpx"]);

              Assert.Equal(2, files.Count);
              Assert.All(files, f => Assert.EndsWith(".gpx", f, StringComparison.OrdinalIgnoreCase));
          }
          finally
          {
              Directory.SetCurrentDirectory(previous);
              tmp.Delete(recursive: true);
          }
      }

      [Fact]
      public void ResolveFiles_GlobWithDirectory_StillWorks()
      {
          var tmp = Directory.CreateTempSubdirectory();
          try
          {
              File.WriteAllText(Path.Combine(tmp.FullName, "a.gpx"), "<gpx/>");
              var files = FileResolver.ResolveFiles([Path.Combine(tmp.FullName, "*.gpx")]);
              Assert.Single(files);
          }
          finally { tmp.Delete(recursive: true); }
      }
  }
  ```
- [ ] Run them and watch them fail: `dotnet test cli/tests/GpxAnalyzer.Cli.Tests/ --filter "GpxWriterTests|FileResolverTests"`
  Expected failures: `System.ArgumentException : The value cannot be an empty string. (Parameter 'path')` from `Write_BareFilename_DoesNotThrow` and `WriteEnriched_BareFilename_DoesNotThrow` (#75); `System.InvalidOperationException : No GPX files found in arguments: *.gpx` from `ResolveFiles_BareGlobInCurrentDirectory_FindsTheFiles` (#76). The two "with directory" tests must pass before and after.
- [ ] Add `EnsureDirectory` to `GpxWriter` and use it from both `Write` and `WriteEnriched`
- [ ] Apply the `string.IsNullOrEmpty` guard in `FileResolver.ResolveArg`
- [ ] Run the tests and watch all five pass: `dotnet test cli/tests/GpxAnalyzer.Cli.Tests/ --filter "GpxWriterTests|FileResolverTests"`
- [ ] Run the full CLI suite: `dotnet test cli/tests/GpxAnalyzer.Cli.Tests/`
- [ ] Commit:
  ```bash
  git add cli/src/GpxAnalyzer.Cli.Core/Gpx/GpxWriter.cs cli/src/GpxAnalyzer.Cli.Core/Input/FileResolver.cs cli/tests/GpxAnalyzer.Cli.Tests/Gpx/GpxWriterTests.cs cli/tests/GpxAnalyzer.Cli.Tests/Input/FileResolverTests.cs
  git commit -m "fix(cli): treat an empty directory component as the current directory

  Path.GetDirectoryName returns string.Empty, not null, for an argument with no
  directory component, so the '?? \".\"' fallback in GpxWriter and FileResolver
  never fired.

  GpxWriter.Write/WriteEnriched therefore called Directory.CreateDirectory(\"\"),
  which throws — so 'gpx-analyzer merge a.gpx b.gpx' with its default
  --output merged.gpx produced no file and still exited 0.

  FileResolver hit Directory.Exists(\"\") == false and returned no matches, so
  'gpx-analyzer analyze *.gpx' from PowerShell (which does not expand globs)
  reported 'No GPX files found' in a directory full of them.

  Closes #75
  Closes #76"
  ```

---

### Task 15: GPX export fidelity and the power-namespace round trip

**Issues:** #97, #98, #100, #115

**Files:**
- Modify `cli/src/GpxAnalyzer.Cli.Core/Gpx/GpxWriter.cs:124` (timestamp), `:126` (non-enriched fields), `:175` (power namespace)
- Modify `ui/api/Services/ProfileComputationService.cs:79` (power lookup)
- Test `cli/tests/GpxAnalyzer.Cli.Tests/Gpx/GpxWriterTests.cs` (append — created in Task 14)
- Test `ui/api.Tests/Profile/ProfileApiTests.cs` (append)

> **Ordering:** run this task immediately after Task 14. Both touch `GpxWriter.cs`; Task 14 changes the two entry points (`Write`/`WriteEnriched`), this one changes the body (`WriteToFile`/`WriteEnrichedExtensions`), so they do not conflict — but doing them adjacently keeps the file's history readable.

**Root cause:** Four issues, one task, because #98 and #115 are the *same defect seen from both ends of a contract* and #97/#100 are in the same two methods.

- **#97** `WriteToFile` emits only `lat`/`lon`/`ele`/`time`. Every biometric (`HeartRate`, `Cadence`, `Power`, `Temperature`), every GPS-quality field (`Fix`, `Satellites`, `Hdop`, `Vdop`, `Pdop`) and `DeviceSpeed`/`WaterTemp` are written only under the `if (enrich)` branch or not at all. `GpxWriter.Write` (`enrich: false`) is the writer used by both `split` and `merge`, neither of which exposes `--enrich`, so those two commands can never preserve the data. Verified round-trip: a Garmin GPX with `hr=140 cad=85 pwr=210 temp=18.5 sat=9 hdop=1.1` comes back all `null`.
- **#98 + #115** `WriteElementString(localName, value)` passes `null` for the namespace, which in `XmlWriter` means "inherit the current default namespace" — not "no namespace". The enclosing `<gpx>` declares `xmlns="http://www.topografix.com/GPX/1/1"`, so the element is emitted as `{http://www.topografix.com/GPX/1/1}power`, contradicting the `// Power (bare element)` comment. The CLI's own `GpxExtensionParser` matches on `LocalName` and still finds it, which hides the mismatch, but `ProfileComputationService.cs:79` does `extensions.Element("power")` with an unqualified `XName` and gets null — so for every activity uploaded from a power-meter GPX, `power` is absent from all 500 profile points and `avgPower` is null on every split. **Fixing either side alone is wrong:** namespace the write without namespacing the read and the API still sees null; namespace the read without fixing the write and any file written by a fixed CLI stops parsing. Both sides move in this one commit.
- **#100** `tp.Time.ToString("yyyy-MM-ddTHH:mm:ssZ")` has no `IFormatProvider`. In a custom date/time format string `:` is the time-separator placeholder, replaced by `CurrentCulture.DateTimeFormat.TimeSeparator`. Every other value in the file is written with `CultureInfo.InvariantCulture`; this one call was missed. Verified: under `fi-FI` (also `cs-CZ`, `et-EE`) it yields `2024-01-02T10.04.05Z`, which violates the GPX `xsd:dateTime` schema and makes `GpxParser.cs:58` throw an unhandled `FormatException` on re-import. It affects `analyze --export`, `split`, `merge` and the API's enriched export.

**Fix approach:** Decide the namespace question once and write it explicitly on both sides. The Garmin `TrackPointExtension` schema places `power` in the `gpxtpx` namespace only in v2; the CLI has always emitted it as a sibling of `TrackPointMetrics`, and existing exports carry it in the GPX default namespace. Keep that shape but make it *explicit* rather than inherited, and make the reader ask for the same name.

```csharp
// GpxWriter.WriteToFile — #100 and #97
            w.WriteStartElement("trkpt", GpxNs);
            w.WriteAttributeString("lat", tp.Lat.ToString(CultureInfo.InvariantCulture));
            w.WriteAttributeString("lon", tp.Lon.ToString(CultureInfo.InvariantCulture));
            w.WriteElementString("ele", GpxNs, tp.Ele.ToString(CultureInfo.InvariantCulture));
            // ':' is the time-separator placeholder in a custom format string, so
            // without InvariantCulture this emits e.g. 2024-01-02T10.04.05Z under
            // fi-FI / cs-CZ / et-EE — invalid per the GPX xsd:dateTime schema.
            w.WriteElementString("time", GpxNs,
                tp.Time.ToString("yyyy-MM-ddTHH:mm:ssZ", CultureInfo.InvariantCulture));

            // GPS quality lives on the trkpt itself in GPX 1.1 and is part of the
            // source data, not a computed metric — split and merge have no --enrich
            // flag, so writing it only under `enrich` silently discarded it.
            if (tp.Fix is not null)
                w.WriteElementString("fix", GpxNs, tp.Fix);
            if (tp.Satellites is not null)
                w.WriteElementString("sat", GpxNs, tp.Satellites.Value.ToString(CultureInfo.InvariantCulture));
            if (tp.Hdop is not null)
                w.WriteElementString("hdop", GpxNs, tp.Hdop.Value.ToString(CultureInfo.InvariantCulture));
            if (tp.Vdop is not null)
                w.WriteElementString("vdop", GpxNs, tp.Vdop.Value.ToString(CultureInfo.InvariantCulture));
            if (tp.Pdop is not null)
                w.WriteElementString("pdop", GpxNs, tp.Pdop.Value.ToString(CultureInfo.InvariantCulture));

            if (enrich)
            {
                double grade = 0;
                if (i > 0)
                {
                    double hDist = tp.DistFromPrev;
                    if (hDist > 0)
                        grade = (tp.Ele - points[i - 1].Ele) / hDist;
                }

                WriteEnrichedExtensions(w, tp, cumDist, grade);
            }
            else
            {
                WriteSourceExtensions(w, tp);
            }

            w.WriteEndElement(); // trkpt
```

`fix`/`sat`/`hdop`/`vdop`/`pdop` must be written in GPX 1.1's declared child order (after `time`, before `extensions`), which is where they are placed above.

```csharp
    /// <summary>
    /// Writes the source biometrics for a non-enriched export. split and merge use
    /// this writer and have no --enrich flag, so without it every heart rate,
    /// cadence, power and temperature sample in the input is silently dropped.
    /// </summary>
    private static void WriteSourceExtensions(XmlWriter w, TrackPoint tp)
    {
        bool hasGarmin = tp.HeartRate is not null || tp.Cadence is not null
                      || tp.Temperature is not null || tp.DeviceSpeed is not null
                      || tp.WaterTemp is not null;
        if (!hasGarmin && tp.Power is null) return;

        w.WriteStartElement("extensions", GpxNs);

        if (hasGarmin)
        {
            w.WriteStartElement("TrackPointExtension", GpxtpxNs);
            if (tp.HeartRate is not null)
                w.WriteElementString("hr", GpxtpxNs, tp.HeartRate.Value.ToString(CultureInfo.InvariantCulture));
            if (tp.Cadence is not null)
                w.WriteElementString("cad", GpxtpxNs, tp.Cadence.Value.ToString(CultureInfo.InvariantCulture));
            if (tp.Temperature is not null)
                w.WriteElementString("atemp", GpxtpxNs, tp.Temperature.Value.ToString(CultureInfo.InvariantCulture));
            if (tp.WaterTemp is not null)
                w.WriteElementString("wtemp", GpxtpxNs, tp.WaterTemp.Value.ToString(CultureInfo.InvariantCulture));
            if (tp.DeviceSpeed is not null)
                w.WriteElementString("speed", GpxtpxNs, tp.DeviceSpeed.Value.ToString(CultureInfo.InvariantCulture));
            w.WriteEndElement();
        }

        WritePower(w, tp);

        w.WriteEndElement(); // extensions
    }

    /// <summary>
    /// Power is written in the GPX default namespace, explicitly. The two-argument
    /// WriteElementString overload passes null for the namespace, which XmlWriter
    /// reads as "inherit the in-scope default" rather than "no namespace", so the
    /// element was already landing in GpxNs while the code claimed it was bare —
    /// and ProfileComputationService looked it up with an unqualified XName and
    /// always got null.
    /// </summary>
    private static void WritePower(XmlWriter w, TrackPoint tp)
    {
        if (tp.Power is null) return;
        w.WriteElementString("power", GpxNs, tp.Power.Value.ToString(CultureInfo.InvariantCulture));
    }
```

`WriteEnrichedExtensions` drops its trailing bare-power block and calls `WritePower(w, tp)` instead. The non-enriched path must also declare the `gpxtpx` prefix on the root, so the `if (enrich)` guard around the two `xmlns` attributes at lines 97–101 becomes:

```csharp
        w.WriteAttributeString("xmlns", "gpxtpx", null, GpxtpxNs);
        if (enrich)
            w.WriteAttributeString("xmlns", "gpxa", null, GpxaNs);
```

The API side asks for the same qualified name:

```csharp
// ui/api/Services/ProfileComputationService.cs:79 — before
                    power = ParseInt(extensions.Element("power")?.Value);

// after
                    // GpxWriter emits <power> in the GPX default namespace (the
                    // two-arg WriteElementString overload inherits it), so an
                    // unqualified XName never matched and power was always null,
                    // in ProfileJson and in ComputeKmSplits' AvgPower.
                    power = ParseInt(extensions.Element(ns + "power")?.Value)
                         ?? ParseInt(extensions.Element("power")?.Value);
```

The unqualified fallback keeps any hand-written or third-party GPX working. `ns` is the GPX namespace already in scope in that method (used at line 59 for `trkpt.Element(ns + "extensions")`).

**Steps:**

- [ ] Append the failing CLI regression tests to `cli/tests/GpxAnalyzer.Cli.Tests/Gpx/GpxWriterTests.cs`:
  ```csharp
      [Fact]
      public void Write_NonEnriched_PreservesBiometricsAndGpsQuality()
      {
          var tmp = Directory.CreateTempSubdirectory();
          try
          {
              var t0 = DateTime.Parse("2024-01-02T10:04:05Z").ToUniversalTime();
              var points = new List<TrackPoint>
              {
                  new()
                  {
                      Lat = 48.0, Lon = 2.0, Ele = 100, Time = t0,
                      HeartRate = 140, Cadence = 85, Power = 210, Temperature = 18.5,
                      Satellites = 9, Hdop = 1.1, Fix = "3d",
                  },
                  new()
                  {
                      Lat = 48.001, Lon = 2.0, Ele = 110, Time = t0.AddSeconds(30),
                      HeartRate = 145, Cadence = 86, Power = 215, Temperature = 18.6,
                      Satellites = 9, Hdop = 1.2, Fix = "3d",
                  },
              };

              var outPath = Path.Combine(tmp.FullName, "out.gpx");
              // This is the writer `split` and `merge` use; neither has --enrich.
              GpxWriter.Write(outPath, points, "out");

              var reparsed = GpxParser.ParseFile(outPath).AllPoints();

              Assert.Equal(140, reparsed[0].HeartRate);
              Assert.Equal(85, reparsed[0].Cadence);
              Assert.Equal(210, reparsed[0].Power);
              Assert.Equal(18.5, reparsed[0].Temperature);
              Assert.Equal(9, reparsed[0].Satellites);
              Assert.Equal(1.1, reparsed[0].Hdop);
          }
          finally { tmp.Delete(recursive: true); }
      }

      [Fact]
      public void WriteEnriched_PowerElement_IsInTheGpxDefaultNamespace()
      {
          var tmp = Directory.CreateTempSubdirectory();
          try
          {
              var t0 = DateTime.Parse("2024-01-02T10:04:05Z").ToUniversalTime();
              var points = new List<TrackPoint>
              {
                  new() { Lat = 48.0, Lon = 2.0, Ele = 100, Time = t0, Power = 210 },
                  new() { Lat = 48.001, Lon = 2.0, Ele = 110, Time = t0.AddSeconds(30), Power = 215 },
              };
              var outPath = Path.Combine(tmp.FullName, "enriched.gpx");
              GpxWriter.WriteEnriched(outPath, points, "enriched");

              System.Xml.Linq.XNamespace ns = "http://www.topografix.com/GPX/1/1";
              var doc = System.Xml.Linq.XDocument.Load(outPath);
              var ext = doc.Descendants(ns + "extensions").First();

              // This is the exact lookup ui/api ProfileComputationService performs.
              Assert.NotNull(ext.Element(ns + "power"));
              Assert.Equal("210", ext.Element(ns + "power")!.Value);
          }
          finally { tmp.Delete(recursive: true); }
      }

      [Theory]
      [InlineData("fi-FI")]
      [InlineData("cs-CZ")]
      public void Write_UnderACultureWithANonColonTimeSeparator_EmitsValidIsoTimestamps(string culture)
      {
          var previous = System.Globalization.CultureInfo.CurrentCulture;
          var tmp = Directory.CreateTempSubdirectory();
          try
          {
              System.Globalization.CultureInfo.CurrentCulture =
                  new System.Globalization.CultureInfo(culture);

              var outPath = Path.Combine(tmp.FullName, "out.gpx");
              GpxWriter.Write(outPath, SamplePoints(), "out");

              var xml = File.ReadAllText(outPath);
              Assert.Contains("2024-01-02T10:04:05Z", xml);
              Assert.DoesNotContain("10.04.05", xml);

              // And it must round-trip through the parser.
              var reparsed = GpxParser.ParseFile(outPath).AllPoints();
              Assert.Equal(2, reparsed.Count);
          }
          finally
          {
              System.Globalization.CultureInfo.CurrentCulture = previous;
              tmp.Delete(recursive: true);
          }
      }
  ```
  Note the `[Theory]` requires `InvariantGlobalization` to be off for the **test** project — it is a library, not the AOT exe, so `CultureInfo("fi-FI")` resolves. If `cli/tests/GpxAnalyzer.Cli.Tests/GpxAnalyzer.Cli.Tests.csproj` sets `InvariantGlobalization`, remove it (the exe keeps its own setting).
- [ ] Run them and watch them fail: `dotnet test cli/tests/GpxAnalyzer.Cli.Tests/ --filter GpxWriterTests`
  Expected failures: `Assert.Equal() Failure  Expected: 140  Actual: (null)` (#97); `Assert.NotNull() Failure` — the element is present but under a name the test asks for only after the explicit-namespace change makes the intent stable (#98; before the fix the element *does* land in `GpxNs` by inheritance, so verify this assertion by first running it against the current code and recording the result, then keep it as the contract lock); `Assert.Contains("2024-01-02T10:04:05Z")` fails with the file containing `2024-01-02T10.04.05Z` (#100).
- [ ] Append the failing API regression test to `ui/api.Tests/Profile/ProfileApiTests.cs` — a direct unit test of the parser is clearer than a full upload round-trip:
  ```csharp
      [Fact]
      public void ComputeFromEnrichedGpx_PowerMeterFile_PopulatesThePowerSeries()
      {
          // An enriched GPX exactly as GpxWriter.WriteEnriched emits it.
          const string gpx = """
              <?xml version="1.0" encoding="utf-8"?>
              <gpx xmlns="http://www.topografix.com/GPX/1/1" version="1.1" creator="gpx-analyzer"
                   xmlns:gpxa="http://gpx-analyzer.io/extensions/v1"
                   xmlns:gpxtpx="http://www.garmin.com/xmlschemas/TrackPointExtension/v1">
                <trk><name>t</name><trkseg>
                  <trkpt lat="48.0" lon="2.0"><ele>100</ele><time>2024-01-02T10:04:05Z</time>
                    <extensions>
                      <gpxa:TrackPointMetrics><gpxa:speed>2.5</gpxa:speed><gpxa:dist>0</gpxa:dist><gpxa:grade>0</gpxa:grade></gpxa:TrackPointMetrics>
                      <power>250</power>
                    </extensions></trkpt>
                  <trkpt lat="48.001" lon="2.0"><ele>110</ele><time>2024-01-02T10:04:35Z</time>
                    <extensions>
                      <gpxa:TrackPointMetrics><gpxa:speed>2.6</gpxa:speed><gpxa:dist>75</gpxa:dist><gpxa:grade>0.13</gpxa:grade></gpxa:TrackPointMetrics>
                      <power>260</power>
                    </extensions></trkpt>
                </trkseg></trk>
              </gpx>
              """;

          var tmp = Path.Combine(Path.GetTempPath(), $"pwr_{Guid.NewGuid():N}.gpx");
          File.WriteAllText(tmp, gpx);
          try
          {
              var svc = new ProfileComputationService(
                  NullLogger<ProfileComputationService>.Instance);
              var (profileJson, _, splitsJson) = svc.ComputeFromEnrichedGpx(tmp);

              Assert.NotNull(profileJson);
              // JsonIgnoreCondition.WhenWritingNull means an absent key IS the bug.
              Assert.Contains("\"power\"", profileJson);
              Assert.Contains("250", profileJson);
          }
          finally { File.Delete(tmp); }
      }
  ```
  Read `ui/api/Services/ProfileComputationService.cs` for the real constructor signature and the real name/shape of the entry point before writing this — the method is called from `ActivityProcessingService` and returns a `(profileJson, trackGeoJson, splitsJson)` tuple; match it exactly. Add `using Microsoft.Extensions.Logging.Abstractions;`.
- [ ] Run it and watch it fail: `dotnet test ui/api.Tests/GpxAnalyzer.Api.Tests.csproj --filter ComputeFromEnrichedGpx_PowerMeterFile`
  Expected failure: `Assert.Contains() Failure ... Not found: "power"` — `extensions.Element("power")` returns null because the element is `{http://www.topografix.com/GPX/1/1}power`.
- [ ] Add `CultureInfo.InvariantCulture` to the `time` element in `GpxWriter.WriteToFile`
- [ ] Add `WriteSourceExtensions` + `WritePower`, call them from the non-enriched path, convert `WriteEnrichedExtensions` to use `WritePower`, write the GPS-quality children on the trkpt, and always declare the `gpxtpx` prefix
- [ ] Change the `power` lookup in `ProfileComputationService` to `ns + "power"` with the unqualified fallback
- [ ] Run both tests and watch them pass:
  ```bash
  dotnet test cli/tests/GpxAnalyzer.Cli.Tests/ --filter GpxWriterTests
  dotnet test ui/api.Tests/GpxAnalyzer.Api.Tests.csproj --filter ComputeFromEnrichedGpx_PowerMeterFile
  ```
- [ ] Run both full suites (this task spans two components):
  ```bash
  dotnet test cli/tests/GpxAnalyzer.Cli.Tests/
  dotnet test ui/api.Tests/GpxAnalyzer.Api.Tests.csproj
  ```
- [ ] Commit:
  ```bash
  git add cli/src/GpxAnalyzer.Cli.Core/Gpx/GpxWriter.cs ui/api/Services/ProfileComputationService.cs cli/tests/GpxAnalyzer.Cli.Tests/Gpx/GpxWriterTests.cs ui/api.Tests/Profile/ProfileApiTests.cs
  git commit -m "fix(cli,api): preserve biometrics on export and fix the power namespace round trip

  Three defects in the GPX writer plus its one consumer on the API side.

  WriteToFile emitted only lat/lon/ele/time: every heart rate, cadence, power,
  temperature and GPS-quality field was written under the enrich branch only, and
  split and merge use the non-enriched writer with no --enrich flag, so those two
  commands silently dropped all of it.

  <power> was written with the two-argument WriteElementString overload, which
  passes null for the namespace — XmlWriter reads that as 'inherit the in-scope
  default', not 'no namespace' — so the element landed in the GPX default
  namespace while the code claimed it was bare, and ProfileComputationService
  looked it up with an unqualified XName and always got null. Every power-meter
  upload stored power = null on all 500 profile points and avgPower = null on
  every split. Both sides move together: fixing either alone leaves the contract
  broken in one direction.

  The <time> value was formatted with no IFormatProvider, and ':' is the
  time-separator placeholder in a custom format string, so under fi-FI, cs-CZ or
  et-EE the export carried 2024-01-02T10.04.05Z — invalid per xsd:dateTime, and
  a FormatException on re-import.

  Closes #97
  Closes #98
  Closes #100
  Closes #115"
  ```

---

## Wave 3 — CLI command frontend (BLOCKED on the System.CommandLine 2.x migration)

> **Do not start this wave** until `docs/superpowers/plans/2026-08-28-system-commandline-2-migration.md` is complete and merged. All three tasks modify files in `cli/src/GpxAnalyzer.Cli/Commands/`, which that plan rewrites wholesale: `new Option<T>(name, () => default, desc)` becomes `new Option<T>(name) { DefaultValueFactory = … }`, `cmd.SetHandler((InvocationContext ctx) => …)` becomes `cmd.SetAction((ParseResult pr, CancellationToken ct) => …)`, and `ctx.ParseResult.GetValueForOption(opt)` becomes `pr.GetValue(opt)`. Every code snippet below is written against the **post-migration** API; adapt the accessor names to whatever the migration actually landed on before applying them.
>
> Wave 3 also depends on **Wave 2 / Task 12** (the `TimeSplitter` boundary-point clone), which is the root-cause half of #85.

### Task 16: An unknown `--preset` silently disables GPS outlier filtering

**Issues:** #86

**Files:**
- Modify `cli/src/GpxAnalyzer.Cli/Commands/SharedFlags.cs:48`–`:52`, `:86`–`:88`
- Test `cli/tests/GpxAnalyzer.Cli.Tests/Commands/SharedFlagsTests.cs` (new)

**Root cause:** When `preset` is not a key of `StopDetector.Presets`, lines 50–51 print `"using hiking"` and swap in the hiking `StopConfig`, but the local `preset` string is never reassigned to `StopDetector.PresetHiking`. Line 87 then does `SpeedCalculator.PresetMaxSpeed.TryGetValue(preset, ...)` with the still-invalid name, the lookup fails, and `maxReasonable` stays 0. `ComputeConfig.MaxReasonableSpeed = 0` disables three protections at once: `GpsFilter.FilterOutliers` returns early, `SpeedCalculator.ClampSpeeds` returns 0 without clamping, and `SpeedAnomalyDetector.DetectSpeedSpikes` returns immediately. The dictionary lookups are ordinal and case-sensitive, so `--preset Trail` is enough to trigger it. `BenchmarkRunner.BuildComputeConfig` does it correctly (`PresetMaxSpeed.GetValueOrDefault(combo.Preset, SpeedCalculator.DefaultMaxReasonableSpeed)`), confirming the intent.

**Fix approach:** Reassign the name in the fallback branch, and make the second lookup fail safe rather than fail open.

```csharp
// SharedFlags.BuildConfig — before
        if (!StopDetector.Presets.TryGetValue(preset, out var stopCfg))
        {
            Console.Error.WriteLine($"Warning: unknown preset '{preset}', using hiking");
            stopCfg = StopDetector.Presets[StopDetector.PresetHiking];
        }
        ...
        double maxReasonable = maxSpeed;
        if (maxReasonable <= 0 && SpeedCalculator.PresetMaxSpeed.TryGetValue(preset, out var presetMax))
            maxReasonable = presetMax;

// after
        if (!StopDetector.Presets.TryGetValue(preset, out var stopCfg))
        {
            Console.Error.WriteLine($"Warning: unknown preset '{preset}', using hiking");
            stopCfg = StopDetector.Presets[StopDetector.PresetHiking];
            // The name itself has to fall back too: the PresetMaxSpeed lookup below
            // uses it, and leaving the invalid name there silently sets
            // MaxReasonableSpeed = 0, which disables GPS outlier filtering,
            // speed clamping and speed-spike detection all at once.
            preset = StopDetector.PresetHiking;
        }
        ...
        double maxReasonable = maxSpeed;
        if (maxReasonable <= 0)
            maxReasonable = SpeedCalculator.PresetMaxSpeed.GetValueOrDefault(
                preset, SpeedCalculator.DefaultMaxReasonableSpeed);
```

`preset` is a by-value parameter, so reassigning it is local to the method. The `GetValueOrDefault` form matches `BenchmarkRunner` and makes a future preset added to `Presets` but forgotten in `PresetMaxSpeed` fail safe (25 m/s) instead of fail open (0).

**Steps:**

- [ ] Make `SharedFlags` reachable from the test project. It is `internal static`; add
  `<InternalsVisibleTo Include="GpxAnalyzer.Cli.Tests" />` to an `<ItemGroup>` in `cli/src/GpxAnalyzer.Cli/GpxAnalyzer.Cli.csproj`, and confirm the test project already references the exe project (add `<ProjectReference Include="..\..\src\GpxAnalyzer.Cli\GpxAnalyzer.Cli.csproj" />` to `cli/tests/GpxAnalyzer.Cli.Tests/GpxAnalyzer.Cli.Tests.csproj` if not).
- [ ] Create the failing regression test `cli/tests/GpxAnalyzer.Cli.Tests/Commands/SharedFlagsTests.cs`:
  ```csharp
  using GpxAnalyzer.Cli.Commands;
  using GpxAnalyzer.Cli.Core.Stats;

  namespace GpxAnalyzer.Cli.Tests.Commands;

  public class SharedFlagsTests
  {
      private static ComputeConfig Build(string preset) => SharedFlags.BuildConfig(
          preset: preset, stopSpeed: 0, stopDuration: 0, elevThreshold: 2.0,
          smoothing: "medium", demDir: "", demCache: "", demAuto: false,
          demMaxMem: 0, demSkipVal: false, elevAlgo: "threshold",
          trackSmooth: "none", dpEps: 3.0, segMinLen: 200.0, segMaxDev: 2.0,
          maxHr: 0, maxSpeed: 0);

      [Theory]
      [InlineData("Trail")]     // right preset, wrong case — lookups are ordinal
      [InlineData("hikking")]   // typo
      [InlineData("nonsense")]
      public void BuildConfig_UnknownPreset_StillEnablesGpsOutlierFiltering(string preset)
      {
          var cfg = Build(preset);

          // MaxReasonableSpeed = 0 turns off FilterOutliers, ClampSpeeds AND
          // DetectSpeedSpikes — an unknown preset must not do that silently.
          Assert.True(cfg.MaxReasonableSpeed > 0,
              $"preset '{preset}' fell back to hiking for stops but left MaxReasonableSpeed at {cfg.MaxReasonableSpeed}");
          Assert.Equal(
              SpeedCalculator.PresetMaxSpeed[StopDetector.PresetHiking],
              cfg.MaxReasonableSpeed);
      }

      [Fact]
      public void BuildConfig_KnownPreset_UsesItsOwnThreshold()
      {
          Assert.Equal(SpeedCalculator.PresetMaxSpeed[StopDetector.PresetTrail],
              Build(StopDetector.PresetTrail).MaxReasonableSpeed);
          Assert.Equal(SpeedCalculator.PresetMaxSpeed[StopDetector.PresetCycling],
              Build(StopDetector.PresetCycling).MaxReasonableSpeed);
      }
  }
  ```
  `BuildConfig` uses positional parameters today; the named-argument form above will not compile if the migration reordered them — check the signature and adjust.
- [ ] Run it and watch it fail: `dotnet test cli/tests/GpxAnalyzer.Cli.Tests/ --filter SharedFlagsTests`
  Expected failure: `preset 'Trail' fell back to hiking for stops but left MaxReasonableSpeed at 0` (three times, once per `InlineData`). `BuildConfig_KnownPreset_UsesItsOwnThreshold` must pass before and after.
- [ ] Add `preset = StopDetector.PresetHiking;` to the fallback branch and switch the second lookup to `GetValueOrDefault(preset, SpeedCalculator.DefaultMaxReasonableSpeed)`
- [ ] Run the tests and watch all four pass: `dotnet test cli/tests/GpxAnalyzer.Cli.Tests/ --filter SharedFlagsTests`
- [ ] Run the full CLI suite: `dotnet test cli/tests/GpxAnalyzer.Cli.Tests/`
- [ ] Commit:
  ```bash
  git add cli/src/GpxAnalyzer.Cli/Commands/SharedFlags.cs cli/src/GpxAnalyzer.Cli/GpxAnalyzer.Cli.csproj cli/tests/GpxAnalyzer.Cli.Tests/
  git commit -m "fix(cli): fall back the preset NAME, not just the stop config

  On an unknown --preset, BuildConfig printed 'using hiking' and swapped in the
  hiking StopConfig but left the invalid name in the local variable. The
  PresetMaxSpeed lookup further down then missed, MaxReasonableSpeed stayed 0,
  and that one value disables GpsFilter.FilterOutliers, SpeedCalculator.ClampSpeeds
  and SpeedAnomalyDetector.DetectSpeedSpikes at once. Since the lookups are
  ordinal, '--preset Trail' was enough: the run kept every GPS teleport, reported
  an inflated distance and max speed, and emitted no anomaly — while '--preset
  trail' on the same file removed the outlier.

  Reassigns the name in the fallback and uses the fail-safe GetValueOrDefault
  form BenchmarkRunner already uses.

  Closes #86"
  ```

---

### Task 17: `split` corrupts neighbouring segments and misreads a unit-less interval

**Issues:** #85, #108

**Files:**
- Modify `cli/src/GpxAnalyzer.Cli/Commands/SplitCommand.cs:77`–`:95` (write/compute ordering), `:106`–`:118` (`ParseDuration`)
- Test `cli/tests/GpxAnalyzer.Cli.Tests/Commands/SplitCommandTests.cs` (new)

**Root cause:** Both are in `SplitCommand`; one task.

- **#85** `TimeSplitter.ByTime` duplicated the boundary point *by reference*, so segment *i*'s last `TrackPoint` and segment *i+1*'s first were the same mutable object. The loop writes segment *i* (line 85) then calls `ComputePipeline.Compute` (line 88), which mutates `Ele` (DEM correction and `ElevationSmoother`) and `Lat`/`Lon` (`TrackSmoother`) in place; segment *i+1* is serialized on the *next* iteration, so its first trkpt carries the DEM-corrected/smoothed values while every other point in that file is raw, and its statistics are computed from an already-smoothed point. Both mutating steps are on by default (`--smoothing medium`, `--dem-auto-download true`). **Task 12 fixed the shared reference in `TimeSplitter`.** This task adds the belt-and-braces ordering change and the command-level regression test that closes the issue.
- **#108** `ParseDuration` only recognises `h`/`m`/`s` suffixes; anything else falls through to `TimeSpan.TryParse`, whose format treats a bare integer as a whole number of **days**. `--interval 24` is accepted as 24 days, the `splitInterval <= TimeSpan.Zero` guard never fires, and a 7-day track produces one segment while the tool reports `"Split into 1 segments (interval: 24)"`.

**Fix approach:** Compute on a cloned point list so no segment's analysis can touch another segment's data, and reject a unit-less interval instead of reinterpreting it.

```csharp
// SplitCommand — segment loop
                    try
                    {
                        GpxWriter.Write(outPath, seg.Points, $"{prefix}-{i + 1:D3}");
                        Console.Error.WriteLine($"  {filename} ({seg.Points.Count} points)");

                        // Compute on a copy: ComputePipeline mutates Ele and Lat/Lon
                        // in place (DEM correction, elevation and track smoothing),
                        // and boundary points are shared between adjacent segments.
                        var forAnalysis = seg.Points.Select(p => p.Clone()).ToList();
                        var (summary, _) = ComputePipeline.Compute(forAnalysis, 1, cfg);
                        formatter.Format(Console.Out, filename, summary, cfg.StopConfig);
                    }
```

```csharp
    private static TimeSpan ParseDuration(string s)
    {
        s = s.Trim().ToLowerInvariant();
        if (s.EndsWith("h") && double.TryParse(s[..^1], NumberStyles.Float, CultureInfo.InvariantCulture, out var hours))
            return TimeSpan.FromHours(hours);
        if (s.EndsWith("m") && double.TryParse(s[..^1], NumberStyles.Float, CultureInfo.InvariantCulture, out var minutes))
            return TimeSpan.FromMinutes(minutes);
        if (s.EndsWith("s") && double.TryParse(s[..^1], NumberStyles.Float, CultureInfo.InvariantCulture, out var seconds))
            return TimeSpan.FromSeconds(seconds);

        // Deliberately NOT falling through to TimeSpan.TryParse: its format reads a
        // bare integer as a whole number of DAYS, so '--interval 24' was silently
        // accepted as 24 days instead of the 24 hours the user meant.
        return TimeSpan.Zero;
    }
```

`TimeSpan.Zero` makes the existing guard at line 56 fire with `Error: invalid interval '24'`. Widen that message so the user knows what to type:

```csharp
            if (splitInterval <= TimeSpan.Zero)
            {
                Console.Error.WriteLine(
                    $"Error: invalid interval '{interval}' — use a unit suffix, e.g. 24h, 90m or 30s");
                return 1;   // see Task 18: the command now returns an exit code
            }
```

The explicit `NumberStyles.Float` + `CultureInfo.InvariantCulture` on the three `double.TryParse` calls removes the file's reliance on the exe-level `InvariantGlobalization=true`, which is a build setting the Core library does not share.

**Steps:**

- [ ] Create the failing regression test `cli/tests/GpxAnalyzer.Cli.Tests/Commands/SplitCommandTests.cs`. `ParseDuration` is `private static`; expose it by changing it to `internal static` (the `InternalsVisibleTo` from Task 16 covers it) rather than reflecting on it:
  ```csharp
  using GpxAnalyzer.Cli.Commands;

  namespace GpxAnalyzer.Cli.Tests.Commands;

  public class SplitCommandTests
  {
      [Theory]
      [InlineData("24h", 24 * 60)]
      [InlineData("90m", 90)]
      [InlineData("30s", 0.5)]
      [InlineData("1.5h", 90)]
      public void ParseDuration_WithUnitSuffix_ParsesAsExpected(string input, double expectedMinutes)
      {
          Assert.Equal(expectedMinutes, SplitCommand.ParseDuration(input).TotalMinutes, 6);
      }

      [Theory]
      [InlineData("24")]      // user meant 24 hours; TimeSpan.TryParse reads 24 DAYS
      [InlineData("1")]
      [InlineData("")]
      [InlineData("banana")]
      public void ParseDuration_WithoutAUnit_IsRejected(string input)
      {
          Assert.Equal(TimeSpan.Zero, SplitCommand.ParseDuration(input));
      }
  }
  ```
- [ ] Add the end-to-end segment-isolation test to the same file:
  ```csharp
      [Fact]
      public void Split_MultiDayTrack_DoesNotLeakSmoothedValuesIntoTheNextSegmentsFile()
      {
          var tmp = Directory.CreateTempSubdirectory();
          try
          {
              // A 3-day track with a pronounced elevation profile, so smoothing
              // demonstrably changes the values it touches.
              var t0 = DateTime.Parse("2024-01-01T08:00:00Z").ToUniversalTime();
              var points = new List<TrackPoint>();
              for (int day = 0; day < 3; day++)
                  for (int i = 0; i < 40; i++)
                      points.Add(new TrackPoint
                      {
                          Lat = 45.0 + i * 0.001, Lon = 6.0, Ele = 1000 + (i % 7) * 60,
                          Time = t0.AddDays(day).AddMinutes(i * 15),
                      });

              var srcPath = Path.Combine(tmp.FullName, "multiday.gpx");
              GpxWriter.Write(srcPath, points, "multiday");

              var segments = TimeSplitter.ByTime(
                  GpxParser.ParseFile(srcPath).AllPoints(), TimeSpan.FromHours(24));
              Assert.True(segments.Count >= 2);

              // Reproduce SplitCommand's loop, including the compute-on-a-copy fix.
              var cfg = SharedFlags.BuildConfig(
                  preset: "hiking", stopSpeed: 0, stopDuration: 0, elevThreshold: 2.0,
                  smoothing: "medium", demDir: "", demCache: "", demAuto: false,
                  demMaxMem: 0, demSkipVal: false, elevAlgo: "threshold",
                  trackSmooth: "medium", dpEps: 3.0, segMinLen: 200.0, segMaxDev: 2.0,
                  maxHr: 0, maxSpeed: 0);

              var outDir = Path.Combine(tmp.FullName, "splits");
              Directory.CreateDirectory(outDir);
              var written = new List<string>();
              for (int i = 0; i < segments.Count; i++)
              {
                  var outPath = Path.Combine(outDir, $"segment-{i + 1:D3}.gpx");
                  GpxWriter.Write(outPath, segments[i].Points, $"segment-{i + 1:D3}");
                  written.Add(outPath);
                  var forAnalysis = segments[i].Points.Select(p => p.Clone()).ToList();
                  ComputePipeline.Compute(forAnalysis, 1, cfg);
              }

              // Segment i's tail and segment i+1's head are the SAME source point.
              // Both files must record its original, unsmoothed elevation.
              var seg1 = GpxParser.ParseFile(written[0]).AllPoints();
              var seg2 = GpxParser.ParseFile(written[1]).AllPoints();
              Assert.Equal(seg1[^1].Ele, seg2[0].Ele, 6);
              Assert.Equal(seg1[^1].Lat, seg2[0].Lat, 9);
          }
          finally { tmp.Delete(recursive: true); }
      }
  ```
  Add the required `using` directives (`GpxAnalyzer.Cli.Core.Gpx`, `.Split`, `.Stats`).
- [ ] Run them and watch them fail: `dotnet test cli/tests/GpxAnalyzer.Cli.Tests/ --filter SplitCommandTests`
  Expected failures: `ParseDuration_WithoutAUnit_IsRejected("24")` fails with `Expected: 00:00:00  Actual: 24.00:00:00` (#108). The segment-isolation test should **pass** once Task 12 is in — if it fails with `Assert.Equal() Failure  Expected: 1060  Actual: 1047.3` on the elevation, Task 12 was not applied; go back and apply it.
- [ ] Change `ParseDuration` to `internal static`, drop the `TimeSpan.TryParse` fallback, and pin the three `double.TryParse` calls to `InvariantCulture`
- [ ] Widen the invalid-interval error message
- [ ] Apply the compute-on-a-copy change in the segment loop
- [ ] Run the tests and watch all pass: `dotnet test cli/tests/GpxAnalyzer.Cli.Tests/ --filter SplitCommandTests`
- [ ] Run the full CLI suite: `dotnet test cli/tests/GpxAnalyzer.Cli.Tests/`
- [ ] Manually verify the ordering end-to-end: `dotnet run --project cli/src/GpxAnalyzer.Cli -- split cli/tests/GpxAnalyzer.Cli.Tests/testdata/two-segments.gpx --interval 1h --output-dir /tmp/splitcheck --dem-auto-download false` and confirm no `Error processing segment` lines
- [ ] Commit:
  ```bash
  git add cli/src/GpxAnalyzer.Cli/Commands/SplitCommand.cs cli/tests/GpxAnalyzer.Cli.Tests/Commands/SplitCommandTests.cs
  git commit -m "fix(cli): analyze split segments on a copy and reject unit-less intervals

  TimeSplitter shares the boundary point between adjacent segments (now cloned,
  but the command should not depend on that): the loop wrote segment i, then ran
  ComputePipeline on it, which mutates Ele and Lat/Lon in place via DEM correction
  and smoothing — both on by default — and segment i+1 was serialized on the next
  iteration, so its first trkpt carried smoothed values that exist nowhere in the
  source file. Computing on a cloned list removes the coupling entirely.

  ParseDuration also fell through to TimeSpan.TryParse, whose format reads a bare
  integer as a whole number of DAYS, so '--interval 24' silently meant 24 days:
  a 7-day track produced one segment identical to the input, reported as success.
  A unit-less value is now rejected with a message naming the accepted suffixes.

  Closes #85
  Closes #108"
  ```

---

### Task 18: `analyze` reports success after a failure and overwrites duplicate basenames

**Issues:** #107, #88

**Files:**
- Modify `cli/src/GpxAnalyzer.Cli/Commands/AnalyzeCommand.cs:61`–`:72` (exit code), `:77`–`:98` (export naming)
- Test `cli/tests/GpxAnalyzer.Cli.Tests/Commands/AnalyzeCommandTests.cs` (new)

**Root cause:** Both are in `AnalyzeCommand`; one task.

- **#107** The per-file `try`/`catch` writes the message to stderr and continues; the handler never records a failure and never sets an exit code. If every input fails to parse the command produces no stdout at all and still returns 0. Since `analyze --format json` is the documented feed for the AI analyzer, `gpx-analyzer analyze corrupt.gpx --format json > stats.json && gpx-ai-analyzer analyze --input stats.json` proceeds through the `&&` and runs the AI analyzer on an empty file.
- **#88** `AnalyzeFile` derives the export path solely from `Path.GetFileNameWithoutExtension(path) + "_processed.gpx"` joined to the flat export directory, discarding the source directory structure. `FileResolver.FindGpxInDir` enumerates with `SearchOption.AllDirectories` and `ResolveFiles` de-duplicates on the *absolute* path, so `tracks/2023/morning-run.gpx` and `tracks/2024/morning-run.gpx` both resolve and both map to `out/morning-run_processed.gpx`. The second write clobbers the first while line 96 reports both as successful.

**Fix approach:** Track failures and set the exit code; track claimed output paths and disambiguate collisions with the source's parent directory.

```csharp
        cmd.SetAction((ParseResult pr) =>
        {
            var files = pr.GetValue(filesArg) ?? [];
            var format = pr.GetValue(formatOption) ?? "text";
            var export = pr.GetValue(exportOpt) ?? "";
            var enrich = pr.GetValue(enrichOpt);

            var formatter = FormatterFactory.Create(format, GpxAnalyzer.Cli.Output.JsonContext.Default.Options);
            var resolvedFiles = FileResolver.ResolveFiles(files);
            var cfg = SharedFlags.BuildConfig(/* … */);

            // Output paths claimed so far, so two inputs with the same basename in
            // different directories cannot silently overwrite each other.
            var claimed = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            int failures = 0;

            foreach (var path in resolvedFiles)
            {
                try
                {
                    AnalyzeFile(path, formatter, cfg, export, enrich, claimed);
                }
                catch (Exception ex)
                {
                    Console.Error.WriteLine($"Error analyzing {path}: {ex.Message}");
                    failures++;
                }
            }

            // A pipeline cannot distinguish "parsed nothing" from "analyzed
            // everything" when the exit code is 0 either way.
            return failures > 0 ? 1 : 0;
        });
```

```csharp
    private static void AnalyzeFile(string path, IFormatter formatter, ComputeConfig cfg,
        string exportDir, bool enrich, HashSet<string> claimedOutputs)
    {
        var doc = GpxParser.ParseFile(path);
        var points = doc.AllPoints();
        var (summary, processed) = ComputePipeline.Compute(points, doc.SegmentCount(), cfg);
        formatter.Format(Console.Out, path, summary, cfg.StopConfig);

        if (!string.IsNullOrEmpty(exportDir))
        {
            string baseName = Path.GetFileNameWithoutExtension(path);
            string outPath = ClaimOutputPath(exportDir, baseName, path, claimedOutputs);
            Directory.CreateDirectory(exportDir);

            if (enrich)
                GpxWriter.WriteEnriched(outPath, processed, baseName);
            else
                GpxWriter.Write(outPath, processed, baseName);

            Console.Error.WriteLine($"Exported: {outPath} ({processed.Count} points)");
        }
    }

    /// <summary>
    /// Reserves a unique export path. Recursive input resolution can yield several
    /// files with the same basename in different directories; without this they all
    /// map to one output and the last write silently wins.
    /// </summary>
    private static string ClaimOutputPath(string exportDir, string baseName,
        string sourcePath, HashSet<string> claimed)
    {
        string candidate = Path.Combine(exportDir, baseName + "_processed.gpx");
        if (claimed.Add(candidate)) return candidate;

        // First disambiguator: the source's parent directory (tracks/2023 -> 2023).
        var parent = Path.GetFileName(Path.GetDirectoryName(sourcePath) ?? "");
        if (!string.IsNullOrEmpty(parent))
        {
            candidate = Path.Combine(exportDir, $"{parent}_{baseName}_processed.gpx");
            if (claimed.Add(candidate)) return candidate;
        }

        for (int n = 2; ; n++)
        {
            candidate = Path.Combine(exportDir, $"{baseName}_processed_{n}.gpx");
            if (claimed.Add(candidate)) return candidate;
        }
    }
```

`ClaimOutputPath` is `internal static` so the test can call it directly.

**Steps:**

- [ ] Create the failing regression test `cli/tests/GpxAnalyzer.Cli.Tests/Commands/AnalyzeCommandTests.cs`:
  ```csharp
  using GpxAnalyzer.Cli.Commands;

  namespace GpxAnalyzer.Cli.Tests.Commands;

  public class AnalyzeCommandTests
  {
      [Fact]
      public void ClaimOutputPath_DuplicateBasenamesInDifferentDirs_ProducesDistinctPaths()
      {
          var claimed = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

          var a = AnalyzeCommand.ClaimOutputPath(
              "out", "morning-run", Path.Combine("tracks", "2023", "morning-run.gpx"), claimed);
          var b = AnalyzeCommand.ClaimOutputPath(
              "out", "morning-run", Path.Combine("tracks", "2024", "morning-run.gpx"), claimed);

          Assert.NotEqual(a, b);
          Assert.Contains("2024", b);
      }

      [Fact]
      public void ClaimOutputPath_ThreeWayCollision_StillProducesDistinctPaths()
      {
          var claimed = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
          var paths = new[]
          {
              AnalyzeCommand.ClaimOutputPath("out", "run", Path.Combine("a", "run.gpx"), claimed),
              AnalyzeCommand.ClaimOutputPath("out", "run", Path.Combine("b", "run.gpx"), claimed),
              AnalyzeCommand.ClaimOutputPath("out", "run", Path.Combine("a", "run.gpx"), claimed),
          };
          Assert.Equal(3, paths.Distinct(StringComparer.OrdinalIgnoreCase).Count());
      }

      [Fact]
      public async Task Analyze_CorruptFile_ExitsNonZero()
      {
          var tmp = Directory.CreateTempSubdirectory();
          try
          {
              var bad = Path.Combine(tmp.FullName, "corrupt.gpx");
              File.WriteAllText(bad, "<gpx><trk><trkseg><trkpt lat=");  // malformed XML

              var root = Program.BuildRootCommand();   // see note below
              var exitCode = await root.Parse(["analyze", bad, "--format", "json"]).InvokeAsync();

              Assert.NotEqual(0, exitCode);
          }
          finally { tmp.Delete(recursive: true); }
      }
  }
  ```
  The exit-code test needs a seam into the command tree. If the migration did not already extract one, add `internal static RootCommand BuildRootCommand()` to `cli/src/GpxAnalyzer.Cli/Program.cs` and have `Main` call it — a pure extraction with no behavior change. Match `Parse(...).InvokeAsync()` to whatever the migrated `Program.cs` uses.
- [ ] Run them and watch them fail: `dotnet test cli/tests/GpxAnalyzer.Cli.Tests/ --filter AnalyzeCommandTests`
  Expected failures: the two `ClaimOutputPath` tests fail to compile (the method does not exist) — add an `internal static` stub returning `Path.Combine(exportDir, baseName + "_processed.gpx")` so they fail as `Assert.NotEqual() Failure  Values are equal` instead; `Analyze_CorruptFile_ExitsNonZero` fails with `Assert.NotEqual() Failure  Expected: Not 0  Actual: 0` (#107).
- [ ] Implement `ClaimOutputPath` and thread the `claimed` set through `AnalyzeFile`
- [ ] Add the `failures` counter and return the exit code from the handler
- [ ] Run the tests and watch all three pass: `dotnet test cli/tests/GpxAnalyzer.Cli.Tests/ --filter AnalyzeCommandTests`
- [ ] Run the full CLI suite: `dotnet test cli/tests/GpxAnalyzer.Cli.Tests/`
- [ ] Verify the pipeline scenario by hand:
  ```bash
  dotnet run --project cli/src/GpxAnalyzer.Cli -- analyze /tmp/corrupt.gpx --format json > /tmp/stats.json; echo "exit=$?"
  ```
  expect `exit=1`
- [ ] Commit:
  ```bash
  git add cli/src/GpxAnalyzer.Cli/Commands/AnalyzeCommand.cs cli/src/GpxAnalyzer.Cli/Program.cs cli/tests/GpxAnalyzer.Cli.Tests/Commands/AnalyzeCommandTests.cs
  git commit -m "fix(cli): report analyze failures in the exit code and stop clobbering exports

  The per-file catch wrote to stderr and continued without recording anything, so
  a run where every input failed to parse produced no stdout and still exited 0.
  'analyze corrupt.gpx --format json > stats.json && gpx-ai-analyzer …' therefore
  proceeded through the && and fed the AI analyzer an empty file.

  --export also derived the output name from the basename alone, discarding the
  source directory. FileResolver enumerates recursively and de-duplicates on the
  absolute path, so tracks/2023/morning-run.gpx and tracks/2024/morning-run.gpx
  both mapped to out/morning-run_processed.gpx: the second write silently won
  while both were reported as exported.

  Counts failures into the exit code and reserves a unique output path per input,
  disambiguating with the source's parent directory.

  Closes #88
  Closes #107"
  ```

---

## Wave 4 — ai-analyzer robustness

Not blocked by anything. `ai-analyzer/src/GpxAiAnalyzer/GpxAiAnalyzer.csproj` already references `System.CommandLine` `2.*` and already uses `SetAction`, so Task 22 is independent of the CLI migration.

### Task 19: `TrackReport` deserialization crashes or silently corrupts numbers

**Issues:** #89, #90, #109

**Files:**
- Modify `ai-analyzer/src/GpxAiAnalyzer.Core/Models/TrackReport.cs:12`–`:26` (null defaults), `:73`–`:97` (`LenientDoubleConverter`), `:102`–`:126` (`LenientIntConverter`)
- Test `ai-analyzer/tests/GpxAiAnalyzer.Tests/Models/TrackReportTests.cs` (new)

**Root cause:** Three defects in one file, all on the LLM-response deserialization path.

- **#89** `Difficulty`, `KeySegments`, `Recommendations` and `Effort` are non-nullable with default initializers, but System.Text.Json does **not** preserve a property's default when the JSON carries an explicit `null` — it deserializes null and assigns it. `TrackAnalyzer.JsonOptions` sets no `RespectNullableAnnotations`, and there is no `[JsonRequired]` or null-coalescing setter. Every consumer then dereferences unguarded: `ReportFormatter.FormatText` hits `report.Difficulty.Grade` (line 46), `report.KeySegments.Count` (51), `report.Effort.FitnessLevel` (67) and `report.Recommendations.Count` (74). A model returning `"key_segments": null` for a short flat walk crashes the CLI *after* the paid call, and makes the API mark the activity failed with an opaque NRE.
- **#90** `LenientDoubleConverter.Read` does `reader.GetString()?.Trim().Replace(",", "")` before parsing with `InvariantCulture`. The comma-strip is meant to remove English thousands separators but also destroys comma decimal separators. The French path is first-class — `PromptBuilder.cs:119` instructs the model to "Respond entirely in French (français)" and `AiAnalysisService` propagates `Activity.Language` — and a model writing French output emits French number formatting. `"3,2"` becomes `"32"`, which parses cleanly to 32.0 and renders as a 32 km climb for a 3.2 km one. No exception, no log. `LenientIntConverter` has the identical defect.
- **#109** `LenientIntConverter.Read` calls `reader.GetInt32()` as soon as the token is a Number. `Utf8JsonReader.GetInt32()` throws `FormatException` when the number is not representable as an `Int32` — a decimal point is enough. The converter's whole purpose is to tolerate what the LLM produces (its String branch returns null rather than throwing on garbage), yet its Number branch is stricter than the default serializer. `"calorie_estimate": 1200.0` — a very common LLM formatting — discards an otherwise perfect report.

**Fix approach:** Make the collection/object properties null-tolerant at the setter, and make both converters actually lenient.

```csharp
// TrackReport.cs — null-tolerant properties (#89)
public sealed class TrackReport
{
    private readonly DifficultyRating _difficulty = new();
    private readonly List<KeySegment> _keySegments = [];
    private readonly List<string> _recommendations = [];
    private readonly EffortEstimate _effort = new();

    // System.Text.Json assigns an explicit JSON null over the initializer, so the
    // default has to be re-applied in the setter. Every consumer (ReportFormatter)
    // dereferences these unguarded.
    [JsonPropertyName("difficulty")]
    public DifficultyRating Difficulty
    {
        get => _difficulty;
        init => _difficulty = value ?? new();
    }

    [JsonPropertyName("key_segments")]
    public List<KeySegment> KeySegments
    {
        get => _keySegments;
        init => _keySegments = value ?? [];
    }

    [JsonPropertyName("recommendations")]
    public List<string> Recommendations
    {
        get => _recommendations;
        init => _recommendations = value ?? [];
    }

    [JsonPropertyName("summary")]
    public string Summary { get; init; } = "";

    [JsonPropertyName("effort")]
    public EffortEstimate Effort
    {
        get => _effort;
        init => _effort = value ?? new();
    }
}
```

Apply the same `?? ""` guard to `Summary`, `DifficultyRating.Grade`/`Justification`, `KeySegment.Type`/`Description` and `EffortEstimate.FitnessLevel`/`EstimatedDuration` — a `null` string in any of them reaches `ReportFormatter` the same way.

```csharp
// Shared numeric-string normalisation (#90)
internal static class LenientNumber
{
    /// <summary>
    /// Normalises an LLM-written numeric string. A comma is a thousands separator
    /// in English output and a DECIMAL separator in French output, and this project
    /// asks the model to answer in French — so stripping commas unconditionally
    /// turned "3,2" into "32". Disambiguate by shape instead.
    /// </summary>
    public static string? Normalize(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return null;

        var s = raw.Trim();

        // Strip anything that is not a digit, sign, dot or comma (units like
        // " km", " kcal", thin spaces used as French group separators).
        s = new string(s.Where(c => char.IsDigit(c) || c is '-' or '+' or '.' or ',').ToArray());
        if (s.Length == 0) return null;

        bool hasDot = s.Contains('.');
        bool hasComma = s.Contains(',');

        if (hasDot && hasComma)
        {
            // Both present: the LAST one is the decimal separator.
            char dec = s.LastIndexOf('.') > s.LastIndexOf(',') ? '.' : ',';
            char grp = dec == '.' ? ',' : '.';
            s = s.Replace(grp.ToString(), string.Empty).Replace(dec, '.');
        }
        else if (hasComma)
        {
            // A single comma with 1-2 trailing digits is a French decimal ("3,2",
            // "12,75"). Exactly three trailing digits is an English group ("1,200").
            int idx = s.LastIndexOf(',');
            int trailing = s.Length - idx - 1;
            s = s.Count(c => c == ',') == 1 && trailing is 1 or 2
                ? s.Replace(',', '.')
                : s.Replace(",", string.Empty);
        }

        return s;
    }
}

public sealed class LenientDoubleConverter : JsonConverter<double?>
{
    public override double? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.Number)
            return reader.GetDouble();
        if (reader.TokenType == JsonTokenType.String)
        {
            var s = LenientNumber.Normalize(reader.GetString());
            if (s is not null &&
                double.TryParse(s, NumberStyles.Any, CultureInfo.InvariantCulture, out var v))
                return v;
            return null;
        }
        if (reader.TokenType == JsonTokenType.Null)
            return null;
        reader.Skip();
        return null;
    }
    // Write unchanged
}

public sealed class LenientIntConverter : JsonConverter<int?>
{
    public override int? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.Number)
        {
            // GetInt32() throws FormatException on any non-Int32-representable
            // number — a decimal point is enough — which defeats the converter's
            // own leniency contract. "calorie_estimate": 1200.0 is common.
            if (reader.TryGetInt32(out var i)) return i;
            if (reader.TryGetDouble(out var d) && d >= int.MinValue && d <= int.MaxValue)
                return (int)Math.Round(d);
            return null;
        }
        if (reader.TokenType == JsonTokenType.String)
        {
            var s = LenientNumber.Normalize(reader.GetString());
            if (s is null) return null;
            if (int.TryParse(s, NumberStyles.Any, CultureInfo.InvariantCulture, out var v))
                return v;
            // "1200.0" as a string is the same case as the Number branch above.
            if (double.TryParse(s, NumberStyles.Any, CultureInfo.InvariantCulture, out var dv)
                && dv >= int.MinValue && dv <= int.MaxValue)
                return (int)Math.Round(dv);
            return null;
        }
        if (reader.TokenType == JsonTokenType.Null)
            return null;
        reader.Skip();
        return null;
    }
    // Write unchanged
}
```

`DifficultyRating.Score` is a plain non-nullable `int` with no converter, so `"score": 3.0` still throws. Give it the same protection with a non-nullable wrapper:

```csharp
    [JsonPropertyName("score")]
    [JsonConverter(typeof(LenientIntNonNullConverter))]
    public int Score { get; init; }
```

```csharp
/// <summary>Non-nullable companion to LenientIntConverter; unparseable values become 0.</summary>
public sealed class LenientIntNonNullConverter : JsonConverter<int>
{
    private static readonly LenientIntConverter Inner = new();

    public override int Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        => Inner.Read(ref reader, typeof(int?), options) ?? 0;

    public override void Write(Utf8JsonWriter writer, int value, JsonSerializerOptions options)
        => writer.WriteNumberValue(value);
}
```

**Steps:**

- [ ] Create the failing regression test `ai-analyzer/tests/GpxAiAnalyzer.Tests/Models/TrackReportTests.cs`:
  ```csharp
  using System.Text.Json;
  using System.Text.Json.Serialization;
  using GpxAiAnalyzer.Core.Models;

  namespace GpxAiAnalyzer.Tests.Models;

  public class TrackReportTests
  {
      // Must mirror TrackAnalyzer.JsonOptions exactly.
      private static readonly JsonSerializerOptions Opts = new()
      {
          PropertyNameCaseInsensitive = true,
          NumberHandling = JsonNumberHandling.AllowReadingFromString,
      };

      // ── #89: explicit nulls must not overwrite the defaults ──────────────
      [Fact]
      public void Deserialize_ExplicitNullCollections_LeavesUsableDefaults()
      {
          const string json = """
              {
                "difficulty": {"grade":"Easy","score":1,"justification":"Flat."},
                "key_segments": null,
                "recommendations": null,
                "summary": "Short flat walk.",
                "effort": null
              }
              """;

          var report = JsonSerializer.Deserialize<TrackReport>(json, Opts)!;

          // ReportFormatter.FormatText dereferences all of these unguarded.
          Assert.NotNull(report.KeySegments);
          Assert.Empty(report.KeySegments);
          Assert.NotNull(report.Recommendations);
          Assert.Empty(report.Recommendations);
          Assert.NotNull(report.Effort);
          Assert.NotNull(report.Difficulty);
      }

      [Fact]
      public void Deserialize_ExplicitNullDifficulty_LeavesUsableDefault()
      {
          const string json = """{"difficulty": null, "summary": "x"}""";
          var report = JsonSerializer.Deserialize<TrackReport>(json, Opts)!;
          Assert.NotNull(report.Difficulty);
          Assert.NotNull(report.Difficulty.Grade);
      }

      // ── #90: French decimals must not be multiplied by 10 ────────────────
      [Theory]
      [InlineData("\"3,2\"", 3.2)]      // French decimal
      [InlineData("\"12,75\"", 12.75)]  // French decimal
      [InlineData("\"1,200\"", 1200)]   // English thousands group
      [InlineData("\"1,200.5\"", 1200.5)]
      [InlineData("\"3.2\"", 3.2)]
      [InlineData("\"3.2 km\"", 3.2)]
      public void LenientDouble_NumericStrings_ParseToTheIntendedValue(string jsonValue, double expected)
      {
          var json = $$"""{"key_segments":[{"type":"climb","description":"d","distance_km":{{jsonValue}}}]}""";
          var report = JsonSerializer.Deserialize<TrackReport>(json, Opts)!;
          Assert.Equal(expected, report.KeySegments[0].DistanceKm!.Value, 6);
      }

      // ── #109: a fractional number must not throw ─────────────────────────
      [Fact]
      public void LenientInt_FractionalNumber_DoesNotThrow()
      {
          const string json = """
              {
                "summary": "x",
                "effort": {"fitness_level":"intermediate","estimated_duration":"6h","calorie_estimate": 1200.0}
              }
              """;

          var report = JsonSerializer.Deserialize<TrackReport>(json, Opts)!;
          Assert.Equal(1200, report.Effort.CalorieEstimate);
      }

      [Fact]
      public void DifficultyScore_FractionalNumber_DoesNotThrow()
      {
          const string json = """{"difficulty":{"grade":"Moderate","score":3.0,"justification":"j"},"summary":"x"}""";
          var report = JsonSerializer.Deserialize<TrackReport>(json, Opts)!;
          Assert.Equal(3, report.Difficulty.Score);
      }
  }
  ```
- [ ] Run them and watch them fail: `dotnet test ai-analyzer/tests/GpxAiAnalyzer.Tests/ --filter TrackReportTests`
  Expected failures: `Assert.NotNull() Failure: Value is null` on `report.KeySegments` (#89); `Assert.Equal() Failure  Expected: 3.2  Actual: 32` for `"3,2"` and `Expected: 12.75  Actual: 1275` for `"12,75"` (#90); `System.FormatException : Either the JSON value is not in a supported format, or is out of range for an Int32` on both fractional-number tests (#109).
- [ ] Add the null-tolerant backing fields and `init` accessors to `TrackReport` and the `?? ""` guards on the string properties
- [ ] Add `LenientNumber.Normalize` and rewrite both converters' `Read` methods
- [ ] Add `LenientIntNonNullConverter` and apply it to `DifficultyRating.Score`
- [ ] Run the tests and watch them all pass: `dotnet test ai-analyzer/tests/GpxAiAnalyzer.Tests/ --filter TrackReportTests`
- [ ] Run the full ai-analyzer suite: `dotnet test ai-analyzer/tests/GpxAiAnalyzer.Tests/`
- [ ] Commit:
  ```bash
  git add ai-analyzer/src/GpxAiAnalyzer.Core/Models/TrackReport.cs ai-analyzer/tests/GpxAiAnalyzer.Tests/Models/TrackReportTests.cs
  git commit -m "fix(ai): make TrackReport deserialization null-safe and actually lenient

  System.Text.Json assigns an explicit JSON null over a property's initializer, so
  a model returning \"key_segments\": null on a short flat walk produced a null
  list that ReportFormatter.FormatText dereferenced — a NullReferenceException
  after the paid model call, surfacing in the API as an opaque failed activity.

  LenientDoubleConverter stripped every comma before parsing, which removes an
  English thousands separator but destroys a FRENCH decimal separator — and this
  project explicitly asks the model to answer in French. \"3,2\" became \"32\",
  parsed cleanly, and rendered a 3.2 km climb as 32.0 km with no error anywhere.

  LenientIntConverter called GetInt32() on any Number token, which throws on a
  decimal point, so \"calorie_estimate\": 1200.0 — a very common LLM formatting —
  discarded an otherwise perfect report. DifficultyRating.Score had no converter
  at all and the same exposure.

  Closes #89
  Closes #90
  Closes #109"
  ```

---

### Task 20: `TrackAnalyzer` breaks on a clean response and hides an empty one

**Issues:** #110, #112

**Files:**
- Modify `ai-analyzer/src/GpxAiAnalyzer.Core/Analysis/TrackAnalyzer.cs:37`–`:50` (`ExtractJson`), `:83`–`:84` (empty-response guard)
- Test `ai-analyzer/tests/GpxAiAnalyzer.Tests/Analysis/TrackAnalyzerTests.cs` (new)

**Root cause:** Both are in `TrackAnalyzer`; one task.

- **#110** `ExtractJson` short-circuits on `trimmed.StartsWith('{')` and returns the whole string without locating the closing brace. System.Text.Json rejects any non-whitespace content after the top-level value, so a response that begins with the JSON object and appends a closing remark throws `JsonException`. The irony is that the fallback path (`IndexOf('{')` .. `LastIndexOf('}')`) handles this correctly — the guard clause is what breaks it. A *cleaner* model response (JSON first) fails where a messier one (prose preamble, then JSON) succeeds.
- **#112** `response.Text ?? throw new InvalidOperationException("AI returned an empty response.")` never fires: `Microsoft.Extensions.AI`'s `ChatResponse.Text` is a non-nullable string that concatenates the text of all response messages and yields `""` when there is none. The empty case is real — with `UseFunctionInvocation()` a model can end the exchange having emitted only tool calls, or hit the function-invocation iteration limit — and the user gets `JsonException: The input does not contain any JSON tokens` pointing at a parsing problem when the model simply returned nothing.

**Fix approach:** Always locate the JSON object's bounds, and guard on emptiness rather than on null.

```csharp
    /// <summary>
    /// Extracts the JSON object from a model response. Never trusts the response to
    /// contain nothing but JSON: a response that STARTS with '{' can still append a
    /// closing remark, and System.Text.Json rejects trailing content after the
    /// top-level value.
    /// </summary>
    internal static string ExtractJson(string text)
    {
        var trimmed = text.Trim();

        var startIdx = trimmed.IndexOf('{');
        var endIdx = trimmed.LastIndexOf('}');
        if (startIdx >= 0 && endIdx > startIdx)
            return trimmed[startIdx..(endIdx + 1)];

        return trimmed;
    }
```

```csharp
        var response = await client.GetResponseAsync(messages, chatOptions, ct);

        // ChatResponse.Text is non-nullable and returns "" when the response
        // carried no text — a model that ends on tool calls only, or that hits the
        // function-invocation iteration limit. The old "?? throw" was dead code and
        // the user got an opaque JsonException instead.
        var text = response.Text;
        if (string.IsNullOrWhiteSpace(text))
            throw new InvalidOperationException(
                "AI returned an empty response (no assistant text — the model may have " +
                "ended on tool calls or hit the function-invocation limit).");

        var json = ExtractJson(text);
```

`ExtractJson` becomes `internal static` so the test can call it; add `<InternalsVisibleTo Include="GpxAiAnalyzer.Tests" />` to `ai-analyzer/src/GpxAiAnalyzer.Core/GpxAiAnalyzer.Core.csproj` if it is not already there.

**Steps:**

- [ ] Create the failing regression test `ai-analyzer/tests/GpxAiAnalyzer.Tests/Analysis/TrackAnalyzerTests.cs`:
  ```csharp
  using System.Text.Json;
  using System.Text.Json.Serialization;
  using GpxAiAnalyzer.Core.Analysis;
  using GpxAiAnalyzer.Core.Models;

  namespace GpxAiAnalyzer.Tests.Analysis;

  public class TrackAnalyzerTests
  {
      private static readonly JsonSerializerOptions Opts = new()
      {
          PropertyNameCaseInsensitive = true,
          NumberHandling = JsonNumberHandling.AllowReadingFromString,
      };

      [Fact]
      public void ExtractJson_JsonFollowedByProse_ReturnsOnlyTheJson()
      {
          const string response =
              """{"difficulty":{"grade":"Easy","score":1,"justification":"j"},"summary":"s"}""" +
              "\n\nLet me know if you'd like a deeper breakdown of the climbs.";

          var json = TrackAnalyzer.ExtractJson(response);

          // Must be deserializable: System.Text.Json rejects trailing content.
          var report = JsonSerializer.Deserialize<TrackReport>(json, Opts);
          Assert.NotNull(report);
          Assert.Equal("Easy", report!.Difficulty.Grade);
      }

      [Fact]
      public void ExtractJson_ProsePreambleThenJson_StillWorks()
      {
          const string response =
              "Here is the analysis:\n```json\n{\"summary\":\"s\"}\n```";
          var report = JsonSerializer.Deserialize<TrackReport>(
              TrackAnalyzer.ExtractJson(response), Opts);
          Assert.NotNull(report);
          Assert.Equal("s", report!.Summary);
      }

      [Fact]
      public void ExtractJson_PlainJson_IsUnchanged()
      {
          const string response = """{"summary":"s"}""";
          Assert.Equal(response, TrackAnalyzer.ExtractJson(response));
      }
  }
  ```
- [ ] Run them and watch the first fail: `dotnet test ai-analyzer/tests/GpxAiAnalyzer.Tests/ --filter TrackAnalyzerTests`
  Expected failure: `System.Text.Json.JsonException : ... additional text encountered after the top-level value` from `ExtractJson_JsonFollowedByProse_ReturnsOnlyTheJson`. The other two must pass before and after.
- [ ] Rewrite `ExtractJson` to always locate the brace bounds, and make it `internal static`
- [ ] Replace the dead `?? throw` with the `string.IsNullOrWhiteSpace` guard and its diagnosable message
- [ ] Run the tests and watch all three pass: `dotnet test ai-analyzer/tests/GpxAiAnalyzer.Tests/ --filter TrackAnalyzerTests`
- [ ] Run the full ai-analyzer suite: `dotnet test ai-analyzer/tests/GpxAiAnalyzer.Tests/`
- [ ] Commit:
  ```bash
  git add ai-analyzer/src/GpxAiAnalyzer.Core/ ai-analyzer/tests/GpxAiAnalyzer.Tests/Analysis/TrackAnalyzerTests.cs
  git commit -m "fix(ai): extract JSON by its braces and diagnose an empty response

  ExtractJson short-circuited on trimmed.StartsWith('{') and returned the whole
  string, so a response that opened with the JSON object and closed with a remark
  ('Let me know if you'd like a deeper breakdown') threw JsonException on the
  trailing text — while a messier response with a prose preamble went through the
  IndexOf/LastIndexOf fallback and worked. The guard clause was the bug.

  The 'response.Text ?? throw' guard was also dead code: ChatResponse.Text is
  non-nullable and yields \"\" when the model ended on tool calls or hit the
  function-invocation limit, so the user got 'The input does not contain any JSON
  tokens' instead of being told the model returned nothing.

  Closes #110
  Closes #112"
  ```

---

### Task 21: Anthropic and Mistral silently discard the requested model

**Issues:** #91

**Files:**
- Modify `ai-analyzer/src/GpxAiAnalyzer.Core/Providers/AnthropicProvider.cs:25`
- Modify `ai-analyzer/src/GpxAiAnalyzer.Core/Providers/MistralProvider.cs:18`
- Test `ai-analyzer/tests/GpxAiAnalyzer.Tests/Providers/ProviderRegistryTests.cs` (append)

**Root cause:** `AnthropicProvider.CreateClient` never reads `options.Model` — it returns `client.Messages` with no model applied — and `MistralProvider.cs:18` has the same defect (`return client.Completions;`). `TrackAnalyzer.AnalyzeAsync` never sets `ChatOptions.ModelId` either (it sets only `Tools`), so there is no downstream place where the model could be recovered. Meanwhile both callers actively supply it: `AnalyzeCommand.cs:75` maps `--model` into `ProviderOptions.Model`, and `ui/api`'s `AiAnalysisService` reads `AiProvider:Model` from settings and logs `"Running AI analysis with provider={Provider}, model={Model}"` — so the log asserts a model that was never sent. `OpenAIProvider.cs:17`, `OllamaProvider.cs:16`, `GeminiProvider.cs:21` and `AzureOpenAIProvider.cs:19` all honour it.

**Fix approach:** Neither SDK's `IChatClient` takes a model at construction the way `OpenAIClient.GetChatClient(model)` does, so bind it at the `IChatClient` level with `Microsoft.Extensions.AI`'s `ConfigureOptions`, which applies to every request the client makes.

```csharp
// AnthropicProvider.cs — after
namespace GpxAiAnalyzer.Core.Providers;

using Anthropic.SDK;
using Microsoft.Extensions.AI;

public sealed class AnthropicProvider : IChatClientProvider
{
    public const string DefaultModel = "claude-sonnet-4-5";

    public string Name => "anthropic";

    public IChatClient CreateClient(ProviderOptions options)
    {
        var apiKey = options.ApiKey ?? Environment.GetEnvironmentVariable("ANTHROPIC_API_KEY");
        var client = apiKey is not null ? new AnthropicClient(apiKey) : new AnthropicClient();

        var model = options.Model ?? DefaultModel;

        // The SDK's IChatClient takes no model at construction, and TrackAnalyzer
        // does not set ChatOptions.ModelId — so without binding it here the
        // --model / AiProvider:Model value was dropped on the floor while the API
        // logged the model it thought it was using.
        return new ChatClientBuilder(client.Messages)
            .ConfigureOptions(o => o.ModelId ??= model)
            .Build();
    }
}
```

```csharp
// MistralProvider.cs — after
public sealed class MistralProvider : IChatClientProvider
{
    public const string DefaultModel = "mistral-large-latest";

    public string Name => "mistral";

    public IChatClient CreateClient(ProviderOptions options)
    {
        var apiKey = options.ApiKey
            ?? Environment.GetEnvironmentVariable("MISTRAL_API_KEY")
            ?? throw new InvalidOperationException(
                "Mistral API key required. Set MISTRAL_API_KEY or use --api-key.");

        var client = new MistralClient(apiKey);
        var model = options.Model ?? DefaultModel;

        return new ChatClientBuilder(client.Completions)
            .ConfigureOptions(o => o.ModelId ??= model)
            .Build();
    }
}
```

`o.ModelId ??= model` leaves an explicit per-request `ModelId` intact, so a future caller that does set it wins. Verify the `ConfigureOptions` overload against the `Microsoft.Extensions.AI` version in `GpxAiAnalyzer.Core.csproj` before applying (use `/context7` for the current signature if it does not compile).

**Steps:**

- [ ] Append the failing regression test to `ai-analyzer/tests/GpxAiAnalyzer.Tests/Providers/ProviderRegistryTests.cs`:
  ```csharp
      [Theory]
      [InlineData("anthropic", "ANTHROPIC_API_KEY")]
      [InlineData("mistral", "MISTRAL_API_KEY")]
      public async Task CreateClient_HonoursTheRequestedModel(string provider, string keyEnvVar)
      {
          var previous = Environment.GetEnvironmentVariable(keyEnvVar);
          Environment.SetEnvironmentVariable(keyEnvVar, "test-key-not-used-for-a-real-call");
          try
          {
              var registry = new ProviderRegistry();
              var client = registry.CreateClient(provider,
                  new ProviderOptions { ApiKey = "test-key", Model = "explicitly-requested-model" });

              // Capture the ChatOptions the client would send, without a network call.
              ChatOptions? captured = null;
              var probe = new CapturingChatClient(client, o => captured = o);

              await Assert.ThrowsAnyAsync<Exception>(() =>
                  probe.GetResponseAsync([new ChatMessage(ChatRole.User, "hi")]));

              Assert.NotNull(captured);
              Assert.Equal("explicitly-requested-model", captured!.ModelId);
          }
          finally { Environment.SetEnvironmentVariable(keyEnvVar, previous); }
      }
  ```
  with a small delegating probe in the same file:
  ```csharp
  file sealed class CapturingChatClient(IChatClient inner, Action<ChatOptions?> capture) : IChatClient
  {
      public Task<ChatResponse> GetResponseAsync(
          IEnumerable<ChatMessage> messages, ChatOptions? options = null,
          CancellationToken cancellationToken = default)
      {
          capture(options);
          return inner.GetResponseAsync(messages, options, cancellationToken);
      }

      public IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
          IEnumerable<ChatMessage> messages, ChatOptions? options = null,
          CancellationToken cancellationToken = default)
      {
          capture(options);
          return inner.GetStreamingResponseAsync(messages, options, cancellationToken);
      }

      public object? GetService(Type serviceType, object? serviceKey = null)
          => inner.GetService(serviceType, serviceKey);

      public void Dispose() => inner.Dispose();
  }
  ```
  `IChatClient`'s member list changes between `Microsoft.Extensions.AI` versions — check the interface in the referenced package and implement exactly its members. If the delegating probe proves brittle, replace this test with a simpler one asserting that `CreateClient` for these two providers returns a client whose `GetService(typeof(ChatClientMetadata))` reports the requested `DefaultModelId`; either assertion pins the behaviour.
- [ ] Run it and watch it fail: `dotnet test ai-analyzer/tests/GpxAiAnalyzer.Tests/ --filter CreateClient_HonoursTheRequestedModel`
  Expected failure: `Assert.Equal() Failure  Expected: explicitly-requested-model  Actual: (null)` — `ChatOptions.ModelId` is never set for either provider.
- [ ] Apply the `ChatClientBuilder(...).ConfigureOptions(...)` wrapper in `AnthropicProvider` and `MistralProvider`
- [ ] Run the test and watch it pass: `dotnet test ai-analyzer/tests/GpxAiAnalyzer.Tests/ --filter CreateClient_HonoursTheRequestedModel`
- [ ] Run the full ai-analyzer suite: `dotnet test ai-analyzer/tests/GpxAiAnalyzer.Tests/`
- [ ] Commit:
  ```bash
  git add ai-analyzer/src/GpxAiAnalyzer.Core/Providers/ ai-analyzer/tests/GpxAiAnalyzer.Tests/Providers/ProviderRegistryTests.cs
  git commit -m "fix(ai): honour ProviderOptions.Model for anthropic and mistral

  AnthropicProvider returned client.Messages and MistralProvider returned
  client.Completions without ever reading options.Model, and TrackAnalyzer does
  not set ChatOptions.ModelId either — so --model and AiProvider:Model were
  dropped on the floor for these two providers while AiAnalysisService logged
  'Running AI analysis with provider=…, model=…' naming the model it thought it
  was using. OpenAI, Ollama, Gemini and Azure OpenAI all honour it.

  Binds the model through ChatClientBuilder.ConfigureOptions so it applies to
  every request, using ??= so an explicit per-request ModelId still wins.

  Closes #91"
  ```

---

### Task 22: The missing-input error path exits 0

**Issues:** #111

**Files:**
- Modify `ai-analyzer/src/GpxAiAnalyzer/Commands/AnalyzeCommand.cs:61`
- Test `ai-analyzer/tests/GpxAiAnalyzer.Tests/Commands/AnalyzeCommandTests.cs` (new)

**Root cause:** When neither `--input` is given nor stdin is redirected, the handler writes a usage message to stderr and does a bare `return;`. The action is registered via `SetAction(Func<ParseResult, CancellationToken, Task>)`, which System.CommandLine treats as exit code 0, so the process reports success while producing no report. This is the one genuine user-error branch in the command and it is indistinguishable from a successful run by any automated caller — and it is inconsistent with the neighbouring failure modes (bad JSON, unknown provider, missing API key) which all throw and do surface a non-zero code.

**Fix approach:** Switch the action to the `int`-returning overload and return 1 from that branch.

```csharp
// ai-analyzer/src/GpxAiAnalyzer/Commands/AnalyzeCommand.cs
        command.SetAction(async (ParseResult parseResult, CancellationToken ct) =>
        {
            // …
            else
            {
                Console.Error.WriteLine("Error: provide --input file or pipe JSON via stdin.");
                Console.Error.WriteLine("Usage: gpx-analyzer analyze --format json track.gpx | gpx-ai-analyzer analyze --provider openai");
                // A bare `return` here is exit code 0: a CI step redirecting stdout
                // to report.json wrote an empty file and its `if [ $? -ne 0 ]` guard
                // never fired.
                return 1;
            }

            // … the rest of the handler …

            ReportFormatter.Format(Console.Out, stats.Filename, report, format);
            return 0;
        });
```

The lambda's return type changes from `Task` to `Task<int>`; System.CommandLine 2.x has an `Func<ParseResult, CancellationToken, Task<int>>` overload of `SetAction`. Confirm against the referenced version.

**Steps:**

- [ ] Create the failing regression test `ai-analyzer/tests/GpxAiAnalyzer.Tests/Commands/AnalyzeCommandTests.cs`:
  ```csharp
  using System.CommandLine;
  using GpxAiAnalyzer.Commands;
  using GpxAiAnalyzer.Core.Providers;

  namespace GpxAiAnalyzer.Tests.Commands;

  public class AnalyzeCommandTests
  {
      [Fact]
      public async Task Analyze_WithNoInputAndNoStdin_ExitsNonZero()
      {
          var root = new RootCommand { AnalyzeCommand.Create(new ProviderRegistry()) };

          // xunit does not redirect stdin, so Console.IsInputRedirected is false —
          // exactly the CI case where the upstream gpx-analyzer step was omitted.
          Assert.False(Console.IsInputRedirected,
              "this test requires a non-redirected stdin; run it without piping into the test host");

          var exitCode = await root.Parse(["analyze", "--provider", "openai"]).InvokeAsync();

          Assert.NotEqual(0, exitCode);
      }
  }
  ```
  Check `AnalyzeCommand.Create`'s real signature (it may take the registry, a format option, or both) and the `Parse(...).InvokeAsync()` shape for the referenced System.CommandLine version. If `Console.IsInputRedirected` is true under the test host, mark the test `Skip`-guarded on that condition rather than asserting — the CLI behaviour is still verified by the manual step below.
- [ ] Run it and watch it fail: `dotnet test ai-analyzer/tests/GpxAiAnalyzer.Tests/ --filter Analyze_WithNoInputAndNoStdin_ExitsNonZero`
  Expected failure: `Assert.NotEqual() Failure  Expected: Not 0  Actual: 0`
- [ ] Change the action to the `Task<int>` overload, `return 1` from the missing-input branch and `return 0` at the end
- [ ] Run the test and watch it pass: `dotnet test ai-analyzer/tests/GpxAiAnalyzer.Tests/ --filter Analyze_WithNoInputAndNoStdin_ExitsNonZero`
- [ ] Run the full ai-analyzer suite: `dotnet test ai-analyzer/tests/GpxAiAnalyzer.Tests/`
- [ ] Verify by hand: `dotnet run --project ai-analyzer/src/GpxAiAnalyzer -- analyze --provider openai > /tmp/report.json; echo "exit=$?"` → expect `exit=1`
- [ ] Commit:
  ```bash
  git add ai-analyzer/src/GpxAiAnalyzer/Commands/AnalyzeCommand.cs ai-analyzer/tests/GpxAiAnalyzer.Tests/Commands/
  git commit -m "fix(ai): exit non-zero when no input is provided

  With neither --input nor a redirected stdin the handler printed a usage hint and
  did a bare 'return', which System.CommandLine reports as exit code 0. A CI step
  running 'gpx-ai-analyzer analyze --provider openai > report.json' with no
  upstream gpx-analyzer wrote an empty report.json, exited 0, and published it
  downstream as valid output. Every neighbouring failure mode — bad JSON, unknown
  provider, missing API key — already surfaces a non-zero code.

  Closes #111"
  ```

---

## Wave 5 — API processing correctness

### Task 23: Stored timestamps are server-local, and activities stick forever after a restart

**Issues:** #113, #114

**Files:**
- Modify `ui/api/Services/ActivityProcessingService.cs:167`–`:170` (timestamp parsing), `:237`–`:245` (failure write)
- Create `ui/api/BackgroundServices/ProcessingRecoveryService.cs`
- Modify `ui/api/Program.cs:139`–`:140` (register the recovery service)
- Test `ui/api.Tests/Processing/ProcessingRecoveryTests.cs` (new)

**Root cause:** Both are in `ActivityProcessingService`; one task.

- **#113** `DateTime.TryParse(stats.StartTime, out var start)` uses `CultureInfo.CurrentCulture` and `DateTimeStyles.None`. `SummaryMapper` emits the value as `s.StartTime.ToString("yyyy-MM-ddTHH:mm:ssZ")` and `GpxParser` produces a genuine UTC `DateTime`, so the string is a correct UTC instant — but with `DateTimeStyles.None`, .NET honours the trailing `Z` by converting the result to the machine's **local** time and returning `Kind = Local`. `activity.StartTime`/`EndTime` are therefore stored shifted by the host's UTC offset while everything else in the system is UTC: `CreatedAt`/`UpdatedAt` are `DateTime.UtcNow`, and `DashboardController.GetSummary` builds `monthStart` from `DateTime.UtcNow` (line 37) and compares it directly against `a.StartTime` (line 38). On a Europe/Paris host an activity starting `2024-06-30T23:30:00Z` is stored as `2024-07-01T01:30:00`, so July's `activitiesThisMonth` counts a June run that June's own totals never contained. Because the offset comes from the host's current DST state, rows written in winter (+1) and summer (+2) are on different scales.
- **#114** Two defects leave rows in a non-terminal `ProcessingStatus` with no path back. (1) The catch block writes the `Failed` status with `await _db.SaveChangesAsync(ct)` using the same token that caused the failure. `ActivityProcessingWorker` passes `stoppingToken` into `ProcessActivityAsync`, so on host shutdown the `OperationCanceledException` from the pipeline is caught here and the recovery save immediately throws on the already-cancelled token; the row keeps the `Analyzing` value committed earlier. (2) The queue is `Channel.CreateUnbounded` registered as a singleton with no persistence and no startup requeue — `Program.cs` only runs `Database.Migrate()` and role seeding. After `docker compose up --build -d` the in-flight activity stays `Analyzing` and every queued id is lost with the channel, so `/profile`, `/track` and `/splits` return `PROFILE_NOT_AVAILABLE` forever while the client polls a status that will never change.

**Fix approach:** Parse as UTC; write the failure with an uncancellable token; requeue non-terminal rows at startup.

```csharp
// ActivityProcessingService.cs — #113
                // SummaryMapper emits a UTC instant with a trailing Z. With
                // DateTimeStyles.None .NET honours the Z by converting to the
                // host's LOCAL time, so the stored value drifts by the host's UTC
                // offset (and by its current DST state) while CreatedAt/UpdatedAt
                // and the dashboard's month boundaries are all UtcNow.
                const DateTimeStyles utcStyles =
                    DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal;

                if (DateTime.TryParse(stats.StartTime, CultureInfo.InvariantCulture, utcStyles, out var start))
                    activity.StartTime = start;
                if (DateTime.TryParse(stats.EndTime, CultureInfo.InvariantCulture, utcStyles, out var end))
                    activity.EndTime = end;
```

Add `using System.Globalization;`.

```csharp
// ActivityProcessingService.cs — #114 part 1
        catch (Exception ex)
        {
            totalSw.Stop();
            _logger.LogError(ex, "[{Id}] Processing failed after {Elapsed:F1}s: {Message}",
                activityId, totalSw.Elapsed.TotalSeconds, ex.Message);
            activity.Status = ProcessingStatus.Failed;
            activity.ErrorMessage = ex.Message;
            activity.UpdatedAt = DateTime.UtcNow;
            // NOT `ct`: the most common reason we are here is that `ct` was
            // cancelled (host shutdown), and saving with it throws immediately,
            // leaving the row stuck in Analyzing with nothing to move it on.
            await _db.SaveChangesAsync(CancellationToken.None);
        }
```

```csharp
// ui/api/BackgroundServices/ProcessingRecoveryService.cs — new
namespace GpxAnalyzer.Api.BackgroundServices;

using System.Threading.Channels;
using GpxAnalyzer.Api.Data;
using GpxAnalyzer.Api.Entities;
using Microsoft.EntityFrameworkCore;

/// <summary>
/// Re-enqueues activities left in a non-terminal state by a previous process.
/// The processing queue is an in-memory Channel, so a restart loses every queued
/// id and abandons whatever was in flight; without this, those rows never reach
/// Completed or Failed and the client polls a status that will never change.
/// </summary>
public class ProcessingRecoveryService : IHostedService
{
    private static readonly ProcessingStatus[] NonTerminal =
    [
        ProcessingStatus.Pending,
        ProcessingStatus.Analyzing,
        ProcessingStatus.AiProcessing,
    ];

    private readonly Channel<(Guid ActivityId, Guid UserId)> _channel;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<ProcessingRecoveryService> _logger;

    public ProcessingRecoveryService(
        Channel<(Guid ActivityId, Guid UserId)> channel,
        IServiceScopeFactory scopeFactory,
        ILogger<ProcessingRecoveryService> logger)
    {
        _channel = channel;
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var stranded = await db.Activities
            .Where(a => NonTerminal.Contains(a.Status))
            .Select(a => new { a.Id, a.UserId })
            .ToListAsync(cancellationToken);

        if (stranded.Count == 0) return;

        _logger.LogWarning(
            "Re-enqueueing {Count} activities left in a non-terminal state by a previous run",
            stranded.Count);

        foreach (var a in stranded)
        {
            // Reset to Pending so the status the client sees matches reality.
            await db.Activities.Where(x => x.Id == a.Id)
                .ExecuteUpdateAsync(s => s.SetProperty(x => x.Status, ProcessingStatus.Pending),
                    cancellationToken);
            await _channel.Writer.WriteAsync((a.Id, a.UserId), cancellationToken);
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
```

```csharp
// Program.cs — register BEFORE the worker so recovery runs first
builder.Services.AddSingleton(Channel.CreateUnbounded<(Guid ActivityId, Guid UserId)>());
builder.Services.AddHostedService<ProcessingRecoveryService>();
builder.Services.AddHostedService<ActivityProcessingWorker>();
```

`ProcessingStatus` is stored as a string, so the `NonTerminal.Contains(a.Status)` translation may fail on SQLite (see the Known Pitfalls in CLAUDE.md). If it does, replace it with three explicit `||` comparisons, or materialize first with `.ToListAsync()` and filter in memory — the row count here is small.

**Steps:**

- [ ] Create the failing regression test `ui/api.Tests/Processing/ProcessingRecoveryTests.cs`:
  ```csharp
  using System.Globalization;
  using GpxAnalyzer.Api.Data;
  using GpxAnalyzer.Api.Entities;
  using GpxAnalyzer.Api.Tests.Helpers;
  using Microsoft.EntityFrameworkCore;
  using Microsoft.Extensions.DependencyInjection;

  namespace GpxAnalyzer.Api.Tests.Processing;

  public class ProcessingRecoveryTests
  {
      // ── #113 ─────────────────────────────────────────────────────────────
      [Fact]
      public void SummaryMapperTimestamp_ParsesBackAsUtc()
      {
          // The exact string SummaryMapper emits for a UTC instant.
          const string emitted = "2024-06-30T23:30:00Z";

          const DateTimeStyles utcStyles =
              DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal;
          Assert.True(DateTime.TryParse(emitted, CultureInfo.InvariantCulture, utcStyles, out var parsed));

          Assert.Equal(DateTimeKind.Utc, parsed.Kind);
          Assert.Equal(new DateTime(2024, 6, 30, 23, 30, 0, DateTimeKind.Utc), parsed);
      }

      [Fact]
      public async Task UploadedActivity_StoresStartTimeInUtc()
      {
          using var factory = new ApiFactory();
          var client = factory.CreateClient();
          var auth = await TestHelpers.RegisterAsync(client, $"utc_{Guid.NewGuid():N}@test.local");
          var authed = TestHelpers.CreateAuthorizedClient(factory, auth.AccessToken);

          var id = await TestHelpers.UploadTestGpxAsync(authed);
          await TestHelpers.WaitForProcessingAsync(authed, id);

          using var scope = factory.Services.CreateScope();
          var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
          var activity = await db.Activities.SingleAsync(a => a.Id == Guid.Parse(id));

          Assert.NotNull(activity.StartTime);
          // The fixture's first trkpt time, read straight out of e2e/Fixtures/test.gpx.
          // Replace this literal with that value before running.
          var expectedUtc = DateTime.Parse("REPLACE_WITH_FIXTURE_FIRST_TRKPT_TIME",
              CultureInfo.InvariantCulture,
              DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal);
          Assert.Equal(expectedUtc, activity.StartTime!.Value);
      }

      // ── #114 ─────────────────────────────────────────────────────────────
      [Fact]
      public async Task Startup_RequeuesActivitiesLeftInANonTerminalState()
      {
          var dbPath = Path.Combine(Path.GetTempPath(), $"recovery_{Guid.NewGuid():N}.db");
          Guid strandedId;
          Guid userId;

          // First "process": create a user and an activity stuck in Analyzing.
          using (var factory = new ApiFactory(dbPath))
          {
              var client = factory.CreateClient();
              var auth = await TestHelpers.RegisterAsync(client, $"stuck_{Guid.NewGuid():N}@test.local");
              userId = Guid.Parse(auth.User.Id);

              using var scope = factory.Services.CreateScope();
              var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
              strandedId = Guid.NewGuid();
              db.Activities.Add(new Activity
              {
                  Id = strandedId,
                  UserId = userId,
                  Name = "interrupted",
                  ActivityType = "trail",
                  GpxFilePath = "missing.gpx",
                  Status = ProcessingStatus.Analyzing,   // killed mid-DEM-download
              });
              await db.SaveChangesAsync();
          }

          // Second "process": the same database, a fresh host.
          using (var factory = new ApiFactory(dbPath))
          {
              _ = factory.CreateClient();   // forces host start, which runs IHostedServices

              var deadline = DateTime.UtcNow.AddSeconds(15);
              ProcessingStatus? status = null;
              while (DateTime.UtcNow < deadline)
              {
                  using var scope = factory.Services.CreateScope();
                  var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                  status = (await db.Activities.SingleAsync(a => a.Id == strandedId)).Status;
                  if (status is ProcessingStatus.Completed or ProcessingStatus.Failed) break;
                  await Task.Delay(300);
              }

              // The GPX is missing, so it must end Failed — the point is that it
              // reaches a TERMINAL state instead of sitting in Analyzing forever.
              Assert.True(status is ProcessingStatus.Completed or ProcessingStatus.Failed,
                  $"stranded activity is still {status} after restart");
          }

          try { File.Delete(dbPath); } catch { /* best-effort */ }
      }
  }
  ```
  `ApiFactory` currently generates its own DB path; add an optional constructor parameter `public ApiFactory(string? dbPath = null)` that uses the given path when supplied, so the restart scenario can reuse one database. Read `ui/api.Tests/Helpers/ApiFactory.cs` and make that the minimal change.
- [ ] Run them and watch them fail: `dotnet test ui/api.Tests/GpxAnalyzer.Api.Tests.csproj --filter ProcessingRecoveryTests`
  Expected failures: `UploadedActivity_StoresStartTimeInUtc` fails with the stored value shifted by the host's UTC offset (on a Europe/Paris machine, two hours ahead in summer) — #113; `stranded activity is still Analyzing after restart` — #114. `SummaryMapperTimestamp_ParsesBackAsUtc` documents the correct parse and passes immediately.
- [ ] Apply the `DateTimeStyles.AdjustToUniversal | AssumeUniversal` parse in `ActivityProcessingService`
- [ ] Change the catch block's save to `CancellationToken.None`
- [ ] Create `ProcessingRecoveryService` and register it in `Program.cs` before `ActivityProcessingWorker`
- [ ] Run the tests and watch them pass: `dotnet test ui/api.Tests/GpxAnalyzer.Api.Tests.csproj --filter ProcessingRecoveryTests`
- [ ] Run the full API suite: `dotnet test ui/api.Tests/GpxAnalyzer.Api.Tests.csproj`
- [ ] Confirm no entity changed, so no migration is needed: `cd ui/api && dotnet ef migrations has-pending-model-changes` (expect "No changes"). If it reports changes, stop and run `dotnet ef migrations add <Name>`.
- [ ] Commit:
  ```bash
  git add ui/api/Services/ActivityProcessingService.cs ui/api/BackgroundServices/ProcessingRecoveryService.cs ui/api/Program.cs ui/api.Tests/
  git commit -m "fix(api): store activity timestamps in UTC and recover stranded activities

  DateTime.TryParse with DateTimeStyles.None honours the trailing Z by converting
  to the host's LOCAL time, so StartTime/EndTime were stored shifted by the host's
  UTC offset while CreatedAt/UpdatedAt and the dashboard's month boundaries are
  all UtcNow. An activity at 2024-06-30T23:30:00Z landed as 2024-07-01T01:30 on a
  Paris host, counted in July's dashboard and missing from June's, and rows
  written in winter and summer were on different scales.

  The failure path also saved the Failed status with the same CancellationToken
  that caused the failure, so on host shutdown the recovery write threw on the
  already-cancelled token and the row kept its Analyzing value; the in-memory
  Channel lost every queued id at the same moment and nothing ever re-enqueued
  them. Adds ProcessingRecoveryService, which requeues non-terminal rows at
  startup, and writes the failure status with CancellationToken.None.

  Closes #113
  Closes #114"
  ```

---

### Task 24: Km splits double-count the boundary segment's elevation

**Issues:** #116

**Files:**
- Modify `ui/api/Services/ProfileComputationService.cs:288`–`:295`
- Test `ui/api.Tests/Profile/ProfileApiTests.cs` (append)

> **Ordering:** rebase onto Task 15, which changed the `power` lookup in the same file (a different method, `ComputeFromEnrichedGpx` vs `ComputeKmSplits`, so no textual conflict is expected).

**Root cause:** For split `km`, `startIdx = Math.Max(0, ptIdx - 1)` deliberately backs up to the last point *before* the split start, while `endIdx` advances to the first point at or past the split end. The elevation loop then runs `for (var i = startIdx; i <= endIdx; i++)` and accumulates `points[i].Ele - points[i-1].Ele` for every `i > startIdx`. The boundary segment `(endIdx-1 → endIdx)` is fully attributed to split `km`, and on the next iteration that same pair becomes `(startIdx → startIdx+1)` and is added again. Unlike the HR/cadence/power averages just below (line 297), which are gated by an explicit `CumDist` range check, the elevation accumulation has no such guard, so exactly one segment per split boundary is counted twice.

**Fix approach:** Attribute each segment to exactly one split — the one containing the segment's **end** point — using the same `CumDist` gate the averages already use.

```csharp
            // Compute elevation gain/loss and averages for points in this split
            double elevGain = 0, elevLoss = 0;
            double hrSum = 0, cadSum = 0, powSum = 0;
            int hrCount = 0, cadCount = 0, powCount = 0;

            for (var i = startIdx; i <= endIdx && i < points.Count; i++)
            {
                // startIdx deliberately backs up one point before the split start,
                // and endIdx advances one past the split end, so the boundary
                // segment belongs to two consecutive splits. Attribute each segment
                // to exactly one split: the one containing its END point. Without
                // this gate every split double-counts one segment's elevation.
                if (i > startIdx &&
                    points[i].CumDist > splitStartDist &&
                    points[i].CumDist <= splitEndDist)
                {
                    var dEle = points[i].Ele - points[i - 1].Ele;
                    if (dEle > 0) elevGain += dEle;
                    else elevLoss += Math.Abs(dEle);
                }

                if (points[i].CumDist >= splitStartDist && points[i].CumDist <= splitEndDist)
                {
                    if (points[i].HeartRate is { } hr) { hrSum += hr; hrCount++; }
                    if (points[i].Cadence is { } cad) { cadSum += cad; cadCount++; }
                    if (points[i].Power is { } pow) { powSum += pow; powCount++; }
                }
            }
```

**Steps:**

- [ ] Append the failing regression test to `ui/api.Tests/Profile/ProfileApiTests.cs`:
  ```csharp
      [Fact]
      public void ComputeKmSplits_SustainedClimb_DoesNotDoubleCountBoundarySegments()
      {
          // A hike logged every 100 m on a sustained 10% climb: each inter-point
          // segment carries ~10 m of gain, so 10 km carries ~1000 m total.
          var t0 = DateTime.Parse("2024-01-01T08:00:00Z",
              System.Globalization.CultureInfo.InvariantCulture,
              System.Globalization.DateTimeStyles.AdjustToUniversal |
              System.Globalization.DateTimeStyles.AssumeUniversal);

          var gpx = BuildEnrichedGpx(pointCount: 101, metresPerPoint: 100, metresGainPerPoint: 10, start: t0);
          var tmp = Path.Combine(Path.GetTempPath(), $"splits_{Guid.NewGuid():N}.gpx");
          File.WriteAllText(tmp, gpx);
          try
          {
              var svc = new ProfileComputationService(
                  Microsoft.Extensions.Logging.Abstractions.NullLogger<ProfileComputationService>.Instance);
              var (_, _, splitsJson) = svc.ComputeFromEnrichedGpx(tmp);

              Assert.NotNull(splitsJson);
              using var doc = System.Text.Json.JsonDocument.Parse(splitsJson!);
              var splits = doc.RootElement.GetProperty("splits");   // adjust to the real shape

              double summed = 0;
              foreach (var s in splits.EnumerateArray())
                  summed += s.GetProperty("elevationGain").GetDouble();

              // 10 km at 10 m per 100 m segment = ~1000 m. Before the fix each of
              // the 10 splits added its ~10 m boundary segment twice, giving ~1100.
              Assert.InRange(summed, 950, 1050);
          }
          finally { File.Delete(tmp); }
      }
  ```
  Write the `BuildEnrichedGpx(pointCount, metresPerPoint, metresGainPerPoint, start)` helper in the same test class, emitting the exact enriched shape `GpxWriter.WriteEnriched` produces (see the fixture string in Task 15) with a `<gpxa:dist>` cumulative distance on every point. Read `ProfileComputationService.ComputeKmSplits` and the `SplitEntry` JSON property names before finalising the assertions — `splitsJson`'s root shape may be a bare array rather than `{ "splits": [...] }`.
- [ ] Run it and watch it fail: `dotnet test ui/api.Tests/GpxAnalyzer.Api.Tests.csproj --filter ComputeKmSplits_SustainedClimb`
  Expected failure: `Assert.InRange() Failure  Range: (950 - 1050)  Actual: ~1100` — one ~10 m boundary segment counted twice per split.
- [ ] Add the `CumDist` gate around the elevation accumulation
- [ ] Run the test and watch it pass: `dotnet test ui/api.Tests/GpxAnalyzer.Api.Tests.csproj --filter ComputeKmSplits_SustainedClimb`
- [ ] Run the full API suite: `dotnet test ui/api.Tests/GpxAnalyzer.Api.Tests.csproj`
- [ ] Commit:
  ```bash
  git add ui/api/Services/ProfileComputationService.cs ui/api.Tests/Profile/ProfileApiTests.cs
  git commit -m "fix(api): stop double-counting boundary segments in km split elevation

  startIdx backs up to the last point before the split start and endIdx advances
  to the first point past the split end, so the boundary segment was fully
  attributed to split N and then again to split N+1. The HR/cadence/power averages
  right below already gate on a CumDist range; the elevation accumulation did not,
  so exactly one segment per boundary was counted twice — a 10 km climb logged
  every 100 m summed to ~1100 m across its splits against the ~1000 m
  ComputePipeline reported for the activity.

  Attributes each segment to the single split containing its end point.

  Closes #116"
  ```

---

## Wave 6 — Client editor & upload correctness

All four tasks use the Vitest harness from Task 1. Map interactions and 30-second timers are not testable through Playwright in this project (CLAUDE.md notes that MapLibre tests check container presence, not WebGL rendering), so the fix approach in each task extracts the defective logic into a pure, exported function and unit-tests that.

### Task 25: `addPoint` passes a polyline index as a waypoint order, so insertion always appends

**Issues:** #118

**Files:**
- Create `ui/client/src/utils/routeInsert.ts`
- Modify `ui/client/src/components/editor/EditorMap.tsx:313`
- Test `ui/client/src/utils/routeInsert.test.ts` (new)

**Root cause:** `nearestPointOnLine(line, pt).properties.index` is the index of the segment within `state.routeCoordinates` — the rendered polyline, often thousands of points. That value is passed straight to `insertWaypoint(lat, lng, insertIndex)`, whose third argument is `afterOrder`, a waypoint order in the `0..waypoints.length-1` space (`editorStore.ts:157` sets `order: afterOrder + 1` and bumps only waypoints with `order > afterOrder`). The two index spaces coincide only in manual mode right after a freehand draw, when `routeCoordinates` *is* the waypoint list; for any routed or imported route they diverge by orders of magnitude. A click halfway along a 1,800-point polyline yields `index ≈ 900`, so the new waypoint gets `order = 901`, bumps nothing, sorts last, and the route detours from the finish back to the clicked point.

**Fix approach:** Translate the polyline index into the waypoint-order space by anchoring each waypoint to its nearest polyline vertex and taking the last waypoint at or before the click.

```ts
// ui/client/src/utils/routeInsert.ts — new
export interface OrderedWaypoint {
  lat: number;
  lon: number;
  order: number;
}

/**
 * Index of the polyline vertex closest to (lon, lat).
 * Squared planar distance is enough: we only need the argmin, and over the span
 * of a single route the latitude scale factor is effectively constant.
 */
export function nearestVertexIndex(
  coordinates: number[][],
  lon: number,
  lat: number,
): number {
  let best = 0;
  let bestD = Infinity;
  for (let i = 0; i < coordinates.length; i++) {
    const dx = coordinates[i][0] - lon;
    const dy = coordinates[i][1] - lat;
    const d = dx * dx + dy * dy;
    if (d < bestD) {
      bestD = d;
      best = i;
    }
  }
  return best;
}

/**
 * Translates an index in the RENDERED POLYLINE space (what turf's
 * nearestPointOnLine returns) into the WAYPOINT ORDER space that
 * editorStore.insertWaypoint(lat, lon, afterOrder) expects.
 *
 * The two coincide only in manual mode right after a freehand draw. For a routed
 * or imported route the polyline has thousands of vertices and the waypoint list
 * has a handful, so passing the polyline index through unchanged always produced
 * an order beyond every existing waypoint — i.e. an append.
 *
 * Returns the order of the last waypoint at or before routeIndex, or
 * (lowest order - 1) when the click precedes the first waypoint.
 */
export function waypointOrderForRouteIndex(
  routeCoordinates: number[][],
  waypoints: OrderedWaypoint[],
  routeIndex: number,
): number {
  if (waypoints.length === 0) return -1;

  const sorted = [...waypoints].sort((a, b) => a.order - b.order);
  let result = sorted[0].order - 1;

  for (const wp of sorted) {
    const anchor = nearestVertexIndex(routeCoordinates, wp.lon, wp.lat);
    if (anchor <= routeIndex) result = wp.order;
    else break;
  }

  return result;
}
```

```tsx
// ui/client/src/components/editor/EditorMap.tsx:313
          const snapped = nearestPointOnLine(line, pt);
          const routeIndex = snapped.properties.index ?? coords.length - 1;

          // properties.index is an index into the rendered polyline, not a
          // waypoint order — insertWaypoint's third argument is an order.
          const afterOrder = waypointOrderForRouteIndex(
            coords,
            useEditorStore.getState().waypoints,
            routeIndex,
          );

          insertWaypoint(lat, lng, afterOrder);
```

**Steps:**

- [ ] Create the failing regression test `ui/client/src/utils/routeInsert.test.ts`:
  ```ts
  import { describe, it, expect } from 'vitest';
  import { waypointOrderForRouteIndex, nearestVertexIndex } from './routeInsert';

  /** A straight west-to-east polyline of `n` vertices from lon 0 to lon 1. */
  function polyline(n: number): number[][] {
    return Array.from({ length: n }, (_, i) => [i / (n - 1), 45]);
  }

  describe('waypointOrderForRouteIndex', () => {
    it('maps a mid-route polyline index onto the enclosing waypoint pair', () => {
      // 4 waypoints spread over an 1,800-point routed polyline.
      const coords = polyline(1800);
      const waypoints = [
        { lat: 45, lon: 0.0, order: 0 },
        { lat: 45, lon: 0.333, order: 1 },
        { lat: 45, lon: 0.666, order: 2 },
        { lat: 45, lon: 1.0, order: 3 },
      ];

      // User clicks halfway between waypoints 1 and 2 -> polyline index ~900.
      const afterOrder = waypointOrderForRouteIndex(coords, waypoints, 900);

      // insertWaypoint gives the new point afterOrder + 1, so it must land
      // between waypoint 1 and waypoint 2 — i.e. afterOrder === 1.
      expect(afterOrder).toBe(1);
    });

    it('appends when the click is past the last waypoint', () => {
      const coords = polyline(1800);
      const waypoints = [
        { lat: 45, lon: 0.0, order: 0 },
        { lat: 45, lon: 0.5, order: 1 },
      ];
      expect(waypointOrderForRouteIndex(coords, waypoints, 1799)).toBe(1);
    });

    it('prepends when the click precedes the first waypoint', () => {
      const coords = polyline(1800);
      const waypoints = [
        { lat: 45, lon: 0.5, order: 0 },
        { lat: 45, lon: 1.0, order: 1 },
      ];
      expect(waypointOrderForRouteIndex(coords, waypoints, 10)).toBe(-1);
    });

    it('is identity-like in manual mode where the polyline IS the waypoint list', () => {
      const waypoints = [
        { lat: 45, lon: 0, order: 0 },
        { lat: 45, lon: 1, order: 1 },
        { lat: 45, lon: 2, order: 2 },
      ];
      const coords = waypoints.map((w) => [w.lon, w.lat]);
      expect(waypointOrderForRouteIndex(coords, waypoints, 1)).toBe(1);
    });

    it('returns -1 for an empty waypoint list', () => {
      expect(waypointOrderForRouteIndex(polyline(10), [], 5)).toBe(-1);
    });
  });

  describe('nearestVertexIndex', () => {
    it('finds the closest vertex', () => {
      expect(nearestVertexIndex(polyline(11), 0.5, 45)).toBe(5);
      expect(nearestVertexIndex(polyline(11), 0.0, 45)).toBe(0);
      expect(nearestVertexIndex(polyline(11), 1.0, 45)).toBe(10);
    });
  });
  ```
- [ ] Run it and watch it fail: `cd ui/client && npx vitest run src/utils/routeInsert.test.ts`
  Expected failure: `Failed to resolve import "./routeInsert"` — the module does not exist. Create it with `waypointOrderForRouteIndex` returning `routeIndex` (the current behaviour) and re-run to see the real assertion failure: `expected 900 to be 1`. Record that before implementing.
- [ ] Implement `nearestVertexIndex` and `waypointOrderForRouteIndex` properly
- [ ] Wire `waypointOrderForRouteIndex` into `EditorMap.handleMapClick` and import it
- [ ] Run the test and watch all six pass: `cd ui/client && npx vitest run src/utils/routeInsert.test.ts`
- [ ] Run the client checks: `cd ui/client && npm run test && npm run lint && npm run build && npm run e2e`
- [ ] Commit:
  ```bash
  git add ui/client/src/utils/routeInsert.ts ui/client/src/utils/routeInsert.test.ts ui/client/src/components/editor/EditorMap.tsx
  git commit -m "fix(client): translate the polyline index into a waypoint order on insert

  nearestPointOnLine's properties.index is an index into state.routeCoordinates —
  the rendered polyline, often thousands of vertices — and it was passed straight
  into insertWaypoint's afterOrder argument, which lives in the 0..n-1 waypoint
  order space. The two coincide only in manual mode right after a freehand draw.
  On a 4-waypoint route with an 1,800-point polyline, clicking halfway gave
  afterOrder 900, so the new waypoint got order 901, bumped nothing, sorted last,
  and the route detoured from the finish back to the clicked point.

  Adds waypointOrderForRouteIndex, which anchors each waypoint to its nearest
  polyline vertex and returns the order of the last one at or before the click.

  Closes #118"
  ```

---

### Task 26: Auto-save clears `isDirty` after the request, discarding edits made in flight

**Issues:** #120

**Files:**
- Modify `ui/client/src/hooks/useAutoSave.ts:15`–`:33`
- Modify `ui/client/src/pages/EditorPage.tsx:100`, `:126` (same post-await `markSaved()` pattern)
- Test `ui/client/src/hooks/useAutoSave.test.ts` (new)

**Root cause:** `doAutoSave` snapshots the store, awaits `routesApi.autoSaveRoute(...)`, then calls `useEditorStore.getState().markSaved()`, which sets `isDirty: false` unconditionally. Any store mutation during the await (every editor action sets `isDirty: true`) is erased from the dirty flag even though its data was never sent. Because the next 30 s tick returns early on `!state.isDirty`, that edit is never auto-saved, and `EditorPage`'s unsaved-changes guards (`handleDiscard` line 145, the `beforeunload` handler line 272) both read `isDirty` and stay silent — so the user navigates away and loses the edit with no prompt.

**Fix approach:** Only clear the dirty flag when the store still holds exactly what was sent. Extract the comparison so it is unit-testable.

```ts
// ui/client/src/hooks/useAutoSave.ts — after
import { useEffect, useRef, useCallback } from 'react';
import { useEditorStore } from '../stores/editorStore';
import { routesApi } from '../api/routes-client';

const AUTO_SAVE_INTERVAL = 30_000; // 30 seconds

export interface AutoSavePayload {
  points: number[][];
  waypoints: unknown[];
  pois: unknown[];
}

/** The exact slice of editor state an auto-save PATCH carries. */
export function autoSavePayload(state: {
  routeCoordinates: number[][];
  waypoints: unknown[];
  pois: unknown[];
}): AutoSavePayload {
  return {
    points: state.routeCoordinates,
    waypoints: state.waypoints,
    pois: state.pois,
  };
}

/**
 * True when the store still holds exactly what was sent, i.e. nothing changed
 * while the request was in flight. Clearing isDirty unconditionally after the
 * await erases edits whose data was never sent, and because the next tick
 * returns early on !isDirty they are never saved at all.
 */
export function payloadIsStillCurrent(sent: AutoSavePayload, current: AutoSavePayload): boolean {
  return JSON.stringify(sent) === JSON.stringify(current);
}

export function useAutoSave() {
  const timerRef = useRef<ReturnType<typeof setInterval> | null>(null);
  const savingRef = useRef(false);

  const doAutoSave = useCallback(async () => {
    const state = useEditorStore.getState();

    if (!state.isDirty || !state.routeId || savingRef.current) return;

    savingRef.current = true;
    const sent = autoSavePayload(state);
    try {
      await routesApi.autoSaveRoute(state.routeId, sent);

      const after = useEditorStore.getState();
      if (payloadIsStillCurrent(sent, autoSavePayload(after))) {
        after.markSaved();
      } else {
        // Something changed during the request: record the attempt but stay
        // dirty so the next tick — and the unsaved-changes guards — still fire.
        useEditorStore.setState({ lastAutoSave: new Date() });
      }
    } catch (err) {
      console.error('Auto-save failed:', err);
    } finally {
      savingRef.current = false;
    }
  }, []);

  useEffect(() => {
    timerRef.current = setInterval(doAutoSave, AUTO_SAVE_INTERVAL);
    return () => {
      if (timerRef.current) {
        clearInterval(timerRef.current);
        timerRef.current = null;
      }
    };
  }, [doAutoSave]);

  return { autoSave: doAutoSave };
}
```

Apply the same guard at `EditorPage.tsx:100` and `:126`, using the full-save payload those call sites build rather than `autoSavePayload`.

**Steps:**

- [ ] Create the failing regression test `ui/client/src/hooks/useAutoSave.test.ts`:
  ```ts
  import { describe, it, expect, beforeEach, vi, afterEach } from 'vitest';
  import { useEditorStore } from '../stores/editorStore';
  import { routesApi } from '../api/routes-client';
  import { useAutoSave, payloadIsStillCurrent, autoSavePayload } from './useAutoSave';

  describe('payloadIsStillCurrent', () => {
    it('is true for identical payloads', () => {
      const a = { points: [[0, 0]], waypoints: [{ id: 'w1' }], pois: [] };
      expect(payloadIsStillCurrent(a, structuredClone(a))).toBe(true);
    });

    it('is false when a waypoint moved', () => {
      const sent = { points: [[0, 0]], waypoints: [{ id: 'w1', lat: 45 }], pois: [] };
      const now = { points: [[0, 0]], waypoints: [{ id: 'w1', lat: 46 }], pois: [] };
      expect(payloadIsStillCurrent(sent, now)).toBe(false);
    });
  });

  describe('auto-save dirty tracking', () => {
    beforeEach(() => {
      useEditorStore.setState({
        routeId: 'route-1',
        routeCoordinates: [[0, 45], [1, 45]],
        waypoints: [{ id: 'w1', lat: 45, lon: 0, order: 0 }],
        pois: [],
        isDirty: true,
      } as never);
    });

    afterEach(() => vi.restoreAllMocks());

    it('stays dirty when the store changes while the request is in flight', async () => {
      let release: () => void = () => {};
      const inFlight = new Promise<void>((r) => { release = r; });

      vi.spyOn(routesApi, 'autoSaveRoute').mockImplementation(async () => {
        // The user drags a waypoint mid-request.
        useEditorStore.setState({
          waypoints: [{ id: 'w1', lat: 46, lon: 0, order: 0 }],
          isDirty: true,
        } as never);
        await inFlight;
      });

      // useAutoSave's returned autoSave is a plain async function; call it
      // directly rather than mounting a component and waiting 30 s.
      const { autoSave } = useAutoSave();
      const pending = autoSave();
      release();
      await pending;

      // The moved waypoint was never sent, so the editor must still be dirty:
      // EditorPage's discard prompt and beforeunload guard both read isDirty.
      expect(useEditorStore.getState().isDirty).toBe(true);
    });

    it('clears isDirty when nothing changed during the request', async () => {
      vi.spyOn(routesApi, 'autoSaveRoute').mockResolvedValue(undefined as never);

      const { autoSave } = useAutoSave();
      await autoSave();

      expect(useEditorStore.getState().isDirty).toBe(false);
    });
  });
  ```
  `useAutoSave` calls `useRef`/`useCallback`/`useEffect`, so calling it outside a component will throw. Either wrap it with `renderHook` from `@testing-library/react`, or — simpler and preferable — extract `doAutoSave`'s body into an exported `performAutoSave()` free function that `useAutoSave` wraps, and test that. Pick one and make it consistent with the fix code above.
- [ ] Run it and watch it fail: `cd ui/client && npx vitest run src/hooks/useAutoSave.test.ts`
  Expected failure: `Failed to resolve import` for `payloadIsStillCurrent`/`autoSavePayload`; add them, re-run, and see `expected false to be true` on `stays dirty when the store changes while the request is in flight` — `markSaved()` cleared the flag unconditionally.
- [ ] Implement `autoSavePayload`, `payloadIsStillCurrent` and the guarded `markSaved` in `useAutoSave.ts`
- [ ] Apply the same guard at `EditorPage.tsx:100` and `:126`
- [ ] Run the test and watch all four pass: `cd ui/client && npx vitest run src/hooks/useAutoSave.test.ts`
- [ ] Run the client checks: `cd ui/client && npm run test && npm run lint && npm run build && npm run e2e`
- [ ] Commit:
  ```bash
  git add ui/client/src/hooks/useAutoSave.ts ui/client/src/hooks/useAutoSave.test.ts ui/client/src/pages/EditorPage.tsx
  git commit -m "fix(client): keep the editor dirty when edits land during an auto-save

  doAutoSave snapshotted the store, awaited the PATCH, then called markSaved(),
  which clears isDirty unconditionally. A waypoint dragged while the request was
  in flight had its data left out of that request and its dirty flag erased
  anyway, and since the next 30s tick returns early on !isDirty it was never
  saved. EditorPage's discard prompt and beforeunload handler both read isDirty,
  so the user navigated away, reset() ran on unmount, and the edit was gone with
  no warning.

  Clears isDirty only when the store still holds exactly what was sent.

  Closes #120"
  ```

---

### Task 27: Cutoff times wrap at 24 h, making day-2 cutoffs unrepresentable

**Issues:** #121

**Files:**
- Modify `ui/client/src/utils/dayNight.ts` (add `hhmmToElapsedSeconds`, `elapsedSecondsToDayOffset`)
- Modify `ui/client/src/components/race-plan/CheckpointEditor.tsx:70`–`:79` and the cutoff/pause inputs
- Test `ui/client/src/utils/dayNight.test.ts` (new)

**Root cause:** `hhmmToSeconds` converts the wall-clock `<input type="time">` value to seconds-since-start using only minute-of-day arithmetic: `diffMinutes = targetMinutes - startMinutes; if (diffMinutes < 0) diffMinutes += 24*60`. The result is always under 24 h, so any cutoff on day 2+ collapses into the first day. The display side (`formatArrivalTime` in `utils/dayNight.ts:120`, `Math.floor(totalMinutes/60) % 24`) is also lossy, so the round trip cannot be repaired from the rendered value. This is an ultra-trail race planner where 30–46 h cutoffs are the norm: a Friday-18:00 start with a Saturday-20:00 cutoff (26 h = 93,600 s) is saved as 7,200 s, and `CheckpointTimeline`'s `isLate` test then flags that checkpoint and every later one in red while the printed plan shows a cutoff 24 h early.

**Fix approach:** Carry an explicit day offset alongside the wall-clock time, in both the conversion and the UI.

```ts
// ui/client/src/utils/dayNight.ts — append
/**
 * Converts a wall-clock "HH:mm" cutoff into seconds since the race start.
 *
 * `dayOffset` is how many midnights the cutoff falls after the one implied by
 * the plain wall-clock reading. Minute-of-day arithmetic alone caps the result
 * at 24 h, which makes every day-2+ cutoff in an ultra unrepresentable.
 */
export function hhmmToElapsedSeconds(
  startTime: string,
  hhmm: string,
  dayOffset = 0,
): number | null {
  if (!hhmm) return null;

  const [hh, mm] = hhmm.split(':').map(Number);
  const [sh, sm] = (startTime || '00:00').split(':').map(Number);
  if ([hh, mm, sh, sm].some((n) => Number.isNaN(n))) return null;

  let diffMinutes = hh * 60 + mm - (sh * 60 + sm);
  if (diffMinutes < 0) diffMinutes += 24 * 60;

  return (diffMinutes + dayOffset * 24 * 60) * 60;
}

/** How many whole days after the start a given elapsed time falls on. */
export function elapsedSecondsToDayOffset(startTime: string, seconds: number): number {
  const [sh, sm] = (startTime || '00:00').split(':').map(Number);
  if (Number.isNaN(sh) || Number.isNaN(sm)) return 0;
  return Math.floor((sh * 60 + sm + seconds / 60) / (24 * 60));
}
```

`CheckpointEditor` replaces its local `hhmmToSeconds` with `hhmmToElapsedSeconds`, holds a `cutoffDayOffset` in `form`, seeds it from `elapsedSecondsToDayOffset(startTime, existing.cutoffTimeSeconds)` in the effect that loads `existing`, and renders a small day selector beside the cutoff input:

```tsx
  <div className="flex gap-2">
    <input
      type="time"
      value={secondsToHHMM(form.cutoffTimeSeconds)}
      onChange={(e) =>
        setForm((f) => ({
          ...f,
          cutoffTimeSeconds: hhmmToElapsedSeconds(startTime, e.target.value, f.cutoffDayOffset),
        }))
      }
      className="…"
    />
    <select
      value={form.cutoffDayOffset}
      onChange={(e) => {
        const dayOffset = Number(e.target.value);
        setForm((f) => ({
          ...f,
          cutoffDayOffset: dayOffset,
          cutoffTimeSeconds: hhmmToElapsedSeconds(
            startTime, secondsToHHMM(f.cutoffTimeSeconds), dayOffset),
        }));
      }}
      className="…"
    >
      <option value={0}>{t('checkpoint.sameDay')}</option>
      <option value={1}>+1 d</option>
      <option value={2}>+2 d</option>
    </select>
  </div>
```

Add `checkpoint.sameDay` to `ui/client/public/locales/en/activities.json` (or whichever namespace `CheckpointEditor` uses — read its `useTranslation(...)` call) and to the `fr` counterpart.

**Steps:**

- [ ] Create the failing regression test `ui/client/src/utils/dayNight.test.ts`:
  ```ts
  import { describe, it, expect } from 'vitest';
  import { hhmmToElapsedSeconds, elapsedSecondsToDayOffset } from './dayNight';

  describe('hhmmToElapsedSeconds', () => {
    it('handles a same-day cutoff', () => {
      // Start 06:00, cutoff 14:30 -> 8 h 30 min
      expect(hhmmToElapsedSeconds('06:00', '14:30', 0)).toBe(8.5 * 3600);
    });

    it('handles an overnight cutoff on the first night', () => {
      // Start 18:00 Friday, cutoff 04:00 Saturday -> 10 h
      expect(hhmmToElapsedSeconds('18:00', '04:00', 0)).toBe(10 * 3600);
    });

    it('represents a day-2 cutoff instead of wrapping it into day 1', () => {
      // The reported case: start Friday 18:00, official cutoff Saturday 20:00.
      // 26 h elapsed = 93,600 s. Minute-of-day arithmetic alone yields 7,200 s.
      expect(hhmmToElapsedSeconds('18:00', '20:00', 1)).toBe(26 * 3600);
    });

    it('represents a 46 h cutoff (a normal UTMB-scale finish limit)', () => {
      // Start 18:00, cutoff 16:00 two days later.
      expect(hhmmToElapsedSeconds('18:00', '16:00', 1)).toBe(46 * 3600);
    });

    it('returns null for empty or malformed input', () => {
      expect(hhmmToElapsedSeconds('18:00', '')).toBeNull();
      expect(hhmmToElapsedSeconds('18:00', 'not-a-time')).toBeNull();
    });
  });

  describe('elapsedSecondsToDayOffset', () => {
    it('round-trips a day-2 cutoff', () => {
      const seconds = 26 * 3600;
      const offset = elapsedSecondsToDayOffset('18:00', seconds);
      expect(offset).toBe(1);
      expect(hhmmToElapsedSeconds('18:00', '20:00', offset)).toBe(seconds);
    });

    it('reports 0 for a same-day cutoff', () => {
      expect(elapsedSecondsToDayOffset('06:00', 8.5 * 3600)).toBe(0);
    });
  });
  ```
- [ ] Run it and watch it fail: `cd ui/client && npx vitest run src/utils/dayNight.test.ts`
  Expected failure: `Failed to resolve import` for both new exports (they do not exist). Add stubs delegating to the current minute-of-day logic and re-run to see `expected 7200 to be 93600` on the day-2 test. Record that before implementing.
- [ ] Implement `hhmmToElapsedSeconds` and `elapsedSecondsToDayOffset` in `dayNight.ts`
- [ ] Replace `CheckpointEditor`'s local `hhmmToSeconds` with the shared function, add `cutoffDayOffset` to `form`, seed it from `existing`, and add the day selector to the cutoff input
- [ ] Add the `checkpoint.sameDay` key to the `en` and `fr` locale files for the namespace `CheckpointEditor` uses
- [ ] Run the test and watch all seven pass: `cd ui/client && npx vitest run src/utils/dayNight.test.ts`
- [ ] Run the client checks: `cd ui/client && npm run test && npm run lint && npm run build && npm run e2e`
- [ ] Commit:
  ```bash
  git add ui/client/src/utils/dayNight.ts ui/client/src/utils/dayNight.test.ts ui/client/src/components/race-plan/CheckpointEditor.tsx ui/client/public/locales/
  git commit -m "fix(client): represent checkpoint cutoffs beyond the first 24 hours

  hhmmToSeconds converted the wall-clock cutoff using minute-of-day arithmetic
  with a single +24h wrap, so the result was always under 24 h and every day-2+
  cutoff collapsed into day 1. In an ultra-trail planner where 30-46 h cutoffs are
  normal, a Friday-18:00 start with a Saturday-20:00 cutoff (26 h = 93,600 s) was
  saved as 7,200 s, CheckpointTimeline's isLate test then flagged that checkpoint
  and every later one red, and the printed plan showed a cutoff 24 h early. The
  display side wraps at 24 h too, so the value could not be recovered.

  Adds hhmmToElapsedSeconds/elapsedSecondsToDayOffset carrying an explicit day
  offset, and a day selector beside the cutoff input.

  Closes #121"
  ```

---

### Task 28: The upload loop indexes a stale array while remove buttons stay enabled

**Issues:** #122

**Files:**
- Modify `ui/client/src/pages/UploadPage.tsx:96`–`:128` (`handleUploadAll`), `:279` (the remove button)
- Test `ui/client/e2e/upload.spec.ts` (append)

**Root cause:** `handleUploadAll` iterates `for (let i = 0; i < files.length; i++)` over the `files` array captured at render time and writes results back positionally with `prev.map((f, idx) => idx === i ? … : f)`. Meanwhile the per-row remove button is rendered for any entry still in `pending` state and is never disabled while `isUploading` is true (only "Clear all" is hidden). Removing an entry mid-run shifts every later index, so the loop both uploads the removed file — read from the stale closure array — and applies the resulting status and `activityId` to a different row: with A, B, C queued, removing B during A's upload creates an unwanted activity on the server from B, marks C as `processing` with B's `activityId`, never uploads C, and points C's "View" button at B's activity.

**Fix approach:** Key the queue by a stable per-entry id rather than by array position, read the live list through a ref, and disable the remove button while an upload is running.

```tsx
// UploadPage.tsx — the queued-file entry gains an id
interface QueuedFile {
  id: string;
  file: File;
  status: UploadStatus;
  activityId?: string;
  error?: string;
}

// addFiles assigns one:
//   { id: crypto.randomUUID(), file, status: 'pending' }

  // A ref mirroring `files`, so the async loop reads the LIVE list rather than
  // the array captured when handleUploadAll was created.
  const filesRef = useRef<QueuedFile[]>(files);
  useEffect(() => { filesRef.current = files; }, [files]);

  const handleUploadAll = async () => {
    if (isUploading) return;

    const queue = files.filter((f) => f.status === 'pending').map((f) => f.id);
    if (queue.length === 0) return;

    setIsUploading(true);

    for (const id of queue) {
      // Re-read from the live list: the entry may have been removed since the
      // queue was built, and its position will have shifted regardless.
      const entry = filesRef.current.find((f) => f.id === id);
      if (!entry || entry.status !== 'pending') continue;

      setFiles((prev) => prev.map((f) => (f.id === id ? { ...f, status: 'uploading' } : f)));

      try {
        const result = await api.uploadGpx(entry.file, activityType);
        setFiles((prev) =>
          prev.map((f) => (f.id === id ? { ...f, status: 'processing', activityId: result.id } : f)),
        );
      } catch (err) {
        setFiles((prev) =>
          prev.map((f) =>
            f.id === id
              ? { ...f, status: 'error', error: err instanceof Error ? err.message : t('uploadFailed') }
              : f,
          ),
        );
      }
    }

    setIsUploading(false);
    queryClient.invalidateQueries({ queryKey: ['activities'] });
    queryClient.invalidateQueries({ queryKey: ['dashboard'] });
  };
```

`removeFile` takes the id instead of the index, and the button is disabled during the run:

```tsx
  const removeFile = (id: string) => setFiles((prev) => prev.filter((f) => f.id !== id));

  // …in the row render:
  <button
    onClick={() => removeFile(f.id)}
    disabled={isUploading}
    className="… disabled:opacity-40 disabled:cursor-not-allowed"
    aria-label={t('removeFile')}
  >
    ✕
  </button>
```

Disabling is belt-and-braces: the id-keyed loop already skips a removed entry, but leaving the control live during an upload invites the user to try.

**Steps:**

- [ ] Append the failing regression test to `ui/client/e2e/upload.spec.ts`:
  ```ts
  test('removing a queued file mid-upload does not upload it or mislabel the next row', async ({ page }) => {
    await mockAllApi(page);

    const uploadedNames: string[] = [];
    let releaseFirst: () => void = () => {};
    const firstInFlight = new Promise<void>((r) => { releaseFirst = r; });

    await page.route('**/api/activities/upload', async (route) => {
      const post = route.request().postData() ?? '';
      const name = /filename="([^"]+)"/.exec(post)?.[1] ?? 'unknown';
      uploadedNames.push(name);
      if (uploadedNames.length === 1) await firstInFlight;   // hold A in flight
      await route.fulfill({ json: { id: `activity-${uploadedNames.length}`, name, status: 'Pending' } });
    });

    await page.goto('/upload');
    await page.setInputFiles('input[type="file"]', ['a.gpx', 'b.gpx', 'c.gpx'].map((n) => ({
      name: n,
      mimeType: 'application/gpx+xml',
      buffer: Buffer.from('<gpx version="1.1"><trk><trkseg/></trk></gpx>'),
    })));

    await page.getByRole('button', { name: /upload/i }).click();

    // While a.gpx is in flight, the user changes their mind about b.gpx.
    const removeB = page.getByRole('button', { name: /remove/i }).nth(1);
    if (await removeB.isEnabled()) await removeB.click();

    releaseFirst();
    await expect.poll(() => uploadedNames.length, { timeout: 10_000 }).toBeGreaterThanOrEqual(2);
    await page.waitForTimeout(500);

    // b.gpx must never reach the server, and c.gpx must.
    expect(uploadedNames).not.toContain('b.gpx');
    expect(uploadedNames).toContain('c.gpx');
  });
  ```
  Read `ui/client/e2e/upload.spec.ts` and `UploadPage.tsx` first for the real file-input selector, the real upload-button label and the remove button's accessible name; the current button renders a bare `✕` with no `aria-label`, so **add the `aria-label` as part of this task** (it is also an accessibility fix the test depends on).
- [ ] Run it and watch it fail: `cd ui/client && npm run build && npx playwright test e2e/upload.spec.ts --project=desktop -g "mid-upload"`
  Expected failure: `expect(uploadedNames).not.toContain('b.gpx')` fails — the loop read `files[1]` from the stale closure array and uploaded the removed file.
- [ ] Add `id` to the queued-file entry type and assign one in `addFiles`
- [ ] Add `filesRef` + its sync effect, rewrite `handleUploadAll` to iterate ids, and change `removeFile` to take an id
- [ ] Add `disabled={isUploading}` and the `aria-label` to the remove button
- [ ] Run the test and watch it pass: `cd ui/client && npm run build && npx playwright test e2e/upload.spec.ts --project=desktop -g "mid-upload"`
- [ ] Run the client checks: `cd ui/client && npm run test && npm run lint && npm run build && npm run e2e`
- [ ] Commit:
  ```bash
  git add ui/client/src/pages/UploadPage.tsx ui/client/e2e/upload.spec.ts
  git commit -m "fix(client): key the upload queue by id instead of array position

  handleUploadAll iterated the files array captured at render time and wrote
  results back positionally, while the per-row remove button stayed enabled
  throughout the run. Removing an entry mid-upload shifted every later index, so
  with A, B, C queued and B removed during A's upload the loop uploaded B anyway
  — creating an unwanted activity on the server — then marked C as 'processing'
  with B's activityId, never uploaded C, and pointed C's View button at B.

  Iterates a queue of stable ids read through a ref against the live list, and
  disables the remove button (now with an aria-label) while an upload runs.

  Closes #122"
  ```

---

## Wave 7 — Statistics polish & low-severity cleanup

### Task 29: Culture-sensitive timestamp formatting in the CLI→API contract

**Issues:** #87

**Files:**
- Modify `cli/src/GpxAnalyzer.Cli.Core/Output/SummaryMapper.cs:25`–`:26`, `:60`–`:61`, `:151`–`:152`
- Modify `cli/src/GpxAnalyzer.Cli.Core/Output/JsonFormatter.cs:29`–`:30`, `:69`–`:70`, `:137`–`:138`
- Test `cli/tests/GpxAnalyzer.Cli.Tests/Output/SummaryMapperTests.cs` (append)

**Root cause:** These sites format `DateTime` with a custom format string and no `IFormatProvider`, so `CurrentCulture` is used. In a .NET custom date/time format string `:` is **not** a literal — it is the time-separator placeholder replaced by `DateTimeFormatInfo.TimeSeparator`. `SummaryMapper` lives in `GpxAnalyzer.Cli.Core` but is called from `ui/api` (`GpxAnalysisService.cs:81`), and the ASP.NET Core host sets no `InvariantGlobalization` and installs no request-localization middleware, so `CurrentCulture` is the OS culture. Under `fi-FI` or `da-DK` (separator `.`) it emits `2024-06-15T08.00.00Z`; `ActivityProcessingService`'s `DateTime.TryParse` then returns false, `Activity.StartTime` is never assigned, and the activity is persisted with a default date while the AI prompt receives a malformed timestamp. `JsonFormatter` has the same pattern, masked in the CLI exe only because `GpxAnalyzer.Cli.csproj` sets `InvariantGlobalization=true` — a setting the library does not share with its other consumer.

**Fix approach:** Pass `CultureInfo.InvariantCulture` at every site. Route them through one helper so the pattern cannot regress.

```csharp
// SummaryMapper.cs — add near the other private helpers
    /// <summary>
    /// Formats an instant for the CLI JSON contract. ':' is the time-separator
    /// PLACEHOLDER in a custom format string, so without InvariantCulture this
    /// emits "2024-06-15T08.00.00Z" under fi-FI / da-DK — and the API, which calls
    /// this library with the OS culture, then fails to parse its own output.
    /// </summary>
    private static string ToIso(DateTime t) =>
        t.ToString("yyyy-MM-ddTHH:mm:ssZ", CultureInfo.InvariantCulture);
```

Replace all six `…ToString("yyyy-MM-ddTHH:mm:ssZ")` calls across the two files with `ToIso(...)` (add the same private helper to `JsonFormatter`, or make one `internal static` helper in `FormatHelpers` and call it from both — prefer the latter). Add `using System.Globalization;` where missing.

**Steps:**

- [ ] Append the failing regression test to `cli/tests/GpxAnalyzer.Cli.Tests/Output/SummaryMapperTests.cs`:
  ```csharp
      [Theory]
      [InlineData("fi-FI")]
      [InlineData("da-DK")]
      public void ToGpxStats_UnderACultureWithANonColonTimeSeparator_EmitsIsoTimestamps(string culture)
      {
          var previous = System.Globalization.CultureInfo.CurrentCulture;
          try
          {
              System.Globalization.CultureInfo.CurrentCulture =
                  new System.Globalization.CultureInfo(culture);

              var s = new Summary
              {
                  StartTime = new DateTime(2024, 6, 15, 8, 0, 0, DateTimeKind.Utc),
                  EndTime = new DateTime(2024, 6, 15, 11, 30, 0, DateTimeKind.Utc),
              };

              var stats = SummaryMapper.ToGpxStats("track.gpx", s);

              Assert.Equal("2024-06-15T08:00:00Z", stats.StartTime);
              Assert.Equal("2024-06-15T11:30:00Z", stats.EndTime);

              // And the API must be able to parse its own producer's output.
              Assert.True(DateTime.TryParse(stats.StartTime,
                  System.Globalization.CultureInfo.InvariantCulture,
                  System.Globalization.DateTimeStyles.AdjustToUniversal |
                  System.Globalization.DateTimeStyles.AssumeUniversal, out _));
          }
          finally { System.Globalization.CultureInfo.CurrentCulture = previous; }
      }
  ```
- [ ] Run it and watch it fail: `dotnet test cli/tests/GpxAnalyzer.Cli.Tests/ --filter ToGpxStats_UnderACultureWithANonColonTimeSeparator`
  Expected failure: `Assert.Equal() Failure  Expected: 2024-06-15T08:00:00Z  Actual: 2024-06-15T08.00.00Z`
- [ ] Add the shared invariant-ISO helper and route all six sites in `SummaryMapper.cs` and `JsonFormatter.cs` through it
- [ ] Sweep for any other occurrence: `grep -rn 'ToString("yyyy-MM-ddTHH:mm:ssZ")' cli/ ui/` must return nothing (the `GpxWriter` site was fixed in Task 15)
- [ ] Run the test and watch it pass: `dotnet test cli/tests/GpxAnalyzer.Cli.Tests/ --filter ToGpxStats_UnderACultureWithANonColonTimeSeparator`
- [ ] Run the full CLI and API suites (the API consumes `SummaryMapper`):
  ```bash
  dotnet test cli/tests/GpxAnalyzer.Cli.Tests/
  dotnet test ui/api.Tests/GpxAnalyzer.Api.Tests.csproj
  ```
- [ ] Commit:
  ```bash
  git add cli/src/GpxAnalyzer.Cli.Core/Output/ cli/tests/GpxAnalyzer.Cli.Tests/Output/SummaryMapperTests.cs
  git commit -m "fix(cli): format contract timestamps with InvariantCulture

  ':' is the time-separator placeholder in a .NET custom date/time format string,
  not a literal, so 'yyyy-MM-ddTHH:mm:ssZ' with no IFormatProvider emits
  2024-06-15T08.00.00Z under fi-FI or da-DK. SummaryMapper lives in the Core
  library but is called from ui/api, which sets no InvariantGlobalization and no
  request localization, so CurrentCulture is the OS culture: the API produced a
  malformed timestamp, its own DateTime.TryParse then returned false, the activity
  was persisted with a default date and the AI prompt got the malformed string.

  JsonFormatter had the same pattern, masked only by the CLI exe's own
  InvariantGlobalization setting, which the library does not share.

  Closes #87"
  ```

---

### Task 30: Two statistics calculators attribute time and grade to meaningless intervals

**Issues:** #102, #103

**Files:**
- Modify `cli/src/GpxAnalyzer.Cli.Core/Stats/BiometricsCalculator.cs:105`–`:112`
- Modify `cli/src/GpxAnalyzer.Cli.Core/Stats/EffortCalculator.cs:37`, `:132`, `:162`–`:184`
- Test `cli/tests/GpxAnalyzer.Cli.Tests/Stats/BiometricsCalculatorTests.cs` (append), `cli/tests/GpxAnalyzer.Cli.Tests/Stats/EffortCalculatorTests.cs` (append)

**Root cause:** Two independent-but-analogous defects: both attribute a quantity to an interval too small or too large to carry it.

- **#102** `ComputeHRZones` attributes `points[i].Time - points[i-1].Time` in full to whichever zone `points[i]`'s HR falls into, with no upper bound on `dt`. Every other stage treats an interval longer than `ElevationSmoother.GapThreshold` (10 min) as a discontinuity — `EnrichPoints` zeroes distance and speed, the smoothers split their windows at `GapIndices` — so the zone accumulator is the only consumer of raw `dt`. A 90 min ride with a 25 min tunnel gap credits Z3 with the full 25 min from the single sample after it, and the five zone durations then sum to more than the ride's moving time in a payload that presents them as a breakdown of the session.
- **#103** `ComputeTerrainDifficulty` (and identically `ToblerTime` line 37 and `EquivalentFlatDistance` line 132) computes `grade = dEle / dist` after rejecting only segments with `dist < 0.1` — a 10 cm floor. At that scale the denominator is GPS jitter and the numerator independent elevation noise, so the quotient is meaningless. `maxGrade = grades.Max()` propagates the worst such quotient into `MaxGradePercent`, and `avgGrade = grades.Average()` is an *unweighted* mean over segments, so the many near-zero-length samples recorded while the user is nearly stationary outweigh the real terrain. Tobler and Minetti are protected by their own saturation; the difficulty score is not — `normMaxGrade` saturates at 1.0 and contributes its full 0.25 weight, so a flat 1 Hz road run with a 0.3 m / 0.5 m sample at a traffic light reports `max_grade_percent = 166.7` and grades "Moderate" instead of "Easy".

**Fix approach:**

```csharp
// BiometricsCalculator.ComputeHRZones — #102
            var dt = points[i].Time - points[i - 1].Time;
            // Cap at the pipeline's recording-gap threshold: crediting a single
            // post-gap sample with the whole gap makes the five zone durations sum
            // to more than the session's own moving time.
            if (dt <= TimeSpan.Zero || dt > Elevation.ElevationSmoother.GapThreshold) continue;
```

```csharp
// EffortCalculator — #103
    /// <summary>
    /// Shortest segment whose grade is meaningful. Below a few metres the
    /// denominator is GPS jitter and the numerator independent elevation noise,
    /// so the quotient is arbitrary — and it propagated straight into
    /// MaxGradePercent and the composite difficulty score.
    /// </summary>
    private const double MinGradeSegmentM = 5.0;

    // in ComputeTerrainDifficulty, ToblerTime and EquivalentFlatDistance:
            var dist = points[i].DistFromPrev;
            if (dist < MinGradeSegmentM) continue;
```

and make the average distance-weighted, so a stationary stretch cannot outvote the terrain:

```csharp
        var totalSegmentDist = segmentDists.Sum();
        // Distance-weighted: an unweighted mean lets the many near-stationary
        // samples dominate the few long segments that carry the real terrain.
        var avgGrade = totalSegmentDist > 0
            ? grades.Zip(segmentDists, (g, d) => g * d).Sum() / totalSegmentDist
            : 0;
        var maxGrade = grades.Max();
```

Update the variance calculation below to use the same weighted mean.

**Steps:**

- [ ] Append the failing regression test to `cli/tests/GpxAnalyzer.Cli.Tests/Stats/BiometricsCalculatorTests.cs`:
  ```csharp
      [Fact]
      public void ComputeHRZones_RecordingGap_DoesNotCreditTheGapToAZone()
      {
          var t0 = DateTime.Parse("2024-01-01T10:00:00Z").ToUniversalTime();
          var points = new List<TrackPoint>();

          // 30 min of riding at 120 bpm, one sample every 10 s
          for (int i = 0; i < 180; i++)
              points.Add(new TrackPoint { Lat = 48.0, Lon = 2.0, Time = t0.AddSeconds(i * 10), HeartRate = 120 });

          // 25 min tunnel: no samples. First sample after it reads 145 bpm.
          var resume = points[^1].Time.AddMinutes(25);
          points.Add(new TrackPoint { Lat = 48.0, Lon = 2.0, Time = resume, HeartRate = 145 });
          for (int i = 1; i < 180; i++)
              points.Add(new TrackPoint { Lat = 48.0, Lon = 2.0, Time = resume.AddSeconds(i * 10), HeartRate = 120 });

          var result = BiometricsCalculator.Compute(points, new BiometricsConfig { MaxHR = 190 });

          Assert.NotNull(result.HeartRate);
          var zoneTotal = result.HeartRate!.Zones.Aggregate(TimeSpan.Zero, (a, z) => a + z.Duration);
          var elapsed = points[^1].Time - points[0].Time;

          // The gap must not be attributed to any zone, so the zones cannot sum
          // to more than the recorded time minus the gap.
          Assert.True(zoneTotal <= elapsed - TimeSpan.FromMinutes(20),
              $"zone durations sum to {zoneTotal} over an elapsed {elapsed} that includes a 25 min gap");
      }
  ```
  Check `HeartRateResult`'s zone-collection property name against `BiometricsCalculator.cs` before running.
- [ ] Append the failing regression test to `cli/tests/GpxAnalyzer.Cli.Tests/Stats/EffortCalculatorTests.cs`:
  ```csharp
      [Fact]
      public void ComputeTerrainDifficulty_FlatRunWithASubMetreJitterSegment_DoesNotReportAnImpossibleGrade()
      {
          var t0 = DateTime.Parse("2024-01-01T10:00:00Z").ToUniversalTime();
          var points = new List<TrackPoint>();

          // 1 Hz flat road run: ~3 m per second, elevation noise of +/- 0.3 m
          for (int i = 0; i < 600; i++)
              points.Add(new TrackPoint
              {
                  Lat = 48.0 + i * 0.000027, Lon = 2.0,
                  Ele = 35 + (i % 3) * 0.3,
                  Time = t0.AddSeconds(i),
                  DistFromPrev = i == 0 ? 0 : 3.0,
              });

          // At a traffic light two consecutive samples are 0.3 m apart with a
          // residual 0.5 m elevation difference -> grade = 167%.
          points[300].DistFromPrev = 0.3;
          points[300].Ele = points[299].Ele + 0.5;

          var s = new Summary
          {
              TotalDistance = 1800,
              TotalTime = TimeSpan.FromSeconds(600),
              MovingTime = TimeSpan.FromSeconds(600),
          };

          var effort = EffortCalculator.ComputeAll(points, s);

          Assert.True(effort.TerrainDifficulty.MaxGradePercent < 30,
              $"a flat road run reported max_grade_percent = {effort.TerrainDifficulty.MaxGradePercent:F1}");
          Assert.Equal("Easy", effort.TerrainDifficulty.Grade);
      }
  ```
  Check `EffortResult`'s property names (`TerrainDifficulty`, `MaxGradePercent`, `Grade`) and `ComputeAll`'s signature against `EffortCalculator.cs` before running.
- [ ] Run them and watch them fail:
  ```bash
  dotnet test cli/tests/GpxAnalyzer.Cli.Tests/ --filter "ComputeHRZones_RecordingGap|ComputeTerrainDifficulty_FlatRunWithASubMetreJitterSegment"
  ```
  Expected failures: `zone durations sum to 01:00:00 over an elapsed 01:25:00 that includes a 25 min gap` (#102); `a flat road run reported max_grade_percent = 166.7` (#103).
- [ ] Cap `dt` at `GapThreshold` in `ComputeHRZones`
- [ ] Raise the grade floor to `MinGradeSegmentM` in all three `EffortCalculator` sites and make `avgGrade` (and the variance) distance-weighted
- [ ] Run the tests and watch both pass
- [ ] Run the full CLI suite: `dotnet test cli/tests/GpxAnalyzer.Cli.Tests/`
- [ ] Commit:
  ```bash
  git add cli/src/GpxAnalyzer.Cli.Core/Stats/BiometricsCalculator.cs cli/src/GpxAnalyzer.Cli.Core/Stats/EffortCalculator.cs cli/tests/GpxAnalyzer.Cli.Tests/Stats/
  git commit -m "fix(cli): bound the intervals HR zones and grades are computed over

  ComputeHRZones attributed the full inter-sample interval to whichever zone the
  later sample fell in, with no upper bound. Every other stage treats an interval
  over 10 min as a discontinuity, so a 25 min tunnel gap credited Z3 with 25 min
  of 'Tempo' work from one sample and the five zone durations summed to more than
  the ride's own moving time — in a payload presenting them as a breakdown of the
  session.

  ComputeTerrainDifficulty guarded grades only at 0.1 m, where the denominator is
  GPS jitter and the numerator independent elevation noise. Two samples 0.3 m
  apart with a 0.5 m residual gave a 167% grade, which propagated into
  max_grade_percent and saturated normMaxGrade's full 0.25 weight, grading a flat
  road run 'Moderate'. Raises the floor to 5 m and makes the average
  distance-weighted so near-stationary samples cannot outvote the terrain.

  Closes #102
  Closes #103"
  ```

---

### Task 31: Low-severity detector and heuristic cleanup

**Issues:** #101, #104, #105, #106

**Files:**
- Modify `cli/src/GpxAnalyzer.Cli.Core/Stats/ActivityTypeDetector.cs:175` and its `BackyardLapTolerance` constant
- Modify `cli/src/GpxAnalyzer.Cli.Core/Anomaly/Detectors/ElevationAnomalyDetector.cs:50`
- Modify `cli/src/GpxAnalyzer.Cli.Core/Anomaly/AnomalyDetector.cs:41`–`:42`
- Modify `cli/src/GpxAnalyzer.Cli.Core/Anomaly/Detectors/BiometricAnomalyDetector.cs:19`
- Modify `cli/src/GpxAnalyzer.Cli.Core/Dem/DemSource.cs:168`–`:188` and the now-dead `CrossTileElevation`
- Test `cli/tests/GpxAnalyzer.Cli.Tests/Stats/ActivityTypeDetectorTests.cs` (append), `cli/tests/GpxAnalyzer.Cli.Tests/Anomaly/AnomalyDetectorTests.cs` (new)

**Root causes and fixes** — four small, independent changes, grouped because each is a few lines and all are bounded heuristics:

- **#101** `BackyardLapTolerance` is `0.5` and the guard is `Math.Abs(estimatedLaps - Math.Round(estimatedLaps)) > BackyardLapTolerance`. The distance from any real number to its nearest integer is at most 0.5, and at exactly 0.5 `Math.Round` uses banker's rounding so the result *is* 0.5, never greater — the condition can never be true and the check never rejects anything. A 30 km interval workout (4.474 laps) passes and is labelled sub-type "backyard", which `ui/api` surfaces in the UI. **Fix:** change the constant to `0.15` (a meaningful fraction of a 6.706 km lap, ~1 km).
- **#104** `DetectRaw` reports the transition as `StartIndex = i-1, EndIndex = i`, and `AnomalyCorrector.CorrectElevationSpike` treats the whole inclusive range as bad and interpolates every point in it — so the healthy point *before* the spike is always rewritten. A single-point raw spike also yields two overlapping anomalies (`[i-1,i]` and `[i,i+1]`), applied in sequence with the second reading elevations the first modified, so the healthy point after is rewritten too. And because detection uses raw values while correction writes the processed ones, it fires even when DEM correction has already replaced the elevations with accurate SRTM values. **Fix:** flag only the spiking point (`StartIndex = EndIndex = i`), and skip `DetectRaw` when DEM correction ran:
  ```csharp
  // ElevationAnomalyDetector.DetectRaw
                  anomalies.Add(new TrackAnomaly
                  {
                      Type = AnomalyType.ElevationSpike,
                      // Flag only the spiking point. The previous point is healthy,
                      // and CorrectElevationSpike overwrites every index in the
                      // inclusive range with an interpolated value.
                      StartIndex = i,
                      EndIndex = i,
                      // …the rest unchanged
                  });

  // AnomalyDetector.Detect
          // Raw elevation anomalies (pre-smoothing data). Skipped when DEM
          // correction ran: the spike no longer exists in the processed
          // elevations, so "correcting" it would overwrite accurate SRTM data.
          if (rawElevations != null && !hasDemCorrection)
              anomalies.AddRange(ElevationAnomalyDetector.DetectRaw(points, rawElevations, cfg));
  ```
- **#105** `BiometricAnomalyDetector.Detect` decides whether the file has HR by scanning only points 0..99 and returns an empty list when none carries a value, skipping both `DetectHrOutOfRange` and `DetectHrSpike` for the whole track. Every other detector scans the full list. A 5,000-point ride whose chest strap pairs after two minutes reports no HR anomaly at all, and `--fix-anomalies` leaves its 250 bpm dropouts in place. **Fix:**
  ```csharp
          // Scan the whole list: a chest strap that pairs a couple of minutes in
          // is common, and every other detector covers the full track.
          bool hasHr = points.Any(p => p.HeartRate.HasValue);
          if (!hasHr) return anomalies;
  ```
- **#106** `CollectTileKeys` adds the south, east and south-east neighbour tiles for any point within ~92 m of a tile's south or east edge, and `PreloadAsync` downloads and fully loads them. Those neighbours exist only to serve `CrossTileElevation`, which is unreachable: `GetElevation` returns early when `row > GridSize-1` or `col > GridSize-1`, so `floor(row)` can equal `GridSize-1` only when `row` is exactly `GridSize-1`, while `needSouth`/`needEast` additionally require `row > r0` / `col > c0` — the conditions can never both hold. SRTM tiles duplicate their shared edge row and column, so a point is always fully interpolable inside its own tile. The neighbours are pure network and memory cost, and they count toward the `--dem-max-memory` check: a ride along a tile boundary can abort with "requires ~103 MB (4 tiles)" when only ~52 MB would ever be read. **Fix:** delete the three `seen.Add(...)` neighbour lines, the `nearSouth`/`nearEast` locals, the `BoundaryThreshold` constant, and the unreachable `CrossTileElevation` method plus its call site.

**Steps:**

- [ ] Append the failing regression test to `cli/tests/GpxAnalyzer.Cli.Tests/Stats/ActivityTypeDetectorTests.cs`:
  ```csharp
      [Fact]
      public void DetectFromStats_IntervalWorkoutOnAnHourlyCadence_IsNotLabelledBackyard()
      {
          // 30 km at 10 km/h with three ~10 min rests spaced almost exactly 60 min
          // apart: a common interval session. 30 / 6.706 = 4.474 laps, which is
          // nowhere near a whole number of backyard laps.
          var stats = BuildBackyardShapedStats(totalDistanceKm: 30.0, stopIntervalMinutes: 60);

          var detection = ActivityTypeDetector.DetectFromStats(stats);

          Assert.NotEqual("backyard", detection.SubType);
      }

      [Fact]
      public void DetectFromStats_RealBackyardUltra_IsStillLabelled()
      {
          // 6 laps of 6.706 km = 40.236 km on an hourly cadence.
          var stats = BuildBackyardShapedStats(totalDistanceKm: 6 * 6.706, stopIntervalMinutes: 60);
          Assert.Equal("backyard", ActivityTypeDetector.DetectFromStats(stats).SubType);
      }
  ```
  Write the `BuildBackyardShapedStats(double totalDistanceKm, int stopIntervalMinutes)` helper in the test class, producing a `GpxStats` whose `Stops` are spaced on the given cadence and whose other fields satisfy the detector's earlier gates. Read `ActivityTypeDetector.DetectFromStats` and the surrounding backyard checks (`BackyardMinIntervalMin`, `BackyardMaxIntervalMin`, `BackyardMaxCv`) first so the fixture actually reaches line 175 — otherwise the test passes for the wrong reason.
- [ ] Create `cli/tests/GpxAnalyzer.Cli.Tests/Anomaly/AnomalyDetectorTests.cs` with the #104 and #105 tests:
  ```csharp
  using GpxAnalyzer.Cli.Core.Anomaly;
  using GpxAnalyzer.Cli.Core.Anomaly.Detectors;
  using GpxAnalyzer.Cli.Core.Gpx;

  namespace GpxAnalyzer.Cli.Tests.Anomaly;

  public class AnomalyDetectorTests
  {
      [Fact]
      public void DetectRaw_SinglePointSpike_FlagsOnlyTheSpikingPoint()
      {
          var t0 = DateTime.Parse("2024-01-01T10:00:00Z").ToUniversalTime();
          var points = new List<TrackPoint>();
          for (int i = 0; i < 5; i++)
              points.Add(new TrackPoint
              {
                  Lat = 48.0 + i * 0.0001, Lon = 2.0, Ele = 100,
                  Time = t0.AddSeconds(i * 5), DistFromPrev = i == 0 ? 0 : 11,
              });

          var raw = new List<double> { 100, 900, 102, 103, 104 };  // spike at index 1

          var anomalies = ElevationAnomalyDetector.DetectRaw(points, raw, AnomalyConfig.Default());

          Assert.NotEmpty(anomalies);
          foreach (var a in anomalies)
          {
              // CorrectElevationSpike rewrites every index in the inclusive range,
              // so a range covering a healthy neighbour destroys good data.
              Assert.Equal(a.StartIndex, a.EndIndex);
              Assert.Equal(1, a.StartIndex);
          }
      }

      [Fact]
      public void Detect_WithDemCorrectionApplied_DoesNotEmitRawElevationSpikes()
      {
          var t0 = DateTime.Parse("2024-01-01T10:00:00Z").ToUniversalTime();
          var points = new List<TrackPoint>();
          for (int i = 0; i < 5; i++)
              points.Add(new TrackPoint
              {
                  Lat = 48.0 + i * 0.0001, Lon = 2.0, Ele = 512 + i,   // accurate SRTM values
                  Time = t0.AddSeconds(i * 5), DistFromPrev = i == 0 ? 0 : 11,
              });

          var raw = new List<double> { 100, 900, 102, 103, 104 };

          var report = AnomalyDetector.Detect(points, [], 7.0, 44, 0,
              hasDemCorrection: true, rawElevations: raw, cfg: AnomalyConfig.Default());

          Assert.DoesNotContain(report.Anomalies, a => a.Type == AnomalyType.ElevationSpike);
      }

      [Fact]
      public void BiometricDetect_HeartRateStartingAfterPoint100_StillDetectsOutOfRange()
      {
          var t0 = DateTime.Parse("2024-01-01T10:00:00Z").ToUniversalTime();
          var points = new List<TrackPoint>();

          // Chest strap pairs after 120 points (two minutes at 1 Hz)
          for (int i = 0; i < 120; i++)
              points.Add(new TrackPoint { Lat = 48.0, Lon = 2.0, Time = t0.AddSeconds(i) });
          for (int i = 120; i < 400; i++)
              points.Add(new TrackPoint
              {
                  Lat = 48.0, Lon = 2.0, Time = t0.AddSeconds(i),
                  HeartRate = i is >= 200 and < 210 ? 250 : 140,   // a dropout run
              });

          var anomalies = BiometricAnomalyDetector.Detect(points, AnomalyConfig.Default());

          Assert.Contains(anomalies, a => a.Type == AnomalyType.HeartRateOutOfRange);
      }
  }
  ```
  Check `AnomalyDetector.Detect`'s parameter order and `AnomalyConfig.Default()`'s `HrMaxBpm` before running, so the 250 bpm value is genuinely out of range.
- [ ] Run them and watch them fail:
  ```bash
  dotnet test cli/tests/GpxAnalyzer.Cli.Tests/ --filter "ActivityTypeDetectorTests|AnomalyDetectorTests"
  ```
  Expected failures: `Assert.NotEqual() Failure  Expected: Not "backyard"` (#101); `Assert.Equal() Failure  Expected: 1  Actual: 0` on `a.StartIndex` (#104); `Assert.DoesNotContain() Failure` (#104's DEM half); `Assert.Contains() Failure: Collection was empty` (#105).
- [ ] Change `BackyardLapTolerance` from `0.5` to `0.15`
- [ ] Narrow `DetectRaw`'s reported range to `[i, i]` and gate the `DetectRaw` call in `AnomalyDetector.Detect` on `!hasDemCorrection`
- [ ] Replace the 100-point HR probe with `points.Any(p => p.HeartRate.HasValue)`
- [ ] Delete the neighbour-tile collection from `CollectTileKeys` (the three `seen.Add`, the two locals, `BoundaryThreshold`) and remove the unreachable `CrossTileElevation` method and its call site in `GetElevation`
- [ ] Run the tests and watch them pass: `dotnet test cli/tests/GpxAnalyzer.Cli.Tests/ --filter "ActivityTypeDetectorTests|AnomalyDetectorTests"`
- [ ] Run the full CLI suite: `dotnet test cli/tests/GpxAnalyzer.Cli.Tests/`
- [ ] Verify #106 by hand against a real boundary track — run `analyze --dem-auto-download true --dem-max-memory 64` on a track crossing a tile edge and confirm it no longer aborts, and that the elevation values are unchanged from before the edit
- [ ] Commit as four separate commits so each issue closes cleanly:
  ```bash
  git add cli/src/GpxAnalyzer.Cli.Core/Stats/ActivityTypeDetector.cs cli/tests/GpxAnalyzer.Cli.Tests/Stats/ActivityTypeDetectorTests.cs
  git commit -m "fix(cli): give the backyard lap-distance check a meaningful tolerance

  BackyardLapTolerance was 0.5 and the guard compared it against the distance
  from estimatedLaps to its nearest integer — which is at most 0.5 by definition,
  and exactly 0.5 under banker's rounding, never greater. The check could never
  reject anything, leaving 'estimatedLaps < 3' as the only distance constraint,
  so a 30 km interval run on an hourly cadence (4.474 laps) was labelled sub-type
  'backyard' and surfaced as a backyard ultra in the UI. 0.15 is ~1 km of a
  6.706 km lap.

  Closes #101"

  git add cli/src/GpxAnalyzer.Cli.Core/Anomaly/Detectors/ElevationAnomalyDetector.cs cli/src/GpxAnalyzer.Cli.Core/Anomaly/AnomalyDetector.cs cli/tests/GpxAnalyzer.Cli.Tests/Anomaly/AnomalyDetectorTests.cs
  git commit -m "fix(cli): flag only the spiking point, and skip raw spikes after DEM correction

  DetectRaw reported the transition as [i-1, i] and CorrectElevationSpike
  interpolates every index in that inclusive range, so the healthy point before
  the spike was always rewritten — and a single-point spike produced two
  overlapping anomalies applied in sequence, taking out the point after it too.
  Detection also ran on raw elevations while correction writes the processed
  ones, so with --dem-auto it overwrote accurate SRTM values for a spike that no
  longer existed in the data being corrected.

  Closes #104"

  git add cli/src/GpxAnalyzer.Cli.Core/Anomaly/Detectors/BiometricAnomalyDetector.cs
  git commit -m "fix(cli): scan the whole track for heart-rate presence

  Detect probed only points 0..99 and returned an empty list when none carried a
  HeartRate, skipping DetectHrOutOfRange and DetectHrSpike for the entire file.
  A 5,000-point ride whose chest strap pairs after two minutes reported no HR
  anomaly at all and --fix-anomalies left its 250 bpm dropouts in place, while
  the same file with the strap paired from the start reported them. Every other
  detector already scans the full list.

  Closes #105"

  git add cli/src/GpxAnalyzer.Cli.Core/Dem/DemSource.cs
  git commit -m "fix(cli): stop downloading DEM neighbour tiles for an unreachable path

  CollectTileKeys added the south, east and south-east neighbours for any point
  within ~92 m of a tile edge, and PreloadAsync downloaded and fully loaded them.
  They existed only to serve CrossTileElevation, which cannot be reached:
  GetElevation returns early when row or col exceeds GridSize-1, so floor(row)
  can equal GridSize-1 only at exactly GridSize-1, while needSouth/needEast
  require row > r0 / col > c0 — the conditions are mutually exclusive. SRTM tiles
  duplicate their shared edge row and column, so a point is always fully
  interpolable inside its own tile.

  The phantom tiles were pure network and memory cost and counted toward
  --dem-max-memory, so a ride along a tile boundary could abort with 'requires
  ~103 MB (4 tiles)' when only ~52 MB would ever be read.

  Closes #106"
  ```

---

## Deferred / rejected

Four findings from the same audit were adversarially **refuted** and are deliberately not in this plan. They are recorded here with the reasoning so they are not re-litigated. None has a GitHub issue.

### 1. Service-worker `CacheFirst` (7 days) serving pre-reanalysis track/profile/splits — `ui/client/vite.config.ts:67`

**Refuted.** The route in question never matches any request, so it cannot serve stale data. `vite.config.ts:67` uses an anchored path-only regex (`/^\/api\/activities\/[^/]+\/(track|profile|splits)$/`), emitted verbatim into the generated service worker. Workbox 7.4.0 routes a `RegExp` capture to `RegExpRoute`, whose matcher tests the **full href**, not the pathname — so the `^\/api\/` anchor can never match a URL that begins with a scheme. Verified empirically against `localhost:8081`, `localhost:4173` and an https host: `exec(url.href)` returns null in every case (while `exec(url.pathname)` would match). With no matching route Workbox's `handleRequest` returns undefined, `respondWith` is never called, and the request goes to the network normally; `api-geodata-cache` is never created, written or read.

Two supporting claims were also wrong: React Query invalidation **does** cover the geodata queries (`useProfile`/`useTrack`/`useSplits` use keys `['activity', id, …]` and TanStack matches by key prefix, so `invalidateQueries({queryKey:['activity', id]})` invalidates all three), and `fetchJson`'s `cache: 'no-cache'` therefore does reach the network on every refetch.

**Two notes worth carrying forward, as separate concerns rather than as this bug:**
- **Active defect, not filed:** all six `runtimeCaching` rules are dead for the same anchoring reason, so the offline API caching documented in CLAUDE.md does not work at all. Worth its own issue if offline support matters.
- **Latent trap:** if someone fixes the pattern (drops the `^`, or switches to a `({url}) => url.pathname…` callback) *without* adding cache invalidation on reanalyze/fix-anomalies, the reviewer's scenario becomes real at that moment — nothing versions or busts `api-geodata-cache`.

### 2. `ValidateTile` samples only the NW–SE diagonal — `cli/src/GpxAnalyzer.Cli.Core/Dem/DemSource.cs:313`

**Refuted.** The arithmetic observation is correct — for `GridSize` 1201 and 3601 all 101 sampled indices satisfy `row == col`, so the probe is a corner-to-corner transect rather than the intended ~1% sample — but the bug argued from it is not reachable, and the stated failure scenario is self-refuting. The premise was "the corners hold valid land while the diagonal runs over water", yet index 0 **is** the NW corner and is the *first* sample, so any tile with a non-void NW corner returns true immediately; only the NE and SW corners go unsampled.

The data premise is also wrong for every DEM this code consumes. `TileDownloader.cs:12` pulls from the void-filled Skadi/Terrain-Tiles product, which has continuous global coverage and does not emit `-32768` over water; official SRTM encodes water as 0, and voids are clustered blobs well under 1% of a tile. Requiring **all** 101 evenly spaced transect cells to be void means the tile is functionally empty — exactly what the check exists to reject. And even a wrongly rejected tile is handled gracefully: `GetElevation` returns `(0, false)` and `ComputePipeline.cs:47` only overwrites `Ele` when `ok` is true, so points keep their GPS elevation.

**Accurate side note, no action:** the `--dem-skip-validation` escape hatch is CLI-only — `GpxAnalysisService` and `RouteElevationService` both call `DemSource.CreateAuto(...)` without `WithSkipValidation`. That would matter if the trigger were reachable.

*(A stride coprime with `GridSize` would be a tidier probe. That is a code-quality nit, not a bug — do not open an issue for it.)*

### 3. `SummaryMapper` drops `FilteredPoints`, breaking the JSON contract — `cli/src/GpxAnalyzer.Cli.Core/Output/SummaryMapper.cs:35`

**Refuted.** The code facts are accurate but no failing path exists. `AnalyzeCommand` deserializes with only `PropertyNameCaseInsensitive`, so the default `UnmappedMemberHandling = Skip` silently ignores the extra `filtered_points` member — deserialization succeeds and every consumed field is correct. Mapping the field would change nothing observable: `PromptBuilder` has no code referencing a filtered-point count, `ui/api`'s `Activity` entity has no column for it, and a repo-wide grep finds zero client references. There is also no numeric inconsistency — `ComputePipeline.cs:19` sets `PointCount` *before* filtering, so `point_count` is the raw pre-filter count in both producers.

`filtered_points` is a CLI-local diagnostic whose only consumers are `TextFormatter.cs:36` and `BenchmarkOutput.cs:159`. The data-quality channel that actually feeds the AI and the API is `anomalies` (quality score, speed spikes, correction applied), which **both** producers map and which `PromptBuilder.cs:73` consumes in a "## Data Quality" section. The filename sub-claim was inverted too: `GpxAnalysisService.cs:81` passes a server-side absolute storage path and `stats.Filename` is only a display label, so `Path.GetFileName` is a deliberate normalization that avoids leaking server paths into the AI prompt. The project's own contract test (`GpxStatsNegativeTests.cs:197`) enumerates the full contract payload and deliberately omits `filtered_points`.

What remains is a documentation imprecision in the `GpxStats.cs:6` XML comment ("matching the CLI JSON output exactly") — a comment nit, not a bug.

### 4. `ReportFormatter` formats doubles with `CurrentCulture` — `ai-analyzer/src/GpxAiAnalyzer.Core/Output/ReportFormatter.cs:57`

**Refuted.** The low-level observation is true — line 57 uses `$"{seg.DistanceKm:F1} km"` with no `IFormatProvider`, `GpxAiAnalyzer.csproj` does not set `InvariantGlobalization` (only `GpxAnalyzer.Cli.csproj` does), and `Program.cs` pins no culture, so on an fr-FR host the text report prints "12,5 km". But the claimed cross-environment diff cannot happen: `ReportFormatter` has exactly one caller in the whole repo (`AnalyzeCommand.cs:84`, writing to `Console.Out`). The API never references it — `ActivityProcessingService.cs:220` serializes the report with System.Text.Json, which emits invariant numeric tokens, and the React client formats for display. There is no "identical report generated inside the API container" to mismatch against.

No snapshot test exists or could break (nothing in the ai-analyzer suite references `ReportFormatter` or `FormatText`, and CI does not invoke the binary's output), and no downstream scraper of the text format exists — the documented machine-readable path is `--format json`, which goes through `FormatJson` and is culture-immune. The comparison with `PromptBuilder.cs:17` is a category error: `PromptBuilder` pins invariant because its string is fed to an LLM, whereas `FormatText` is human-facing terminal output, where `CurrentCulture` is the .NET-idiomatic default — showing "12,5 km" to a French user reading a French-language report is correct localization, not drift. Half the cited evidence does not even exhibit the behaviour: line 58's `$"{seg.ElevationChange:+0;-0}m"` is culture-invariant for the claimed case (zero decimal places, no group separator, literal `+`/`-`).

**Note for anyone tempted to "fix" this anyway:** doing so would make the human-facing report *less* correctly localized. Leave it.

---

## Coverage check

All 50 confirmed findings are assigned to exactly one task:

| Wave | Tasks | Issues | Count |
|---|---|---|---|
| 1 | 1–7 | #92, #93, #94, #95, #96, #117, #119 | 7 |
| 2 | 8–15 | #73, #74, #75, #76, #77, #78, #79, #80, #81, #82, #83, #84, #97, #98, #99, #100, #115 | 17 |
| 3 | 16–18 | #85, #86, #88, #107, #108 | 5 |
| 4 | 19–22 | #89, #90, #91, #109, #110, #111, #112 | 7 |
| 5 | 23–24 | #113, #114, #116 | 3 |
| 6 | 25–28 | #118, #120, #121, #122 | 4 |
| 7 | 29–31 | #87, #101, #102, #103, #104, #105, #106 | 7 |
| | **31 tasks** | **#73–#122** | **50** |
