#!/bin/bash

set -e

git clone --branch "4.4.1-stable" --depth 1 https://github.com/godotengine/godot.git

cd godot

/project/emsdk/emsdk activate latest
source /project/emsdk/emsdk_env.sh

mkdir -p bin

mkdir -p ../bin/linuxbsd
scons platform=linuxbsd float=64 precision=double production=yes
scons platform=linuxbsd float=64 precision=double production=yes target=template_release arch=x86_64
scons platform=linuxbsd float=64 precision=double production=yes target=template_debug arch=x86_64
mv bin/* ../bin/linuxbsd/.

mkdir -p ../bin/windows
scons platform=windows float=64 precision=double production=yes
scons platform=windows float=64 precision=double production=yes target=template_release arch=x86_64
scons platform=windows float=64 precision=double production=yes target=template_debug arch=x86_64
mv bin/* ../bin/windows/.

mkdir -p ../bin/web-release-no-threads
scons platform=web target=template_release precision=double threads=no
mv bin/* ../bin/web-release-no-threads/.

mkdir -p ../bin/web-debug-no-threads
scons platform=web target=template_debug precision=double threads=no
mv bin/* ../bin/web-debug-no-threads/.
