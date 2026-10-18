using UnityEngine;

/// <summary>Knockouts, round and match wins, and the upgrade draft.</summary>
public partial class PlayerMove
{
    private bool roundOver;
    private AbilityType[] offeredCards = new AbilityType[3];

    // ===================================================================================
    // Round end
    // ===================================================================================

    private void CheckForKnockout()
    {
        if (hp > 0f || roundOver)
            return;
        roundOver = true;

        if (opponent.CheckHP() <= 0f)
        {
            ui.TieGame();
            opponent.DoubleDeath();
            return;
        }

        // The player who lost the round gets to draft an upgrade.
        CardsUI();
        if (opponent.RoundWin())
        {
            if (playerIndex == 0)
                ui.P1Round();
            else
                ui.P2Round();
        }
    }

    /// <summary>Both players hit 0 at once; stops the other player from also reporting the KO.</summary>
    public void DoubleDeath() => roundOver = true;

    /// <summary>Returns true if the match continues, false if this win ended it.</summary>
    public bool RoundWin()
    {
        roundsWon++;
        roundsWonText.text = roundsWon.ToString();
        busy = true;

        if (roundsWon < ui.RoundMax())
            return true;

        if (playerIndex == 0)
            ui.P1Win();
        else
            ui.P2Win();
        return false;
    }

    public void SubtractWin()
    {
        roundsWon--;
        roundsWonText.text = roundsWon.ToString();
    }

    // ===================================================================================
    // Upgrade draft
    // ===================================================================================

    /// <summary>Deals three upgrade cards and shows them on the card UI.</summary>
    public void CardsUI()
    {
        offeredCards = AbilityCatalog.DrawCards(3, stats);

        TMPro.TextMeshProUGUI[] cardLabels = { card1, card2, card3 };
        for (int i = 0; i < cardLabels.Length; i++)
            cardLabels[i].text = i < offeredCards.Length ? AbilityCatalog.CardText(offeredCards[i]) : "";
    }

    /// <summary>Called by the card buttons with 1, 2 or 3. Applies the upgrade and starts the next round.</summary>
    public void AbilitiesChooser(int cardChosen)
    {
        int index = cardChosen - 1;
        if (index < 0 || index >= offeredCards.Length)
        {
            Debug.LogWarning($"AbilitiesChooser: no card in slot {cardChosen}");
            return;
        }

        AbilityType ability = offeredCards[index];
        ApplyAbility(ability);
        Debug.Log($"Player {playerIndex + 1} picked {ability}");

        Start();
        opponent.Start();
    }

    private void ApplyAbility(AbilityType ability)
    {
        switch (ability)
        {
            case AbilityType.Damage:                stats.damage += 0.5f; break;
            case AbilityType.Health:                stats.maxHp += 2f; break;
            case AbilityType.Speed:                 stats.speed *= 1.5f; break;
            case AbilityType.Regen:                 stats.regen += 0.15f; break;
            case AbilityType.StillRegen:            stats.stillRegen += 0.3f; break;
            case AbilityType.FasterCooldown:        stats.cooldownReduction += 0.15f; break;
            case AbilityType.BlockStun:             opponent.StunTime(0.4f); break;
            case AbilityType.Range:                 stats.range += 0.4f; break;
            case AbilityType.SlowingPunches:        stats.slowOnHitSeconds += 3f; break;
            case AbilityType.Dash:                  stats.canDash = true; break;
            case AbilityType.BlockBuffsDamage:      stats.blockDamageBonus += 0.25f; break;
            case AbilityType.Gambling:              stats.gambleLevel++; break;
            case AbilityType.AttackingInvisibility: stats.invisDuration += 0.5f; break;
            case AbilityType.FloatingJump:          stats.canFloat = true; break;
            case AbilityType.Projectile:            stats.hasProjectile = true; break;

            case AbilityType.Vampirism:
                stats.damage -= 0.25f;
                stats.lifesteal = 0.4f;
                break;

            case AbilityType.SelfDamageSuperhuman:
                stats.selfDamage += 0.3f;
                stats.damage += 1f;
                opponent.StunTime(0.6f);
                break;

            case AbilityType.DamageBuildup:
                // First pick starts at 1.2x per second, each extra pick adds 0.2.
                stats.buildupRate = stats.buildupRate > 0f ? stats.buildupRate + 0.2f : 1.2f;
                break;
        }
    }
}
