#!/usr/bin/env bash

##########################################################################
# This is the modified Cake bootstrapper script for Linux and OS X.
##########################################################################

CAKE_VERSION=0.27.2
DEVOPS_VERSION=0.5.0-beta.3

# Define directories.
SCRIPT_DIR=$( cd "$( dirname "${BASH_SOURCE[0]}" )" && pwd )
TOOLS_DIR=$SCRIPT_DIR/tools
CAKE_DLL=$TOOLS_DIR/Cake.CoreCLR.$CAKE_VERSION/Cake.dll
NUGET_URL="https://www.nuget.org/api/v2/package"

SCRIPT="build.cake"
CAKE_ARGUMENTS=()

# Parse arguments.
for i in "$@"; do
    case $1 in
        -s|--script) SCRIPT="$2"; shift ;;
        --) shift; CAKE_ARGUMENTS+=("$@"); break ;;
        *) CAKE_ARGUMENTS+=("$1") ;;
    esac
    shift
done

###########################################################################
# RESTORE CAKE AND LIBS
###########################################################################

$cake_props = @"
<Project Sdk="Microsoft.NET.Sdk">
<PropertyGroup>
  <TargetFramework>netstandard2.0</TargetFramework>
</PropertyGroup>
<ItemGroup>
  <PackageReference Include="Cake.CoreCLR" Version="$CAKE_VERSION" />
  <PackageReference Include="Cake.Bakery" Version="0.2.0" />
  <PackageReference Include="MicroElements.DevOps" Version="$DEVOPS_VERSION" />
</ItemGroup>
</Project>
"@

$cake_props_path = "/tools/cake.props"

if [ -f "$cake_props_path" ]
then 
    mkdir -p $TOOLS_DIR
    echo "$cake_props" > "$cake_props_path"
fi

# Restore Cake
exec dotnet restore $cake_props_path --packages $TOOLS_DIR

# Start Cake
echo "Running build script..."
echo "CakeArguments: $CAKE_ARGUMENTS"
exec dotnet "$CAKE_DLL" $SCRIPT "${CAKE_ARGUMENTS[@]}"
