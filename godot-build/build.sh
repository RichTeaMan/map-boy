#!/bin/bash

docker pull ubuntu
docker build -t godot-build .

docker run --rm -it -v $(pwd)/bin:/project/bin godot-build

