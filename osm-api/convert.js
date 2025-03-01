const areaJson = require('./areas.json');

for (const area of areaJson) {
    if (area.id === 28934) {

        for (const coord of area.coordinates) {
            console.log(`${coord.lat},${coord.lon}`);
        }
    }
}