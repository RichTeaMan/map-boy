#!/bin/bash

set -e

echo "Building web export..."
# build from github is zipped and loses eXecutable
chmod +x ../godot-build/bin/linuxbsd/godot.linuxbsd.editor.double.x86_64
../godot-build/bin/linuxbsd/godot.linuxbsd.editor.double.x86_64 --headless --export-release Web ../app/project.godot

rm static-files -rf
mkdir static-files
cp ../app/export/web/* static-files/. -r
rm static-files/.gdignore -f

echo "Building API..."
dotnet publish -c Release -o bin/published

docker build -t osm-api --file docker/Dockerfile .

