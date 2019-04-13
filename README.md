# Frogtown Mod Library
Contains shared logic for updating player prefabs, interacting with chat, maintaining RoR2Application.isModded, and the interface for toggling mods on an off.

This mod is a prerequisite for:
- [Cheat Chat Commands Mod](https://github.com/ToyDragon/ROR2ModChatCommandCheats)
- [Healing Helper Mod](https://github.com/ToyDragon/ROR2ModHealingHelper)
- [Character Randomizer](https://github.com/ToyDragon/ROR2ModCharacterRandomizer)
- [Engineer Fixes](https://github.com/ToyDragon/ROR2ModEngineerLunarCoinFix)

# Installation
1. Install [BepInEx Mod Pack](https://thunderstore.io/package/bbepis/BepInExPack/)
2. Visit the [releases page](https://github.com/ToyDragon/ROR2ModShared/releases)
3. Download the latest FrogtownShared.dll
4. Move FrogtownShared.dll to your \BepInEx\plugins folder

# Developers
This library can help you:
- Toggle your mods on/off
- Maintain the isModded flag
- Add chat commands

```C#
public ModDetails modDetails;
public void Awake()
{
    //Initializing a ModDetails object will add your mod to the mod
    //list, and allow it to be enabled or disabled from chat commands.
    modDetails = new ModDetails("com.frogtown.chatcheats");
    
    //This library adds chat command "/enable_mod" and "/disable_mod"
    //that users will use to turn your mod on or off. When all mods are
    //disabled it will update the isModded flag to false, and when any
    //are enabled it will set it back to true.
    
    //Add chat commands
    FrogtownShared.AddChatCommand("give_item", OnGiveCommand);
    //You command will be called when any user types "/give_item x y z" in the chat.
    
}

//userName is the display name of the user, and pieces is the command and list of parameters.
private bool OnGiveCommand(string userName, string[] pieces)
{
    //All of your code is still ran while disabled, so you must check
    //the enabled flag before doing anything.
    if (!modDetails.enabled)
    {
        return false;
    }
    
    //Use FrogtownShared.GetPlayerWithName to get a reference to the
    //player who called your chat command.
    PlayerCharacterMasterController player = FrogtownShared.GetPlayerWithName(userName);
    
    //Do the stuff
}
```
