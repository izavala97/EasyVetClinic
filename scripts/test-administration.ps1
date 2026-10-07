[CmdletBinding()]
param([string]$Configuration = 'Debug')

$ErrorActionPreference = 'Stop'
$repositoryRoot = Split-Path -Parent $PSScriptRoot
$apiDirectory = Join-Path $repositoryRoot 'WebAPI'
$assembly = Join-Path $apiDirectory "bin\$Configuration\net10.0\WebAPI.dll"
$testDirectory = Join-Path ([IO.Path]::GetTempPath()) ("EasyVetClinic-admin-" + [Guid]::NewGuid())
$database = Join-Path $testDirectory 'administration.db'
$processes = [Collections.Generic.List[Diagnostics.Process]]::new()
$script:checks = 0

function Start-TestApi {
    param([string]$Identity, [string]$Email = '', [string]$OwnerId = 'owner')

    $listener = [Net.Sockets.TcpListener]::new([Net.IPAddress]::Loopback, 0)
    $listener.Start()
    $port = $listener.LocalEndpoint.Port
    $listener.Stop()
    $url = "http://127.0.0.1:$port"
    $start = [Diagnostics.ProcessStartInfo]::new('dotnet')
    $start.ArgumentList.Add($assembly)
    $start.WorkingDirectory = $apiDirectory
    $start.UseShellExecute = $false
    $start.Environment['ASPNETCORE_ENVIRONMENT'] = 'Development'
    $start.Environment['ASPNETCORE_URLS'] = $url
    $start.Environment['ConnectionStrings__ClinicDatabase'] = "Data Source=$database"
    $start.Environment['Database__Provider'] = 'Sqlite'
    $start.Environment['PlatformAdministration__OwnerObjectId'] = $OwnerId
    $start.Environment['DevelopmentIdentity__ObjectId'] = $Identity
    $start.Environment['DevelopmentIdentity__DisplayName'] = $Identity
    $start.Environment['DevelopmentIdentity__Email'] = $Email
    $start.Environment['Logging__LogLevel__Default'] = 'Warning'
    $start.Environment['Logging__LogLevel__Microsoft.AspNetCore'] = 'Warning'
    $start.Environment['ApplicationInsights__ConnectionString'] = ''
    $start.Environment['APPLICATIONINSIGHTS_CONNECTION_STRING'] = ''
    $start.Environment['APPINSIGHTS_CONNECTION_STRING'] = ''
    $start.Environment['ApplicationInsights__DisableTelemetry'] = 'true'
    $process = [Diagnostics.Process]::Start($start)
    $processes.Add($process)
    $deadline = [DateTime]::UtcNow.AddSeconds(30)
    while ([DateTime]::UtcNow -lt $deadline) {
        if ($process.HasExited) { throw "API for $Identity exited before becoming ready." }
        try {
            $ready = Invoke-WebRequest "$url/health" -TimeoutSec 1 -SkipHttpErrorCheck
            if ($ready.StatusCode -eq 200) { return $url }
        }
        catch [System.Net.Http.HttpRequestException] { }
        catch [System.Threading.Tasks.TaskCanceledException] { }
        Start-Sleep -Milliseconds 200
    }
    throw "API for $Identity did not become ready."
}

function Assert-Request {
    param(
        [string]$Base, [string]$Path, [int]$Status = 200, [string]$Method = 'GET',
        [object]$Body = $null, [string]$Clinic = ''
    )
    $arguments = @{
        Uri = "$Base/api/$Path"; Method = $Method; SkipHttpErrorCheck = $true
        Headers = @{}; TimeoutSec = 15
    }
    if ($Clinic) { $arguments.Headers['X-EasyVet-Clinic-Id'] = $Clinic }
    if ($null -ne $Body) {
        $arguments.ContentType = 'application/json'
        $arguments.Body = ConvertTo-Json -InputObject $Body -Depth 10
    }
    $response = Invoke-WebRequest @arguments
    $content = if ($response.Content -is [byte[]]) {
        [Text.Encoding]::UTF8.GetString($response.Content)
    } else { [string]$response.Content }
    if ($response.StatusCode -ne $Status) {
        throw "$Method $Path expected $Status, got $($response.StatusCode): $content"
    }
    $script:checks++
    if ($content -and $response.Headers['Content-Type'] -match 'json') {
        return ConvertFrom-Json $content
    }
}

function Assert-Value {
    param([bool]$Condition, [string]$Message)
    if (-not $Condition) { throw $Message }
    $script:checks++
}

try {
    & dotnet build (Join-Path $apiDirectory 'WebAPI.csproj') --no-restore --configuration $Configuration
    if ($LASTEXITCODE -ne 0) { throw 'API build failed.' }
    New-Item -ItemType Directory -Path $testDirectory | Out-Null
    $owner = Start-TestApi 'owner' 'maizorin@hotmail.es'
    $admin = Start-TestApi 'clinic-admin' 'admin@example.test'
    $vet = Start-TestApi 'veterinarian' 'vet@example.test'
    $staff = Start-TestApi 'staff' 'staff@example.test'
    $outsider = Start-TestApi 'outsider' 'maizorin@hotmail.es'
    $noEmail = Start-TestApi 'no-email'
    $noOwner = Start-TestApi 'unconfigured' '' ''

    $me = Assert-Request $owner 'me'
    Assert-Value ($me.isPlatformOwner -and !$me.clinicId) 'Owner should have platform access without a clinic membership.'
    $me = Assert-Request $outsider 'me'
    Assert-Value (!$me.isPlatformOwner -and !$me.clinicId) 'Email alone must never grant owner access.'
    $me = Assert-Request $noOwner 'me'
    Assert-Value (!$me.isPlatformOwner) 'Empty owner configuration must fail closed.'
    $null = Assert-Request $outsider 'platform/clinics' 403
    $null = Assert-Request $noOwner 'platform/clinics' 403

    $profile = @{
        name = 'First clinic'; address = ''; logoUrl = ''
    }
    $null = Assert-Request $outsider 'onboarding/clinic' 403 'POST' $profile
    $first = Assert-Request $owner 'onboarding/clinic' 201 'POST' $profile
    $profile.name = 'Second clinic'
    $second = Assert-Request $owner 'onboarding/clinic' 201 'POST' $profile
    $clinics = Assert-Request $owner 'platform/clinics'
    Assert-Value ($clinics.Count -eq 2) 'Owner must be able to register more than one clinic.'
    $null = Assert-Request $owner 'dashboard' 403
    $null = Assert-Request $owner 'dashboard' 403 'GET' $null 'missing-clinic'
    $null = Assert-Request $owner 'dashboard' 200 'GET' $null $first.id
    $null = Assert-Request $outsider 'dashboard' 403 'GET' $null $first.id

    $invitation = Assert-Request $owner 'clinic/invitations' 200 'POST' @{ email = 'admin@example.test'; role = 'ClinicAdmin' } $first.id
    Assert-Value ($invitation.token.Length -eq 64) 'Invitation tokens must contain 256 bits of randomness.'
    $expiry = [DateTimeOffset]$invitation.invitation.expiresAt
    Assert-Value ($expiry -gt [DateTimeOffset]::UtcNow.AddDays(6.99) -and $expiry -le [DateTimeOffset]::UtcNow.AddDays(7)) 'Invitations must expire in seven days.'
    $null = Assert-Request $outsider 'invitations/accept' 403 'POST' @{ token = $invitation.token }
    $null = Assert-Request $noEmail 'invitations/accept' 403 'POST' @{ token = $invitation.token }
    $null = Assert-Request $admin 'invitations/accept' 200 'POST' @{ token = $invitation.token }
    $null = Assert-Request $admin 'invitations/accept' 400 'POST' @{ token = $invitation.token }
    $null = Assert-Request $admin 'invitations/accept' 400 'POST' @{ token = ('A' * 64) }
    $null = Assert-Request $admin 'invitations/accept' 400 'POST' @{ token = 'short' }
    $null = Assert-Request $admin 'platform/clinics' 403
    $null = Assert-Request $admin 'onboarding/clinic' 403 'POST' $profile
    $null = Assert-Request $admin 'dashboard' 200
    $null = Assert-Request $admin 'dashboard' 403 'GET' $null $second.id
    $null = Assert-Request $admin 'clinic/invitations' 400 'POST' @{ email = 'staff@example.test'; role = 'SuperAdmin' }
    $null = Assert-Request $admin 'clinic/invitations' 400 'POST' @{ email = 'invalid'; role = 'Staff' }
    $users = Assert-Request $admin 'clinic/users'
    $adminId = $users[0].id
    $null = Assert-Request $admin "clinic/users/$adminId" 409 'PUT' @{ role = 'Staff'; isActive = $true }
    $null = Assert-Request $admin "clinic/users/$adminId" 409 'PUT' @{ role = 'ClinicAdmin'; isActive = $false }
    $null = Assert-Request $owner "clinic/users/$adminId" 404 'PUT' @{ role = 'Staff'; isActive = $true } $second.id

    $revoked = Assert-Request $admin 'clinic/invitations' 200 'POST' @{ email = 'staff@example.test'; role = 'Staff' }
    $null = Assert-Request $owner "clinic/invitations/$($revoked.invitation.id)" 404 'DELETE' $null $second.id
    $null = Assert-Request $admin "clinic/invitations/$($revoked.invitation.id)" 204 'DELETE'
    $null = Assert-Request $staff 'invitations/accept' 400 'POST' @{ token = $revoked.token }
    $inviteVet = Assert-Request $admin 'clinic/invitations' 200 'POST' @{ email = 'vet@example.test'; role = 'Veterinarian' }
    $null = Assert-Request $vet 'invitations/accept' 200 'POST' @{ token = $inviteVet.token }
    $inviteStaff = Assert-Request $admin 'clinic/invitations' 200 'POST' @{ email = 'staff@example.test'; role = 'Staff' }
    $null = Assert-Request $staff 'invitations/accept' 200 'POST' @{ token = $inviteStaff.token }
    $null = Assert-Request $vet 'clinic/users' 403
    $null = Assert-Request $staff 'clinic/invitations' 403 'POST' @{ email = 'outsider@example.test'; role = 'ClinicAdmin' }
    $null = Assert-Request $staff 'clinic' 403 'PUT' $profile
    $null = Assert-Request $staff 'dashboard' 200
    $members = Assert-Request $admin 'clinic/users'
    $staffId = ($members | Where-Object displayName -eq 'staff').id
    $null = Assert-Request $admin "clinic/users/$staffId" 400 'PUT' @{ role = 'SuperAdmin'; isActive = $true }
    $null = Assert-Request $admin "clinic/users/$staffId" 204 'PUT' @{ role = 'Staff'; isActive = $false }
    $null = Assert-Request $staff 'dashboard' 403
    $null = Assert-Request $admin "clinic/users/$staffId" 204 'PUT' @{ role = 'Veterinarian'; isActive = $true }
    $me = Assert-Request $staff 'me'
    Assert-Value ($me.role -eq 'Veterinarian') 'Role changes must apply on the next request.'

    $null = Assert-Request $admin "platform/clinics/$($first.id)/access" 403 'PUT' @{ isSuspended = $true; reason = 'Unpaid' }
    $null = Assert-Request $owner "platform/clinics/$($first.id)/access" 400 'PUT' @{ isSuspended = $true; reason = '' }
    $pending = Assert-Request $admin 'clinic/invitations' 200 'POST' @{ email = 'maizorin@hotmail.es'; role = 'Staff' }
    $null = Assert-Request $owner "platform/clinics/$($first.id)/access" 204 'PUT' @{ isSuspended = $true; reason = 'Subscription unpaid' }
    $me = Assert-Request $admin 'me'
    Assert-Value ($me.isSuspended) 'Members must receive an explicit suspension flag.'
    $null = Assert-Request $admin 'dashboard' 403
    $null = Assert-Request $vet 'patients' 403
    $null = Assert-Request $staff 'clinic' 403
    $null = Assert-Request $admin 'clinic/users' 403
    $null = Assert-Request $outsider 'invitations/accept' 403 'POST' @{ token = $pending.token }
    $null = Assert-Request $owner 'dashboard' 200 'GET' $null $first.id
    $null = Assert-Request $owner 'clinic/users' 200 'GET' $null $first.id
    $null = Assert-Request $owner "platform/clinics/$($first.id)/access" 204 'PUT' @{ isSuspended = $false; reason = '' }
    $null = Assert-Request $admin 'dashboard' 200

    $multi = Assert-Request $owner 'clinic/invitations' 200 'POST' @{ email = 'admin@example.test'; role = 'ClinicAdmin' } $second.id
    $null = Assert-Request $admin 'invitations/accept' 200 'POST' @{ token = $multi.token }
    $me = Assert-Request $admin 'me'
    Assert-Value ($me.clinics.Count -eq 2 -and !$me.clinicId) 'Multiple memberships require explicit clinic selection.'
    $null = Assert-Request $admin 'dashboard' 403
    $null = Assert-Request $admin 'dashboard' 200 'GET' $null $first.id
    $null = Assert-Request $admin 'dashboard' 200 'GET' $null $second.id
    Write-Host "PASS: $script:checks administration checks."
}
finally {
    foreach ($process in $processes) {
        if (-not $process.HasExited) {
            Stop-Process -Id $process.Id
            $process.WaitForExit()
        }
        $process.Dispose()
    }
    foreach ($name in @('administration.db', 'administration.db-shm', 'administration.db-wal')) {
        $path = Join-Path $testDirectory $name
        if (Test-Path $path) { Remove-Item -LiteralPath $path -Force }
    }
    if (Test-Path $testDirectory) { Remove-Item -LiteralPath $testDirectory }
}
