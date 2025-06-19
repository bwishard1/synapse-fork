# Synapse Fork – `synapse-fork`

Fork of the Synapse event-driven workflow engine with:
- ✅ CLI token fix (ENV var support)
- ✅ Docker Compose local dev stack
- 🔐 Early Keycloak integration prep

---

## 🔧 What This Fork Adds

### 🛠 CLI Auth Patch
Synapse CLI now supports `SYNAPSE_API_AUTH_TOKEN` via environment variable.  
No need to hardcode in `config.yaml`.

Code change in `Program.cs` → `TokenFactory` with env fallback.

### 🐳 Docker Compose Stack
- Synapse API, Operator, Correlator (built from source)
- Redis (Garnet)
- Kafka + Zookeeper

### 📦 Keycloak (coming soon)
Testing Synapse + Keycloak integration for both:
- Authenticated user CLI access
- Workflows getting JWT tokens to interact with secured services

---

## 🧪 Quickstart

```bash
cd deployments/docker-compose
docker compose up --build

export SYNAPSE_API_AUTH_TOKEN=<your_token>
dotnet run --project ../../src/cli/Synapse.Cli/Synapse.Cli.csproj -- workflow list
