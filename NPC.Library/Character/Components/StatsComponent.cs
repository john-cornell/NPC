namespace NPC.Library.Character.Components;

using System;

public class StatsComponent
{
    public int Strength { get; set; } = 10;
    public int Dexterity { get; set; } = 10;
    public int Constitution { get; set; } = 10;
    public int Intelligence { get; set; } = 10;
    public int Wisdom { get; set; } = 10;
    public int Charisma { get; set; } = 10;

    public int MaxHP { get; set; } = 20;
    public int CurrentHP { get; set; } = 20;

    public int BaseArmorClass { get; set; } = 10;

    public StatsComponent(int str = 10, int dex = 10, int con = 10, int intel = 10, int wis = 10, int cha = 10, int maxHp = 20)
    {
        Strength = str;
        Dexterity = dex;
        Constitution = con;
        Intelligence = intel;
        Wisdom = wis;
        Charisma = cha;
        MaxHP = maxHp;
        CurrentHP = maxHp;
    }

    public void TakeDamage(int amount)
    {
        CurrentHP -= amount;
        if (CurrentHP < 0) CurrentHP = 0;
    }

    public void Heal(int amount)
    {
        CurrentHP += amount;
        if (CurrentHP > MaxHP) CurrentHP = MaxHP;
    }
}
