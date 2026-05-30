$ApiKey = "YOUR_GEMINI_API_KEY"
$Url = "https://generativelanguage.googleapis.com/v1beta/models/gemini-1.5-flash:generateContent?key=$ApiKey"

$Payload = @{
    contents = @(
        @{
            parts = @(
                @{
                    text = "Hello"
                }
            )
        }
    )
} | ConvertTo-Json -Depth 10

$Headers = @{
    "Content-Type" = "application/json"
}

try {
    Write-Host "Sending POST to $Url"
    $Response = Invoke-RestMethod -Uri $Url -Method Post -Body $Payload -Headers $Headers
    $Response | ConvertTo-Json -Depth 10
}
catch {
    Write-Host "Error occurred:"
    Write-Host $_.Exception.Response.StatusCode.value__
    $stream = $_.Exception.Response.GetResponseStream()
    $reader = New-Object System.IO.StreamReader($stream)
    $responseBody = $reader.ReadToEnd()
    Write-Host "Response Body:"
    Write-Host $responseBody
}
