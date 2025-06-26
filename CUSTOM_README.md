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

## 🧱 Known Issues + Fixes

### ❌ CLI returns 401 even with valid token

**Fix:** The upstream CLI did not attach `SYNAPSE_API_AUTH_TOKEN` to HTTP requests.  
✅ Patched in this fork via `Program.cs` → `TokenFactory` override.

---

## 🧪 Quickstart

```bash
cd deployments/docker-compose
docker compose -f docker-compose.build.yml up -d

# Ensure running
docker compose -f docker-compose.build.yml ps

# Create the workflow
curl -X POST http://localhost:8080/api/v1/workflows \
  -H "Content-Type: application/json" \
  -H "Authorization: Bearer h0v0fixhkg9z4bgtxele7892x9sjtw7o" \
  -d @/Users/bwishard/code/synapse/examples/greeter.json

# List workflows to ensure running
curl http://localhost:8080/api/v1/workflows \
  -H "Authorization: Bearer h0v0fixhkg9z4bgtxele7892x9sjtw7o" | jq


## 🧪 Quickstart Old
cd deployments/docker-compose
docker compose -f docker-compose.build.yml up --build

# Open new terminal window
# Token can be found in deployments/docker-compose/config/tokens.yaml
export SYNAPSE_API_AUTH_TOKEN=h0v0fixhkg9z4bgtxele7892x9sjtw7o
dotnet run --project ../../src/cli/Synapse.Cli/Synapse.Cli.csproj -- workflow list

# Deploy a workflow
cd ../..
dotnet run --project ../../src/cli/Synapse.Cli/Synapse.Cli.csproj -- workflow create -f examples/kafka-greeter.yaml

# Then send a Kafka event
docker compose exec kafka kafka-console-producer.sh --broker-list localhost:9092 --topic my.kafka.topic

# Type:
{"name":"TestUser"}
