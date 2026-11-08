#!/bin/bash
# like gt_restart.sh but starts the exe directly (the Steam URL launch sometimes does nothing): kill, install everything, launch, wait for the test console
GT="/c/Program Files (x86)/Steam/steamapps/common/Gorilla Tag"
taskkill //IM "Gorilla Tag.exe" //F >/dev/null 2>&1; sleep 4
cd "/c/Users/drago/Documents/J'sLiveEventsMod" && python tools/build_galactus.py install | tail -1
rm -f "$GT/BepInEx/LogOutput.log" "$GT/BepInEx/eventtest.log"
powershell -NoProfile -Command "Start-Process -FilePath 'C:\Program Files (x86)\Steam\steamapps\common\Gorilla Tag\Gorilla Tag.exe' -WorkingDirectory 'C:\Program Files (x86)\Steam\steamapps\common\Gorilla Tag'"
for i in $(seq 1 100); do grep -q "test console ready" "$GT/BepInEx/LogOutput.log" 2>/dev/null && break; sleep 3; done; echo "plugin up"
