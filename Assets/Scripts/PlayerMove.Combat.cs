using UnityEngine;

/// <summary>Attacks, blocks, and HP changes.</summary>
public partial class PlayerMove
{
    private enum Height { High, Low }

    private const float AttackWindup = 5f / 12f;    // delay before the hit is checked
    private const float AttackRecovery = 5f / 6f;
    private const float BlockRecovery = 2f / 3f;
    private const float BlockWindow = 2f / 3f;      // block pressed while the attack is already coming
    private const float LateBlockDelay = 0.25f;     // otherwise, check again this much later...
    private const float LateBlockWindow = 0.95f;    // ...and hold the block this long if it catches
    private const float BuildupHitDivisor = 10f;

    private const float HighAttackBaseLength = 0.4336568f;
    private const float HighAttackThickness = 0.04112294f;
    private const float LowAttackBaseLength = 0.9560172f;
    private const float LowAttackThickness = 0.3665857f;

    private bool highAttackPending, lowAttackPending;
    private float highAttackTimer, lowAttackTimer;

    private bool highBlocking, lowBlocking;
    private bool blockActive;
    private float blockTimer;

    private bool highLateCheck, lowLateCheck;
    private float highLateTimer, lowLateTimer;

    private void ResetCombatState()
    {
        highAttackPending = lowAttackPending = false;
        highAttackTimer = lowAttackTimer = 0f;
        highBlocking = lowBlocking = false;
        blockActive = false;
        blockTimer = 0f;
        highLateCheck = lowLateCheck = false;
        highLateTimer = lowLateTimer = 0f;
    }

    // ===================================================================================
    // Public API (names unchanged)
    // ===================================================================================

    public void AttackHigh() => StartAttack(Height.High);

    public void AttackLow()
    {
        connected = true;   // low attack doubles as the "player has joined" button
        StartAttack(Height.Low);
    }

    public void BlockHigh() => StartBlock(Height.High);
    public void BlockLow() => StartBlock(Height.Low);

    public bool IsHAttacking() => highAttackPending;
    public bool IsLAttacking() => lowAttackPending;
    public bool IsHBlocking() => highBlocking;
    public bool IsLBlocking() => lowBlocking;

    public void GotHit(float amount)
    {
        hp -= amount;
        UpdateHealthBar();
    }

    /// <summary>Called on the blocker when they parry, if they have Block Buffs Damage.</summary>
    public void DamageBlock()
    {
        damage += stats.blockDamageBonus;
        roundBaseDamage = damage;
    }

    /// <summary>Opponent's Block Stun card makes our blocked attacks stun us longer.</summary>
    public void StunTime(float extraSeconds) => stats.blockedStun += extraSeconds;

    // ===================================================================================
    // Attacking
    // ===================================================================================

    private void StartAttack(Height height)
    {
        if (!gameStarted)
            return;

        animator.SetTrigger(height == Height.High ? "attackhigh" : "attacklow");

        float windup = AttackWindup - stats.cooldownReduction / 2f;
        if (height == Height.High)
        {
            highAttackPending = true;
            highAttackTimer = windup;
        }
        else
        {
            lowAttackPending = true;
            lowAttackTimer = windup;
        }

        EnterBusy(AttackRecovery - stats.cooldownReduction);
    }

    private void UpdateAttacks(float dt)
    {
        highAttackTimer -= dt;
        if (highAttackPending && highAttackTimer <= 0f)
        {
            highAttackPending = false;
            highAttackTimer = 0f;
            ResolveAttack(Height.High);
        }

        lowAttackTimer -= dt;
        if (lowAttackPending && lowAttackTimer <= 0f)
        {
            lowAttackPending = false;
            lowAttackTimer = 0f;
            ResolveAttack(Height.Low);
        }
    }

    private void ResolveAttack(Height height)
    {
        // NOTE: the original checked the high-attack hitbox (child 0) for BOTH attack heights.
        // Kept as-is; see the notes that came with this rewrite.
        bool landed = playerIndex == 0 ? attackHit.P1Hit() : attackHit.P2Hit();

        if (landed)
        {
            GotHit(stats.selfDamage);

            bool blocked = height == Height.High ? opponent.IsHBlocking() : opponent.IsLBlocking();
            if (blocked)
            {
                actionCooldown = stats.blockedStun;
                opponent.DamageBlock();
                parrySound.Play();
            }
            else
            {
                LandHit(height);
            }
        }

        // Damage Buildup is spent on every swing, hit or miss.
        if (stats.buildupRate > 0f)
            damage = roundBaseDamage;
    }

    private void LandHit(Height height)
    {
        (height == Height.High ? punchSound : kickSound).Play();

        float hitDamage = damage;
        if (stats.buildupRate > 0f)
            hitDamage /= BuildupHitDivisor;
        if (stats.invisDuration > 0f)
            GoInvisible();
        if (stats.gambleLevel > 0)
            hitDamage = RollGamble(hitDamage);

        opponent.GotHit(hitDamage);

        if (stats.lifesteal > 0f)
            Heal(stats.lifesteal);
        if (stats.slowOnHitSeconds > 0f)
            opponent.SlowMovement(stats.slowOnHitSeconds);
    }

    /// <summary>20% chance to multiply damage, otherwise divide it, by 1 to (5 * level - 1).</summary>
    private float RollGamble(float baseDamage)
    {
        int factor = Random.Range(1, 5 * stats.gambleLevel);
        bool jackpot = Random.Range(1, 11) < 3;
        return jackpot ? baseDamage * factor : baseDamage / factor;
    }

    // ===================================================================================
    // Blocking
    // ===================================================================================

    private void StartBlock(Height height)
    {
        if (!gameStarted)
            return;

        if (BlockCatches(height))
        {
            RaiseBlock(height, BlockWindow);
        }
        else if (height == Height.High)
        {
            highLateCheck = true;
            highLateTimer = LateBlockDelay;
        }
        else
        {
            lowLateCheck = true;
            lowLateTimer = LateBlockDelay;
        }

        animator.SetTrigger(height == Height.High ? "blockhigh" : "blocklow");
        EnterBusy(BlockRecovery - stats.cooldownReduction);
    }

    /// <summary>True if the opponent is inside our block zone and mid-swing at that height.</summary>
    private bool BlockCatches(Height height)
    {
        return InBlockZone(height) && OpponentSwinging(height);
    }

    private bool InBlockZone(Height height)
    {
        if (height == Height.High)
            return playerIndex == 0 ? highBlockZone.P1Block() : highBlockZone.P2Block();
        return playerIndex == 0 ? lowBlockZone.P1Block() : lowBlockZone.P2Block();
    }

    private bool OpponentSwinging(Height height)
    {
        return height == Height.High ? opponent.IsHAttacking() : opponent.IsLAttacking();
    }

    private void RaiseBlock(Height height, float duration)
    {
        if (height == Height.High)
            highBlocking = true;
        else
            lowBlocking = true;
        blockTimer = duration;
        blockActive = true;
    }

    private void UpdateBlocks(float dt)
    {
        blockTimer -= dt;
        if (blockActive && blockTimer <= 0f)
        {
            blockActive = false;
            blockTimer = 0f;
            highBlocking = false;
            lowBlocking = false;
        }

        highLateTimer -= dt;
        if (highLateCheck && highLateTimer <= 0f)
        {
            highLateCheck = false;
            highLateTimer = 0f;
            if (BlockCatches(Height.High))
                RaiseBlock(Height.High, LateBlockWindow);
        }

        lowLateTimer -= dt;
        if (lowLateCheck && lowLateTimer <= 0f)
        {
            lowLateCheck = false;
            lowLateTimer = 0f;
            if (BlockCatches(Height.Low))
                RaiseBlock(Height.Low, LateBlockWindow);
        }
    }

    // ===================================================================================
    // Health
    // ===================================================================================

    private void Heal(float amount)
    {
        hp = Mathf.Min(hp + amount, stats.maxHp);
        UpdateHealthBar();
    }

    private void UpdateHealthBar() => HealthBar.value = hp;
}
