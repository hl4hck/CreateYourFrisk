﻿/// <summary>
/// Class for ingame items. Used to create TestDog# items. But now...
/// </summary>
public class UnderItem { 
    //private static int dogNumber = 1;

    public UnderItem(string Name, int type = 0) {
        //Let's end this dog tyranny!
        //ID = "DOGTEST" + dogNumber;
        //ShortName = "TestDog" + dogNumber;
        //dogNumber++;
        foreach (string str in Inventory.addedItems) {
            if (str.ToLower() == Name.ToLower()) {
                this.Name = Name;
                ShortName = Name;
                this.Type = type;
                return;
            }
        }

        if (Inventory.NametoDesc.Keys.Count == 0) {
            Inventory.luaInventory = new LuaInventory();
            Inventory.AddItemsToDictionaries();
        }

        this.Name = Name; string Sn = "", Desc = ""; int Ty = type;
        if (!Inventory.NametoDesc.TryGetValue(Name, out Desc))     UnitaleUtil.DisplayLuaError("Creating an item", "The item \"" + Name + "\" that is currently created doesn't have a description.");
        if (!Inventory.NametoShortName.TryGetValue(Name, out Sn))  Sn = Name;
        if (type == 0)                                             Inventory.NametoType.TryGetValue(Name, out Ty);

        ShortName = Sn; Description = Desc; Type = Ty;
    }

    public string Name { get; private set; }
    public string ShortName { get; private set; }
    public string Description { get; private set; }
    public int Type { get; private set; } //0 = normal, 1 = equipATK, 2 = equipDEF, 3 = special

    public void inOverworldUse() {}

    public void inCombatUse() {}
}