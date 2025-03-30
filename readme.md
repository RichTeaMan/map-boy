# Map Boy

OpenStreetMap, now in 3d!

This project uses OSM data and renders building in 3D within Godot.

## Background

Map Boy implements a server-client architecture with some preprocessing. A dotnet project processes OSM data to find buildings and highways,
another dotnet projects hosts that data over a REST API, and finally a Godot project consumes that API to render the map in 3D.

## Requirements

Map boy requires [dotnet SDK 9.0](https://dotnet.microsoft.com/en-us/download/dotnet/9.0) and [Godot](https://godotengine.org/)
(official build of Godot don't work with Map Boy, see below).

### Custom Godot build

However, Map Boy needs Godot with double floating point precision, which doesn't have an official build. Instead, this project
includes a docker image that builds Godot for Linux, Windows and web exports. A shell script wraps all the docker instuctions:

```bash
# only works on Linux with Docker installed. Windows builds will also be created
cd godot-build
./build.sh
```

Godot builds will be saved to `godot-build/bin/`.

## Running the server

```bash
./download-data.sh greater-london
(cd osm-tool && dotnet run ../greater-london.osm.pbf)
(cd osm-api && mv -f ../osm-tool/greater-london.osm.pbf.db osm.db && dotnet run)
```

## Running the client

Open the custom Godot build from `godot-build/bin` and run the godot project in `app`.
