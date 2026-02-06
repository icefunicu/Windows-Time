$serviceName = "ScreenTimeWin"
$binPath = "$(Get-Location)\publish\ScreenTimeWin.Service.exe"

Write-Host "Publishing Service..."
dotnet publish src/ScreenTimeWin.Service -c Release -o ./publish

if (!(Test-Path $binPath)) {
    Write-Error "Publish failed. $binPath not found."
    exit
}

$service = Get-Service -Name $serviceName -ErrorAction SilentlyContinue
if ($service) {
    Write-Host "Stopping existing service..."
    Stop-Service $serviceName
    Write-Host "Removing existing service..."
    sc.exe delete $serviceName
    Start-Sleep -Seconds 2
}

Write-Host "Creating Service..."
sc.exe create $serviceName binPath= $binPath start= auto

Write-Host "Starting Service..."
Start-Service $serviceName

Write-Host "Done! Service is running."
Get-Service $serviceName
