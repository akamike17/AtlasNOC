# F11 — Auditoría de huecos reales (GAP AUDIT)

> Rama: `deepseek-rebuild`
> Commit base auditado: `88b0975` (harden: Fase A–G dominio/infraestructura)
> Método: comparación de la especificación (`segir.md` §6–10, `rev.md` §27–28) contra lo que existe en disco, verificando compilación y tests.

## Clasificación

- `EXISTS_COMPLETE` — implementado y cubierto.
- `EXISTS_PARTIAL` — presente pero incompleto frente a la spec.
- `MISSING` — no existe.
- `DUPLICATE` — existe más de una vez o reimplementa algo ya existente.
- `BROKEN` — existe pero no funciona.

---

## 1. Seguridad (rev.md §1–4, §23–25)

| Elemento | Estado |
|---|---|
| Secretos fuera de repo (`ConnectionStrings` vacías, `TestDatabaseConfiguration`) | EXISTS_COMPLETE |
| API key auth (`ApiKeyAuthenticationHandler` + `ApiScopes` + `ApiScopeAuthorizationHandler`) | EXISTS_COMPLETE |
| Lockout + IsActive en login | EXISTS_COMPLETE |
| ReadOnly realmente read-only (Sites/Devices/Alerts/Incidents) | EXISTS_COMPLETE |
| Auditoría administrativa con IP/UA | EXISTS_COMPLETE |
| Cookie secure + ForwardedHeaders | EXISTS_COMPLETE |
| Data Protection con thumbprint | EXISTS_PARTIAL — no lanza en producción si el thumbprint es inválido (§23). |

## 2. Discovery / SNMP (rev.md §5–7)

| Elemento | Estado |
|---|---|
| `SnmpConnectionOptions` (v2c/v3 authPriv) + `ResolvedDeviceCredential` | EXISTS_COMPLETE |
| Credencial y site seleccionados aplicados en DiscoveryExecutor | EXISTS_COMPLETE |
| SNMP v2c/v3, interfaz real (ifIndex/ifTable), LLDP | EXISTS_COMPLETE |
| CIDR `/32` `/31`, concurrencia limitada | EXISTS_COMPLETE |
| CDP (MIB Cisco) | MISSING — pospuesto por diseño (§6: "CDP posterior") |

## 3. Drivers vendor (rev.md §8–9, §35)

| Elemento | Estado |
|---|---|
| MikroTik credential-aware, parser uptime, TLS/pinning | EXISTS_COMPLETE |
| Ubiquiti separado en `UniFiController` / `AirOsDeviceDriver`, parser robusto | EXISTS_COMPLETE |
| `CiscoDriver` (SNMP+CDP/LLDP, sysObjectID) | MISSING — `GenericSnmpDriver` cubre SNMP genérico; no hay driver Cisco específico (spec lo lista como driver mínimo). |

## 4. Topología (rev.md §10–12, §39)

| Elemento | Estado |
|---|---|
| Correlación O(n), `NeighborObservationStatus` | EXISTS_COMPLETE |
| Link canónico único A-B/B-A (FK + índice) | EXISTS_COMPLETE |
| Rediscovery idempotente + refresh/stale | EXISTS_COMPLETE |
| Evidence UI (detalle de observación en topología) | EXISTS_PARTIAL — el backend persiste evidencia; la UI de detalle de link/evidencia no está completa. |

## 5. Polling (rev.md §13–14, §15)

| Elemento | Estado |
|---|---|
| No fabrica 0; MarkSeen separado de MarkPolled | EXISTS_COMPLETE |
| Concurrencia configurable + PollingProfile | EXISTS_COMPLETE |
| Workers separados Web/Worker (`AddAtlasWorkers`) | EXISTS_COMPLETE |

## 6. Alertas / Notificaciones (rev.md §20–22)

| Elemento | Estado |
|---|---|
| ConsecutiveFaults real, operadores validados, recovery | EXISTS_COMPLETE |
| `NotificationDelivery` con estados por canal, retries | EXISTS_COMPLETE |

## 7. UI / API (rev.md §27–28, §19, §37; segir.md §8–10)

| Elemento | Estado |
|---|---|
| UsersController CRUD completo + protección último admin | EXISTS_COMPLETE |
| **InterfacesController** | MISSING |
| **LinksController** (index/detail/confirm/reject/manual/edit-metadata) | MISSING |
| **MetricsController** (MVC) | MISSING |
| **SubscribersController** | MISSING |
| **IntegrationsController** | MISSING |
| **API controllers** (Devices/Sites/Discovery/Alerts/Incidents/Subscribers/Integrations/System) | MISSING — solo existen Topology y Metrics |
| **Vistas** Interfaces/Links/Metrics/Subscribers/Integrations | MISSING |
| `IInterfaceService`, `ISubscriberService`, `IServiceEndpointService` | MISSING |
| `ILinkService` (reject/manual/edit-metadata) | EXISTS_PARTIAL — solo List + Confirm |

## 8. E2E / robustez (rev.md §29–33)

| Elemento | Estado |
|---|---|
| 18 flujos independientes | EXISTS_PARTIAL — existe suite; no separada en 18 etapas |
| E2E no mata procesos ajenos (KillPortListener) | EXISTS_COMPLETE (removido) |
| E2E outage sin tocar DB directa (`ILabNetworkControl`) | EXISTS_PARTIAL |
| **E2E fixtures lanzan error en vez de omitir sin `ATLASNOC_TEST_CONNECTION`** | BROKEN — `ApiKeyAuthFactory`/`LoginTestFactory`/`LockoutTestFactory` usan `Resolve()` (lanza) en `ConfigureWebHost`, produciendo 12 errores en lugar de 12 skips. |

---

## Resumen ejecutivo

Dominio, infraestructura, seguridad y workers están **completos y verdes** (build Release 0/0, 123 unit tests). El hueco concentrado es **Fase G — superficie operacional de UI/API**: 5 controllers MVC, 8 API controllers, sus servicios de aplicación y vistas. Estos son los siguientes entregables.