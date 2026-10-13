# Floci demo: AWS locally, €0 cost

.NET 10 Minimal API (Vertical Slice + MediatR + FluentValidation) that stores a document in **S3**
and publishes an event to **SQS**. Everything runs against [Floci](https://floci.io), a local AWS emulator,
with no real AWS account. The stack also starts the **Azure**, **GCP** and **OCI** emulators, plus
[floci-ui](https://github.com/floci-io/floci-ui) to view and manage resources across all four clouds
from the browser.

```
POST /documents  ->  S3 (object)  +  SQS (DocumentUploaded event)
```

## Requirements

- Docker Desktop (with Git installed: the UI image is built from GitHub the first time)
- Terraform >= 1.5
- .NET 10 SDK

## Quick start

```powershell
.\demo.ps1
```

Or step by step (PowerShell):

```powershell
# 1. Start the emulators (AWS 4566, Azure 4577, GCP 4588, OCI 4599) and the UI (4500)
docker compose up -d
docker compose ps          # wait until all four emulators are "healthy"

# 2. Open the UI: http://localhost:4500

# 3. Create bucket and queue
cd infra
terraform init
terraform apply -auto-approve
cd ..

# 4. Integration tests
dotnet test

# 5. (Optional) Run the API and try it manually (http://localhost:5000)
dotnet run --project src/FlociDemo.Api
# in another terminal:
Invoke-RestMethod -Method Post -Uri http://localhost:5000/documents -ContentType "application/json" `
  -Body '{"fileName":"hello.txt","content":"AWS on my laptop"}'
```

After step 5 you will see the object in the bucket and the message in the queue from the UI.
The tests purge the queue and delete their own messages, so use step 5 to see a message.

With bash/curl:

```bash
curl -X POST http://localhost:5000/documents -H "Content-Type: application/json" \
     -d '{"fileName":"hello.txt","content":"AWS on my laptop"}'
```

### Cleanup

```powershell
cd infra; terraform destroy -auto-approve; cd ..
docker compose down
```

Floci keeps data in memory: if you run `docker compose down` without `terraform destroy`,
delete `infra/terraform.tfstate*` before the next `apply` to start from scratch.

### Multi-cloud

| Cloud | Emulator          | Endpoint                   | Services shown in the UI                                                         |
|-------|-------------------|----------------------------|----------------------------------------------------------------------------------|
| AWS   | `floci/floci`     | `http://localhost:4566`    | All except containers and scheduler (S3, SQS, Lambda, EC2, EKS, RDS, DynamoDB...) |
| Azure | `floci/floci-az`  | `http(s)://localhost:4577` | compute, k8s, storage, database, nosql, networking, messaging, secrets           |
| GCP   | `floci/floci-gcp` | `http://localhost:4588`    | k8s, serverless, containers, storage, database, messaging, scheduler, secrets    |
| OCI   | `floci/floci-oci` | `http://localhost:4599`    | k8s, serverless, storage, messaging, streams, identity, secrets, kms             |

From the UI (http://localhost:4500) you can switch clouds and create, list and delete resources.
Services not listed in the table show "Operation is not supported by this adapter":
this is a limitation of floci-ui at the pinned commit, not of the configuration.

## Structure

```
docker-compose.yml          AWS (4566), Azure (4577), GCP (4588), OCI (4599) emulators + UI (4500)
infra/                      Terraform: S3 bucket + SQS queue against the local endpoint
src/FlociDemo.Api/          Minimal API (Vertical Slice)
  Common/                   AWS client configuration (endpoint, path-style)
  Features/Documents/       UploadDocument slice (endpoint, validator, handler)
tests/FlociDemo.Tests/      Integration tests with xUnit + FluentAssertions
demo.ps1                    Script for recording the video
```

## How the code points to Floci

- **Terraform:** `infra/providers.tf` sets S3 and SQS `endpoints` to `http://localhost:4566`,
  dummy credentials, `s3_use_path_style = true` and disables validations against real AWS.
- **.NET:** `appsettings.json` defines `Aws:ServiceUrl`. When set, the SDK uses the emulator
  (`test`/`test` credentials, S3 in path-style). If left empty, it uses real AWS with the default
  credential chain.
- **UI:** `docker-compose.yml` passes it `FLOCI_ENDPOINT=http://floci-aws:4566` and the same `test`
  credentials, so it sees the same account (`000000000000`) as Terraform and the API.

## Notes

- Floci only covers what it documents; check [S3](https://floci.io/floci/services/s3/) and
  [SQS](https://floci.io/floci/services/sqs/) if you add operations.
- The first `terraform apply` may take about 25 s to create the queue; speed up that part if you record a video.
- An emulator is not a substitute for real AWS: IAM, limits and latencies may differ.
