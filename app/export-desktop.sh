#!/bin/bash

set -e

EDITOR_PATH="../godot-build/bin/linuxbsd-editor/godot.linuxbsd.editor.double.x86_64"

# build from github is zipped and loses eXecutable
chmod +x "$EDITOR_PATH"

echo "Building Linux export..."
$EDITOR_PATH --headless --export-release Linux project.godot

echo "Building Windows export..."
$EDITOR_PATH --headless --export-release Windows project.godot
