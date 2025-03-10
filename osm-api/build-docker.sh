#!/bin/bash

dotnet publish -c Release -o bin/published

docker build -t osm-api --file docker/Dockerfile .

