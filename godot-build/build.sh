#!/bin/bash

set -e

docker pull ubuntu
docker build -t godot-build .

mkdir -p bin

test -t 1 && USE_TTY="-t" # check if TTY is available
docker run --rm ${USE_TTY} --user $(id -u):$(id -g) -v $(pwd)/bin:/project/bin godot-build

chown -R $(id -u):$(id -g) ./bin
