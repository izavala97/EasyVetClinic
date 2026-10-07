[CmdletBinding()]
param([string]$Configuration = 'Debug')

$ErrorActionPreference = 'Stop'
$repositoryRoot = Split-Path -Parent $PSScriptRoot
$apiDirectory = Join-Path $repositoryRoot 'WebAPI'
$assembly = Join-Path $apiDirectory "bin\$Configuration\net10.0\WebAPI.dll"
$testDirectory = Join-Path ([IO.Path]::GetTempPath()) ("EasyVetClinic-commerce-" + [Guid]::NewGuid())
$database = Join-Path $testDirectory 'commercial.db'
$processes = [Collections.Generic.List[Diagnostics.Process]]::new()
$script:checks = 0

function Start-TestApi {
    param([string]$Identity, [string]$Email = '', [string]$OwnerId = 'commerce-owner')

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
    $deadline = [DateTime]::UtcNow.AddSeconds(45)
    while ([DateTime]::UtcNow -lt $deadline) {
        if ($process.HasExited) { throw "API for $Identity exited before becoming ready." }
        try {
            $ready = Invoke-WebRequest "$url/health" -TimeoutSec 1 -SkipHttpErrorCheck
            if ($ready.StatusCode -eq 200) { return $url }
        }
        catch [System.Net.Http.HttpRequestException] { }
        catch [System.Threading.Tasks.TaskCanceledException] { }
        Start-Sleep -Milliseconds 250
    }
    throw "API for $Identity did not become ready."
}

function Invoke-Api {
    param(
        [string]$Base, [string]$Path, [string]$Method = 'GET',
        [object]$Body = $null, [string]$Clinic = '', [int]$Status = 200
    )
    $arguments = @{
        Uri = "$Base/api/$Path"; Method = $Method; SkipHttpErrorCheck = $true
        Headers = @{}; TimeoutSec = 20
    }
    if ($Clinic) { $arguments.Headers['X-EasyVet-Clinic-Id'] = $Clinic }
    if ($null -ne $Body) {
        $arguments.ContentType = 'application/json'
        $arguments.Body = ConvertTo-Json -InputObject $Body -Depth 12
    }
    $response = Invoke-WebRequest @arguments
    $content = if ($response.Content -is [byte[]]) {
        [Text.Encoding]::UTF8.GetString($response.Content)
    } else { [string]$response.Content }
    if ($response.StatusCode -ne $Status) {
        throw "$Method /api/$Path expected $Status, got $($response.StatusCode): $content"
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
    & dotnet run --project (Join-Path $PSScriptRoot 'MigrationUpgradeCheck\MigrationUpgradeCheck.csproj') --configuration $Configuration -- (Join-Path $testDirectory 'upgrade.db')
    if ($LASTEXITCODE -ne 0) { throw 'Populated pre-commerce migration upgrade checks failed.' }
    $owner = Start-TestApi 'commerce-owner' 'owner@example.test'
    $admin = Start-TestApi 'commerce-admin' 'admin@example.test'
    $vet = Start-TestApi 'commerce-vet' 'vet@example.test'

    $profile = @{
        name = 'Commercial clinic'; address = ''; logoUrl = ''
    }
    $clinicA = Invoke-Api $owner 'onboarding/clinic' 'POST' $profile -Status 201
    $profile.name = 'Second commercial clinic'
    $clinicB = Invoke-Api $owner 'onboarding/clinic' 'POST' $profile -Status 201
    $invitation = Invoke-Api $owner 'clinic/invitations' 'POST' @{ email = 'admin@example.test'; role = 'ClinicAdmin' } $clinicA.id 200
    $null = Invoke-Api $admin 'invitations/accept' 'POST' @{ token = $invitation.token } -Status 200
    $invitation = Invoke-Api $owner 'clinic/invitations' 'POST' @{ email = 'vet@example.test'; role = 'Veterinarian' } $clinicA.id 200
    $null = Invoke-Api $vet 'invitations/accept' 'POST' @{ token = $invitation.token } -Status 200
    $invitation = Invoke-Api $owner 'clinic/invitations' 'POST' @{ email = 'admin@example.test'; role = 'ClinicAdmin' } $clinicB.id 200
    $null = Invoke-Api $admin 'invitations/accept' 'POST' @{ token = $invitation.token } -Status 200

    $patient = Invoke-Api $vet 'patients' 'POST' @{
        guardianIds = @(); name = 'Luna'; species = 'Dog'; breed = 'Mixed'; sex = 'Female'
        weight = '12'; color = 'Brown'; allergies = ''; newGuardian = @{
            name = 'Pet guardian'; phone = '5551234567'; alternatePhone = ''; address = ''
            identityType = ''; identityNumber = ''; identityDocumentUrl = ''
        }
    } $clinicA.id 201
    $consultation = Invoke-Api $vet "patients/$($patient.id)/consultations" 'POST' @{
        clinicianName = ''; appointmentId = $null; clinicianObjectId = $null
    } $clinicA.id 201
    $lookupPath = 'billing/accounts?consultationId=' + $consultation.id
    $unlinked = @(Invoke-Api $vet $lookupPath 'GET' $null $clinicA.id)
    Assert-Value ($unlinked.Count -eq 0) 'A valid consultation with no account must return an empty filtered list.'
    $httpClient = [Net.Http.HttpClient]::new()
    try {
        $pendingCreates = @()
        foreach ($attempt in 1..2) {
            $message = [Net.Http.HttpRequestMessage]::new([Net.Http.HttpMethod]::Post, "$vet/api/billing/accounts")
            $message.Headers.Add('X-EasyVet-Clinic-Id', $clinicA.id)
            $message.Content = [Net.Http.StringContent]::new(
                (ConvertTo-Json @{ patientId = $patient.id; consultationId = $consultation.id; currency = 'MXN' }),
                [Text.Encoding]::UTF8, 'application/json')
            $pendingCreates += @{ Message = $message; Task = $httpClient.SendAsync($message) }
        }
        $createdIds = @()
        $createdStatuses = @()
        foreach ($pending in $pendingCreates) {
            $response = $pending.Task.GetAwaiter().GetResult()
            try {
                $content = $response.Content.ReadAsStringAsync().GetAwaiter().GetResult()
                Assert-Value ([int]$response.StatusCode -in @(200, 201)) "Simultaneous linked creation must not fail: $content"
                $createdStatuses += [int]$response.StatusCode
                $createdIds += (ConvertFrom-Json $content).id
            }
            finally { $response.Dispose(); $pending.Message.Dispose() }
        }
        Assert-Value (@($createdIds | Select-Object -Unique).Count -eq 1) 'Concurrent consultation creation must return the same account.'
        Assert-Value (@($createdStatuses | Where-Object { $_ -eq 201 }).Count -eq 1) 'Exactly one concurrent request must create an account.'
    }
    finally { $httpClient.Dispose() }
    $linkedAccount = Invoke-Api $vet "billing/accounts/$($createdIds[0])" 'GET' $null $clinicA.id
    Assert-Value ($linkedAccount.patientId -eq $patient.id -and $linkedAccount.consultationId -eq $consultation.id) 'Consultation-linked accounts must preserve patient and consultation references.'
    $linked = @(Invoke-Api $vet $lookupPath 'GET' $null $clinicA.id)
    Assert-Value ($linked.Count -eq 1 -and $linked[0].id -eq $linkedAccount.id) 'Consultation lookup must return its linked account.'
    $null = Invoke-Api $admin $lookupPath 'GET' $null $clinicB.id 404
    $null = Invoke-Api $vet 'billing/accounts?consultationId=missing' 'GET' $null $clinicA.id 404
    $replayedAccount = Invoke-Api $vet 'billing/accounts' 'POST' @{
        patientId = $patient.id; consultationId = $consultation.id; currency = 'MXN'
    } $clinicA.id 200
    Assert-Value ($replayedAccount.id -eq $linkedAccount.id) 'Linked account creation retries must return the existing account.'
    $null = Invoke-Api $vet 'billing/accounts' 'POST' @{
        patientId = $patient.id; consultationId = $consultation.id; currency = 'USD'
    } $clinicA.id 409

    $productBody = @{
        name = 'Diet food'; category = 'Food'; description = ''; sku = 'FOOD-01'; unit = 'kg'
        unitPrice = 6.25; stockOnHand = 5; lowStockThreshold = 1; trackStock = $true; isFavorite = $true
    }
    $product = Invoke-Api $admin 'catalog/products' 'POST' $productBody $clinicA.id 201
    $null = Invoke-Api $admin 'sales' 'POST' @{
        patientId = $null; paymentMethod = 'Cash'
        lines = @(@{ productId = $product.id; quantity = 1 }); serviceLines = @()
    } $clinicA.id 410
    $legacyStock = Invoke-Api $admin "catalog/products/$($product.id)" 'GET' $null $clinicA.id
    Assert-Value ($legacyStock.stockOnHand -eq 5) 'Disabled legacy checkout must never mutate inventory.'
    Assert-Value ($product.stockOnHand -eq 5 -and $product.isFavorite -and $product.version -eq 1) 'Product creation must preserve stock, favorite, and version.'
    $serviceBody = @{
        name = 'Consultation'; category = 'Clinical'; description = 'Standard appointment'; sku = $null
        unit = 'visit'; unitPrice = 30; stockOnHand = 0; lowStockThreshold = 0; trackStock = $false; isFavorite = $true
    }
    $service = Invoke-Api $admin 'catalog/services' 'POST' $serviceBody $clinicA.id 201
    $linkedAccount = Invoke-Api $vet "billing/accounts/$($linkedAccount.id)/lines" 'POST' @{
        catalogItemId = $service.id; itemType = 'Service'; quantity = 1; expectedVersion = $linkedAccount.version
    } $clinicA.id 200
    Assert-Value ($linkedAccount.patientName -eq 'Luna' -and $linkedAccount.consultationId -eq $consultation.id) 'AddLine must preserve the linked patient name and consultation context.'
    $linkedLine = $linkedAccount.lines[0].id
    $linkedAccount = Invoke-Api $vet "billing/accounts/$($linkedAccount.id)/lines/$linkedLine" 'PUT' @{
        quantity = 2; expectedVersion = $linkedAccount.version
    } $clinicA.id 200
    Assert-Value ($linkedAccount.patientName -eq 'Luna') 'Line updates must preserve the patient name.'
    $linkedDeletePath = "billing/accounts/$($linkedAccount.id)/lines/$linkedLine" + "?expectedVersion=$($linkedAccount.version)"
    $linkedAccount = Invoke-Api $vet $linkedDeletePath 'DELETE' $null $clinicA.id 200
    Assert-Value ($linkedAccount.patientName -eq 'Luna') 'Line deletion must preserve the patient name.'
    $productBody.isFavorite = $false
    $product = Invoke-Api $admin "catalog/products/$($product.id)?expectedVersion=1" 'PUT' $productBody $clinicA.id 200
    $serviceBody.unitPrice = 30
    $service = Invoke-Api $admin "catalog/services/$($service.id)?expectedVersion=1" 'PUT' $serviceBody $clinicA.id 200
    Assert-Value ($product.version -eq 2 -and $service.version -eq 2) 'Catalog updates must advance optimistic versions.'
    $null = Invoke-Api $vet 'catalog/products' 'POST' $productBody $clinicA.id 403
    $null = Invoke-Api $vet 'inventory/adjustments' 'POST' @{ productId = $product.id; quantityChange = 1; reason = 'Unauthorized' } $clinicA.id 403

    $otherProductBody = @{
        name = 'Other food'; category = 'Food'; description = ''; sku = 'FOOD-02'; unit = 'kg'
        unitPrice = 6.25; stockOnHand = 5; lowStockThreshold = 1; trackStock = $true; isFavorite = $false
    }
    $otherProduct = Invoke-Api $admin 'catalog/products' 'POST' $otherProductBody $clinicB.id 201
    $account = Invoke-Api $admin 'billing/accounts' 'POST' @{ patientId = $null; consultationId = $null; currency = 'USD' } $clinicA.id 201
    $null = Invoke-Api $admin "billing/accounts/$($account.id)/lines" 'POST' @{
        catalogItemId = $otherProduct.id; itemType = 'Product'; quantity = 1; expectedVersion = 1
    } $clinicA.id 404

    $account = Invoke-Api $admin "billing/accounts/$($account.id)/lines" 'POST' @{
        catalogItemId = $product.id; itemType = 'Product'; quantity = 2; expectedVersion = 1; expectedCatalogVersion = 2
    } $clinicA.id 200
    $lineId = $account.lines[0].id
    $null = Invoke-Api $vet "billing/accounts/$($account.id)/lines/$lineId" 'PUT' @{
        quantity = 2; unitPrice = 1; expectedVersion = 2
    } $clinicA.id 403
    $account = Invoke-Api $admin "billing/accounts/$($account.id)/lines" 'POST' @{
        catalogItemId = $service.id; itemType = 'Service'; quantity = 1; expectedVersion = 2; expectedCatalogVersion = 2
    } $clinicA.id 200
    Assert-Value ($account.total -eq 42.50 -and $account.lines.Count -eq 2) 'Server must calculate totals from catalog snapshots.'
    $lineId = ($account.lines | Where-Object { $_.itemType -eq 'Product' }).id
    $null = Invoke-Api $admin "billing/accounts/$($account.id)/lines/$lineId" 'PUT' @{
        quantity = 2; unitPrice = 7; expectedVersion = $account.version
    } $clinicA.id 400
    $account = Invoke-Api $admin "billing/accounts/$($account.id)/lines/$lineId" 'PUT' @{
        quantity = 2; unitPrice = 7; expectedVersion = $account.version; priceOverrideReason = 'Approved special price'
    } $clinicA.id 200
    Assert-Value ($account.total -eq 44 -and ($account.lines | Where-Object { $_.id -eq $lineId }).priceOverrideReason -eq 'Approved special price') 'Administrator override must recalculate totals and persist its reason.'

    $account = Invoke-Api $admin "billing/accounts/$($account.id)/open" 'POST' @{ expectedVersion = $account.version } $clinicA.id 200
    $account = Invoke-Api $admin "billing/accounts/$($account.id)/payments" 'POST' @{
        amount = 10; method = 'Cash'; idempotencyKey = 'pay-1'; expectedVersion = $account.version; note = 'Deposit'; cashReceived = 20
    } $clinicA.id 200
    Assert-Value ($account.status -eq 'Open' -and $account.paymentStatus -eq 'PartiallyPaid' -and $account.balanceDue -eq 34) 'Partial payment must not close an account.'
    $null = Invoke-Api $admin "billing/accounts/$($account.id)/payments" 'POST' @{
        amount = 11; method = 'Cash'; idempotencyKey = 'pay-1'; expectedVersion = 4; note = 'Deposit'; cashReceived = 20
    } $clinicA.id 409
    $null = Invoke-Api $admin "billing/accounts/$($account.id)/payments" 'POST' @{
        amount = 10; method = 'Cash'; idempotencyKey = 'pay-1'; expectedVersion = 4; note = 'Deposit'; cashReceived = 20
    } $clinicA.id 200
    Assert-Value ($account.payments.Count -eq 1) 'Repeated idempotency key must not duplicate payment.'
    $null = Invoke-Api $admin "billing/accounts/$($account.id)/payments" 'POST' @{
        amount = 10; method = 'Cash'; idempotencyKey = 'pay-1'; expectedVersion = 4; note = 'Deposit'; cashReceived = 30
    } $clinicA.id 409
    $null = Invoke-Api $admin "billing/accounts/$($account.id)/payments" 'POST' @{
        amount = 1; method = 'Cash'; idempotencyKey = 'invalid-cash'; expectedVersion = $account.version
    } $clinicA.id 400
    $null = Invoke-Api $admin "billing/accounts/$($account.id)/payments" 'POST' @{
        amount = 1; method = 'Cash'; idempotencyKey = 'invalid-cash'; expectedVersion = $account.version; cashReceived = 0.5
    } $clinicA.id 400
    $null = Invoke-Api $admin "billing/accounts/$($account.id)/payments" 'POST' @{
        amount = 1; method = 'Card'; idempotencyKey = 'invalid-card'; expectedVersion = $account.version; cashReceived = 1
    } $clinicA.id 400
    $inventory = Invoke-Api $admin 'inventory' 'GET' $null $clinicA.id
    Assert-Value ($inventory[0].stockOnHand -eq 5) 'Payment must not decrement inventory.'
    $account = Invoke-Api $admin "billing/accounts/$($account.id)/fulfill" 'POST' @{ expectedVersion = $account.version } $clinicA.id 200
    $inventory = Invoke-Api $admin 'inventory' 'GET' $null $clinicA.id
    Assert-Value (($inventory | Where-Object { $_.id -eq $product.id }).stockOnHand -eq 3) 'Fulfillment must decrement stock once.'
    $movementCount = (Invoke-Api $admin "inventory/movements?productId=$($product.id)" 'GET' $null $clinicA.id).Count
    $account = Invoke-Api $admin "billing/accounts/$($account.id)/fulfill" 'POST' @{ expectedVersion = $account.version } $clinicA.id 200
    $movementCountAfterRetry = (Invoke-Api $admin "inventory/movements?productId=$($product.id)" 'GET' $null $clinicA.id).Count
    Assert-Value ($movementCount -eq $movementCountAfterRetry -and $movementCountAfterRetry -eq 2) 'Repeated fulfillment must not write another stock movement.'

    $account = Invoke-Api $admin "billing/accounts/$($account.id)/payments" 'POST' @{
        amount = 34; method = 'Card'; idempotencyKey = 'pay-2'; expectedVersion = $account.version; note = ''
    } $clinicA.id 200
    Assert-Value ($account.status -eq 'Closed' -and $account.paymentStatus -eq 'Paid' -and $account.balanceDue -eq 0) 'Full payment must close the account.'
    $version = $account.version
    $account = Invoke-Api $admin "billing/accounts/$($account.id)/refunds" 'POST' @{
        amount = 5; method = 'Card'; idempotencyKey = 'refund-1'; expectedVersion = $version; note = 'Customer refund'
    } $clinicA.id 200
    Assert-Value ($account.status -eq 'Closed' -and $account.paymentStatus -eq 'PartiallyRefunded' -and $account.amountPaid -eq 39) 'Refund must be recorded separately from account status.'
    $receipt = Invoke-Api $admin "billing/accounts/$($account.id)" 'GET' $null $clinicA.id
    Assert-Value ($receipt.lines.Count -eq 2 -and $receipt.payments.Count -eq 3 -and $receipt.currency -eq 'USD') 'Account detail must serve a stable receipt and payment history.'
    $cashPayment = $receipt.payments | Where-Object { $_.method -eq 'Cash' }
    Assert-Value ($cashPayment.cashReceived -eq 20 -and $cashPayment.changeGiven -eq 10 -and $cashPayment.idempotencyKey -eq 'pay-1') 'Receipt must persist cash tender, server-calculated change, and the replay correlation key.'
    Assert-Value (($receipt.lines | Where-Object { $_.id -eq $lineId }).priceOverrideReason -eq 'Approved special price') 'Price override reason must remain available in persisted receipt history.'
    $history = Invoke-Api $admin 'billing/accounts' 'GET' $null $clinicA.id
    Assert-Value (($history | Where-Object { $_.id -eq $account.id }).patientId -eq $null) 'Standalone accounts must appear in clinic account history.'
    $inventory = Invoke-Api $admin 'inventory' 'GET' $null $clinicA.id
    Assert-Value (($inventory | Where-Object { $_.id -eq $product.id }).stockOnHand -eq 3) 'Refund must not restock automatically.'

    $null = Invoke-Api $admin 'inventory/adjustments' 'POST' @{
        productId = $product.id; quantityChange = 2.5; reason = 'Supplier delivery'; idempotencyKey = 'receive-1'
    } $clinicA.id 200
    $null = Invoke-Api $admin 'inventory/adjustments' 'POST' @{
        productId = $product.id; quantityChange = 2.5; reason = 'Supplier delivery'; idempotencyKey = 'receive-1'
    } $clinicA.id 200
    $null = Invoke-Api $admin 'inventory/adjustments' 'POST' @{
        productId = $product.id; quantityChange = 2; reason = 'Supplier delivery'; idempotencyKey = 'receive-1'
    } $clinicA.id 409
    $null = Invoke-Api $admin 'inventory/adjustments' 'POST' @{
        productId = $product.id; quantityChange = 2.5; reason = 'Different reason'; idempotencyKey = 'receive-1'
    } $clinicA.id 409
    $null = Invoke-Api $admin 'inventory/adjustments' 'POST' @{
        productId = $otherProduct.id; quantityChange = 2.5; reason = 'Supplier delivery'; idempotencyKey = 'receive-1'
    } $clinicA.id 409
    $null = Invoke-Api $admin 'inventory/adjustments' 'POST' @{
        productId = $product.id; quantityChange = 1; reason = 'Missing key'
    } $clinicA.id 400
    $null = Invoke-Api $admin 'inventory/adjustments' 'POST' @{
        productId = $product.id; quantityChange = -10; reason = 'Invalid negative balance'; idempotencyKey = 'invalid-negative'
    } $clinicA.id 409
    $inventory = Invoke-Api $admin 'inventory' 'GET' $null $clinicA.id
    Assert-Value (($inventory | Where-Object { $_.id -eq $product.id }).stockOnHand -eq 5.5) 'Inventory receiving must preserve decimal quantities.'
    $receives = @(Invoke-Api $admin "inventory/movements?productId=$($product.id)" 'GET' $null $clinicA.id |
        Where-Object { $_.reason -eq 'Supplier delivery' })
    Assert-Value ($receives.Count -eq 1) 'Identical inventory retries must persist exactly one ledger movement.'
    $null = Invoke-Api $admin 'inventory/adjustments' 'POST' @{
        productId = $otherProduct.id; quantityChange = 1; reason = 'Other clinic delivery'; idempotencyKey = 'receive-1'
    } $clinicB.id 200

    $today = Invoke-Api $vet 'operations/today?currency=USD' 'GET' $null $clinicA.id
    Assert-Value ($today.activeConsultations -eq 1 -and $today.consultations[0].id -eq $consultation.id) 'Operations must return same-clinic active consultation details.'
    Assert-Value ($today.pendingAccounts -eq 1 -and $today.outstandingBalance -eq 0 -and $today.currency -eq 'USD') 'Operations must exclude closed accounts from pending balance and preserve currency.'
    $todayOtherClinic = Invoke-Api $admin 'operations/today' 'GET' $null $clinicB.id
    Assert-Value ($todayOtherClinic.activeConsultations -eq 0 -and $todayOtherClinic.pendingAccounts -eq 0) 'Operations data must never cross clinic boundaries.'
    $summary = Invoke-Api $admin 'operations/summary' 'GET' $null $clinicA.id
    $usdTotals = $summary.totalsByCurrency | Where-Object { $_.currency -eq 'USD' }
    Assert-Value ($usdTotals.billedTotal -eq 44 -and $usdTotals.paymentsReceived -eq 44 -and $usdTotals.refundsRecorded -eq 5 -and $usdTotals.netPayments -eq 39) 'Operations summary must aggregate actual decimal payment and refund records.'
    $null = Invoke-Api $admin 'operations/summary?from=2026-10-10&to=2026-10-01' 'GET' $null $clinicA.id 400
    $null = Invoke-Api $vet 'operations/today' 'GET' $null $clinicB.id 403

    $draft = Invoke-Api $admin 'billing/accounts' 'POST' @{ patientId = $null; consultationId = $null; currency = 'USD' } $clinicA.id 201
    $draft = Invoke-Api $admin "billing/accounts/$($draft.id)/lines" 'POST' @{
        catalogItemId = $product.id; itemType = 'Product'; quantity = 10; expectedVersion = $draft.version
    } $clinicA.id 200
    Assert-Value ($draft.lines.Count -eq 1) 'Added draft product line must be returned to the caller.'
    $lineToRemove = ($draft.lines | Select-Object -First 1).id
    $deleteLinePath = "billing/accounts/$($draft.id)/lines/$lineToRemove" + "?expectedVersion=$($draft.version)"
    $draft = Invoke-Api $admin $deleteLinePath 'DELETE' $null $clinicA.id 200
    Assert-Value ($draft.lines.Count -eq 0 -and $draft.total -eq 0) 'Draft charge lines must be removable with an expected version.'
    $draft = Invoke-Api $admin "billing/accounts/$($draft.id)/void" 'POST' @{
        expectedVersion = $draft.version; reason = 'Entered in error'
    } $clinicA.id 200
    Assert-Value ($draft.status -eq 'Voided') 'Voiding a draft account must record its status.'
    $null = Invoke-Api $vet "billing/accounts/$($draft.id)/void" 'POST' @{
        expectedVersion = $draft.version; reason = 'Unauthorized'
    } $clinicA.id 403

    $null = Invoke-Api $admin "catalog/products/$($product.id)" 'DELETE' $null $clinicA.id 204
    $null = Invoke-Api $admin "catalog/services/$($service.id)" 'DELETE' $null $clinicA.id 204
    $activeProducts = Invoke-Api $admin 'catalog/products' 'GET' $null $clinicA.id
    $allProducts = Invoke-Api $admin 'catalog/products?includeArchived=true' 'GET' $null $clinicA.id
    Assert-Value (-not ($activeProducts | Where-Object { $_.id -eq $product.id }) -and ($allProducts | Where-Object { $_.id -eq $product.id }).isArchived) 'Catalog deletion must archive without erasing history.'

    $dispenseProduct = Invoke-Api $admin 'catalog/products' 'POST' @{
        name = 'Clinical dispensing'; category = 'Medication'; description = ''; sku = 'DISPENSE'
        unit = 'ml'; unitPrice = 1; stockOnHand = 5; lowStockThreshold = 1
        trackStock = $true; isFavorite = $false
    } $clinicA.id 201
    $dispenseAccount = Invoke-Api $vet 'billing/accounts' 'POST' @{
        patientId = $patient.id; consultationId = $null; currency = 'MXN'
    } $clinicA.id 201
    $dispenseAccount = Invoke-Api $vet "billing/accounts/$($dispenseAccount.id)/lines" 'POST' @{
        catalogItemId = $dispenseProduct.id; itemType = 'Product'; quantity = 1; expectedVersion = $dispenseAccount.version
    } $clinicA.id 200
    $dispenseLine = $dispenseAccount.lines[0].id
    $dispenseAccount = Invoke-Api $vet "billing/accounts/$($dispenseAccount.id)/fulfill" 'POST' @{
        expectedVersion = $dispenseAccount.version
    } $clinicA.id 200
    Assert-Value ($dispenseAccount.status -eq 'Draft' -and $dispenseAccount.amountPaid -eq 0 -and $dispenseAccount.lines[0].fulfilledQuantity -eq 1) 'Dispensing must work before opening or payment and preserve Draft status.'
    $dispenseStock = Invoke-Api $vet "catalog/products/$($dispenseProduct.id)" 'GET' $null $clinicA.id
    Assert-Value ($dispenseStock.stockOnHand -eq 4) 'Draft dispensing must immediately reduce stock.'
    $null = Invoke-Api $vet "billing/accounts/$($dispenseAccount.id)/lines/$dispenseLine" 'PUT' @{
        quantity = 2; expectedVersion = $dispenseAccount.version
    } $clinicA.id 409
    $dispenseDelete = "billing/accounts/$($dispenseAccount.id)/lines/$dispenseLine" + "?expectedVersion=$($dispenseAccount.version)"
    $null = Invoke-Api $vet $dispenseDelete 'DELETE' $null $clinicA.id 409
    $dispenseService = Invoke-Api $admin 'catalog/services' 'POST' @{
        name = 'Dispensing visit'; category = 'Clinical'; description = ''; sku = $null; unit = 'visit'
        unitPrice = 10; stockOnHand = 0; lowStockThreshold = 0; trackStock = $false; isFavorite = $false
    } $clinicA.id 201
    $dispenseAccount = Invoke-Api $vet "billing/accounts/$($dispenseAccount.id)" 'GET' $null $clinicA.id
    $dispenseAccount = Invoke-Api $vet "billing/accounts/$($dispenseAccount.id)/lines" 'POST' @{
        catalogItemId = $dispenseService.id; itemType = 'Service'; quantity = 1; expectedVersion = $dispenseAccount.version
    } $clinicA.id 200
    Assert-Value ($dispenseAccount.status -eq 'Draft' -and $dispenseAccount.lines.Count -eq 2) 'More service charges must remain editable after draft dispensing.'
    $dispenseAccount = Invoke-Api $vet "billing/accounts/$($dispenseAccount.id)/lines" 'POST' @{
        catalogItemId = $dispenseProduct.id; itemType = 'Product'; quantity = 2; expectedVersion = $dispenseAccount.version
    } $clinicA.id 200
    $dispenseAccount = Invoke-Api $vet "billing/accounts/$($dispenseAccount.id)/fulfill" 'POST' @{
        expectedVersion = $dispenseAccount.version
    } $clinicA.id 200
    $dispenseAccount = Invoke-Api $vet "billing/accounts/$($dispenseAccount.id)/fulfill" 'POST' @{
        expectedVersion = $dispenseAccount.version
    } $clinicA.id 200
    $dispenseStock = Invoke-Api $vet "catalog/products/$($dispenseProduct.id)" 'GET' $null $clinicA.id
    $dispenseMovements = @(Invoke-Api $vet "inventory/movements?productId=$($dispenseProduct.id)" 'GET' $null $clinicA.id)
    Assert-Value ($dispenseStock.stockOnHand -eq 2 -and $dispenseMovements.Count -eq 3) 'Later draft fulfillment must deduct only new lines and retries must not duplicate movements.'

    for ($index = 0; $index -lt 201; $index++) {
        $null = Invoke-Api $admin 'billing/accounts' 'POST' @{
            patientId = $null; consultationId = $null; currency = 'MXN'
        } $clinicA.id 201
    }
    $recentAccounts = @(Invoke-Api $admin 'billing/accounts' 'GET' $null $clinicA.id)
    Assert-Value ($recentAccounts.Count -eq 200 -and -not ($recentAccounts | Where-Object { $_.id -eq $linkedAccount.id })) 'Unfiltered history must omit an older account beyond the latest 200.'
    $olderLinked = @(Invoke-Api $vet $lookupPath 'GET' $null $clinicA.id)
    Assert-Value ($olderLinked.Count -eq 1 -and $olderLinked[0].id -eq $linkedAccount.id) 'Exact consultation lookup must find an account beyond the latest 200.'

    Write-Host "Commercial integration checks passed: $script:checks"
}
finally {
    foreach ($process in $processes) {
        try {
            if (-not $process.HasExited) {
                $process.Kill($true)
                $process.WaitForExit(5000) | Out-Null
            }
            $process.Dispose()
        }
        catch { }
    }
    if (Test-Path $testDirectory) {
        Remove-Item -LiteralPath $testDirectory -Recurse -Force
    }
}
