Allows building floors out of stuff*.

# Important

You can safely add this mod to you save games, but *you can not remove it* from save games. (If you really must do this, you'll have to remove all built stuffed floors added by this mod first).

# Features

This mod does two things. First, using Architect Sense it provides a framework for modders to define their own stuffed floor types. Second, it uses this framework to create a number of new floors that are stuffed.

**For players**
Adds several new floors, and existing floors from Telkir's [More Floors](http://steamcommunity.com/sharedfiles/filedetails/?id=725623521), CuproPanda's [Extra Floors](https://ludeon.com/forums/index.php?topic=13400#msg135940) and Pravus' [Fences and Floors](http://steamcommunity.com/sharedfiles/filedetails/?id=784370602) in all types of stone, metal, wood.

Works great with other mods that add more resources, e.g. [Minerals and Materials](http://steamcommunity.com/sharedfiles/filedetails/?id=728233992) [Kura's Extra Minerals](http://steamcommunity.com/sharedfiles/filedetails/?id=852103845), [Extended Woodworking](http://steamcommunity.com/sharedfiles/filedetails/?id=836912371), [Vegetable Garden](http://steamcommunity.com/sharedfiles/filedetails/?id=822470192) and [GlitterTech](http://steamcommunity.com/sharedfiles/filedetails/?id=725576127).

This mod also organizes and where needed, removes non-stuffed versions of the floors added by RimWorld, [More Floors](http://steamcommunity.com/sharedfiles/filedetails/?id=725623521), [Extended Woodworking](http://steamcommunity.com/sharedfiles/filedetails/?id=836912371), [Minerals and Materials](http://steamcommunity.com/sharedfiles/filedetails/?id=728233992), [GlitterTech](http://steamcommunity.com/sharedfiles/filedetails/?id=725576127), [More Furniture](http://steamcommunity.com/sharedfiles/filedetails/?id=739089840), [Vegetable Garden](http://steamcommunity.com/sharedfiles/filedetails/?id=822470192), and [Floored](http://steamcommunity.com/sharedfiles/filedetails/?id=801544922).

Other mods can easily add more floors using existing materials, or completely new types of materials.

**For modders**
Adds a custom FloorTypeDef that derives from TerrainDef, and allows modders to create floortypes by setting a texture and a list of stuffCategories to generate terrain defs for. Removing now obsolete terrainDefs and/or architect categories is also easily handled. See [GitHub](https://github.com/fluffy-mods/StuffedFloors/wiki/For-Modders) for a guide on using Stuffed Floors in other mods.

# Known issues

While this mod will happily generate more floors for any mods that add materials to the metallic, stony and/or woody stuff types, it only cleans up the floors added by mods that are explicitly supported. Any other mods that add floor types may appear as duplicates. If you encounter such an issue, please let the author(s) of said mod(s) know so that they can correctly set up their designator groups!

# Powered by Harmony

![Powered by Harmony](https://raw.githubusercontent.com/pardeike/Harmony/master/HarmonyLogo.png)
