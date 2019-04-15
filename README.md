# Frogtown Mod Library
Contains shared logic for enabling/disabling mods. For mod developers, it also adds an interface for interacting with chat commands.

![In game popup](https://github.com/ToyDragon/ROR2ModShared/blob/master/Images/ingame.png?raw=true)

## Usage
Toggle mods on and off using the checkbox in the far right column. Most mods don't support being toggled on or off without restarting, so you may see a red "X" in the status column indicating you need to restart for the change to take effect. If the mod has a github repository like this one it will automatically be checked for updates, and if one is available you can click the new version text to jump to the releases page and download it. Close the popup with escape and open it with ctrl+F10.

![Close up](https://github.com/ToyDragon/ROR2ModShared/blob/master/Images/closeup.png?raw=true)

 Use the checkbox in the left column to collapse mods from the same author, so that you can remove clutter and enable/disable all of them at once. Mouse over a mod to remind yourself what it does.

![description](https://github.com/ToyDragon/ROR2ModShared/blob/master/Images/tooltip.png?raw=true)

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
- Distribute updates
- Add chat commands

```C#
public ModDetails modDetails;
public void Awake()
{
    //Initializing a ModDetails object will allow you to assign
    //a short description and github repository to your mod, and allow
    //it to be enabled or disabled without needing to restart the game.
    //Without this specified the DLL containing your plugin will be 
    //moved to a DisabledMods folder that BepInEx doesn't scan. If your
    //mod relies on any other external files being in the same place
    //this may cause issues.
    modDetails = new ModDetails("com.frogtown.chatcheats")
    {
        //This description shows up as a tooltip when hovering over
        //your mod in the mod list, it shoud be very short. A link to
        //your github repository will be included, feel free to put any
        //additional documentation there.
        description = "Adds the /change_char and /give_item chat commands.",
        
        //githubAuthor and githubRepo must match your author and repo names
        //exactly, they are used for github links and release lookup. Right now
        //there is no way to use a different display name, so for example I have
        //to use "ToyDragon" instead of Frog. No real reason, I'm just lazy. Let
        //me know if this is important to you.
        githubAuthor = "ToyDragon",
        githubRepo = "ROR2ModChatCommandCheats",
    };
    FrogtownShared.RegisterMod(modDetails);
    
    //When all mods are disabled the isModded flag will be updated
    //to false, and when any are enabled it will set it back to true.
    
    //Add chat commands
    FrogtownShared.AddChatCommand("give_item", OnGiveCommand);
    //You command will be called when any user types "/give_item x y z" in the chat.
    //Come on guys the console is really gross, who likes that thing. Just use chat
    //commands.    
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

## Releases
The manager will check your github repos releases page periodically (at most every 24 hours), and display bright blue text if there is a newer version the user doesn't have installed. The user can click the link to be brought to your releases page, so include installation instructions there.

## ThunderStore Integration
Right now the manager links to your github page, and there is no option to redirect to Thunderstore. Redistributing DLLs like this is pretty mega yikes, and I think it'd be best if we as a community put a big emphasis on the mods being open source to minimize issues. Thunderstore is great for discoverability, but I think it's reasonable to use github releases for release management after the mods are installed. Let me know what you think.
