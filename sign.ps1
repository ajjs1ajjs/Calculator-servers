<#
.SYNOPSIS
    Signs the published AIResourceCalculator.exe with an Authenticode signature.

.DESCRIPTION
    If -PfxPath is given (a purchased / corporate code-signing certificate), signs with it.
    Otherwise it uses or creates a self-signed certificate in CurrentUser\My. A self-signed
    cert is fine for internal use, but external PCs will still show SmartScreen warnings
    until that certificate is added to their Trusted Root store.

.EXAMPLE
    ./sign.ps1
    ./sign.ps1 -PfxPath C:\certs\company.pfx -PfxPassword (Read-Host -AsSecureString)
#>
param(
    [string]$ExePath = "publish/AIResourceCalculator.exe",
    [string]$PfxPath,
    [System.Security.SecureString]$PfxPassword,
    [string]$TimestampUrl = "http://timestamp.digicert.com",
    [string]$SelfSignedSubject = "CN=AIResourceCalculator Dev",
    # Opt-in: add the self-signed cert to this machine's Trusted Root so the signature
    # validates locally. This changes the machine trust store - off by default.
    [switch]$TrustLocally
)

$ErrorActionPreference = "Stop"

if (-not (Test-Path $ExePath)) {
    throw "$ExePath not found. Run first: dotnet publish AIResourceCalculator/AIResourceCalculator.csproj -c Release --output publish"
}

if ($PfxPath) {
    Write-Host "Signing with PFX certificate: $PfxPath"
    $cert = Get-PfxCertificate -FilePath $PfxPath -Password $PfxPassword
} else {
    $cert = Get-ChildItem Cert:\CurrentUser\My |
        Where-Object { $_.Subject -eq $SelfSignedSubject -and $_.HasPrivateKey } |
        Select-Object -First 1
    if (-not $cert) {
        Write-Host "Creating self-signed certificate: $SelfSignedSubject"
        $cert = New-SelfSignedCertificate -Type CodeSigningCert -Subject $SelfSignedSubject `
            -CertStoreLocation Cert:\CurrentUser\My -KeyExportPolicy Exportable `
            -KeyUsage DigitalSignature -NotAfter (Get-Date).AddYears(3)
    } else {
        Write-Host "Using existing self-signed certificate."
    }

    # Trust the self-signed cert locally so the signature validates on this machine.
    # This is a machine trust-store change, so it only happens with explicit -TrustLocally.
    # (On other PCs, import this cert into their Trusted Root to avoid SmartScreen warnings.)
    if ($TrustLocally) {
        $root = New-Object System.Security.Cryptography.X509Certificates.X509Store("Root", "CurrentUser")
        $root.Open("ReadWrite")
        if (-not ($root.Certificates | Where-Object { $_.Thumbprint -eq $cert.Thumbprint })) {
            Write-Host "Adding self-signed cert to CurrentUser Trusted Root."
            $root.Add($cert)
        }
        $root.Close()
    }
}

$signArgs = @{
    FilePath      = $ExePath
    Certificate   = $cert
    HashAlgorithm = "SHA256"
}
# Timestamp only if the internet is reachable (keeps the signature valid past cert expiry).
try {
    $null = Invoke-WebRequest $TimestampUrl -UseBasicParsing -TimeoutSec 5 -Method Head
    $signArgs.TimestampServer = $TimestampUrl
} catch {
    Write-Warning "Timestamp server unreachable - signing without a timestamp."
}

$result = Set-AuthenticodeSignature @signArgs
Write-Host "Signature status: $($result.Status)"

# The signature is embedded regardless of trust. A self-signed cert that isn't in the
# machine's Trusted Root reports as untrusted here - that's expected, the file IS signed.
if ($result.SignerCertificate) {
    Write-Host "Done: $ExePath signed ($($cert.Subject))."
    if ($result.Status -ne "Valid") {
        Write-Warning "Signature embedded but not trusted on this machine (self-signed). Use -PfxPath with a CA-issued cert, or -TrustLocally, for a Valid status."
    }
} else {
    throw "Signing failed: $($result.StatusMessage)"
}
