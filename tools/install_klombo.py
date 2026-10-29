"""python install_klombo.py  (game closed) -> copies the plugin dll (with Klombo) and the freshly built J CLIENT dll into the game's plugin folders; the Klombo models were copied already."""
import os, shutil
G = r'C:\Program Files (x86)\Steam\steamapps\common\Gorilla Tag\BepInEx\plugins'
P = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
shutil.copy2(os.path.join(P, 'bin', 'Release', 'net472', 'JsLiveEventsMod.dll'), os.path.join(G, 'JsLiveEventsMod', 'JsLiveEventsMod.dll')); print('plugin dll copied')
jc = r'C:\Users\drago\OneDrive\Documents\J CLIENT\bin\Release\net472\JClient.dll'
if os.path.exists(jc): shutil.copy2(jc, os.path.join(G, 'JClient', 'JClient.dll')); print('J CLIENT dll copied')
else: print('J CLIENT dll not found at', jc)
