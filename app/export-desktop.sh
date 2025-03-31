#!/bin/bash

set -e

# build from github is zipped and loses eXecutable
chmod +x ../godot-build/bin/linuxbsd/godot.linuxbsd.editor.double.x86_64

echo "Building Linux export..."
../godot-build/bin/linuxbsd/godot.linuxbsd.editor.double.x86_64 --headless --export-release Linux project.godot

echo "Building Windows export..."
../godot-build/bin/linuxbsd/godot.linuxbsd.editor.double.x86_64 --headless --export-release Windows project.godot
