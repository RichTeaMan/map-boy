#!/bin/bash

curl https://download.geofabrik.de/europe/united-kingdom/england/greater-london-latest.osm.pbf -o greater-london.osm.pbf
curl https://download.geofabrik.de/europe/united-kingdom/england-latest.osm.pbf -o england.osm.pbf

# https://open-geography-portalx-ons.hub.arcgis.com/datasets/ons::ons-postcode-directory-february-2024-for-the-uk/about
curl -L https://www.arcgis.com/sharing/rest/content/items/e14b1475ecf74b58804cf667b6740706/data -o postcode-data.zip
