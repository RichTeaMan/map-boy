#!/bin/bash

echo "Building web export..."
../godot-build/bin/godot.linuxbsd.editor.double.x86_64 --headless --export-release Web ../app/project.godot

rm static-files -rf
mkdir static-files
cp ../app/web/* static-files/. -r
rm static-files/.gdignore -f

echo "Building API..."
dotnet publish -c Release -o bin/published

docker build -t osm-api --file docker/Dockerfile .

