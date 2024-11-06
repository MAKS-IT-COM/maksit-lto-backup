# Define project path
$projectPath = "./MaksIT.LTO.Backup/MaksIT.LTO.Backup.csproj"

# List of runtime identifiers for self-contained deployments
$runtimes = @(
    "win-x64"
)

# Define build configurations
$configurations = @(
    "Release"
)

# Output directory
$outputDir = "./build_outputs"

# Clean output directory if exists, then recreate it
if (Test-Path -Path $outputDir) {
    Remove-Item -Recurse -Force -Path $outputDir
}
New-Item -ItemType Directory -Path $outputDir

# Build "normal" binaries (framework-dependent)
foreach ($config in $configurations) {
    $normalBinOutput = "$outputDir/normal/$config"
    dotnet publish $projectPath -c $config -o $normalBinOutput --self-contained false
    Write-Output "Built normal bin for configuration: $config"
}

# Build "self-contained" binaries for multiple runtimes
foreach ($config in $configurations) {
    foreach ($runtime in $runtimes) {
        $selfContainedOutput = "$outputDir/self-contained/$config/$runtime"
        dotnet publish $projectPath -c $config -r $runtime --self-contained true -o $selfContainedOutput
        Write-Output "Built self-contained bin for configuration: $config, runtime: $runtime"
    }
}

Write-Output "Build process completed!"