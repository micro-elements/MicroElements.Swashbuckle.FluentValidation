#!/usr/bin/env bash

##########################################################################
# This is the modified Cake bootstrapper script for Linux and OS X.
##########################################################################

echo "Starting build.sh"

CAKE_VERSION=0.27.2
DEVOPS_VERSION=0.5.0-beta.5

# Define directories.
SCRIPT_DIR=$( cd "$( dirname "${BASH_SOURCE[0]}" )" && pwd )
TOOLS_DIR=$SCRIPT_DIR/tools
CAKE_DLL=$TOOLS_DIR/cake.coreclr/$CAKE_VERSION/Cake.dll
NUGET_URL="https://www.nuget.org/api/v2/package"

SCRIPT="$TOOLS_DIR/microelements.devops/$DEVOPS_VERSION/scripts/main.cake"
CAKE_PROPS_PATH=$TOOLS_DIR/cake.props
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

CAKE_ARGUMENTS+=("--rootDir=\"$SCRIPT_DIR\"");

echo "===========VARIABLES============"
echo "SCRIPT_DIR: $SCRIPT_DIR"
echo "TOOLS_DIR: $TOOLS_DIR"
echo "CAKE_DLL: $CAKE_DLL"
echo "NUGET_URL: $NUGET_URL"
echo "CAKE_PROPS_PATH: $CAKE_PROPS_PATH"
echo "CAKE_ARGUMENTS: ${CAKE_ARGUMENTS[*]}"

###########################################################################
# RESTORE CAKE AND LIBS
###########################################################################

# Make sure the tools folder exist.
if [ ! -d "$TOOLS_DIR" ]; then
  mkdir "$TOOLS_DIR"
fi

# Write cake.props to tools folder.
if [ ! -f "$CAKE_PROPS_PATH" ]
then
    echo "cake.props doesnot exists"
    cat > "$CAKE_PROPS_PATH" <<EOL
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
EOL
    echo "cake.props written to $CAKE_PROPS_PATH"
    cat "$CAKE_PROPS_PATH"
fi

# Restore Cake
dotnet restore $CAKE_PROPS_PATH --packages $TOOLS_DIR

# Start Cake
echo "Running build script..."
echo "CakeArguments: $CAKE_ARGUMENTS"
exec dotnet "$CAKE_DLL" $SCRIPT "${CAKE_ARGUMENTS[@]}"
