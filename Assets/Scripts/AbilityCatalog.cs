using System.Collections.Generic;
using UnityEngine;

/// <summary>Upgrade cards. The numeric values match the old dictionary ids (0 to 17).</summary>
public enum AbilityType
{
    Damage,
    Health,
    Speed,
    Regen,
    StillRegen,
    FasterCooldown,
    BlockStun,
    Range,
    SlowingPunches,
    Dash,
    BlockBuffsDamage,
    Vampirism,
    Gambling,
    AttackingInvisibility,
    SelfDamageSuperhuman,
    DamageBuildup,
    FloatingJump,
    Projectile,
}

/// <summary>Card text, availability rules, and drawing a hand of cards.</summary>
public static class AbilityCatalog
{
    private static readonly Dictionary<AbilityType, string> cardText = new Dictionary<AbilityType, string>
    {
        { AbilityType.Damage,                "Damage" },
        { AbilityType.Health,                "Health" },
        { AbilityType.Speed,                 "Speed" },
        { AbilityType.Regen,                 "Regen" },
        { AbilityType.StillRegen,            "Still\nRegen" },
        { AbilityType.FasterCooldown,        "Faster\nCooldown" },
        { AbilityType.BlockStun,             "Block\nStun" },
        { AbilityType.Range,                 "Range" },
        { AbilityType.SlowingPunches,        "Slowing\nPunches" },
        { AbilityType.Dash,                  "Dash" },
        { AbilityType.BlockBuffsDamage,      "Block\nBuffs\nDamage" },
        { AbilityType.Vampirism,             "Vampirism" },
        { AbilityType.Gambling,              "Gambling" },
        { AbilityType.AttackingInvisibility, "Attacking\nInvisibility" },
        { AbilityType.SelfDamageSuperhuman,  "Self\nDamage\nSuperhuman" },
        { AbilityType.DamageBuildup,         "Damage\nBuildup" },
        { AbilityType.FloatingJump,          "Floating\nJump" },
        { AbilityType.Projectile,            "Projectile" },
    };

    public static string CardText(AbilityType ability) => cardText[ability];

    /// <summary>One-time unlocks stop showing up once the player owns them.</summary>
    public static bool IsAvailable(AbilityType ability, PlayerStats stats)
    {
        switch (ability)
        {
            case AbilityType.Dash:         return !stats.canDash;
            case AbilityType.FloatingJump: return !stats.canFloat;
            case AbilityType.Projectile:   return !stats.hasProjectile;
            default:                       return true;
        }
    }

    /// <summary>Returns up to <paramref name="count"/> distinct cards the player can still take.</summary>
    public static AbilityType[] DrawCards(int count, PlayerStats stats)
    {
        var pool = new List<AbilityType>();
        foreach (AbilityType ability in System.Enum.GetValues(typeof(AbilityType)))
        {
            if (IsAvailable(ability, stats))
                pool.Add(ability);
        }

        // Partial Fisher-Yates shuffle: only the first `count` slots need to be random.
        int drawn = Mathf.Min(count, pool.Count);
        for (int i = 0; i < drawn; i++)
        {
            int j = Random.Range(i, pool.Count);
            (pool[i], pool[j]) = (pool[j], pool[i]);
        }
        return pool.GetRange(0, drawn).ToArray();
    }
}
