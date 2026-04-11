#!/bin/bash

set -e

CHOWN_ARG="$1"

PLATFORM="$2"
if [[ $PLATFORM == "" ]]; then
  PLATFORM="all"
fi

export GODOT_TAG="4.6.2-stable"
export GODOT_VERSION_STATUS="mapboy"

git -c "advice.detachedhead=false" clone --branch "$GODOT_TAG" --depth 1 https://github.com/godotengine/godot.git

cd godot

python3 misc/scripts/install_d3d12_sdk_windows.py

/project/emsdk/emsdk activate latest
source /project/emsdk/emsdk_env.sh

DATE=$(date --iso-8601)

BIN_PATH="bin/$DATE"

mkdir -p bin

echo "$PLATFORM"

if [[ $PLATFORM == "linux" || $PLATFORM == "all" ]]; then

    mkdir -p ../bin/linux
    mkdir -p ../bin/linux/nuget

    # editor

    scons platform=linuxbsd float=64 precision=double production=yes tools=yes module_mono_enabled=yes mono_glue=no
    ./bin/godot.linuxbsd.editor.double.x86_64.mono  --headless --precision=double --generate-mono-glue modules/mono/glue
    ./modules/mono/build_scripts/build_assemblies.py --precision=double --godot-output-dir=./bin --push-nupkgs-local ./nuget

    # release template

    scons platform=linuxbsd float=64 precision=double production=yes target=template_release arch=x86_64
    
    cp -r bin/godot.linuxbsd.editor.double.x86_64.mono ../bin/linux/.
    cp -r bin/GodotSharp ../bin/linux/.
    cp -r nuget/* ../bin/linux/nuget/.
    cp -r bin/godot.linuxbsd.template_release.double.x86_64 ../bin/linux/.
fi

echo "Running chown "$CHOWN_ARG" ../bin -R"
chown "$CHOWN_ARG" ../bin -R

echo "Deleting build artefacts..."
rm -rf bin
