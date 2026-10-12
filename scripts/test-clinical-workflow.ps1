[CmdletBinding()]
param([string]$Configuration = 'Debug')

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$api = Join-Path $root 'WebAPI'
$directory = Join-Path ([IO.Path]::GetTempPath()) ("EasyVetClinic-clinical-" + [Guid]::NewGuid())
$database = Join-Path $directory 'clinical.db'
$processes = [Collections.Generic.List[Diagnostics.Process]]::new()
$script:checks = 0

function Start-ClinicalApi([string]$identity, [string]$email) {
    $listener = [Net.Sockets.TcpListener]::new([Net.IPAddress]::Loopback, 0)
    $listener.Start(); $port = $listener.LocalEndpoint.Port; $listener.Stop()
    $url = "http://127.0.0.1:$port"
    $start = [Diagnostics.ProcessStartInfo]::new('dotnet')
    $start.ArgumentList.Add((Join-Path $api "bin\$Configuration\net10.0\WebAPI.dll"))
    $start.WorkingDirectory = $api
    $start.UseShellExecute = $false
    $start.Environment['ASPNETCORE_ENVIRONMENT'] = 'Development'
    $start.Environment['ASPNETCORE_URLS'] = $url
    $start.Environment['ConnectionStrings__ClinicDatabase'] = "Data Source=$database"
    $start.Environment['Database__Provider'] = 'Sqlite'
    $start.Environment['PlatformAdministration__OwnerObjectId'] = 'owner'
    $start.Environment['DevelopmentIdentity__ObjectId'] = $identity
    $start.Environment['DevelopmentIdentity__DisplayName'] = $identity
    $start.Environment['DevelopmentIdentity__Email'] = $email
    $start.Environment['Logging__LogLevel__Default'] = 'Warning'
    $start.Environment['ApplicationInsights__DisableTelemetry'] = 'true'
    $start.Environment['ApplicationInsights__ConnectionString'] = ''
    $start.Environment['APPLICATIONINSIGHTS_CONNECTION_STRING'] = ''
    $start.Environment['APPINSIGHTS_CONNECTION_STRING'] = ''
    $process = [Diagnostics.Process]::Start($start)
    $processes.Add($process)
    $deadline = [DateTime]::UtcNow.AddSeconds(40)
    while ([DateTime]::UtcNow -lt $deadline) {
        if ($process.HasExited) { throw "API exited for $identity." }
        try {
            if ((Invoke-WebRequest "$url/health" -TimeoutSec 1 -SkipHttpErrorCheck).StatusCode -eq 200) { return $url }
        } catch [Net.Http.HttpRequestException] { } catch [Threading.Tasks.TaskCanceledException] { }
        Start-Sleep -Milliseconds 200
    }
    throw "API did not become ready for $identity."
}

function Request([string]$base, [string]$path, [string]$method = 'GET', [object]$body = $null, [int]$status = 200, [string]$clinic = '') {
    $arguments = @{ Uri = "$base/api/$path"; Method = $method; SkipHttpErrorCheck = $true; TimeoutSec = 15; Headers = @{} }
    if ($clinic) { $arguments.Headers['X-EasyVet-Clinic-Id'] = $clinic }
    if ($null -ne $body) { $arguments.ContentType = 'application/json'; $arguments.Body = ConvertTo-Json $body -Depth 20 }
    $response = Invoke-WebRequest @arguments
    $content = if ($response.Content -is [byte[]]) { [Text.Encoding]::UTF8.GetString($response.Content) } else { [string]$response.Content }
    if ($response.StatusCode -ne $status) { throw "$method $path expected $status, got $($response.StatusCode): $content" }
    $script:checks++
    if ($content -and $response.Headers['Content-Type'] -match 'json') { return ConvertFrom-Json $content }
}

function Check([bool]$condition, [string]$message) {
    if (!$condition) { throw $message }
    $script:checks++
}

try {
    & dotnet build (Join-Path $api 'WebAPI.csproj') --no-restore --configuration $Configuration
    if ($LASTEXITCODE -ne 0) { throw 'API build failed.' }
    New-Item -ItemType Directory -Path $directory | Out-Null
    $owner = Start-ClinicalApi 'owner' 'owner@example.test'
    $profile = @{ name = 'Clinical test'; address = ''; logoUrl = '' }
    $clinic = Request $owner 'onboarding/clinic' 'POST' $profile 201
    $vet = Start-ClinicalApi 'vet' 'vet@example.test'
    $staff = Start-ClinicalApi 'staff' 'staff@example.test'
    foreach ($member in @(@{ base = $vet; email = 'vet@example.test'; role = 'Veterinarian' }, @{ base = $staff; email = 'staff@example.test'; role = 'Staff' })) {
        $invite = Request $owner 'clinic/invitations' 'POST' @{ email = $member.email; role = $member.role } 200 $clinic.id
        $null = Request $member.base 'invitations/accept' 'POST' @{ token = $invite.token }
    }
    $profile.timeZoneId = 'America/Mexico_City'
    $savedProfile = Request $owner 'clinic' 'PUT' $profile 200 $clinic.id
    Check ($savedProfile.language -eq 'en') 'Existing clinics must default to English.'
    $profile.language = 'es'
    $savedLanguage = Request $owner 'clinic' 'PUT' $profile 200 $clinic.id
    Check ($savedLanguage.language -eq 'es') 'Clinic language must be saved.'
    $staffProfile = Request $staff 'clinic'
    Check ($staffProfile.language -eq 'es') 'All clinic members must receive the same language.'
    $null = Request $staff 'clinic' 'PUT' $profile 403
    $profile.language = 'fr'
    $null = Request $owner 'clinic' 'PUT' $profile 400 $clinic.id
    $persistedProfile = Request $owner 'clinic' 'GET' $null 200 $clinic.id
    Check ($persistedProfile.language -eq 'es') 'Invalid languages must not overwrite the saved preference.'
    $profile.Remove('language')
    $preservedLanguage = Request $owner 'clinic' 'PUT' $profile 200 $clinic.id
    Check ($preservedLanguage.language -eq 'es') 'Updates without language must preserve the preference.'
    $profile.language = 'en'
    $null = Request $owner 'clinic' 'PUT' $profile 200 $clinic.id
    Check ($savedProfile.name -eq 'Clinical test' -and $savedProfile.timeZoneId -eq 'America/Mexico_City') 'Clinic settings must save without clinic-wide clinician credentials.'
    Check ($null -eq $savedProfile.PSObject.Properties['veterinarianName'] -and
        $null -eq $savedProfile.PSObject.Properties['veterinarianTitles'] -and
        $null -eq $savedProfile.PSObject.Properties['veterinarianLicenseNumber']) 'Clinic profiles must not expose obsolete clinician fields.'
    $profile.timeZoneId = 'Invalid/Zone'
    $null = Request $owner 'clinic' 'PUT' $profile 400 $clinic.id
    $profile.timeZoneId = 'America/Mexico_City'
    $null = Request $owner 'clinicians/vet/credentials' 'PUT' @{ professionalTitles = 'DVM'; licenseNumber = 'TEST-123' } 204 $clinic.id
    $patient = Request $staff 'patients' 'POST' @{
        name = 'Luna'; species = 'Feline'
        newGuardian = @{ name = 'Guardian'; phone = '5550100' }
    } 201
    Check ($patient.guardianName -eq 'Guardian') 'Combined intake should create and link the guardian.'
    $null = Request $staff 'patients' 'POST' @{
        guardianIds = @(); name = 'Duplicate'; species = 'Feline'; breed = ''; sex = ''; weight = ''; color = ''; allergies = ''
        newGuardian = @{ name = 'Duplicate'; phone = '5550100' }
    } 409
    $second = Request $staff 'guardians' 'POST' @{ name = 'Second guardian'; phone = '5550200' } 201
    $null = Request $staff "patients/$($patient.id)/guardians/$($second.id)" 'POST' $null 204
    $null = Request $staff "patients/$($patient.id)/primary-guardian/$($second.id)" 'PUT' $null 204
    $searched = Request $staff 'patients?query=5550100'
    Check (@($searched).Count -eq 1) 'Patient search must include secondary guardians.'
    $null = Request $vet "patients/$($patient.id)/weights" 'POST' @{ value = 4.25; unit = 'kg'; measuredOn = '2026-10-07'; recordedBy = 'vet' } 201
    $null = Request $vet "patients/$($patient.id)/weights" 'POST' @{ value = 3; unit = 'kg'; measuredOn = '2025-01-01'; recordedBy = 'vet' } 201
    $record = Request $staff "patients/$($patient.id)"
    Check ($record.weight -eq '4.25 kg') 'Backdated measurement must not replace the current weight.'
    $null = Request $staff "patients/$($patient.id)/weights" 'POST' @{ value = 4; unit = 'kg'; measuredOn = '2026-10-07'; recordedBy = 'staff' } 403
    $appointmentRequest = @{ patientId = $patient.id; startsAt = '2026-10-08T09:00:00Z'; localStartsAt = '2026-10-08T09:00'; reason = 'General consultation'; clinicianName = ''; clinicianObjectId = 'vet'; durationMinutes = 45 }
    $appointment = Request $staff 'appointments' 'POST' $appointmentRequest 201
    Check (([DateTimeOffset]$appointment.startsAt).UtcDateTime.Hour -eq 15) 'Clinic time zone must determine the scheduled instant.'
    $appointmentRequest.localStartsAt = '2026-10-08T09:30'
    $null = Request $staff 'appointments' 'POST' $appointmentRequest 409
    $appointmentRequest.clinicianObjectId = 'owner'
    $null = Request $owner 'appointments' 'POST' $appointmentRequest 201 $clinic.id
    $appointmentRequest.clinicianObjectId = 'vet'
    $appointmentRequest.status = 'CheckedIn'
    $appointmentRequest.expectedVersion = $appointment.version
    $appointmentRequest.localStartsAt = '2026-10-08T09:00'
    $appointment = Request $staff "appointments/$($appointment.id)" 'PUT' $appointmentRequest
    $null = Request $staff "appointments/$($appointment.id)" 'PUT' $appointmentRequest 409
    $null = Request $owner "patients/$($patient.id)/consultations" 'POST' @{ appointmentId = $appointment.id; clinicianObjectId = 'owner' } 400 $clinic.id
    $visit = Request $vet "patients/$($patient.id)/consultations" 'POST' @{ appointmentId = $appointment.id; clinicianObjectId = 'vet' } 201
    $retry = Request $vet "patients/$($patient.id)/consultations" 'POST' @{ appointmentId = $appointment.id; clinicianObjectId = 'vet' }
    Check ($visit.id -eq $retry.id) 'Starting care twice from an appointment must return the same consultation.'
    $appointmentRequest.localStartsAt = '2026-10-09T09:00'
    $parallelAppointment = Request $staff 'appointments' 'POST' $appointmentRequest 201
    $client = [Net.Http.HttpClient]::new()
    $contents = [Collections.Generic.List[Net.Http.StringContent]]::new()
    try {
        $payload = ConvertTo-Json @{ appointmentId = $parallelAppointment.id }
        $tasks = 1..3 | ForEach-Object {
            $content = [Net.Http.StringContent]::new($payload, [Text.Encoding]::UTF8, 'application/json')
            $contents.Add($content)
            $client.PostAsync("$vet/api/patients/$($patient.id)/consultations", $content)
        }
        $startedIds = foreach ($task in $tasks) {
            $response = $task.GetAwaiter().GetResult()
            try {
                $body = $response.Content.ReadAsStringAsync().GetAwaiter().GetResult()
                Check ([int]$response.StatusCode -in @(200, 201)) "Concurrent consultation start failed: $body"
                (ConvertFrom-Json $body).id
            } finally { $response.Dispose() }
        }
        Check (@($startedIds | Select-Object -Unique).Count -eq 1) 'Concurrent appointment starts must create only one consultation.'
    } finally {
        foreach ($content in $contents) { $content.Dispose() }
        $client.Dispose()
    }
    $visit = Request $vet "consultations/$($visit.id)"
    Check ($visit.clinicianName -eq 'vet' -and $visit.clinicianTitles -eq 'DVM' -and $visit.clinicianLicenseNumber -eq 'TEST-123') 'Clinical attribution must snapshot the assigned clinician.'
    $save = @{ chiefComplaint = 'Routine'; clinicalNotes = 'Healthy'; diagnosis = 'Wellness'; instructions = 'Follow up'; status = 'InProgress'; prescriptionItems = @(@{ medicationName = 'External medication'; presentation = 'Tablet'; concentration = ''; dosageDirections = 'As directed'; sortOrder = 0 }); expectedVersion = $visit.version }
    $null = Request $staff "consultations/$($visit.id)" 'PUT' $save 403
    $saved = Request $vet "consultations/$($visit.id)" 'PUT' $save
    $null = Request $vet "consultations/$($visit.id)" 'PUT' $save 409
    $save.expectedVersion = $saved.version
    $save.prescriptionItems[0].id = $saved.prescriptionItems[0].id
    $save.status = 'Completed'
    $completed = Request $vet "consultations/$($visit.id)" 'PUT' $save
    Check ($completed.prescriptionItems[0].id -eq $saved.prescriptionItems[0].id) 'Prescription identifiers should remain stable.'
    $save.expectedVersion = $completed.version
    $null = Request $vet "consultations/$($visit.id)" 'PUT' $save 409
    $null = Request $owner "consultations/$($visit.id)" 'DELETE' $null 409 $clinic.id
    $null = Request $vet "consultations/$($visit.id)/amendments" 'POST' @{ reason = 'Correction'; note = 'Updated follow-up recommendation'; expectedVersion = $completed.version } 204
    $amended = Request $staff "consultations/$($visit.id)"
    Check ($amended.amendments.Count -eq 1 -and $amended.clinicalNotes -eq 'Healthy') 'Amendments must preserve original notes.'
    $record = Request $staff "patients/$($patient.id)"
    Check (![string]::IsNullOrWhiteSpace($record.lastVisit)) 'Completed care must update last visit.'
    $cancelled = Request $vet "patients/$($patient.id)/consultations" 'POST' @{ clinicianObjectId = 'vet' } 201
    $cancelled = Request $vet "consultations/$($cancelled.id)"
    $null = Request $vet "consultations/$($cancelled.id)/cancel" 'POST' @{ reason = 'Created by mistake'; expectedVersion = $cancelled.version } 204
    $cancelled = Request $staff "consultations/$($cancelled.id)"
    Check ($cancelled.status -eq 'Cancelled' -and $cancelled.cancellationReason -eq 'Created by mistake') 'Cancellation must retain history and reason.'
    Write-Host "Clinical workflow verification passed: $script:checks checks."
} finally {
    foreach ($process in $processes) {
        if (!$process.HasExited) { Stop-Process -Id $process.Id -Force; $process.WaitForExit() }
        $process.Dispose()
    }
    if (Test-Path $directory) { Remove-Item -LiteralPath $directory -Recurse -Force }
}
