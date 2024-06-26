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
        # Get the ModifyPath value
        $modifyPath = Get-ItemProperty -Path $subkey.PSPath -Name ModifyPath -ErrorAction SilentlyContinue

        # Print the ModifyPath value
        Write-Output "ModifyPath: $($modifyPath.ModifyPath)"
        
        # Split the command and arguments
        $command, $args = $modifyPath.ModifyPath -split ' ', 2
        
        # Execute the ModifyPath value using Start-Process
        Start-Process -FilePath $command -ArgumentList $args
    }
}