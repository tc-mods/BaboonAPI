#!/usr/bin/env bash

export DOTNET_ROOT=/opt/buildhome/.dotnet

dotnet tool install -g fsdocs-tool
# Build the project first so fsdocs can read the XML file
dotnet build

# Build docs
fsdocs build --parameters root /
