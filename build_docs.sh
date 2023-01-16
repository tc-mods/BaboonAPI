#!/usr/bin/env bash

export DOTNET_ROOT=/opt/buildhome/.dotnet

# Install fsdocs-tool for .NET 6
dotnet tool install --global fsdocs-tool --version 16.1.1

# Build the project first so fsdocs can read the XML file
dotnet build

# Build docs
fsdocs build --parameters root / fsdocs-logo-link $DEPLOY_PRIME_URL fsdocs-collection-name-link $DEPLOY_PRIME_URL
