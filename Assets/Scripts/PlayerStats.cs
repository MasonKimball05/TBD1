using UnityEngine;

/// <summary>
/// Everything about a fighter that carries over between rounds and gets upgraded by cards.
/// A new match starts by replacing this with a fresh instance, so the defaults below ARE the
/// starting values.
/// </summary>
[System.Serializable]
public class PlayerStats
{
    [Header("Core")]
    public float damage = 1f;
    public float maxHp = 5f;
    public float speed = 5f;

    [Header("Healing")]
    public float regen = 0f;          // HP per second, always
    public float stillRegen = 0f;     // extra HP per second while standing still
    public float lifesteal = 0f;      // HP gained when one of our hits lands

    [Header("Combat")]
    public float cooldownReduction = 0f;
    public float blockedStun = 1.2f;  // how long WE are stunned when the opponent blocks our attack
    public float range = 0f;          // added to attack hitbox length
    public float slowOnHitSeconds = 0f;
    public float blockDamageBonus = 0f;
    public float selfDamage = 0f;     // HP we lose every time our attack connects
    public int gambleLevel = 0;
    public float invisDuration = 0f;  // seconds invisible after landing a hit
    public float buildupRate = 0f;    // damage multiplier applied every second (0 = off)

    [Header("One-time unlocks")]
    public bool canDash = false;
    public bool canFloat = false;
    public bool hasProjectile = false;
}
