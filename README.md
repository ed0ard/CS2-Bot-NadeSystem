# CS2-Bot-NadeSystem
CS2-Bot-NadeSystem is a plugin based on CounterStrikeSharp that allows bots to replay lineups recorded by players and throw nades deftly according to the situation.

# Features
1. Bots can "rethrow" the nades recorded by players on every map.

2. Bots can make their own decisions to throw nades or not when passing by recorded lineups.

3. Players can record lineups in an intuitive way on every map for bots to use.
# Requirement
[NadeLauncher](https://github.com/StefanKunde/NadeLauncher)

[Ray-Trace](https://github.com/FUNPLAY-pro-CS2/Ray-Trace)

# Installation
1. Download the latest **RayTrace-MM.tar.gz** and **RayTrace-CSS-API.tar.gz** from [Ray-Trace](https://github.com/FUNPLAY-pro-CS2/Ray-Trace/releases)

2. Extract the folders and upload them to `game/csgo/addons` on your server

3. Download the latest **NadeSystem.zip** from [here](https://github.com/ed0ard/CS2-Bot-NadeSystem/releases) and **NadeLauncher.dll** from [here](https://github.com/StefanKunde/NadeLauncher/releases)

4. Extract the `NadeSystem` folder and upload it to `game/csgo/addons/counterstrikesharp/plugins` on your server

5. Upload `NadeLauncher.dll` to `game/csgo/addons/counterstrikesharp/plugins/NadeLauncher/NadeLauncher.dll` on your server

# Commands
`bot_nades`  
Shows the current nade throwing mode

`bot_nades off`  
Bots won't throw any nades

`bot_nades normal`  
Bots follow almost the same count limits as human players (default)

`bot_nades more`  
Bots use the same decision logic as normal mode with higher count limits (recommended)

`bot_nades max`  
Bots have minimal limitations and think less before throwing nades

# Instructions
## Record Lineups
Record lineups using [NadeLauncher](https://github.com/StefanKunde/NadeLauncher).

<img width="2560" height="1440" alt="730_71" src="https://github.com/user-attachments/assets/1b380d1c-5c72-4715-bec1-e0f5e920cc9a" />

(You can find your lineup libraries in `game\csgo\addons\counterstrikesharp\plugins\NadeLauncher\data\lineups`)

## Add Descriptions
If you want the bots to **only** use the lineups at **round start**, add uppercase `T` for Terrorists and uppercase `CT` for Counter-Terrorists at the start of lineup descriptions.

If your lineup needs to **break the window first**, stand in the same position and throw a flashbang to smash the window, and add `decoy` to the lineup description. Then the bots will throw a decoy first to break the window before triggering your lineup.

## Convert Lineups
Run **convert_lineups.py** in your CMD or PowerShell with the command `python convert_lineups.py "NadeLauncherPath" "NadeSystemPath"`.

(For example, `python convert_lineups.py "C:\Steam\steamapps\common\Counter-Strike Global Offensive\game\csgo\addons\counterstrikesharp\plugins\NadeLauncher" "C:\Steam\steamapps\common\Counter-Strike Global Offensive\game\csgo\addons\counterstrikesharp\plugins\NadeSystem"`)

If you don't have Python installed on your computer, double-click **convert_lineups.exe** to convert lineups automatically.

(After conversion, you can check lineup libraries for bots in `game\csgo\addons\counterstrikesharp\plugins\NadeSystem\grenades`)

## So bots can "rethrow" the nades recorded by you now

<img width="2560" height="1440" alt="730_73" src="https://github.com/user-attachments/assets/ac569784-374f-494c-a4be-bdd89d0347c8" />

# Credits
Special thanks to [QQundalf](https://github.com/StefanKunde) and [DANK1NG1I45l4](https://github.com/DANK1NG1I45l4).
