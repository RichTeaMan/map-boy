#!/bin/bash

set -e

PLATFORM="$1"
if [[ $PLATFORM == "" ]]; then
  PLATFORM="all"
fi

git clone --branch "4.4.1-stable" --depth 1 https://github.com/godotengine/godot.git

cd godot

/project/emsdk/emsdk activate latest
source /project/emsdk/emsdk_env.sh

mkdir -p bin



if [[ $PLATFORM == "linuxbsd-editor" || $PLATFORM == "all" ]]; then
    mkdir -p ../bin/linuxbsd-editor
    scons platform=linuxbsd float=64 precision=double production=yes
    mv bin/* ../bin/linuxbsd-editor/.
fi
if [[ $PLATFORM == "linuxbsd-template-release" || $PLATFORM == "all" ]]; then
    mkdir -p ../bin/linuxbsd-template-release
    scons platform=linuxbsd float=64 precision=double production=yes target=template_release arch=x86_64
    mv bin/* ../bin/linuxbsd-template-release
fi
if [[ $PLATFORM == "linuxbsd-template-debug" || $PLATFORM == "all" ]]; then
    mkdir -p ../bin/linuxbsd-template-debug
    scons platform=linuxbsd float=64 precision=double production=yes target=template_debug arch=x86_64
    mv bin/* ../bin/linuxbsd-template-debug
fi

if [[ $PLATFORM == "windows-editor" || $PLATFORM == "all" ]]; then
    mkdir -p ../bin/windows-editor
    scons platform=windows float=64 precision=double production=yes
    mv bin/* ../bin/windows-editor
fi
if [[ $PLATFORM == "windows-template-release" || $PLATFORM == "all" ]]; then
    mkdir -p ../bin/windows-template-release
    scons platform=windows float=64 precision=double production=yes target=template_release arch=x86_64
    mv bin/* ../bin/windows-template-release
fi
if [[ $PLATFORM == "windows-template-debug" || $PLATFORM == "all" ]]; then
    mkdir -p ../bin/windows-template-debug
    scons platform=windows float=64 precision=double production=yes target=template_debug arch=x86_64
    mv bin/* ../bin/windows-template-debug
fi

if [[ $PLATFORM == "web-release-no-threads" || $PLATFORM == "all" ]]; then
    mkdir -p ../bin/web-release-no-threads
    scons platform=web target=template_release precision=double threads=no
    mv bin/* ../bin/web-release-no-threads/.
fi
if [[ $PLATFORM == "web-debug-no-threads" || $PLATFORM == "all" ]]; then
    mkdir -p ../bin/web-debug-no-threads
    scons platform=web target=template_debug precision=double threads=no
    mv bin/* ../bin/web-debug-no-threads/.
fi
