using System;
using System.Collections.Generic;
using UnityEngine;

namespace Lanternlight
{
    /// <summary>Blessings the player can pick at dawn.</summary>
    public enum UpgradeType
    {
        WideBeam,
        DeepTank,
        HotterFlame,
        SpareWick,
        SecondFlare,
        SteadyWick,
    }

    public class UpgradeInfo
    {
        public UpgradeType Type;
        public string Title;
        public string Line1;
        public string Line2;
        public int MaxStacks;

        /// <summary>The dawn blessings. Descriptions are split in two lines that fit the choice cards.</summary>
        public static readonly UpgradeInfo[] All =
        {
            new UpgradeInfo { Type = UpgradeType.WideBeam,    Title = "WIDE BEAM",    Line1 = "BEAM IS 25%",    Line2 = "WIDER",          MaxStacks = 2 },
            new UpgradeInfo { Type = UpgradeType.DeepTank,    Title = "DEEP TANK",    Line1 = "+30 OIL",        Line2 = "CAPACITY",       MaxStacks = 2 },
            new UpgradeInfo { Type = UpgradeType.HotterFlame, Title = "HOTTER FLAME", Line1 = "BURN SHADOWS",   Line2 = "35% FASTER",     MaxStacks = 2 },
            new UpgradeInfo { Type = UpgradeType.SpareWick,   Title = "SPARE WICK",   Line1 = "+1 MAX FLAME",   Line2 = "AND RELIGHT IT", MaxStacks = 2 },
            new UpgradeInfo { Type = UpgradeType.SecondFlare, Title = "SECOND FLARE", Line1 = "+1 FLARE",       Line2 = "EVERY NIGHT",    MaxStacks = 1 },
            new UpgradeInfo { Type = UpgradeType.SteadyWick,  Title = "STEADY WICK",  Line1 = "BEAM USES 25%",  Line2 = "LESS OIL",       MaxStacks = 2 },
        };
    }

    /// <summary>
    /// Everything that carries from one night to the next. A copy is saved at the
    /// start of each night so "retry night" restores exactly where the player was.
    /// </summary>
    public class RunState
    {
        public int NightIndex;
        public int Score;
        public int Flames = Tuning.StartingFlames;
        public int MaxFlames = Tuning.StartingFlames;
        int[] stacks = new int[Enum.GetValues(typeof(UpgradeType)).Length];

        public int Stacks(UpgradeType type) => stacks[(int)type];

        // Stats derived from upgrades.
        public float BeamWidthMultiplier => 1f + 0.25f * Stacks(UpgradeType.WideBeam);
        public float OilCapacity => Tuning.OilCapacity + 30f * Stacks(UpgradeType.DeepTank);
        public float BurnMultiplier => 1f + 0.35f * Stacks(UpgradeType.HotterFlame);
        public float DrainMultiplier => Mathf.Pow(0.75f, Stacks(UpgradeType.SteadyWick));
        public int FlaresPerNight => 1 + Stacks(UpgradeType.SecondFlare);

        public bool IsLastNight => NightIndex >= NightDefinition.All.Length - 1;

        public void ApplyUpgrade(UpgradeType type)
        {
            stacks[(int)type]++;
            if (type == UpgradeType.SpareWick)
            {
                MaxFlames++;
                Flames++;
            }
        }

        /// <summary>Picks up to <paramref name="count"/> different blessings that aren't maxed out.</summary>
        public List<UpgradeInfo> RollUpgradeChoices(int count)
        {
            var pool = new List<UpgradeInfo>();
            foreach (UpgradeInfo info in UpgradeInfo.All)
            {
                if (Stacks(info.Type) < info.MaxStacks)
                {
                    pool.Add(info);
                }
            }
            var picks = new List<UpgradeInfo>();
            while (picks.Count < count && pool.Count > 0)
            {
                int i = UnityEngine.Random.Range(0, pool.Count);
                picks.Add(pool[i]);
                pool.RemoveAt(i);
            }
            return picks;
        }

        public RunState Clone()
        {
            var copy = (RunState)MemberwiseClone();
            copy.stacks = (int[])stacks.Clone(); // the array must not be shared with the snapshot
            return copy;
        }
    }
}
