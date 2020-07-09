#!/usr/bin/env bash

##########################################################################
# This is the modified Cake bootstrapper script for Linux and OS X.
##########################################################################

echo "Starting build.sh"

DEVOPS_VERSION=1.11.0
NUGET_URL=https://api.nuget.org/v3/index.json
NUGET_BETA_URL=https://www.myget.org/F/micro-elements/api/v3/index.json

# Define directories.
SCRIPT_DIR=$( cd "$( dirname "${BASH_SOURCE[0]}" )" && pwd )
TOOLS_DIR=$SCRIPT_DIR/tools

# Script to run.
SCRIPT=$TOOLS_DIR/microelements.devops/$DEVOPS_VERSION/scripts/main.cake

CAKE_PROPS_PATH=$TOOLS_DIR/cake.props
CAKE_VERSION="0.38.0"
CAKE_ARGUMENTS=()

# Parse arguments.
for i in "$@"; do
    case $1 in
        -s|--script) SCRIPT="$2"; shift ;;
        --cake-version) CAKE_VERSION="--version=$2"; shift ;;
        --) shift; CAKE_ARGUMENTS+=("$@"); break ;;
        *) CAKE_ARGUMENTS+=("$1") ;;
    esac
    shift
done

CAKE_ARGUMENTS+=("--rootDir=$SCRIPT_DIR");
CAKE_ARGUMENTS+=("--devOpsVersion=$DEVOPS_VERSION");
CAKE_ARGUMENTS+=("--devOpsRoot=$TOOLS_DIR/microelements.devops/$DEVOPS_VERSION");

echo "===========VARIABLES============"
echo "SCRIPT_DIR: $SCRIPT_DIR"
echo "SCRIPT: $SCRIPT"
echo "TOOLS_DIR: $TOOLS_DIR"
echo "NUGET_URL: $NUGET_URL"
echo "NUGET_BETA_URL: $NUGET_BETA_URL"
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
  <TargetFramework>netcoreapp3.0</TargetFramework>
</PropertyGroup>
<ItemGroup>
  <PackageReference Include="MicroElements.DevOps" Version="$DEVOPS_VERSION" />
</ItemGroup>
</Project>
EOL
    echo "cake.props written to $CAKE_PROPS_PATH"
    cat "$CAKE_PROPS_PATH"
fi

# Restore Cake and Packages
dotnet restore $CAKE_PROPS_PATH --packages $TOOLS_DIR --source "$NUGET_URL" --source "$NUGET_BETA_URL"
dotnet tool restore

# Start Cake
echo "Running build script..."
CMD="dotnet cake $SCRIPT ${CAKE_ARGUMENTS[@]}"
echo $CMD
exec $CMD
