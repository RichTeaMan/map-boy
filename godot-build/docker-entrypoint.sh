#!/bin/bash

set -e

mkdir bin -p

git clone --branch "4.4-stable" --depth 1 https://github.com/godotengine/godot.git

cd godot

/project/emsdk/emsdk activate latest
source /project/emsdk/emsdk_env.sh

mkdir bin
scons platform=linuxbsd float=64 precision=double production=yes
scons platform=windows float=64 precision=double production=yes
scons platform=web target=template_release
scons platform=web target=template_debug

mv bin/* ../bin/.
