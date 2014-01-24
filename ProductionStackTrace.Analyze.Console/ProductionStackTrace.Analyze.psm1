$scriptPath = split-path -parent $MyInvocation.MyCommand.Definition

function Convert-ProductionStackTrace {
    param (
        [Parameter(Mandatory=$False)] $logfile,
        [Parameter(Mandatory=$False)] $outfile
    )

    $ver = [System.Environment]::Version
    if (($ver.Major -eq 4) -and ($ver.Minor -eq 0)) {
        if ($ver.Revision -eq 1) {
            $analyzerExe = (Join-Path $scriptPath "net40")
        } else {
            $analyzerExe = (Join-Path $scriptPath "net45")
        }
    } else {
        $analyzerExe = (Join-Path $scriptPath "net20")
    }

    $analyzerExe = (Join-Path $analyzerExe "ProductionStackTrace.Analyze.Console.exe")

    if (!$logfile -and !$outfile) {
        Start-Process $analyzerExe
    } elseif (!$outfile) {
        Get-Content $logfile | & $analyzerExe
    } else {
        Get-Content $logfile | & $analyzerExe | Out-File $outfile
    }
}