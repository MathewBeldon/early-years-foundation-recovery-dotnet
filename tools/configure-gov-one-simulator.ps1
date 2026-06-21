# Configures the GOV.UK One Login simulator redirect URLs for the .NET app.
# Run after: podman compose up -d

param(
    [string]$SimulatorUrl = "http://localhost:3333",
    [string]$ServiceUrl = "http://localhost:5000",
    [string]$ClientId = "HGIOgho9HIRhgoepdIOPFdIUWgewi0jw"
)

$body = @{
    clientConfiguration = @{
        clientId = $ClientId
        redirectUrls = @("$ServiceUrl/users/auth/openid_connect/callback")
        postLogoutRedirectUrls = @("$ServiceUrl/users/sign_out")
        tokenAuthenticationMethod = "private_key_jwt"
    }
} | ConvertTo-Json -Depth 5

Write-Host "Configuring simulator at $SimulatorUrl ..."
Invoke-RestMethod -Method Post -Uri "$SimulatorUrl/config" -ContentType "application/json" -Body $body -ErrorAction Stop
Write-Host "Done. Callback: $ServiceUrl/users/auth/openid_connect/callback"
