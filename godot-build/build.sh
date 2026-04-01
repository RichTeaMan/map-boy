#!/bin/bash

set -e

DEBUG_MODE="0"

DATE=$(date +%s)

CONTAINER_NAME="godot-build-$DATE"

if [[ "$*" == *"--debug"* ]]
then
    DEBUG_MODE="1"
fi

# eurgh
ARGS=$(ARGS="$@" node -e "console.log(process.env.ARGS.replaceAll('--debug', ''))" | xargs)

echo "Debug mode activated, the container will not be deleted once completed."

docker pull --quiet ubuntu
docker build --quiet -t godot-build .

mkdir -p bin

DOCKER_PARAMS="--rm"
if [[ "$DEBUG_MODE" == "1" ]]
then
    DOCKER_PARAMS=""
fi

test -t 1 && USE_TTY="-it" # check if TTY is available
docker run --name ${CONTAINER_NAME} ${DOCKER_PARAMS} ${USE_TTY} -v $(pwd)/bin:/project/bin godot-build "$(id -u):$(id -g)" "$ARGS" && true
EXIT_CODE=$?

if [[ "$EXIT_CODE" != "0" ]]
then
    echo "Failure detected!"
    if [[ "$DEBUG_MODE" == "1" ]]
    then
        VOLUME_PATH=$(docker inspect "$CONTAINER_NAME" | jq .[0].GraphDriver.Data.UpperDir -r)
        echo "The volume can be inspected:"
        echo "sudo ls $VOLUME_PATH/project"
    fi
fi
