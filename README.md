# PostMortemPossession
Mount &amp; Blade II - mod

This Mount &amp; Blade II: Bannerlord mod allows you to take control of friendly soldiers when the player gets knocked out during battle.
This mod was inspired by [Control Your Allies After Death](https://www.nexusmods.com/mountandblade2bannerlord/mods/407) (made by Rafaws). The key diffence between these two mods is that this one allows the player to select the friendly unit they wish to control.

The mod has 4 options that can be configered in a json file.
1. allowControlAllies (bool-default true) : if the player should be able to control ally soldiers
   1. true: Player can control ally and party soldiers
   2. false: Player can only control party soldiers
2. muteExceptions (bool-default true) : if exceptions should be posted to the ingame log
3. verbose (bool-default true) : if various status messages should be posted to the ingame log
4. hotkey (string-default "O") : the key that triggers the "Possession"
   1. almost all keys can be used

## How to
How to control/possess a friendly NPC:
1. Get knocked out :p
2. Follow a friendly soldier with the camera
   1. You can do this by getting close to a soldier and then left clicking
3. Hit the hotkey (default O)

You can repeat this process every time the character is knocked out or killed.

## Installation
You can either compile the project yourself or go to TODO to download the module.
To compile the source youself, follow [this guide](https://docs.bannerlordmodding.com/_tutorials/basic-csharp-mod.html#introduction) and replace SubModule.xml and the MySubModule.cs with files in this repo (xml file with the same name and "PostMortemPossession.cs").
The nuget library Newtonsoft.Json is also used and will have to be installed. The Taleworlds and MountAndBlade references (.dll's) can be found here: ..Mount & Blade II Bannerlord\\bin\\Win64_Shipping_Client
