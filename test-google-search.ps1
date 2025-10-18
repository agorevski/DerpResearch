# Test Google Custom Search API Configuration
# Run with: powershell -ExecutionPolicy Bypass -File test-google-search.ps1

Write-Host "=== Google Custom Search API Test ===" -ForegroundColor Cyan
Write-Host ""

# Read configuration from appsettings.json
$config = Get-Content -Path "appsettings.json" | ConvertFrom-Json

$apiKey = $config.GoogleCustomSearch.ApiKey
$searchEngineId = $config.GoogleCustomSearch.SearchEngineId

# Mask credentials for display
function Mask-Credential($credential) {
    if ([string]::IsNullOrEmpty($credential)) {
        return "[NOT SET]"
    }
    if ($credential.StartsWith("YOUR-")) {
        return "[PLACEHOLDER - NOT CONFIGURED]"
    }
    if ($credential.Length -le 8) {
        return "*" * $credential.Length
    }
    $start = $credential.Substring(0, 4)
    $end = $credential.Substring($credential.Length - 4)
    $middle = "*" * ($credential.Length - 8)
    return "$start$middle$end"
}

Write-Host "API Key: $(Mask-Credential $apiKey)"
Write-Host "Search Engine ID: $(Mask-Credential $searchEngineId)"
Write-Host ""

# Validate credentials
if ([string]::IsNullOrEmpty($apiKey) -or $apiKey.StartsWith("YOUR-")) {
    Write-Host "❌ ERROR: Google API Key not configured in appsettings.json" -ForegroundColor Red
    exit 1
}

if ([string]::IsNullOrEmpty($searchEngineId) -or $searchEngineId.StartsWith("YOUR-")) {
    Write-Host "❌ ERROR: Search Engine ID not configured in appsettings.json" -ForegroundColor Red
    exit 1
}

# Perform test search
$testQuery = "test"
Write-Host "Performing test search for: '$testQuery'"
Write-Host ""

$encodedQuery = [System.Uri]::EscapeDataString($testQuery)
$url = "https://www.googleapis.com/customsearch/v1?key=$apiKey&cx=$searchEngineId&q=$encodedQuery&num=3"
Write-Host "Sending request to Google Custom Search API..." -ForegroundColor Gray

$response = Invoke-RestMethod -Uri $url -Method Get -ErrorAction Stop
Write-Host "Raw JSON Response:" -ForegroundColor Gray
Write-Host ($response | ConvertTo-Json -Depth 10)
Write-Host ""
