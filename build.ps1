#################################################################################################################
# This is the MicroElements.DevOps Cake bootstrapper script for PowerShell.
# For latest version see: https://github.com/micro-elements/MicroElements.DevOps/blob/master/resources/build.ps1
#################################################################################################################

[CmdletBinding()]
Param(
    [string]$Script = "build.cake",
    [string]$Target,
    [string]$Configuration,
    [ValidateSet("Quiet", "Minimal", "Normal", "Verbose", "Diagnostic")]
    [string]$Verbosity,
    [Parameter(Position=0,Mandatory=$false,ValueFromRemainingArguments=$true)]
    [string[]]$ScriptArgs
)

Write-Host "Preparing to run build script..."

if(!$PSScriptRoot){
    $PSScriptRoot = Split-Path $MyInvocation.MyCommand.Path -Parent
}

$CAKE_VERSION = "0.38.0"
$DEVOPS_VERSION = "1.11.0"
$NUGET_URL = "https://api.nuget.org/v3/index.json"
$NUGET_BETA_URL = "https://www.myget.org/F/micro-elements/api/v3/index.json"
#$NUGET_URL = "file://C:\NuGet"

$TOOLS_DIR = Join-Path $PSScriptRoot "tools"
$CAKE_DLL = Join-Path $TOOLS_DIR "Cake.CoreCLR/$CAKE_VERSION/Cake.dll"

# Script to run.
$Script = Join-Path $TOOLS_DIR "microelements.devops/$DEVOPS_VERSION/scripts/main.cake"

$cake_props = @"
<Project Sdk="Microsoft.NET.Sdk">
<PropertyGroup>
  <TargetFramework>netstandard2.0</TargetFramework>
</PropertyGroup>
<ItemGroup>
  <PackageReference Include="Cake.CoreCLR" Version="$CAKE_VERSION" />
  <PackageReference Include="MicroElements.DevOps" Version="$DEVOPS_VERSION" />
</ItemGroup>
</Project>
"@

$cake_props_path = ".\tools\cake.props"

if(!(Test-Path $cake_props_path))
{
    New-Item -ItemType Directory -Force -Path $TOOLS_DIR
    $cake_props >> $cake_props_path
}

# Restore Cake
&dotnet restore $cake_props_path --packages $TOOLS_DIR --source @("$NUGET_URL") --source @("$NUGET_BETA_URL")

# Build Cake arguments
$cakeArguments = @("$Script");
if ($Target) { $cakeArguments += "-target=$Target" }
if ($Configuration) { $cakeArguments += "-configuration=$Configuration" }
if ($Verbosity) { $cakeArguments += "-verbosity=$Verbosity" }
$cakeArguments += ("--rootDir="+@("$PSScriptRoot"));
$cakeArguments += ("--devOpsVersion=$DEVOPS_VERSION");
$cakeArguments += ("--devOpsRoot=""$TOOLS_DIR/microelements.devops/$DEVOPS_VERSION""");
$cakeArguments += $ScriptArgs

# Start Cake
Write-Host "Running build script..."
Write-Host "CakeArguments: $cakeArguments"
&dotnet $CAKE_DLL $cakeArguments
exit $LASTEXITCODE
