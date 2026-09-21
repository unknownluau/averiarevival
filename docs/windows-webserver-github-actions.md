# Windows Webserver GitHub Actions Deploy

This repo now includes a deploy workflow at [.github/workflows/roblox-webserver-deploy.yml](/C:/Users/user/RiderProjects/averia-revival/.github/workflows/roblox-webserver-deploy.yml).

It does this:

1. Builds and publishes:
   - `Roblox.Website`
   - `Roblox.ApiProxy`
   - `Roblox.Services.Data`
   - `Roblox.Services.DataStore`
   - `Roblox.Services.Moderation`
   - `Roblox.Services.Donation`
2. Uploads the publish output as a workflow artifact.
3. Copies that bundle to the Windows webserver over SSH.
4. Runs [Deploy-Roblox-WebServices.ps1](/C:/Users/user/RiderProjects/averia-revival/scripts/windows/Deploy-Roblox-WebServices.ps1) on the server.
5. Stops the six Windows services, syncs the new files in, preserves server-local `appsettings*.json` and `game-servers.json`, and starts the services again.

## GitHub Side Setup

Create these repository secrets:

- `WEB_SERVER_HOST`
  - Your Windows webserver public IP or hostname.
- `WEB_SERVER_USER`
  - The Windows account that OpenSSH accepts.
- `WEB_SERVER_SSH_KEY`
  - The private key GitHub Actions will use.
- `WEB_SERVER_PORT`
  - Optional. Defaults to `22`.

Create these repository variables if you want to override defaults:

- `WEB_DEPLOY_ROOT`
  - Default: `C:\AveriaServices`
- `WEB_STAGING_ROOT`
  - Default: `averia-deploy`
- `WEB_APIPROXY_SERVICE`
  - Default: `Roblox.ApiProxy`
  - Must match the actual Windows service name or display name.
- `WEB_WEBSITE_SERVICE`
  - Default: `Roblox.Website`
  - Must match the actual Windows service name or display name.
- `WEB_DATA_SERVICE`
  - Default: `Roblox.Services.Data`
  - Must match the actual Windows service name or display name.
- `WEB_DATASTORE_SERVICE`
  - Default: `Roblox.Services.DataStore`
  - Must match the actual Windows service name or display name.
- `WEB_MODERATION_SERVICE`
  - Default: `Roblox.Services.Moderation`
  - Must match the actual Windows service name or display name.
- `WEB_DONATION_SERVICE`
  - Default: `Roblox.Services.Donation`
  - Must match the actual Windows service name or display name.

## One-Time Windows Server Setup

### 1. Install OpenSSH Server

Make sure the Windows server accepts SSH logins.

### 2. Create a deployment root

Example:

```powershell
New-Item -ItemType Directory -Force -Path 'C:\AveriaServices\Roblox.ApiProxy' | Out-Null
New-Item -ItemType Directory -Force -Path 'C:\AveriaServices\Roblox.Website' | Out-Null
New-Item -ItemType Directory -Force -Path 'C:\AveriaServices\Roblox.Services.Data' | Out-Null
New-Item -ItemType Directory -Force -Path 'C:\AveriaServices\Roblox.Services.DataStore' | Out-Null
New-Item -ItemType Directory -Force -Path 'C:\AveriaServices\Roblox.Services.Moderation' | Out-Null
New-Item -ItemType Directory -Force -Path 'C:\AveriaServices\Roblox.Services.Donation' | Out-Null
```

### 3. Put production config on the server

Because the deploy script preserves `appsettings*.json`, keep your production settings on the server inside those deploy folders.

At minimum:

- `C:\AveriaServices\Roblox.ApiProxy\appsettings.Production.json`
- `C:\AveriaServices\Roblox.Website\appsettings.Production.json`
- `C:\AveriaServices\Roblox.Services.Data\appsettings.Production.json`
- `C:\AveriaServices\Roblox.Services.DataStore\appsettings.Production.json`
- `C:\AveriaServices\Roblox.Services.Moderation\appsettings.Production.json`
- `C:\AveriaServices\Roblox.Services.Donation\appsettings.Production.json`

If you use website game server mappings, also keep this on the server:

- `C:\AveriaServices\Roblox.Website\game-servers.json`

Recommended split:

- `Roblox.ApiProxy`
  - `Authorization`
  - `RccAuthorization`
  - `FrontendProxy:DestinationPrefix`
  - `InternalServiceHosts`
  - `ReverseProxy`
  - `Jwt:Sessions`
  - `Postgres`
  - `Redis`
  - optional `RedisAuthentication`
- `Roblox.Website`
  - keep your existing full production config
- `Roblox.Services.Data`
  - `Authorization`
  - `Postgres`
  - `Redis`
  - optional `RedisAuthentication`
  - `Jwt:Sessions`
  - `IsCdnEnabled`
  - `CdnBaseUrl`
  - `Directories:Asset`
  - `Directories:Storage`
  - `CloudflareR2`
  - `AssetValidation`
- `Roblox.Services.DataStore`
  - `Postgres`
  - `Redis`
  - optional `RedisAuthentication`
  - `Authorization`
  - optional `RccAuthorization`
- `Roblox.Services.Moderation`
  - `Authorization`
  - `Postgres`
  - optional `Redis`
  - optional `RedisAuthentication`
- `Roblox.Services.Donation`
  - `Authorization`
  - `Postgres`
  - optional `Redis`
  - optional `RedisAuthentication`
  - `Stripe:WebhookSecret`
  - `Kofi:VerificationToken`
  - `Discord:WebhookUrl`

Keep these Donation credentials only in the server-local `appsettings.Production.json`. Do not commit them. Rotate any Stripe webhook secret or infrastructure credential that was previously committed.

Set the frontend destination in the server-local `Roblox.ApiProxy\appsettings.Production.json`:

```json
"FrontendProxy": {
  "DestinationPrefix": "http://127.0.0.1:3000/"
}
```

The frontend proxy only handles `averia.lol` and `www.averia.lol`. Specific `ReverseProxy` routes are evaluated first, then classic website APIs and assets continue to `Roblox.Website`, and remaining requests on those two hosts stream to the frontend.

Add the data upload route and cluster to the server-local `Roblox.ApiProxy\appsettings.Production.json`:

```json
"data-upload-route": {
  "ClusterId": "data-cluster",
  "Order": 3,
  "Match": {
    "Hosts": [ "data.averia.lol" ],
    "Methods": [ "POST" ],
    "Path": "/Data/Upload.ashx"
  }
}
```

```json
"data-cluster": {
  "Destinations": {
    "primary": { "Address": "http://127.0.0.1:5206" }
  }
}
```

Also add this entry to `InternalServiceRoutes` so the API proxy forwards the authenticated session:

```json
{
  "Hosts": [ "data.averia.lol" ],
  "PathPrefixes": [ "/Data/Upload.ashx" ]
}
```

Update the server-local `Roblox.ApiProxy\appsettings.Production.json` so both donation routes use the Donation cluster:

```json
"stripe-api-route": {
  "ClusterId": "donation-cluster",
  "Order": 4,
  "Match": { "Path": "/stripe-api/{**catch-all}" }
},
"donation-api-route": {
  "ClusterId": "donation-cluster",
  "Order": 5,
  "Match": { "Path": "/donation-api/{**catch-all}" }
}
```

The `donation-cluster` destination should remain `http://127.0.0.1:5205`.

### 4. Replace the old Stripe Windows service

The Donation app retains port `5205` and the public `/stripe-api/*` route, so the old Stripe service must be stopped before registering the replacement.

```powershell
Stop-Service -Name 'Roblox.Services.Stripe' -ErrorAction SilentlyContinue
# Use NSSM or your service wrapper to unregister Roblox.Services.Stripe.
```

Create the new `Roblox.Services.Donation` service after publishing and verifying its production config. Once the new service is verified, remove the old `C:\AveriaServices\Roblox.Services.Stripe` deploy folder.

### 5. Register the six Windows services

Use NSSM or your preferred service wrapper.

Each service should run from its deployed folder, not from the git checkout.

Suggested ports:

- `Roblox.ApiProxy` -> `http://127.0.0.1:5200`
- `Roblox.Website` -> `http://127.0.0.1:5000`
- `Roblox.Services.Data` -> `http://127.0.0.1:5206`
- `Roblox.Services.DataStore` -> `http://127.0.0.1:5203`
- `Roblox.Services.Moderation` -> `http://127.0.0.1:5204`
- `Roblox.Services.Donation` -> `http://127.0.0.1:5205`

Set environment variables per service:

- `ASPNETCORE_ENVIRONMENT=Production`
- `ASPNETCORE_URLS=http://127.0.0.1:PORT`

Example NSSM layout:

- Application: `C:\Program Files\dotnet\dotnet.exe`
- Arguments: `C:\AveriaServices\Roblox.ApiProxy\Roblox.ApiProxy.dll`
- Startup directory: `C:\AveriaServices\Roblox.ApiProxy`

Repeat for the other apps.

Register the data upload service with NSSM:

```powershell
New-Item -ItemType Directory -Force -Path 'C:\AveriaServices\Roblox.Services.Data' | Out-Null
& 'C:\ProjectX\nssm.exe' install 'Roblox.Services.Data' 'C:\Program Files\dotnet\dotnet.exe' 'C:\AveriaServices\Roblox.Services.Data\Roblox.Services.Data.dll'
& 'C:\ProjectX\nssm.exe' set 'Roblox.Services.Data' AppDirectory 'C:\AveriaServices\Roblox.Services.Data'
& 'C:\ProjectX\nssm.exe' set 'Roblox.Services.Data' AppEnvironmentExtra 'ASPNETCORE_ENVIRONMENT=Production' 'ASPNETCORE_URLS=http://127.0.0.1:5206'
& 'C:\ProjectX\nssm.exe' set 'Roblox.Services.Data' Start SERVICE_AUTO_START
& 'C:\ProjectX\nssm.exe' start 'Roblox.Services.Data'
```

After creating the services, verify the exact names with:

```powershell
Get-Service | Where-Object { $_.DisplayName -like '*Roblox*' -or $_.Name -like '*Roblox*' } | Select-Object Name, DisplayName
```

If the names differ from the defaults, set the GitHub repository variables:

- `WEB_APIPROXY_SERVICE`
- `WEB_WEBSITE_SERVICE`
- `WEB_DATA_SERVICE`
- `WEB_DATASTORE_SERVICE`
- `WEB_MODERATION_SERVICE`
- `WEB_DONATION_SERVICE`

## nginx Side

Point nginx at `Roblox.ApiProxy`, not `Roblox.Website`.

Public flow:

- `nginx` -> `Roblox.ApiProxy`
- `Roblox.ApiProxy` -> `Roblox.Website`
- `Roblox.ApiProxy` -> frontend for non-backend requests on `averia.lol` and `www.averia.lol`
- `Roblox.ApiProxy` -> `Roblox.Services.Data` for `POST /Data/Upload.ashx` on `data.averia.lol`
- `Roblox.ApiProxy` -> `Roblox.Services.DataStore` for `gamepersistence.averia.lol`
- `Roblox.ApiProxy` -> `Roblox.Services.Moderation` for `/moderation/*` on `assetgame.averia.lol` and `www.averia.lol`
- `Roblox.ApiProxy` -> `Roblox.Services.Donation` for `/stripe-api/*` and `/donation-api/*`

Configure Ko-fi to send webhooks to:

- `https://www.averia.lol/donation-api/kofi/webhook`

Keep these headers:

- `Host $host`
- `CF-Connecting-IP $http_cf_connecting_ip`
- WebSocket upgrade headers for upgraded connections

## Workflow Behavior

The deploy workflow triggers on:

- push to `main` when files under `Roblox/` or the deploy workflow/scripts change
- manual `workflow_dispatch`

The workflow always deploys all six web apps together. That is deliberate for now so the proxy, website, and extracted services stay in sync.

The uploaded publish bundle is staged under `%USERPROFILE%\averia-deploy\<run id>` on the server, then synced into `C:\AveriaServices\...`.

## Recommended Rollout

1. Set up the Windows services manually first.
2. Confirm you can run all six apps from `C:\AveriaServices`.
3. Switch nginx to `Roblox.ApiProxy`.
4. Add the GitHub secrets and variables.
5. Run the workflow manually once with `workflow_dispatch`.
6. After that, let pushes to `main` deploy automatically.
