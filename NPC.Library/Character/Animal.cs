namespace NPC.Library.Character;

using System;
using System.Collections.Generic;

public enum AnimalType
{
    Sheep,
    Fox
}

public class Animal : Character
{
    public AnimalType AnimalType { get; set; }
    public decimal MeatAmount { get; set; } = 0;
    
    // For tracking decay when dead
    public decimal DecayTimer { get; set; } = 0;
    public bool IsFullyDecayed { get; set; } = false;

    public Animal(string name, AnimalType type) : base(new Drives(new Dictionary<DriveType, decimal>
    {
        { DriveType.Satiety, 1.0m },
        { DriveType.Thirst, 1.0m },
        { DriveType.Fatigue, 0.0m }
    }))
    {
        Name = name;
        AnimalType = type;
        if (type == AnimalType.Sheep)
        {
            // Sheep have 10-20 meat
            MeatAmount = new Random().Next(10, 21);
        }
    }

    public void UpdateDecay(decimal tickSeconds)
    {
        if (IsDead && !IsFullyDecayed)
        {
            DecayTimer += tickSeconds;
            // Let's say a sheep fully rots in 100 seconds
            if (DecayTimer > 100)
            {
                MeatAmount = 0;
                IsFullyDecayed = true;
            }
        }
    }
}
