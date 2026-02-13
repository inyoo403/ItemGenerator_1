using System;
using System.Collections.Generic;
using UnityEngine;

// Defines item grades used across the system
public enum ItemRarity { Normal, Rare, Epic, Unique, Legendary }

[Serializable]
public class Equipment
{
    public string itemName;
    public ItemRarity rarity;
    public Sprite itemSprite; 
    
    // Base stats influenced by room level and difficulty
    public int str, dex, atk;

    // Bonus options (High Return)
    public float critRate, lifeSteal, cooldownRed, armorPen, goldGain, moveSpeedInc, atkSpeedInc;

    // Penalty options (High Risk)
    public float manaCostInc, healRecDec, armorDec, moveSpeedDec, fireWeakness, atkSpeedDec;

    public Equipment(ItemRarity rarity, int roomLevel, float difficulty, float penaltyScale, bool applyTradeOff)
    {
        this.rarity = rarity;
        this.itemName = $"[{rarity}] Lv.{roomLevel} Gear";
        
        GenerateBaseStats(roomLevel, difficulty);

        // Apply trade-offs only for Epic+ items based on probability roll
        if (applyTradeOff && (int)rarity >= 2)
        {
            GenerateTradeOffs(roomLevel, penaltyScale);
        }
    }

    // Calculates basic power scaling using room level and global difficulty
    private void GenerateBaseStats(int level, float difficulty)
    {
        int tier = UnityEngine.Random.Range((int)rarity + 1, (int)rarity + 4);
        float scaling = (level / 20f + 1f) * tier * difficulty;
        
        str = Mathf.RoundToInt(scaling * 1.5f);
        atk = Mathf.RoundToInt(scaling * 0.8f);
    }

    // Assigns independent bonuses and penalties based on rarity
    private void GenerateTradeOffs(int level, float penaltyScale)
    {
        // Epic: 2/1, Unique: 3/2, Legendary: 4/3 slots
        int bonusCount = (int)rarity;
        int penaltyCount = bonusCount - 1;

        // Scaling factor for additional options based on room level
        float levelFactor = 1f + (level * 0.05f);
        float finalMult = levelFactor * penaltyScale;

        for (int i = 0; i < bonusCount; i++) ApplyRandomBonus(finalMult);
        for (int i = 0; i < penaltyCount; i++) ApplyRandomPenalty(finalMult);
    }

    private void ApplyRandomBonus(float mult)
    {
        int pick = UnityEngine.Random.Range(0, 7);
        switch (pick) {
            case 0: critRate += 5f * mult; break;
            case 1: lifeSteal += 2f * mult; break;
            case 2: cooldownRed += 5f * mult; break;
            case 3: armorPen += 8f * mult; break;
            case 4: goldGain += 15f * mult; break;
            case 5: moveSpeedInc += 10f * mult; break;
            case 6: atkSpeedInc += 10f * mult; break;
        }
    }

    private void ApplyRandomPenalty(float mult)
    {
        int pick = UnityEngine.Random.Range(0, 6);
        switch (pick) {
            case 0: manaCostInc += 10f * mult; break;
            case 1: healRecDec += 15f * mult; break;
            case 2: armorDec += 10f * mult; break;
            case 3: moveSpeedDec += 8f * mult; break;
            case 4: fireWeakness += 20f * mult; break;
            case 5: atkSpeedDec += 8f * mult; break;
        }
    }

    // Returns a summary string for UI and logging
    public string GetStatString()
    {
        string s = $"S:{str} A:{atk} ";
        if (critRate > 0) s += $"Crit:+{critRate:F0}% ";
        if (moveSpeedInc > 0) s += $"MS:+{moveSpeedInc:F0}% ";
        if (moveSpeedDec > 0) s += $"MS:-{moveSpeedDec:F0}% ";
        if (fireWeakness > 0) s += $"FireVuln:+{fireWeakness:F0}% ";
        return s.Trim();
    }
}