#!/bin/bash

set -e

CHOWN_ARG="$1"

PLATFORM="$2"
if [[ $PLATFORM == "" ]]; then
  PLATFORM="all"
fi

export GODOT_TAG="4.6-stable"
export GODOT_VERSION_STATUS="mapboy"

git clone --branch "$GODOT_TAG" --depth 1 https://github.com/godotengine/godot.git

cd godot

python3 misc/scripts/install_d3d12_sdk_windows.py

/project/emsdk/emsdk activate latest
source /project/emsdk/emsdk_env.sh

DATE=$(date --iso-8601)

BIN_PATH="bin/$DATE"

mkdir -p bin

if [[ $PLATFORM == "linuxbsd-editor" || $PLATFORM == "all" ]]; then
    mkdir -p ../bin/linuxbsd-editor
    mkdir -p ../bin/linuxbsd-editor/nuget
    scons platform=linuxbsd float=64 precision=double production=yes tools=yes module_mono_enabled=yes mono_glue=no
    ./bin/godot.linuxbsd.editor.double.x86_64.mono  --headless --precision=double --generate-mono-glue modules/mono/glue
    ./modules/mono/build_scripts/build_assemblies.py --precision=double --godot-output-dir=./bin --push-nupkgs-local ./nuget
    cp -r bin/* ../bin/linuxbsd-editor/.
    cp -r nuget/* ../bin/linuxbsd-editor/nuget/.
    chown "$CHOWN_ARG" ../bin -R
fi
if [[ $PLATFORM == "linuxbsd-template-release" || $PLATFORM == "all" ]]; then
    mkdir -p ../bin/linuxbsd-template-release
    scons platform=linuxbsd float=64 precision=double production=yes target=template_release arch=x86_64
    cp -r bin/* ../bin/linuxbsd-template-release
    chown "$CHOWN_ARG" ../bin -R
fi
if [[ $PLATFORM == "linuxbsd-template-debug" || $PLATFORM == "all" ]]; then
    mkdir -p ../bin/linuxbsd-template-debug
    scons platform=linuxbsd float=64 precision=double production=yes target=template_debug arch=x86_64
    cp -r bin/* ../bin/linuxbsd-template-debug
    chown "$CHOWN_ARG" ../bin -R
fi

if [[ $PLATFORM == "windows-editor" || $PLATFORM == "all" ]]; then
    mkdir -p ../bin/windows-editor
    scons platform=windows float=64 precision=double production=yes
    cp -r bin/* ../bin/windows-editor
    chown "$CHOWN_ARG" ../bin -R
fi
if [[ $PLATFORM == "windows-template-release" || $PLATFORM == "all" ]]; then
    mkdir -p ../bin/windows-template-release
    scons platform=windows float=64 precision=double production=yes target=template_release arch=x86_64
    cp -r bin/* ../bin/windows-template-release
    chown "$CHOWN_ARG" ../bin -R
fi
if [[ $PLATFORM == "windows-template-debug" || $PLATFORM == "all" ]]; then
    mkdir -p ../bin/windows-template-debug
    scons platform=windows float=64 precision=double production=yes target=template_debug arch=x86_64
    cp -r bin/* ../bin/windows-template-debug
    chown "$CHOWN_ARG" ../bin -R
fi

if [[ $PLATFORM == "web-release-no-threads" || $PLATFORM == "all" ]]; then
    mkdir -p ../bin/web-release-no-threads
    scons platform=web target=template_release precision=double threads=no
    cp -r bin/* ../bin/web-release-no-threads/.
    chown "$CHOWN_ARG" ../bin -R
fi
if [[ $PLATFORM == "web-debug-no-threads" || $PLATFORM == "all" ]]; then
    mkdir -p ../bin/web-debug-no-threads
    scons platform=web target=template_debug precision=double threads=no
    cp -r bin/* ../bin/web-debug-no-threads/.
    chown "$CHOWN_ARG" ../bin -R
fi

if [[ $PLATFORM == "net-linuxbsd-editor" || $PLATFORM == "all" ]]; then
    mkdir -p ../bin/net-linuxbsd-editor
    scons platform=linuxbsd module_mono_enabled=yes float=64 precision=double production=yes
    ./bin/godot.linuxbsd.editor.double.x86_64.mono --headless --generate-mono-glue modules/mono/glue
    ./modules/mono/build_scripts/build_assemblies.py --precision=double --godot-output-dir=./bin
    cp -r bin/* ../bin/net-linuxbsd-editor/.
    chown "$CHOWN_ARG" ../bin -R
fi
if [[ $PLATFORM == "net-linuxbsd-template-release" || $PLATFORM == "all" ]]; then
    mkdir -p ../bin/net-linuxbsd-template-release
    scons platform=linuxbsd module_mono_enabled=yes float=64 precision=double production=yes target=template_release arch=x86_64
    ./bin/godot.linuxbsd.editor.double.x86_64.mono --headless --generate-mono-glue modules/mono/glue
    ./modules/mono/build_scripts/build_assemblies.py --precision=double --godot-output-dir=./bin
    cp -r bin/* ../bin/net-linuxbsd-template-release
    chown "$CHOWN_ARG" ../bin -R
fi
if [[ $PLATFORM == "net-linuxbsd-template-debug" || $PLATFORM == "all" ]]; then
    mkdir -p ../bin/net-linuxbsd-template-debug
    scons platform=linuxbsd module_mono_enabled=yes float=64 precision=double production=yes arch=x86_64
    scons platform=linuxbsd module_mono_enabled=yes float=64 precision=double production=yes target=template_debug arch=x86_64
    ls -lh
    echo "bin"
    ls ./bin -lh
    ./bin/godot.linuxbsd.editor.double.x86_64.mono --headless --generate-mono-glue modules/mono/glue
    ./modules/mono/build_scripts/build_assemblies.py --precision=double --godot-output-dir=./bin
    cp -r bin/* ../bin/net-linuxbsd-template-debug
    chown "$CHOWN_ARG" ../bin -R
fi

if [[ $PLATFORM == "net-windows-editor" || $PLATFORM == "all" ]]; then
    mkdir -p ../bin/net-windows-editor
    scons platform=windows module_mono_enabled=yes float=64 precision=double production=yes
    cp -r bin/* ../bin/net-windows-editor
    chown "$CHOWN_ARG" ../bin -R
fi
if [[ $PLATFORM == "net-windows-template-release" || $PLATFORM == "all" ]]; then
    mkdir -p ../bin/net-windows-template-release
    scons platform=windows module_mono_enabled=yes float=64 precision=double production=yes target=template_release arch=x86_64
    cp -r bin/* ../bin/net-windows-template-release
    chown "$CHOWN_ARG" ../bin -R
fi
if [[ $PLATFORM == "net-windows-template-debug" || $PLATFORM == "all" ]]; then
    mkdir -p ../bin/net-windows-template-debug
    scons platform=windows module_mono_enabled=yes float=64 precision=double production=yes target=template_debug arch=x86_64
    cp -r bin/* ../bin/net-windows-template-debug
    chown "$CHOWN_ARG" ../bin -R
fi
