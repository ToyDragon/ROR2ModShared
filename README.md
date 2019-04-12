# Frogtown Mod Library
Contains shared logic for updating player prefabs, interacting with chat, and maintaining RoR2Application.isModded.

This mod is a prerequisite for:
- [Cheat Chat Commands Mod](https://github.com/ToyDragon/ROR2ModChatCommandCheats)
- [Healing Helper Mod](https://github.com/ToyDragon/ROR2ModHealingHelper)
- [Character Randomizer](https://github.com/ToyDragon/ROR2ModCharacterRandomizer)

Also check out:
- [Engineer Fixes](https://github.com/ToyDragon/ROR2ModEngineerLunarCoinFix)

# Installation
1. Install [Unity Mod Manager](https://www.nexusmods.com/site/mods/21/)
2. Visit the [releases page](https://github.com/ToyDragon/ROR2ModShared/releases)
3. Download the latest FrogtownShared.zip
4. Unzip and move the FrogtownShared folder to your Mods folder

# Developers
The main benefit of this library is keeping track of how many mods are enabled, and updating the isModded flag. When you toggle the status of your mod call FrogtownShared.ModToggled(value), and it will automatically update the isModded flag.

Another benefit is easy chat commands, add a chat command by calling FrogtownShared.AddChatCommand("YourCommand", yourDelegate). Your delegate will be provided the user who called it, and the list of command pieces including the command itself.
