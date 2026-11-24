[CmdletBinding()]
param(
    [Parameter(Mandatory=$false)]
    [string]$Action = "list",
    [string]$DeviceId = "",
    [string]$Type = "Playback",
    [bool]$Enable = $true
)

$APO_PRE_GUID = "{EACD2258-FCAC-4FF4-B36D-419E924A6D79}"
$APO_POST_GUID = "{EC1CC9CE-FAED-4822-828A-82A81A6F018F}"
$SFX_KEY = "{d04e05a6-594b-4fb6-a80d-01af5eed7d1d},5"
$EFX_KEY = "{d04e05a6-594b-4fb6-a80d-01af5eed7d1d},7"
$LFX_KEY = "{d04e05a6-594b-4fb6-a80d-01af5eed7d1d},1"
$GFX_KEY = "{d04e05a6-594b-4fb6-a80d-01af5eed7d1d},2"
$DIS_KEY = "{1da5d803-d492-4edd-8c23-e0c0ffee7f0e},5"

function Get-AudioDevices {
    $results = @()
    $categories = @(
        @{ Name = "Playback"; Key = "Render" },
        @{ Name = "Recording"; Key = "Capture" }
    )

    foreach ($cat in $categories) {
        $regPath = "HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\MMDevices\Audio\" + $cat.Key
        if (Test-Path $regPath) {
            $deviceKeys = Get-ChildItem $regPath -ErrorAction SilentlyContinue
            foreach ($dev in $deviceKeys) {
                try {
                    $state = (Get-ItemProperty $dev.PSPath -ErrorAction SilentlyContinue).DeviceState
                    # 1 = Active
                    if ($state -eq 1) {
                        $id = $dev.PSChildName
                        $propPath = Join-Path $dev.PSPath "Properties"
                        $fxPath = Join-Path $dev.PSPath "FxProperties"

                        $name = ""
                        $adapter = ""
                        if (Test-Path $propPath) {
                            $props = Get-ItemProperty $propPath -ErrorAction SilentlyContinue
                            $name = $props.'{a45c254e-df1c-4efd-8020-67d146a850e0},2'
                            $adapter = $props.'{b3f8fa53-0004-438e-9003-51a46e139bfc},6'
                        }

                        $installed = $false
                        if (Test-Path $fxPath) {
                            $fx = Get-ItemProperty $fxPath -ErrorAction SilentlyContinue
                            $sfx = $fx.$SFX_KEY
                            $efx = $fx.$EFX_KEY
                            $lfx = $fx.$LFX_KEY
                            $gfx = $fx.$GFX_KEY
                            if (($sfx -and $sfx -eq $APO_PRE_GUID) -or 
                                ($efx -and $efx -eq $APO_POST_GUID) -or 
                                ($lfx -and $lfx -eq $APO_PRE_GUID) -or 
                                ($gfx -and $gfx -eq $APO_POST_GUID)) {
                                $installed = $true
                            }
                        }

                        $results += [PSCustomObject]@{
                            id = $id
                            type = $cat.Name
                            renderKey = $cat.Key
                            name = if ([string]::IsNullOrWhiteSpace($name)) { "Audio Device" } else { $name }
                            adapter = if ([string]::IsNullOrWhiteSpace($adapter)) { "" } else { $adapter }
                            installed = $installed
                        }
                    }
                } catch {}
            }
        }
    }
    return $results
}

function Set-DeviceAPO {
    param($devId, $targetType, $enableApo)
    $regDir = if ($targetType -match "Capture|Recording") { "Capture" } else { "Render" }
    $devPath = "HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\MMDevices\Audio\$regDir\$devId"
    $fxPath = "$devPath\FxProperties"
    $childPath = "HKLM:\SOFTWARE\EqualizerAPO\Child APOs\$devId"

    if (-not (Test-Path $devPath)) {
        return @{ success = $false; error = "Device endpoint not found" }
    }

    try {
        if (-not (Test-Path $fxPath)) {
            New-Item -Path $fxPath -Force | Out-Null
        }
        if (-not (Test-Path "HKLM:\SOFTWARE\EqualizerAPO\Child APOs")) {
            New-Item -Path "HKLM:\SOFTWARE\EqualizerAPO\Child APOs" -Force | Out-Null
        }
        if (-not (Test-Path $childPath)) {
            New-Item -Path $childPath -Force | Out-Null
        }

        if ($enableApo) {
            # Backup original if not already backed up
            $currentFx = Get-ItemProperty $fxPath -ErrorAction SilentlyContinue
            if ($currentFx.$SFX_KEY -and $currentFx.$SFX_KEY -ne $APO_PRE_GUID) {
                Set-ItemProperty -Path $childPath -Name $SFX_KEY -Value $currentFx.$SFX_KEY -Force
                Set-ItemProperty -Path $childPath -Name "PreMixChild" -Value $currentFx.$SFX_KEY -Force
            }
            if ($currentFx.$EFX_KEY -and $currentFx.$EFX_KEY -ne $APO_POST_GUID) {
                Set-ItemProperty -Path $childPath -Name $EFX_KEY -Value $currentFx.$EFX_KEY -Force
                Set-ItemProperty -Path $childPath -Name "PostMixChild" -Value $currentFx.$EFX_KEY -Force
            }

            # Install Equalizer APO SFX/EFX hooks (Windows 10/11 default)
            Set-ItemProperty -Path $fxPath -Name $SFX_KEY -Value $APO_PRE_GUID -Force
            if ($regDir -eq "Render") {
                Set-ItemProperty -Path $fxPath -Name $EFX_KEY -Value $APO_POST_GUID -Force
            }
            Set-ItemProperty -Path $fxPath -Name "{b725f130-47ef-101a-a5f1-02608c9eebac},10" -Value "Equalizer APO" -Force
            Set-ItemProperty -Path $childPath -Name "Version" -Value "2" -Force
            Set-ItemProperty -Path $childPath -Name "AllowSilentBufferModification" -Value "false" -Force

            # Ensure enhancements are not disabled
            if (Test-Path $fxPath) {
                Set-ItemProperty -Path $fxPath -Name $DIS_KEY -Value 0 -Type DWord -Force -ErrorAction SilentlyContinue
            }
        } else {
            # Restore original or remove APO hook
            $child = Get-ItemProperty $childPath -ErrorAction SilentlyContinue
            if ($child -and $child.$SFX_KEY -and $child.$SFX_KEY -ne "{NONE}") {
                Set-ItemProperty -Path $fxPath -Name $SFX_KEY -Value $child.$SFX_KEY -Force
            } else {
                Remove-ItemProperty -Path $fxPath -Name $SFX_KEY -Force -ErrorAction SilentlyContinue
            }

            if ($child -and $child.$EFX_KEY -and $child.$EFX_KEY -ne "{NONE}") {
                Set-ItemProperty -Path $fxPath -Name $EFX_KEY -Value $child.$EFX_KEY -Force
            } else {
                Remove-ItemProperty -Path $fxPath -Name $EFX_KEY -Force -ErrorAction SilentlyContinue
            }
        }

        # Try to restart Windows Audio Service quietly to apply changes instantly
        try {
            Restart-Service audiosrv -Force -ErrorAction SilentlyContinue
        } catch {}

        return @{ success = $true; enabled = $enableApo; deviceId = $devId }
    } catch {
        return @{ success = $false; error = $_.Exception.Message }
    }
}

if ($Action -eq "list") {
    $devs = Get-AudioDevices
    $devs | ConvertTo-Json -Depth 3
} elseif ($Action -eq "set") {
    $res = Set-DeviceAPO -devId $DeviceId -targetType $Type -enableApo $Enable
    $res | ConvertTo-Json
} elseif ($Action -eq "restart-audio") {
    try {
        Restart-Service audiosrv -Force
        @{ success = $true; message = "Audio service restarted" } | ConvertTo-Json
    } catch {
        @{ success = $false; error = $_.Exception.Message } | ConvertTo-Json
    }
}
