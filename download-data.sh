#!/bin/bash

MAP_NAME="$1"
MAP_NAME="${MAP_NAME,,}" # lowercases. works in bash 4.0

if [[ "$MAP_NAME" = "greater-london" ]]; then
    echo "Downloading $MAP_NAME..."
    curl https://download.geofabrik.de/europe/united-kingdom/england/greater-london-latest.osm.pbf -o greater-london.osm.pbf
elif [[ "$MAP_NAME" = "england" ]]; then
    echo "Downloading $MAP_NAME..."
    curl https://download.geofabrik.de/europe/united-kingdom/england-latest.osm.pbf -o england.osm.pbf
else
    echo "Unknown map name '$MAP_NAME'."
    exit 1
fi

echo "Finished download $MAP_NAME."


# TODO - postcode data
# https://open-geography-portalx-ons.hub.arcgis.com/datasets/ons::ons-postcode-directory-february-2024-for-the-uk/about
# curl -L https://www.arcgis.com/sharing/rest/content/items/e14b1475ecf74b58804cf667b6740706/data -o postcode-data.zip
