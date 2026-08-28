# AtlasNOC - Network Operations Center Management API

[![Build Status](https://github.com/your-org/AtlasNOC/workflows/AtlasNOC%20CI/CD/badge.svg)](https://github.com/your-org/AtlasNOC/actions)
[![Coverage](https://codecov.io/gh/your-org/AtlasNOC/branch/main/graph/badge.svg)](https://codecov.io/gh/your-org/AtlasNOC)
[![Docker](https://img.shields.io/badge/docker-ghcr.io%2Fyour--org%2Fatlasnoc-blue)](https://github.com/your-org/AtlasNOC/pkgs/container/atlasnoc)
[![License](https://img.shields.io/badge/license-MIT-green.svg)](LICENSE)

> A production-ready Network Operations Center API for device monitoring, alerting, incident management, and vulnerability tracking.

## Features

- 📡 **Device Management** - CRUD for network devices (routers, switches, firewalls, servers, APs)
- 🚨 **Alerting** - Multi-severity alerts with acknowledgment/resolution workflows
- 🔧 **Incident Management** - Full incident lifecycle (New → Investigating → Monitoring → Resolved)
- 🔐 **Credential Management** - SNMP v2c/v3 credentials with rotation support
- 🔑 **API Key Auth** - Role-based access (Administrator, NocOperator, ReadOnly)
- 📊 **CVE Tracking** - Automated NVD vulnerability fetching & correlation
- 📈 **Observability** - OpenTelemetry (traces/metrics/logs), Serilog, Seq, Health Checks
- ⚡ **Performance** - Redis caching, EF Core, connection pooling, rate limiting

## Quick Start

### Prerequisites
- .NET 8.0 SDK
- MySQL 8.0+
- Redis 7+
- (Optional) Seq, OpenTelemetry Collector

### Local Development
```bash
# Clone
git clone https://github.com/your-org/AtlasNOC.git
cd AtlasNOC

# Configure
cp appsettings.Development.json.example appsettings.Development.json
# Edit connection strings

# Run
dotnet run --project AtlasNOC.csproj --environment Development
```

### Docker Compose (Full Stack)
```bash
# Configure
cp .env.example .env
# Edit .env with your secrets

# Start all services
docker-compose -f docker-compose.yml -f docker-compose.prod.yml up -d --build

# Verify
curl http://localhost/health/live
curl http://localhost/health/ready
```

## API Endpoints

### Authentication
All endpoints require `X-API-Key` header with valid API key.

| Role | Permissions |
|------|-------------|
| Administrator | Full access |
| NocOperator | Read/Write (no admin) |
| ReadOnly | Read only |

### Devices
| Method | Endpoint | Description |
|--------|----------|-------------|
| GET | `/api/devices` | List all devices |
| GET | `/api/devices/{id}` | Get device by ID |
| POST | `/api/devices` | Create device |
| PUT | `/api/devices/{id}/status` | Update device status |
| PUT | `/api/devices/{id}/details` | Update device details |
| DELETE | `/api/devices/{id}/deactivate` | Deactivate device |
| PUT | `/api/devices/{id}/reactivate` | Reactivate device |
| GET | `/api/devices/down` | Get down devices |

### Alerts
| Method | Endpoint | Description |
|--------|----------|-------------|
| GET | `/api/alerts` | List all alerts |
| GET | `/api/alerts/active` | Active alerts |
| GET | `/api/alerts/critical` | Critical/High alerts |
| GET | `/api/alerts/unacknowledged` | Unacknowledged alerts |
| GET | `/api/alerts/recent?count=10` | Recent alerts |
| POST | `/api/alerts` | Create alert |
| POST | `/api/alerts/{id}/acknowledge` | Acknowledge alert |
| POST | `/api/alerts/{id}/resolve` | Resolve alert |

### Incidents
| Method | Endpoint | Description |
|--------|----------|-------------|
| GET | `/api/incidents` | All incidents |
| GET | `/api/incidents/open` | Open incidents |
| POST | `/api/incidents` | Create incident |
| POST | `/api/incidents/{id}/investigating` | Set investigating |
| POST | `/api/incidents/{id}/monitoring` | Set monitoring |
| POST | `/api/incidents/{id}/resolve` | Resolve incident |

### Credentials (SNMP)
| Method | Endpoint | Description |
|--------|----------|-------------|
| GET | `/api/credentials` | All credentials |
| GET | `/api/credentials/active` | Active credentials |
| POST | `/api/credentials/v2c` | Create v2c credential |
| POST | `/api/credentials/v3` | Create v3 credential |
| POST | `/api/credentials/{id}/rotate-v2c` | Rotate v2c community |
| POST | `/api/credentials/{id}/rotate-v3-auth` | Rotate v3 auth |
| POST | `/api/credentials/{id}/rotate-v3-priv` | Rotate v3 priv |
| POST | `/api/credentials/{id}/deactivate` | Deactivate credential |

### CVEs
| Method | Endpoint | Description |
|--------|----------|-------------|
| GET | `/api/cves` | All CVEs (filter: keyword, severity, since) |
| GET | `/api/cves/{cveId}` | Get by CVE ID |
| GET | `/api/cves/critical` | Critical/High CVEs |
| GET | `/api/cves/recent?count=50` | Recent CVEs |
| GET | `/api/cves/stats` | CVE statistics |
| POST | `/api/cves/fetch` | Trigger manual fetch |

### Audits
| Method | Endpoint | Description |
|--------|----------|-------------|
| GET | `/api/audits/recent?count=50` | Recent audit events |
| GET | `/api/audits/category/{category}` | By category |
| GET | `/api/audits/user/{userId}` | By user |

### API Keys (Admin only)
| Method | Endpoint | Description |
|--------|----------|-------------|
| GET | `/api/apikeys` | List active keys |
| POST | `/api/apikeys` | Create API key |
| POST | `/api/apikeys/{id}/revoke` | Revoke key |

### Health Checks
| Endpoint | Purpose |
|----------|---------|
| `/health/live` | Liveness (k8s livenessProbe) |
| `/health/ready` | Readiness (k8s readinessProbe) |
| `/health` | Full health report |

## Configuration

### Required Environment Variables
```bash
ConnectionStrings__DefaultConnection=Server=...;Database=...;User=...;Password=...
ConnectionStrings__Redis=localhost:6379,password=...,abortConnect=False
Serilog__WriteTo__Seq__ServerUrl=http://seq:5341
Serilog__WriteTo__Seq__ApiKey=your-api-key
OpenTelemetry__Endpoint=http://otel-collector:4317
CveFetcher__NvdApiKey=your-nvd-key  # Optional
Cors__AllowedOrigins__0=https://your-domain.com
```

### Key Settings
```json
{
  "AtlasNoc": {
    "RequireHttps": true,
    "ApplyMigrationsOnStartup": true
  },
  "CveFetcher": {
    "IntervalHours": 24,
    "NvdApiUrl": "https://services.nvd.nist.gov/rest/json/cves/2.0"
  }
}
```

## Architecture

```
┌─────────────┐     ┌─────────────┐     ┌─────────────┐
│   Client    │────▶│   Nginx     │────▶│  AtlasNOC   │
│  (API Key)  │     │  (Rate Lim) │     │   API       │
└─────────────┘     └─────────────┘     └──────┬──────┘
                                                │
        ┌─────────────┐     ┌─────────────┐     │
        │   Seq       │◀────│  OpenTel    │◀────┤
        │  (Logs)     │     │  Collector  │     │
        └─────────────┘     └─────────────┘     │
                                                ▼
        ┌─────────────┐     ┌─────────────┐ ┌──────────┐
        │   MySQL     │◀────│  EF Core    │ │  Redis   │
        │  (Primary)  │     │  (ORM)      │ │ (Cache)  │
        └─────────────┘     └─────────────┘ └──────────┘
```

## Project Structure

```
AtlasNOC/
├── Controllers/           # API Controllers
├── Models/                # DTOs & ViewModels
├── Services/              # Background services, Auth
├── AtlasNOC.Domain/       # Domain layer
│   ├── Data/              # EF Core DbContext
│   ├── Entities/          # Domain entities
│   ├── Enums/             # Domain enums
│   ├── Services/          # Domain services & repos
│   │   ├── Interfaces/    # Service contracts
│   │   └── Implementations/
│   └── ValueObjects/      # Strongly-typed IDs
├── Tests/                 # Unit & integration tests
├── docker-compose.yml     # Local development stack
├── docker-compose.prod.yml # Production stack
├── Dockerfile             # Multi-stage build
├── nginx.conf             # Reverse proxy config
└── k8s-deployment.yaml    # Kubernetes manifests
```

## Development

### Running Tests
```bash
dotnet test
dotnet test --collect:"XPlat Code Coverage"
```

### Code Style
```bash
dotnet format
dotnet build -warnaserror
```

### Migrations
```bash
dotnet ef migrations add MigrationName --project AtlasNOC.Domain --startup-project AtlasNOC.csproj
dotnet ef database update --project AtlasNOC.Domain --startup-project AtlasNOC.csproj
```

## Production Deployment

See [DEPLOYMENT_CHECKLIST.md](DEPLOYMENT_CHECKLIST.md) for complete production deployment guide.

### Docker
```bash
docker build -t atlasnoc:latest .
docker run -d -p 8080:8080 --env-file .env.production atlasnoc:latest
```

### Kubernetes
```bash
kubectl apply -f k8s-deployment.yaml
```

### CI/CD
GitHub Actions workflow in `.github/workflows/ci-cd.yml`:
- Lint & Format check
- Unit tests with coverage
- Security scan (Trivy)
- Docker build & push to GHCR
- Deploy to staging (develop branch)
- Deploy to production (main branch)

## Monitoring

- **Health**: `/health/live`, `/health/ready`, `/health`
- **Logs**: Seq at `http://seq:5341`
- **Traces**: OpenTelemetry → Tempo/Jaeger
- **Metrics**: Prometheus at `/metrics` (via OTel collector)
- **Alerts**: Configure on error rate, latency, health checks

## License

MIT License - see [LICENSE](LICENSE) for details.

## Contributing

1. Fork the repository
2. Create feature branch (`git checkout -b feature/amazing-feature`)
3. Commit changes (`git commit -m 'Add amazing feature'`)
4. Push to branch (`git push origin feature/amazing-feature`)
5. Open Pull Request

## Support

- Issues: [GitHub Issues](https://github.com/your-org/AtlasNOC/issues)
- Documentation: [Wiki](https://github.com/your-org/AtlasNOC/wiki)