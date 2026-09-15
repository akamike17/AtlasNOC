using System.Text.Json;
using Microsoft.Playwright;
using AtlasNOC.Tests.Shared;
using Xunit;

namespace AtlasNOC.Tests.E2E;

[Collection("e2e")]
public sealed class OperationalClosureSmokeTests
{
    private readonly E2EFixture _fx;
    public OperationalClosureSmokeTests(E2EFixture fx) => _fx = fx;

    [SkippableFact(Timeout = 300_000)]
    public async Task Operational_closure_smoke_uses_real_api_and_mysql()
    {
        if (_fx.IsSkipped(out var reason)) throw new SkipTestException(reason);
        var page = await _fx.NewPageAsync();
        await SetupAndLogin(page);
        var n = 0;
        async Task<JsonElement> Post(string path, object body, int expected = 200)
        {
            var raw = await page.EvaluateAsync<string>("async ({url,body}) => { const r=await fetch(url,{method:'POST',headers:{'Content-Type':'application/json'},body:JSON.stringify(body)}); return JSON.stringify({s:r.status,u:r.url,a:r.headers.get('allow'),b:await r.text()}); }", new { url = E2EFixture.BaseUrl + path, body });
            using var doc = JsonDocument.Parse(raw);
            var status = doc.RootElement.GetProperty("s").GetInt32();
            var json = doc.RootElement.GetProperty("b").GetString()!;
            Assert.True(status == expected, $"POST {path} respondió {status} en {doc.RootElement.GetProperty("u").GetString()} allow={doc.RootElement.GetProperty("a").GetString()}: {json}");
            if (status == 204 && string.IsNullOrWhiteSpace(json))
            {
                n++;
                return JsonDocument.Parse("{}").RootElement.Clone();
            }
            Assert.True(JsonDocument.Parse(json).RootElement.ValueKind is not JsonValueKind.Null, $"checkpoint {n + 1} returned null");
            n++;
            return JsonDocument.Parse(json).RootElement.Clone();
        }
        async Task<JsonElement> Get(string path)
        {
            var raw = await page.EvaluateAsync<string>("async url => { const r=await fetch(url); return JSON.stringify({s:r.status,b:await r.text()}); }", E2EFixture.BaseUrl + path);
            using var doc = JsonDocument.Parse(raw);
            Assert.Equal(200, doc.RootElement.GetProperty("s").GetInt32());
            n++;
            return JsonDocument.Parse(doc.RootElement.GetProperty("b").GetString()!).RootElement.Clone();
        }
        static Guid Id(JsonElement e) => e.GetProperty("id").GetGuid();

        for (var zoneIndex = 1; zoneIndex <= 4; zoneIndex++)
            await Post("/api/operations/zones", new { code = $"SMOKE-ZONA-{zoneIndex:00}", name = $"Smoke Zona {zoneIndex}", geography = $"Lab {zoneIndex}", totalCapacityMbps = 1000 });
        var zones = await Get("/api/operations/zones");
        Assert.True(zones.GetArrayLength() >= 4);

        var prospect = await Post("/api/operations/prospects", new { name = "Smoke Cliente", address = "Av. Test 100", phone = "5550100" }); // 1
        await Post($"/api/operations/prospects/{Id(prospect)}/status", new { status = 1 }); // 2
        var customer = await Post("/api/operations/customers", new { serviceCode = "SMOKE-30", name = "Cliente Smoke", phone = "5550101", email = "smoke@example.test" }, 201); // 3
        var customerId = Id(customer);
        var plan = await Post("/api/operations/plans", new { name = "Smoke 50", monthlyPrice = 500, downloadMbps = 50, uploadMbps = 10 }); // 4
        var service = await Post("/api/operations/services", new { customerId, planId = Id(plan), address = "Av. Test 100" }); // 5
        var serviceId = Id(service);
        await Post($"/api/operations/services/{serviceId}/activate", new { }); // 6
        var cpe = await Post("/api/operations/cpe-cases", new { macAddress = "02:00:00:00:30:01", accessPoint = "AP-SMOKE", ipAddress = "10.30.0.2", customerServiceId = serviceId }); // 7
        await Post($"/api/operations/cpe-cases/{Id(cpe)}/decision", new { status = 1, reason = "Smoke autorizado" }); // 8 (honest Unsupported provisioning)
        await Post("/api/operations/coverage", new { address = "Av. Test 100", status = 2, capacityMbps = (int?)null }); // 9
        var asset = await Post("/api/operations/assets", new { assetTag = "SMOKE-CPE-30", type = "CPE", serialNumber = "SN-SMOKE-30", macAddress = "02:00:00:00:30:01" }); // 10
        await Post($"/api/operations/assets/{Id(asset)}/assign", new { serviceId }); // 11
        var billingKey = "smoke-monthly-2026-09";
        await Post($"/api/operations/billing/{customerId}/charge", new { amount = 500, description = "Mensualidad smoke", dueAtUtc = DateTime.UtcNow.AddDays(2), period = "2026-09", idempotencyKey = billingKey }); // 12
        var replay = await Post($"/api/operations/billing/{customerId}/charge", new { amount = 500, description = "Mensualidad smoke retry", dueAtUtc = DateTime.UtcNow.AddDays(2), period = "2026-09", idempotencyKey = billingKey });
        Assert.True(replay.GetProperty("idempotentReplay").GetBoolean());
        var billing = await Get($"/api/operations/billing/{customerId}"); // 13
        Assert.True(billing.ValueKind != JsonValueKind.Null);
        await Post($"/api/operations/billing/{customerId}/suspend-if-overdue", new { }); // 14 (within grace/current)
        var promise = await Post($"/api/operations/billing/{customerId}/promises", new { amount = 250, promisedAtUtc = DateTime.UtcNow, expiresAtUtc = DateTime.UtcNow.AddDays(7), conditions = "Smoke" }); // 15
        await Post($"/api/operations/billing/{customerId}/payment", new { amount = 250, description = "Pago parcial smoke" }); // 16
        await Post($"/api/operations/billing/promises/{Id(promise)}/default", new { }); // 17
        var ticket = await Post("/api/operations/tickets", new { customerId, title = "Falla smoke", description = "Prueba E2E" }); // 18
        await Post("/api/operations/support/interactions", new { ticketId = Id(ticket), channel = 0, symptoms = "Sin enlace", diagnosis = "Simulado", actions = "Validación", result = "Abierto", durationMinutes = 5 }); // 19
        await Post($"/api/operations/tickets/{Id(ticket)}/status", new { status = 1 }); // 20
        var visit = await Post("/api/operations/visits", new { customerId, scheduledAtUtc = DateTime.UtcNow.AddDays(1), workType = "Revisión", estimatedMinutes = 60 }); // 21
        await Post($"/api/operations/visits/{Id(visit)}/complete", new { actualMinutes = 55, travelMinutes = 20, result = "Programado" }); // 22
        await Post("/api/operations/installations", new { serviceId, outsideCity = false, installationFee = 0, routerFee = 0, scheduledAtUtc = DateTime.UtcNow.AddDays(1) }); // 23
        var incident = await Post("/api/operations/incidents/root", new { title = "AP smoke", description = "Falla simulada", rootCauseDeviceId = (string?)null }); // 24
        await Post($"/api/operations/incidents/{Id(incident)}/priority", new { priority = 2, reason = "Smoke" }); // 25
        await Post("/api/operations/support/interactions", new { ticketId = Id(ticket), channel = 0, symptoms = "Sin enlace", diagnosis = "Incidente correlacionado", actions = "Validación", result = "Afectación confirmada", durationMinutes = 5, rootIncidentId = Id(incident) });
        await Post($"/api/incidents/{Id(incident)}/resolve", new { }, 204);
        var preview = await Get($"/api/operations/credits/preview/{customerId}/{Id(incident)}");
        Assert.Equal(customerId, preview.GetProperty("customerId").GetGuid());
        Assert.Equal(Id(incident), preview.GetProperty("incidentId").GetGuid());
        var credit = await Post("/api/operations/credits", new { customerId, incidentId = Id(incident), fromUtc = DateTime.UtcNow.AddHours(-2), toUtc = DateTime.UtcNow, suggestedAmount = 41.66m, reason = "Sugerencia simulada" }); // 26
        await Post($"/api/operations/credits/{Id(credit)}/status", new { status = 0 }); // 27
        await Get($"/api/operations/customers/{customerId}/diagnostic"); // 28
        await Get($"/api/operations/coverage/evaluate?address=Av%20Test%20100&requiredMbps=20"); // 29
        await Get("/api/operations/snapshot"); // 30
        // Contrato del smoke: 4 altas de zona + listado + 30 checkpoints operativos.
        // El flujo completo contabiliza 39 respuestas verificadas.
        Assert.Equal(39, n);
        await page.CloseAsync();
    }

    [SkippableFact(Timeout = 120_000)]
    public async Task Billing_concurrent_same_key_creates_one_entry_and_one_replay()
    {
        if (_fx.IsSkipped(out var reason)) throw new SkipTestException(reason);
        var page = await _fx.NewPageAsync();
        await SetupAndLogin(page);

        async Task<JsonElement> Post(string path, object body, int expected = 200)
        {
            var raw = await page.EvaluateAsync<string>(
                "async ({url,body}) => { const r=await fetch(url,{method:'POST',headers:{'Content-Type':'application/json'},body:JSON.stringify(body)}); return JSON.stringify({s:r.status,b:await r.text()}); }",
                new { url = E2EFixture.BaseUrl + path, body });
            using var doc = JsonDocument.Parse(raw);
            Assert.Equal(expected, doc.RootElement.GetProperty("s").GetInt32());
            return JsonDocument.Parse(doc.RootElement.GetProperty("b").GetString()!).RootElement.Clone();
        }

        var suffix = Guid.NewGuid().ToString("N")[..8];
        var customer = await Post("/api/operations/customers", new
        {
            serviceCode = $"CONC-{suffix}", name = $"Concurrent {suffix}", phone = "5550199", email = $"{suffix}@example.test"
        }, 201);
        var customerId = customer.GetProperty("id").GetGuid();
        var plan = await Post("/api/operations/plans", new
        {
            name = $"Concurrent plan {suffix}", monthlyPrice = 500, downloadMbps = 50, uploadMbps = 10
        });
        var service = await Post("/api/operations/services", new
        {
            customerId, planId = plan.GetProperty("id").GetGuid(), address = $"Concurrent {suffix}"
        });
        await Post($"/api/operations/services/{service.GetProperty("id").GetGuid()}/activate", new { });

        var rawConcurrent = await page.EvaluateAsync<string>(
            "async ({url,body}) => JSON.stringify(await Promise.all([1,2].map(async () => { const r=await fetch(url,{method:'POST',headers:{'Content-Type':'application/json'},body:JSON.stringify(body)}); return {s:r.status,b:await r.json()}; })))",
            new
            {
                url = E2EFixture.BaseUrl + $"/api/operations/billing/{customerId}/charge",
                body = new { amount = 500, description = "Concurrent charge", period = "2026-09", idempotencyKey = $"concurrent-{suffix}" }
            });
        using var concurrent = JsonDocument.Parse(rawConcurrent);
        var responses = concurrent.RootElement.EnumerateArray().ToList();
        Assert.Equal(2, responses.Count);
        Assert.All(responses, response => Assert.Equal(200, response.GetProperty("s").GetInt32()));
        Assert.Single(responses, response => !response.GetProperty("b").GetProperty("idempotentReplay").GetBoolean());
        Assert.Single(responses, response => response.GetProperty("b").GetProperty("idempotentReplay").GetBoolean());
        await page.CloseAsync();
    }

    private static async Task SetupAndLogin(IPage page)
    {
        await page.GotoAsync(E2EFixture.BaseUrl + "/setup");
        if (page.Url.EndsWith("/setup", StringComparison.OrdinalIgnoreCase))
        {
            await page.FillAsync("input[name='WispName']", "Smoke WISP");
            await page.FillAsync("input[name='AdminUserName']", "admin");
            await page.FillAsync("input[name='AdminDisplayName']", "Smoke Admin");
            await page.FillAsync("input[name='Password']", "Password123!");
            await page.FillAsync("input[name='ConfirmPassword']", "Password123!");
            await page.ClickAsync("button[type='submit']");
            await page.WaitForLoadStateAsync(LoadState.NetworkIdle);
        }
        if (!page.Url.Contains("/account/login", StringComparison.OrdinalIgnoreCase))
            await page.GotoAsync(E2EFixture.BaseUrl + "/account/login");
        await page.FillAsync("input[name='userName']", "admin");
        await page.FillAsync("input[name='password']", "Password123!");
        await page.ClickAsync("button[type='submit']");
        await page.WaitForLoadStateAsync(LoadState.NetworkIdle);
        Assert.DoesNotContain("/account/login", page.Url, StringComparison.OrdinalIgnoreCase);
    }
}
