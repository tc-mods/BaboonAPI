#!/usr/bin/env bash

pushd /tmp
wget https://dotnet.microsoft.com/download/dotnet/scripts/v1/dotnet-install.sh
chmod u+x /tmp/dotnet-install.sh
/tmp/dotnet-install.sh --channel 8.0
popd

export DOTNET_ROOT=/opt/buildhome/.dotnet

# Install fsdocs-tool for .NET 8
dotnet tool install --global fsdocs-tool --version 20.0.0

# Build the project first so fsdocs can read the XML file
dotnet build

# Build docs
fsdocs build --parameters root /
