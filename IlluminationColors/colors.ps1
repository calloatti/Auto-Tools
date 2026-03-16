# Get the current folder where the script is running
$PSScriptRoot = Get-Location
$inputFile = Join-Path $PSScriptRoot "colors.txt"


# Starting Order for the presets
$currentOrder = 11000

Get-Content $inputFile | ForEach-Object {
    $line = $_.Trim()
    
    # Skip comments and empty lines
    if ($line.StartsWith("//") -or [string]::IsNullOrWhiteSpace($line)) { return }
    
    $parts = $line.Split(',')
    if ($parts.Count -lt 2) { return }
    
    $hex = $parts[0].Trim().Replace("#", "")
    $name = $parts[1].Trim()
    
    # Clean ID: removes spaces and special characters
    $cleanName = $name.Split('(')[0].Trim().Replace(" ", "").Replace("'", "").Replace("-", "")
    
    # Convert Hex to RGB Bytes
    $rByte = [System.Convert]::ToInt32($hex.Substring(0, 2), 16)
    $gByte = [System.Convert]::ToInt32($hex.Substring(2, 2), 16)
    $bByte = [System.Convert]::ToInt32($hex.Substring(4, 2), 16)
    
    # Precision conversion to ensure Round-Trip accuracy in Unity
    $rFloat = [Math]::Round($rByte / 255, 4)
    $gFloat = [Math]::Round($gByte / 255, 4)
    $bFloat = [Math]::Round($bByte / 255, 4)
    
    # JSON Construction
    $jsonObj = @{
        IlluminationColorSpec = @{
            Id = "Calloatti.$cleanName"
            Color = @{
                r = $rFloat
                g = $gFloat
                b = $bFloat
                a = 1
            }
        }
        IlluminationPresetSpec = @{
            Order = $currentOrder
        }
    }
    
    $fileName = "Calloatti.IlluminationColor.$cleanName.blueprint.json"
    $jsonObj | ConvertTo-Json -Depth 5 | Out-File (Join-Path $PSScriptRoot $fileName) -Encoding utf8
    
    $currentOrder += 10
    Write-Host "Created Blueprint for: $name (Hex: #$hex, Order: $currentOrder)"
}