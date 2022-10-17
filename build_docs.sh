#!/usr/bin/env bash

export DOTNET_ROOT=/opt/buildhome/.dotnet

dotnet tool install -g fsdocs-tool
fsdocs build
