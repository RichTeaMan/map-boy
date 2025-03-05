#!/bin/bash

docker pull ubuntu
docker build -t godot-build .

mkdir -p bin

docker run --rm -it --user $(id -u):$(id -g) -v $(pwd)/bin:/project/bin godot-build

chown -R $(id -u):$(id -g) ./bin
