$AppSecret = "5c126d400e26b1580ca0ddf1f72cf023"
$Url = "http://localhost:3001/webhook"

$PayloadBytes = [System.IO.File]::ReadAllBytes("payload.json")

# Tính HMAC-SHA256
$HMAC = [System.Security.Cryptography.HMACSHA256]::new([System.Text.Encoding]::UTF8.GetBytes($AppSecret))
$HashBytes = $HMAC.ComputeHash($PayloadBytes)
$Signature = [System.BitConverter]::ToString($HashBytes).Replace("-", "").ToLower()

$Headers = @{
    "Content-Type" = "application/json"
    "X-Hub-Signature-256" = "sha256=$Signature"
}

Write-Host "Sending Payload..."
Write-Host "Signature: sha256=$Signature"

# Chuyển bytes thành string để gửi
$PayloadStr = [System.Text.Encoding]::UTF8.GetString($PayloadBytes)

$Response = Invoke-RestMethod -Uri $Url -Method Post -Body $PayloadStr -Headers $Headers
$Response | ConvertTo-Json
