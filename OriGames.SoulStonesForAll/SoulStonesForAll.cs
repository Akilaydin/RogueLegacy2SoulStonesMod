﻿using BepInEx;

using HarmonyLib;

using Wob_Common;

namespace OriGames.SoulStonesForAll
{
    using System;
    using System.Collections.Generic;
    using System.Reflection.Emit;

    using BepInEx.Configuration;

    using GameEventTracking;

    using UnityEngine;

    [BepInPlugin("OriGames.SoulStonesForAll", "Soul Stones For All Mod", "1.0.0")]
    public partial class SoulStonesForAll : BaseUnityPlugin
    {
        private readonly static Dictionary<EnemyRank, float> RankBonus = new Dictionary<EnemyRank, float>();
        
        private const string SETTINGS_BOSS_SOULS_GAIN = "BossBonus";
        private const string SETTINGS_MINI_BOSS_BONUS = "MiniBossBonus";
        private const string SETTINGS_TIER_1_BONUS = "Tier1Bonus";
        private const string SETTINGS_TIER_2_BONUS = "Tier2Bonus";
        private const string SETTINGS_TIER_3_BONUS = "Tier3Bonus";
        private const string SETTINGS_CHEST_BONUS = "ChestBonus";
        private const string SETTINGS_EVERYTHING_BONUS = "EverythingBonus";
        
        private readonly static HashSet<EnemyType> Bosses = new HashSet<EnemyType> {
            /* Lamech  */ EnemyType.SpellswordBoss,
            /* Pirates */ EnemyType.SkeletonBossA, EnemyType.SkeletonBossB,
            /* Naamah  */ EnemyType.DancingBoss,
            /* Enoch   */ EnemyType.StudyBoss, EnemyType.MimicChestBoss,
            /* Irad    */ EnemyType.EyeballBoss_Left, EnemyType.EyeballBoss_Right, EnemyType.EyeballBoss_Bottom, EnemyType.EyeballBoss_Middle,
            /* Tubal   */ EnemyType.CaveBoss,
            /* Jonah   */ EnemyType.TraitorBoss,
            /* Cain    */ EnemyType.FinalBoss,
        };

        protected void Awake()
        {
            WobPlugin.Initialise(this, Logger);
            
            try
            {
                WobSettings.Add(new WobSettings.Entry[]
                {
                    new WobSettings.Num<int>(SETTINGS_TIER_1_BONUS, "Get this amount of soul stones from tier 1 (basic) variant enemies", 0, 1, bounds: (0, 1000000)),
                    new WobSettings.Num<int>(SETTINGS_TIER_2_BONUS, "Get this amount of soul stones from tier 2 (advanced) variant enemies", 0, 1, bounds: (0, 1000000)),
                    new WobSettings.Num<int>(SETTINGS_TIER_3_BONUS, "Get this amount of soul stones from tier 3 (commander) variant enemies",0, 1, bounds: (0, 1000000)),
                    new WobSettings.Num<int>(SETTINGS_MINI_BOSS_BONUS, "Get this amount of soul stones from mini bosses", 0, 1, bounds: (0, 1000000)),
                    new WobSettings.Num<int>(SETTINGS_BOSS_SOULS_GAIN, "Get this amount of soul stones from bosses", 100, 1, bounds: (0, 1000000)),
                    new WobSettings.Num<int>(SETTINGS_CHEST_BONUS, "Get this amount of soul stones for every opened chest", 0, 1, bounds: (0, 1000000)),
                    new WobSettings.Num<int>(SETTINGS_EVERYTHING_BONUS, "Increases amount of soul stones by this value every time they drop", 0, 1, bounds: (0, 1000000)),
                });
                
                Log($"Setting {WobSettings.Get(SETTINGS_BOSS_SOULS_GAIN, 0f)}");
                
                RankBonus.Add(EnemyRank.Basic, WobSettings.Get(SETTINGS_TIER_1_BONUS, 0f));
                RankBonus.Add(EnemyRank.Advanced, WobSettings.Get(SETTINGS_TIER_2_BONUS, 0f));
                RankBonus.Add(EnemyRank.Expert, WobSettings.Get(SETTINGS_TIER_3_BONUS, 0f));
                RankBonus.Add(EnemyRank.Miniboss, WobSettings.Get(SETTINGS_MINI_BOSS_BONUS, 0f));
            
                WobPlugin.Patch();
                
                Log("WobPlugin patched");

                SetBossesSoulsGain(WobSettings.Get(SETTINGS_BOSS_SOULS_GAIN, 100f));
            
                Log("Mod fully initialised");
            }
            catch (Exception e)
            {
                Log(e.ToString());
            }
        }
        
        [HarmonyPatch(typeof(ItemEventTracker), "OnChestOpened")]
        public static class ItemEventTrackerPatch
        {
            static void Postfix(MonoBehaviour sender, EventArgs eventArgs)
            {
                Log($"On chest opened 1");

                if (eventArgs is ChestOpenedEventArgs args)
                {
                    Log($"On chest opened 2");

                    if (args.SpecialItemType == SpecialItemType.Rune 
                        || args.SpecialItemType == SpecialItemType.Ore 
                        || args.SpecialItemType == SpecialItemType.Gold)
                    {
                        Log($"On chest opened 3");

                        GivePlayerSouls(WobSettings.Get(SETTINGS_CHEST_BONUS, 0));
                    }
                }
            }
        }
        
        private static void SetBossesSoulsGain(float newValue)
        {
            Log($"Set Bosses Souls Gain to {newValue}");

            var keys = new List<BossID>(Souls_EV.BOSS_SOUL_DROP_TABLE.Keys);

            foreach (var bossID in keys)
            {
                Souls_EV.BOSS_SOUL_DROP_TABLE[bossID] = new Vector2Int((int)newValue, (int)newValue);
            }
        }

        [HarmonyPatch(typeof(Economy_EV), "GetItemDropValue")]
        public static class EconomyEV_GetItemDropValue_Patch
        {
            private static void Postfix(ItemDropType itemDrop, bool getGoldValueOnly, ref int __result)
            {
                try
                {
                    if (itemDrop == ItemDropType.Soul && !getGoldValueOnly)
                    {
                        float increment = 0f; //WobSettings.Get(SETTINGS_EVERYTHING_BONUS, 0);

                        __result += (int)increment;
                    
                        Log($"EconomyEV_GetItemDropValue_Patch increment was {increment} and final result was {__result}");
                    }
                }
                catch (Exception e)
                {
                    Log($"Exception in EconomyEV_GetItemDropValue_Patch: " + e.ToString());
                }
            }
        }
        
        [HarmonyPatch(typeof(EnemyController), "KillCharacter")]
        public static class EnemyController_KillCharacter_Patch
        {
            private static void Postfix(EnemyController __instance)
            {
                try
                {
                    Log(__instance.EnemyType + "." + __instance.EnemyRank + " killed. Rank bonus is " + RankBonus.ContainsKey(__instance.EnemyRank));
                
                    if (RankBonus.TryGetValue(__instance.EnemyRank, out float soulStonesForEnemy))
                    {
                        Log($"Enemy rank {__instance.EnemyRank} was found and rank bonus is {soulStonesForEnemy}");

                        GivePlayerSouls(soulStonesForEnemy);
                    }
                }
                catch (Exception e)
                {
                    Log($"Exception in EnemyController_KillCharacter_Patch: " + e.ToString());
                }
                
            }
        }
        
        private static void GivePlayerSouls(float amount)
        {
            try
            {
                Log($"Giving player souls {amount}. FakeSoulCounter_STATIC was {SoulDrop.FakeSoulCounter_STATIC} before assigning. Drop value is {Economy_EV.GetItemDropValue(ItemDropType.Soul)}");
                
                SoulDrop.FakeSoulCounter_STATIC = -(int)amount;
            
                ItemDropManager.DropItem(ItemDropType.Soul, (int)amount, Vector3.down, true, true, true);
            
                Messenger<GameMessenger, GameEvent>.Broadcast(GameEvent.SoulChanged, (MonoBehaviour) null, (EventArgs) null);
            }
            catch (Exception e)
            {
                Log($"Exception in GivePlayerSouls: " + e.ToString());
            }
        }

        private static void Log(string message, bool error = true)
        {
            WobPlugin.Log($"{DateTime.Now}: " + message, error);
        }
    }
}