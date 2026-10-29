#!/bin/bash
# closes Gorilla Tag, installs the plugin (+assets if 'assets'), relaunches it and waits for the plugin line
GT="/c/Program Files (x86)/Steam/steamapps/common/Gorilla Tag"
taskkill //IM "Gorilla Tag.exe" //F >/dev/null 2>&1; sleep 4
cd /c/Users/drago/Documents/AstronomicalEventMod && python tools/build_collision.py install | tail -1
rm -f "$GT/BepInEx/LogOutput.log" "$GT/BepInEx/eventtest.log"; start "" "steam://rungameid/1533390"
for i in $(seq 1 100); do grep -q "test console ready" "$GT/BepInEx/LogOutput.log" 2>/dev/null && break; sleep 3; done; echo "plugin up"
