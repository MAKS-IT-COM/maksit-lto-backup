#requires -Version 7.0
#requires -PSEdition Core

function Get-RepoUtilsSrcDirectory {
    return (Split-Path (Split-Path $PSScriptRoot -Parent) -Parent)
}

function Get-RepoUtilsModulesDirectory {
    return Split-Path $PSScriptRoot -Parent
}

if (-not (Get-Command Write-Log -ErrorAction SilentlyContinue)) {
    $loggingModulePath = Join-Path (Get-RepoUtilsModulesDirectory) "Logging.psm1"
    if (Test-Path $loggingModulePath -PathType Leaf) {
        Import-Module $loggingModulePath -Force
    }
}

function Test-IsEngineRuntimeModuleName {
    param(
        [Parameter(Mandatory = $true)]
        [string]$ModuleName
    )

    # Host engine runtime under modules/ (and optional modules/Extensions/) — never dual-homed under plugins/.
    $engineNames = [System.Collections.Generic.HashSet[string]]::new(
        [string[]]@(
            'ChangelogSupport',
            'ExternalCommandSupport',
            'GitTools',
            'Logging',
            'ScriptConfig',
            'TestRunner',
            'EngineContext',
            'PluginSupport',
            'ReleaseSupport',
            'TestSupport'
        ),
        [System.StringComparer]::OrdinalIgnoreCase
    )

    return $engineNames.Contains($ModuleName)
}

function Get-PluginDependencyGroupDirectories {
    param(
        [Parameter(Mandatory = $true)]
        [string]$PluginsRoot
    )

    if (-not (Test-Path -LiteralPath $PluginsRoot -PathType Container)) {
        return @()
    }

    # Prefer Shared (helpers), then stock host groups; any other plugins/{Group}/ is discovered.
    $preferred = @('Shared', 'Platform', 'DotNet', 'Npm')
    $dirs = [System.Collections.Generic.List[string]]::new()
    foreach ($name in $preferred) {
        $path = Join-Path $PluginsRoot $name
        if (Test-Path -LiteralPath $path -PathType Container) {
            $dirs.Add($path)
        }
    }

    Get-ChildItem -LiteralPath $PluginsRoot -Directory -ErrorAction SilentlyContinue |
        Where-Object { $_.Name -notin $preferred } |
        Sort-Object Name |
        ForEach-Object { $dirs.Add($_.FullName) }

    return @($dirs)
}

function Import-PluginDependency {
    param(
        [Parameter(Mandatory = $true)]
        [string]$ModuleName,

        [Parameter(Mandatory = $true)]
        [string]$RequiredCommand
    )

    if (Get-Command $RequiredCommand -ErrorAction SilentlyContinue) {
        return
    }

    $modulesDir = Get-RepoUtilsModulesDirectory
    $engineModuleDir = $PSScriptRoot
    $srcDir = Get-RepoUtilsSrcDirectory
    $pluginsRoot = Join-Path $srcDir 'plugins'
    $candidatePaths = [System.Collections.Generic.List[string]]::new()

    if (Test-IsEngineRuntimeModuleName -ModuleName $ModuleName) {
        # Engine runtime: modules/ only (no plugins/ fallback). Optional Extensions/ for layered hosts.
        $candidatePaths.Add((Join-Path $modulesDir "$ModuleName.psm1"))
        $candidatePaths.Add((Join-Path $engineModuleDir "$ModuleName.psm1"))
        $extensionsDir = Join-Path $modulesDir 'Extensions'
        $candidatePaths.Add((Join-Path $extensionsDir "$ModuleName.psm1"))
    }
    else {
        # Plugin helpers: plugins/{Group}/ only (no modules/ legacy shadow). Groups are discovered.
        foreach ($groupDir in Get-PluginDependencyGroupDirectories -PluginsRoot $pluginsRoot) {
            $candidatePaths.Add((Join-Path $groupDir "$ModuleName.psm1"))
        }
    }

    foreach ($modulePath in $candidatePaths) {
        if (Test-Path -LiteralPath $modulePath -PathType Leaf) {
            Import-Module $modulePath -Force -Global -ErrorAction Stop
            break
        }
    }

    if (-not (Get-Command $RequiredCommand -ErrorAction SilentlyContinue)) {
        throw "Required command '$RequiredCommand' is still unavailable after importing module '$ModuleName'."
    }
}

function Get-ConfiguredPlugins {
    param(
        [Parameter(Mandatory = $true)]
        [psobject]$Settings
    )

    if (-not $Settings.PSObject.Properties['plugins'] -or $null -eq $Settings.plugins) {
        return @()
    }

    if ($Settings.plugins -is [System.Collections.IEnumerable] -and -not ($Settings.plugins -is [string])) {
        return @($Settings.plugins)
    }

    return @($Settings.plugins)
}

function Get-PluginStageLabel {
    param(
        [Parameter(Mandatory = $true)]
        $Plugin
    )

    if (-not $Plugin.PSObject.Properties['stageLabel'] -or [string]::IsNullOrWhiteSpace([string]$Plugin.stageLabel)) {
        return 'release'
    }

    return [string]$Plugin.stageLabel
}

function Get-PluginBranches {
    param(
        [Parameter(Mandatory = $true)]
        $Plugin
    )

    if (-not $Plugin.PSObject.Properties['branches'] -or $null -eq $Plugin.branches) {
        return @()
    }

    if ($Plugin.branches -is [System.Collections.IEnumerable] -and -not ($Plugin.branches -is [string])) {
        return @($Plugin.branches | Where-Object { -not [string]::IsNullOrWhiteSpace($_) })
    }

    if ([string]::IsNullOrWhiteSpace([string]$Plugin.branches)) {
        return @()
    }

    return @([string]$Plugin.branches)
}

function Test-PluginAllowedOnBranch {
    param(
        [Parameter(Mandatory = $true)]
        $Plugin,

        [Parameter(Mandatory = $true)]
        [string]$CurrentBranch
    )

    $allowedBranches = Get-PluginBranches -Plugin $Plugin
    if ($allowedBranches.Count -eq 0) {
        return $true
    }

    if ($allowedBranches -contains '*') {
        return $true
    }

    return $allowedBranches -contains $CurrentBranch
}

function Get-PluginMetadataObject {
    param(
        [Parameter(Mandatory = $true)]
        $Plugin,

        [Parameter(Mandatory = $true)]
        [string]$EngineDirectory
    )

    $modulePath = Resolve-PluginModulePath -Plugin $Plugin -EngineDirectory $EngineDirectory
    if (-not (Test-Path $modulePath -PathType Leaf)) {
        return $null
    }

    try {
        $moduleInfo = Import-Module $modulePath -Force -PassThru -ErrorAction Stop
        $metadataCommand = Get-Command -Name 'Get-PluginMetadata' -Module $moduleInfo.Name -ErrorAction SilentlyContinue
        if (-not $metadataCommand) {
            return $null
        }

        return & $metadataCommand
    }
    catch {
        return $null
    }
}

function Test-PluginCompatible {
    <#
    .SYNOPSIS
        Applies an optional compatibility policy supplied by an extension.
    #>
    param(
        [Parameter(Mandatory = $true)]
        $Plugin,

        [Parameter(Mandatory = $true)]
        [string]$EngineDirectory,

        [Parameter(Mandatory = $false)]
        [bool]$WriteLogs = $true
    )

    $extensionTest = Get-Command Test-ExtensionPluginCompatibility -ErrorAction SilentlyContinue
    if ($extensionTest) {
        return & $extensionTest @PSBoundParameters
    }

    return $true
}

function Test-PluginMutatesRemote {
    param(
        [Parameter(Mandatory = $true)]
        $Plugin,

        [Parameter(Mandatory = $false)]
        [string]$EngineDirectory
    )

    if ($null -eq $Plugin -or [string]::IsNullOrWhiteSpace([string]$Plugin.name)) {
        return $false
    }

    if ([string]::IsNullOrWhiteSpace($EngineDirectory)) {
        if ($Plugin.PSObject.Properties.Name -contains 'context' -and $null -ne $Plugin.context -and $Plugin.context.scriptDir) {
            $EngineDirectory = [string]$Plugin.context.scriptDir
        }
        elseif ($Plugin.PSObject.Properties.Name -contains 'scriptDir' -and -not [string]::IsNullOrWhiteSpace([string]$Plugin.scriptDir)) {
            $EngineDirectory = [string]$Plugin.scriptDir
        }
    }

    if ([string]::IsNullOrWhiteSpace($EngineDirectory)) {
        return $false
    }

    $modulePath = Resolve-PluginModulePath -Plugin $Plugin -EngineDirectory $EngineDirectory
    if (-not (Test-Path $modulePath -PathType Leaf)) {
        return $false
    }

    try {
        $moduleInfo = Import-Module $modulePath -Force -PassThru -ErrorAction Stop
        $metadataCommand = Get-Command -Name 'Get-PluginMetadata' -Module $moduleInfo.Name -ErrorAction SilentlyContinue
        if (-not $metadataCommand) {
            return $false
        }

        $metadata = & $metadataCommand
        if ($null -eq $metadata) {
            return $false
        }

        if ($metadata.PSObject.Properties.Name -contains 'mutatesRemote') {
            return [bool]$metadata.mutatesRemote
        }
    }
    catch {
        return $false
    }

    return $false
}

function Get-SecretEnvironmentValue {
    <#
    .SYNOPSIS
        Reads a secret value from an environment variable by logical name.

    .DESCRIPTION
        Plugins never store secret material in scriptSettings.json. Settings hold a
        logical name (e.g. "GitHub", "NuGet"); the process environment variable with
        that same name must be set before the engine runs.

    .PARAMETER Name
        Logical secret name — also the environment variable name to read.

    .OUTPUTS
        System.String. The environment variable value, or $null when unset.

    .EXAMPLE
        $token = Get-SecretEnvironmentValue -Name 'GitHub'
    #>
    param(
        [Parameter(Mandatory = $true)]
        [string]$Name
    )

    return [Environment]::GetEnvironmentVariable($Name)
}

function Resolve-PluginSecretName {
    <#
    .SYNOPSIS
        Resolves a logical secret name from a plugin's scriptSettings entry.

    .DESCRIPTION
        Reads a string property such as githubSecret / nugetSecret / npmSecret /
        containerRegistrySecret from the plugin settings object. Returns $null when
        the property is missing or blank.

    .PARAMETER PluginSettings
        Plugin settings object from scriptSettings.json (the enabled plugin entry).

    .PARAMETER PropertyName
        Settings property that holds the logical secret name (e.g. 'githubSecret').

    .OUTPUTS
        System.String. Trimmed logical secret name, or $null.

    .EXAMPLE
        $name = Resolve-PluginSecretName -PluginSettings $plugin -PropertyName 'nugetSecret'
        $key  = Get-SecretEnvironmentValue -Name $name
    #>
    param(
        [Parameter(Mandatory = $true)]
        $PluginSettings,

        [Parameter(Mandatory = $true)]
        [string]$PropertyName
    )

    if ($PluginSettings.PSObject.Properties.Name -contains $PropertyName) {
        $value = [string]$PluginSettings.$PropertyName
        if (-not [string]::IsNullOrWhiteSpace($value)) {
            return $value.Trim()
        }
    }

    return $null
}

function Get-RegistryCredentialsFromRuntime {
    <#
    .SYNOPSIS
        Loads container-registry username/password from a logical secret name.

    .DESCRIPTION
        Looks up the environment variable named by SecretName. The value must be
        Base64(UTF8('username:password')). Used by registry login and image-pull
        secret creation — never pass the password itself as a parameter.

    .PARAMETER SecretName
        Logical secret name (environment variable name), not a password or token.

    .PARAMETER SharedSettings
        Optional engine shared context (reserved for callers that thread context).

    .OUTPUTS
        Hashtable with User and Password keys (decoded credential material).

    .EXAMPLE
        $creds = Get-RegistryCredentialsFromRuntime -SecretName 'ContainerRegistry'
        # $creds.User / $creds.Password
    #>
    # SecretName is a logical env-var name from scriptSettings, not a password value.
    [Diagnostics.CodeAnalysis.SuppressMessageAttribute(
        'PSAvoidUsingPlainTextForPassword',
        'SecretName',
        Justification = 'Logical secret name for env lookup (Base64 username:password); not a credential value.'
    )]
    param(
        [Parameter(Mandatory = $true)]
        [string]$SecretName,

        [Parameter(Mandatory = $false)]
        [psobject]$SharedSettings
    )

    $raw = Get-SecretEnvironmentValue -Name $SecretName
    if ([string]::IsNullOrWhiteSpace($raw)) {
        throw "Environment variable '$SecretName' is not set."
    }

    try {
        $decoded = [Text.Encoding]::UTF8.GetString([Convert]::FromBase64String($raw))
    }
    catch {
        throw "Failed to decode '$SecretName' as Base64 (expected base64('username:password')): $($_.Exception.Message)"
    }

    $parts = $decoded -split ':', 2
    if ($parts.Count -ne 2 -or [string]::IsNullOrWhiteSpace($parts[0]) -or [string]::IsNullOrWhiteSpace($parts[1])) {
        throw "Decoded '$SecretName' must be in the form 'username:password'."
    }

    return @{ User = $parts[0]; Password = $parts[1] }
}

function Resolve-EngineDirectoryFromSharedSettings {
    param(
        [Parameter(Mandatory = $true)]
        $SharedSettings
    )

    if ($SharedSettings.PSObject.Properties.Name -contains 'engineScriptDir' -and -not [string]::IsNullOrWhiteSpace([string]$SharedSettings.engineScriptDir)) {
        return [string]$SharedSettings.engineScriptDir
    }

    return [string]$SharedSettings.scriptDir
}

function Test-PluginSkipsRemoteMutation {
    param(
        [Parameter(Mandatory = $true)]
        $Plugin,

        [Parameter(Mandatory = $true)]
        [psobject]$SharedSettings
    )

    $engineDirectory = Resolve-EngineDirectoryFromSharedSettings -SharedSettings $SharedSettings
    if (-not (Test-PluginMutatesRemote -Plugin $Plugin -EngineDirectory $engineDirectory)) {
        return $false
    }

    return ($Plugin.PSObject.Properties.Name -contains 'dryRun' -and $null -ne $Plugin.dryRun -and [bool]$Plugin.dryRun)
}

function Test-IsPublishPlugin {
    param(
        [Parameter(Mandatory = $true)]
        $Plugin,

        [Parameter(Mandatory = $false)]
        [string]$EngineDirectory
    )

    return Test-PluginMutatesRemote -Plugin $Plugin -EngineDirectory $EngineDirectory
}

function Get-PluginSettingValue {
    param(
        [Parameter(Mandatory = $true)]
        [object[]]$Plugins,

        [Parameter(Mandatory = $true)]
        [string]$PropertyName
    )

    foreach ($plugin in $Plugins) {
        if ($null -eq $plugin -or [string]::IsNullOrWhiteSpace($plugin.name)) {
            continue
        }

        if (-not $plugin.PSObject.Properties[$PropertyName]) {
            continue
        }

        $value = $plugin.$PropertyName
        if ($null -eq $value) {
            continue
        }

        if ($value -is [string] -and [string]::IsNullOrWhiteSpace($value)) {
            continue
        }

        return $value
    }

    return $null
}

function Get-PluginPathListSetting {
    param(
        [Parameter(Mandatory = $true)]
        [object[]]$Plugins,

        [Parameter(Mandatory = $true)]
        [string]$PropertyName,

        [Parameter(Mandatory = $true)]
        [string]$BasePath
    )

    $rawPaths = @()
    $value = Get-PluginSettingValue -Plugins $Plugins -PropertyName $PropertyName

    if ($null -eq $value) {
        return @()
    }

    if ($value -is [System.Collections.IEnumerable] -and -not ($value -is [string])) {
        $rawPaths += $value
    }
    else {
        $rawPaths += $value
    }

    $resolvedPaths = @()
    foreach ($path in $rawPaths) {
        if ([string]::IsNullOrWhiteSpace([string]$path)) {
            continue
        }

        $resolvedPaths += [System.IO.Path]::GetFullPath((Join-Path $BasePath ([string]$path)))
    }

    return @($resolvedPaths)
}

function Get-PluginPathSetting {
    param(
        [Parameter(Mandatory = $true)]
        [object[]]$Plugins,

        [Parameter(Mandatory = $true)]
        [string]$PropertyName,

        [Parameter(Mandatory = $true)]
        [string]$BasePath
    )

    $value = Get-PluginSettingValue -Plugins $Plugins -PropertyName $PropertyName
    if ($null -eq $value -or [string]::IsNullOrWhiteSpace([string]$value)) {
        return $null
    }

    return [System.IO.Path]::GetFullPath((Join-Path $BasePath ([string]$value)))
}

function Get-ArchiveNamePattern {
    param(
        [Parameter(Mandatory = $true)]
        [object[]]$Plugins,

        [Parameter(Mandatory = $true)]
        [string]$CurrentBranch
    )

    foreach ($plugin in $Plugins) {
        if ($null -eq $plugin -or [string]::IsNullOrWhiteSpace($plugin.name)) {
            continue
        }

        if (-not $plugin.enabled) {
            continue
        }

        if (-not (Test-PluginAllowedOnBranch -Plugin $plugin -CurrentBranch $CurrentBranch)) {
            continue
        }

        if ($plugin.PSObject.Properties['zipNamePattern'] -and -not [string]::IsNullOrWhiteSpace([string]$plugin.zipNamePattern)) {
            return [string]$plugin.zipNamePattern
        }
    }

    return "release-{version}.zip"
}

function Resolve-PluginModulePath {
    param(
        [Parameter(Mandatory = $true)]
        $Plugin,

        [Parameter(Mandatory = $true)]
        [string]$EngineDirectory
    )

    $srcDir = Split-Path (Split-Path $EngineDirectory -Parent) -Parent
    $pluginsRoot = Join-Path $srcDir "plugins"
    $pluginFileName = "{0}.psm1" -f $Plugin.name
    $candidatePaths = [System.Collections.Generic.List[string]]::new()
    $candidatePaths.Add((Join-Path (Join-Path $EngineDirectory "custom") $pluginFileName))

    $preferredGroups = @('Platform', 'DotNet', 'Npm')
    $candidatePaths.Add((Join-Path (Join-Path $pluginsRoot $preferredGroups[0]) $pluginFileName))

    if (Get-Command Get-ExtensionPluginModulePaths -ErrorAction SilentlyContinue) {
        foreach ($extensionPath in Get-ExtensionPluginModulePaths -PluginsRoot $pluginsRoot -PluginFileName $pluginFileName) {
            $candidatePaths.Add($extensionPath)
        }
    }

    foreach ($group in $preferredGroups[1..($preferredGroups.Count - 1)]) {
        $candidatePaths.Add((Join-Path (Join-Path $pluginsRoot $group) $pluginFileName))
    }

    $reservedPluginDirs = @($preferredGroups + @('Shared'))
    if (Test-Path -LiteralPath $pluginsRoot -PathType Container) {
        Get-ChildItem -LiteralPath $pluginsRoot -Directory -ErrorAction SilentlyContinue |
            Where-Object { $_.Name -notin $reservedPluginDirs } |
            Sort-Object Name |
            ForEach-Object {
                $candidatePaths.Add((Join-Path $_.FullName $pluginFileName))
            }
    }

    foreach ($candidatePath in $candidatePaths) {
        if (Test-Path $candidatePath -PathType Leaf) {
            return $candidatePath
        }
    }

    return $candidatePaths[0]
}

function Test-PluginRunnable {
    param(
        [Parameter(Mandatory = $true)]
        $Plugin,

        [Parameter(Mandatory = $true)]
        [psobject]$SharedSettings,

        [Parameter(Mandatory = $true)]
        [string]$EngineDirectory,

        [Parameter(Mandatory = $false)]
        [bool]$WriteLogs = $true
    )

    if ($null -eq $Plugin -or [string]::IsNullOrWhiteSpace($Plugin.name)) {
        if ($WriteLogs) {
            Write-Log -Level "WARN" -Message "Skipping plugin entry with no name."
        }
        return $false
    }

    if (-not $Plugin.enabled) {
        if ($WriteLogs) {
            Write-Log -Level "WARN" -Message "Skipping plugin '$($Plugin.name)' (disabled)."
        }
        return $false
    }

    $pluginModulePath = Resolve-PluginModulePath -Plugin $Plugin -EngineDirectory $EngineDirectory
    if (-not (Test-Path $pluginModulePath -PathType Leaf)) {
        if ($WriteLogs) {
            Write-Log -Level "ERROR" -Message "Plugin module not found: $pluginModulePath"
        }
        return $false
    }

    return $true
}

function New-PluginInvocationSettings {
    param(
        [Parameter(Mandatory = $true)]
        $Plugin,

        [Parameter(Mandatory = $true)]
        [psobject]$SharedSettings
    )

    $properties = @{}
    foreach ($property in $Plugin.PSObject.Properties) {
        $properties[$property.Name] = $property.Value
    }

    $properties['context'] = $SharedSettings
    return [pscustomobject]$properties
}

function Invoke-ConfiguredPlugin {
    param(
        [Parameter(Mandatory = $true)]
        $Plugin,

        [Parameter(Mandatory = $true)]
        [psobject]$SharedSettings,

        [Parameter(Mandatory = $true)]
        [string]$EngineDirectory
    )

    if (-not (Test-PluginRunnable -Plugin $Plugin -SharedSettings $SharedSettings -EngineDirectory $EngineDirectory -WriteLogs:$true)) {
        if ($Plugin.enabled) {
            return $false
        }

        return $true
    }

    $metadata = Get-PluginMetadataObject -Plugin $Plugin -EngineDirectory $EngineDirectory
    if ($null -ne $metadata -and ($metadata.PSObject.Properties.Name -contains 'providesVersion') -and [bool]$metadata.providesVersion) {
        $versionAlreadySet = $false
        if (Get-Command Get-EngineState -ErrorAction SilentlyContinue) {
            $existingVersion = Get-EngineState -Context $SharedSettings -Name 'version' -ErrorAction SilentlyContinue
            $versionAlreadySet = -not [string]::IsNullOrWhiteSpace([string]$existingVersion)
        }
        elseif (($SharedSettings.PSObject.Properties.Name -contains 'version') -and -not [string]::IsNullOrWhiteSpace([string]$SharedSettings.version)) {
            $versionAlreadySet = $true
        }

        if ($versionAlreadySet) {
            Write-Log -Level "INFO" -Message "Skipping plugin '$($Plugin.name)' (version already resolved during New-EngineContext)."
            return $true
        }

        # Test engine (and other hosts) may not resolve version in New-EngineContext; run the plugin now.
    }

    if ((Test-IsPublishPlugin -Plugin $Plugin) -and ($SharedSettings.PSObject.Properties.Name -contains 'skipPublishPlugins') -and $SharedSettings.skipPublishPlugins) {
        Write-Log -Level "INFO" -Message "Skipping plugin '$($Plugin.name)' (ReleasePublishGuard suppressed publish)."
        return $true
    }

    if (-not (Test-PluginCompatible -Plugin $Plugin -EngineDirectory $EngineDirectory -WriteLogs:$true)) {
        return $true
    }

    $pluginModulePath = Resolve-PluginModulePath -Plugin $Plugin -EngineDirectory $EngineDirectory
    Write-Log -Level "STEP" -Message "Running plugin '$($Plugin.name)'..."

    # Sink plugin success-stream output to the host so it cannot pollute this
    # function's return value. Otherwise `return $false` after CLI stdout becomes
    # @("helm-line…", $false), which is truthy under `if (-not $result)` and
    # causes RELEASE COMPLETE / exit 0 after a failed plugin.
    try {
        $moduleInfo = Import-Module $pluginModulePath -Force -PassThru -ErrorAction Stop
        $invokeCommand = Get-Command -Name "Invoke-Plugin" -Module $moduleInfo.Name -ErrorAction Stop
        $pluginSettings = New-PluginInvocationSettings -Plugin $Plugin -SharedSettings $SharedSettings

        & $invokeCommand -Settings $pluginSettings | ForEach-Object { Write-Host $_ }
        Write-Log -Level "OK" -Message "  Plugin '$($Plugin.name)' completed."
        return $true
    }
    catch {
        Write-Log -Level "ERROR" -Message "  Plugin '$($Plugin.name)' failed: $($_.Exception.Message)"
        return $false
    }
}

Export-ModuleMember -Function Import-PluginDependency, Get-ConfiguredPlugins, Get-PluginStageLabel, Get-PluginBranches, Get-PluginMetadataObject, Test-PluginCompatible, Test-PluginMutatesRemote, Resolve-PluginSecretName, Get-SecretEnvironmentValue, Get-RegistryCredentialsFromRuntime, Test-PluginSkipsRemoteMutation, Test-IsPublishPlugin, Get-PluginSettingValue, Get-PluginPathListSetting, Get-PluginPathSetting, Get-ArchiveNamePattern, Resolve-PluginModulePath, Test-PluginRunnable, New-PluginInvocationSettings, Invoke-ConfiguredPlugin
