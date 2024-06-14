# Define the registry path
$uninstallKey = "HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall"

# Get all subkeys under the uninstall key
$subkeys = Get-ChildItem -Path $uninstallKey

# Loop through each subkey
foreach ($subkey in $subkeys) {
    # Get the DisplayName value
    $displayName = Get-ItemProperty -Path $subkey.PSPath -Name DisplayName -ErrorAction SilentlyContinue

    # Check if DisplayName is "ColorVision"
    if ($displayName.DisplayName -eq "ColorVision") {
        # Get the UninstallString value
        $uninstallString = Get-ItemProperty -Path $subkey.PSPath -Name UninstallString -ErrorAction SilentlyContinue

        # Print the UninstallString value
        Write-Output "UninstallString: $($uninstallString.UninstallString)"
        
        # Split the command and arguments
        $command, $args = $uninstallString.UninstallString -split ' ', 2
        
        # Execute the UninstallString value using Start-Process
        Start-Process -FilePath $command -ArgumentList $args
    }
}