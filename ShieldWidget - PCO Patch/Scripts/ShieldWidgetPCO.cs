using DaggerfallConnect;
using DaggerfallWorkshop;
using DaggerfallWorkshop.Game;
using DaggerfallWorkshop.Game.Formulas;
using DaggerfallWorkshop.Game.Entity;
using DaggerfallWorkshop.Game.Items;
using DaggerfallWorkshop.Game.Utility.ModSupport;
using DaggerfallWorkshop.Game.Utility.ModSupport.ModSettings;
using DaggerfallWorkshop.Game.Utility;
using DaggerfallWorkshop.Utility;
using DaggerfallWorkshop.Game.UserInterfaceWindows;
using DaggerfallWorkshop.Game.MagicAndEffects;
using DaggerfallWorkshop.Game.MagicAndEffects.MagicEffects;
using PhysicalCombatAndArmorOverhaul;
using UnityEngine;
using System;

public class ShieldWidgetPCO : MonoBehaviour
{
    static Mod mod;

    Mod pco;

    [Invoke(StateManager.StateTypes.Start, 0)]

    public static void Init(InitParams initParams)
    {
        mod = initParams.Mod;
        var go = new GameObject(mod.Title);
        go.AddComponent<ShieldWidgetPCO>();
    }

    void Awake()
    {
        pco = ModManager.Instance.GetMod("PhysicalCombatAndArmorOverhaul");


        if (pco == null)
            return;

        ModSettings settings = pco.GetSettings();

        if (settings.GetBool("Modules", "armorHitFormulaRedone"))
        {
            FormulaHelper.RegisterOverride(mod, "CalculateAttackDamage", (Func<DaggerfallEntity, DaggerfallEntity, bool, int, DaggerfallUnityItem, int>)CalculateAttackDamage);
            FormulaHelper.RegisterOverride(mod, "CalculateAdjustmentsToHit", (Func<DaggerfallEntity, DaggerfallEntity, int>)CalculateAdjustmentsToHit);
            FormulaHelper.RegisterOverride(mod, "CalculateWeaponAttackDamage", (Func<DaggerfallEntity, DaggerfallEntity, int, int, DaggerfallUnityItem, int>)CalculateWeaponAttackDamage);
            FormulaHelper.RegisterOverride(mod, "CalculateSuccessfulHit", (Func<DaggerfallEntity, DaggerfallEntity, int, int, bool>)CalculateSuccessfulHit);

            // Overridden Due To FormulaHelper.cs Private Access Modifiers, otherwise would not be included here.
            FormulaHelper.RegisterOverride(mod, "CalculateStruckBodyPart", (Func<int>)CalculateStruckBodyPart);
            FormulaHelper.RegisterOverride(mod, "CalculateBackstabChance", (Func<PlayerEntity, DaggerfallEntity, bool, int>)CalculateBackstabChance);
            FormulaHelper.RegisterOverride(mod, "CalculateBackstabDamage", (Func<int, int, int>)CalculateBackstabDamage);
            //FormulaHelper.RegisterOverride(mod, "GetBonusOrPenaltyByEnemyType", (Func<DaggerfallEntity, EnemyEntity, int>)GetBonusOrPenaltyByEnemyType);
        }

        Mod roleplayRealism = ModManager.Instance.GetMod("RoleplayRealism");

        if (roleplayRealism != null)
        {
            if (roleplayRealism.GetSettings().GetBool("Modules", "advancedArchery"))
            {
                FormulaHelper.RegisterOverride(mod, "AdjustWeaponHitChanceMod", (Func<DaggerfallEntity, DaggerfallEntity, int, int, DaggerfallUnityItem, int>)AdjustWeaponHitChanceMod);
                FormulaHelper.RegisterOverride(mod, "AdjustWeaponAttackDamage", (Func<DaggerfallEntity, DaggerfallEntity, int, int, DaggerfallUnityItem, int>)AdjustWeaponAttackDamage);
            }
        }
    }

    private static int CalculateAttackDamage(DaggerfallEntity attacker, DaggerfallEntity target, bool enemyAnimStateRecord, int weaponAnimTime, DaggerfallUnityItem weapon)
    {
        if (attacker == null || target == null)
            return 0;

        int damageModifiers = 0;
        int damage = 0;
        int chanceToHitMod = 0;
        int backstabChance = 0;
        PlayerEntity player = GameManager.Instance.PlayerEntity;
        short skillID = 0;
        bool unarmedAttack = false;
        bool weaponAttack = false;
        bool bluntWep = false;
        bool specialMonsterWeapon = false;
        bool monsterArmorCheck = false;
        bool critSuccess = false;
        float critDamMulti = 1f;
        int critHitAddi = 0;
        float matReqDamMulti = 1f;

        EnemyEntity AITarget = null;
        AITarget = target as EnemyEntity;

        // Choose whether weapon-wielding enemies use their weapons or weaponless attacks.
        // In classic, weapon-wielding enemies use the damage values of their weapons
        // instead of their weaponless values.
        // For some enemies this gives lower damage than similar-tier monsters
        // and the weaponless values seems more appropriate, so here
        // enemies will choose to use their weaponless attack if it is more damaging.
        EnemyEntity AIAttacker = attacker as EnemyEntity;
        if (AIAttacker != null && weapon != null)
        {
            int weaponAverage = (weapon.GetBaseDamageMin() + weapon.GetBaseDamageMax()) / 2;
            int noWeaponAverage = (AIAttacker.MobileEnemy.MinDamage + AIAttacker.MobileEnemy.MaxDamage) / 2;
            if (noWeaponAverage > weaponAverage)
            {
                // Use hand-to-hand
                weapon = null;
            }
        }

        if (weapon != null)
        {
            if (PhysicalCombatAndArmorOverhaul.PhysicalCombatAndArmorOverhaul.softMatRequireModuleCheck) // Only run if "Soft Material Requirements" module is active.
            {
                // If the attacker is using a weapon, check if the material is high enough to damage the target
                if (target.MinMetalToHit > (WeaponMaterialTypes)weapon.NativeMaterialValue)
                {
                    int targetMatRequire = (int)target.MinMetalToHit;
                    int weaponMatValue = weapon.NativeMaterialValue;
                    matReqDamMulti = targetMatRequire - weaponMatValue;

                    if (matReqDamMulti <= 0) // There is no "bonus" damage for meeting material requirements, nor for exceeding them, just normal unmodded damage.
                        matReqDamMulti = 1;
                    else // There is a damage penalty for attacking a target with below the minimum material requirements of that target, more as the difference between becomes greater.
                        matReqDamMulti = (Mathf.Min(matReqDamMulti * 0.2f, 0.9f) - 1) * -1; // Keeps the damage multiplier penalty from going above 90% reduced damage.

                    if (attacker == player)
                        Debug.LogFormat("1. matReqDamMulti = {0}", matReqDamMulti);
                }
                // Get weapon skill used
                skillID = weapon.GetWeaponSkillIDAsShort();
                if (skillID == 32) // Checks if the weapon being used is in the Blunt Weapon category, then sets a bool value to true.
                    bluntWep = true;
            }
            else
            {
                // If the attacker is using a weapon, check if the material is high enough to damage the target
                if (target.MinMetalToHit > (WeaponMaterialTypes)weapon.NativeMaterialValue)
                {
                    if (attacker == player)
                    {
                        DaggerfallUI.Instance.PopupMessage(TextManager.Instance.GetLocalizedText("materialIneffective"));
                    }
                    return 0;
                }
                // Get weapon skill used
                skillID = weapon.GetWeaponSkillIDAsShort();
                if (skillID == 32) // Checks if the weapon being used is in the Blunt Weapon category, then sets a bool value to true.
                    bluntWep = true;
            }
        }
        else
        {
            skillID = (short)DFCareer.Skills.HandToHand;
        }

        if (attacker == player)
        {
            int playerWeaponSkill = attacker.Skills.GetLiveSkillValue(skillID);
            playerWeaponSkill = (int)Mathf.Ceil(playerWeaponSkill * 1.5f); // Makes it so player weapon skill has 150% of the effect it normally would on hit chance. So now instead of 50 weapon skill adding +50 to the end, 50 will now add +75.
            chanceToHitMod = playerWeaponSkill;
        }
        else
            chanceToHitMod = attacker.Skills.GetLiveSkillValue(skillID);

        if (PhysicalCombatAndArmorOverhaul.PhysicalCombatAndArmorOverhaul.critStrikeModuleCheck) // Applies the 'Critical Strikes Increase Damage' module if it is enabled in the settings.
        {
            if (attacker == player) // Crit modifiers, if true, for the player.
            {
                critSuccess = PhysicalCombatAndArmorOverhaul.PhysicalCombatAndArmorOverhaul.CriticalStrikeHandler(attacker); // Rolls for if the attacker is sucessful with a critical strike, if yes, critSuccess is set to 'true'.

                if (critSuccess)
                {
                    critDamMulti = (attacker.Skills.GetLiveSkillValue(DFCareer.Skills.CriticalStrike) / 5);
                    //Debug.LogFormat("1. critDamMulti From PLAYER Skills = {0}", critDamMulti);
                    critHitAddi = (attacker.Skills.GetLiveSkillValue(DFCareer.Skills.CriticalStrike) / 4);
                    //Debug.LogFormat("2. critHitAddi From PLAYER Skills = {0}", critHitAddi);

                    critDamMulti = (critDamMulti * .05f) + 1;
                    //Debug.LogFormat("3. Final critDamMulti From PLAYER Skills = {0}", critDamMulti);

                    chanceToHitMod += critHitAddi; // Adds the critical success value to the 'chanceToHitMod'.
                }
            }
            else // Crit modifiers, if true, for monsters/enemies.
            {
                critSuccess = PhysicalCombatAndArmorOverhaul.PhysicalCombatAndArmorOverhaul.CriticalStrikeHandler(attacker); // Rolls for if the attacker is sucessful with a critical strike, if yes, critSuccess is set to 'true'.

                if (critSuccess)
                {
                    critDamMulti = (attacker.Skills.GetLiveSkillValue(DFCareer.Skills.CriticalStrike) / 5);
                    //Debug.LogFormat("1. critDamMulti From MONSTER Skills = {0}", critDamMulti);
                    critHitAddi = (attacker.Skills.GetLiveSkillValue(DFCareer.Skills.CriticalStrike) / 10);
                    //Debug.LogFormat("2. critHitAddi From MONSTER Skills = {0}", critHitAddi);

                    critDamMulti = (critDamMulti * .025f) + 1;
                    //Debug.LogFormat("3. Final critDamMulti From MONSTER Skills = {0}", critDamMulti);

                    chanceToHitMod += critHitAddi; // Adds the critical success value to the 'chanceToHitMod'.
                }
            }
        }

        if (attacker == player)
        {
            // Apply swing modifiers
            PhysicalCombatAndArmorOverhaul.PhysicalCombatAndArmorOverhaul.ToHitAndDamageMods swingMods = CalculateSwingModifiers(GameManager.Instance.WeaponManager.ScreenWeapon);
            damageModifiers += swingMods.damageMod;
            chanceToHitMod += swingMods.toHitMod;

            // Apply proficiency modifiers
            PhysicalCombatAndArmorOverhaul.PhysicalCombatAndArmorOverhaul.ToHitAndDamageMods proficiencyMods = PhysicalCombatAndArmorOverhaul.PhysicalCombatAndArmorOverhaul.CalculateProficiencyModifiers(attacker, weapon);
            damageModifiers += proficiencyMods.damageMod;
            chanceToHitMod += proficiencyMods.toHitMod;

            // Apply racial bonuses
            PhysicalCombatAndArmorOverhaul.PhysicalCombatAndArmorOverhaul.ToHitAndDamageMods racialMods = PhysicalCombatAndArmorOverhaul.PhysicalCombatAndArmorOverhaul.CalculateRacialModifiers(attacker, weapon, player);
            damageModifiers += racialMods.damageMod;
            chanceToHitMod += racialMods.toHitMod;

            backstabChance = CalculateBackstabChance(player, null, enemyAnimStateRecord);
            chanceToHitMod += backstabChance;
        }

        // Choose struck body part
        int struckBodyPart = CalculateStruckBodyPart();

        // Get damage for weaponless attacks
        if (skillID == (short)DFCareer.Skills.HandToHand)
        {
            unarmedAttack = true; // Check for later if weapon is NOT being used.

            if (attacker == player || (AIAttacker != null && AIAttacker.EntityType == EntityTypes.EnemyClass))
            {
                if (CalculateSuccessfulHit(attacker, target, chanceToHitMod, struckBodyPart))
                {
                    damage = PhysicalCombatAndArmorOverhaul.PhysicalCombatAndArmorOverhaul.CalculateHandToHandAttackDamage(attacker, target, damageModifiers, attacker == player); // Added my own, non-overriden version of this method for modification.

                    damage = CalculateBackstabDamage(damage, backstabChance);
                }
            }
            else if (AIAttacker != null) // attacker is a monster
            {
                specialMonsterWeapon = PhysicalCombatAndArmorOverhaul.PhysicalCombatAndArmorOverhaul.SpecialWeaponCheckForMonsters(attacker);

                if (specialMonsterWeapon)
                {
                    unarmedAttack = false;
                    weaponAttack = true;
                    weapon = PhysicalCombatAndArmorOverhaul.PhysicalCombatAndArmorOverhaul.MonsterWeaponAssign(attacker);
                    skillID = weapon.GetWeaponSkillIDAsShort();
                    if (skillID == 32) // Checks if the weapon being used is in the Blunt Weapon category, then sets a bool value to true.
                        bluntWep = true;
                }

                // Handle multiple attacks by AI
                int minBaseDamage = 0;
                int maxBaseDamage = 0;
                int attackNumber = 0;
                while (attackNumber < 3) // Classic supports up to 5 attacks but no monster has more than 3
                {
                    if (attackNumber == 0)
                    {
                        minBaseDamage = AIAttacker.MobileEnemy.MinDamage;
                        maxBaseDamage = AIAttacker.MobileEnemy.MaxDamage;
                    }
                    else if (attackNumber == 1)
                    {
                        minBaseDamage = AIAttacker.MobileEnemy.MinDamage2;
                        maxBaseDamage = AIAttacker.MobileEnemy.MaxDamage2;
                    }
                    else if (attackNumber == 2)
                    {
                        minBaseDamage = AIAttacker.MobileEnemy.MinDamage3;
                        maxBaseDamage = AIAttacker.MobileEnemy.MaxDamage3;
                    }

                    int reflexesChance = 50 - (10 * ((int)player.Reflexes - 2));

                    if (DFRandom.rand() % 100 < reflexesChance && minBaseDamage > 0 && CalculateSuccessfulHit(attacker, target, chanceToHitMod, struckBodyPart))
                    {
                        int hitDamage = UnityEngine.Random.Range(minBaseDamage, maxBaseDamage + 1);
                        // Apply special monster attack effects
                        if (hitDamage > 0)
                            FormulaHelper.OnMonsterHit(AIAttacker, target, hitDamage);

                        damage += hitDamage;
                    }
                    ++attackNumber;
                }
                if (damage >= 1)
                    damage = PhysicalCombatAndArmorOverhaul.PhysicalCombatAndArmorOverhaul.CalculateHandToHandAttackDamage(attacker, target, damage, attacker == player); // Added my own, non-overriden version of this method for modification.
            }
        }
        // Handle weapon attacks
        else if (weapon != null)
        {
            weaponAttack = true; // Check for later on if weapon is being used.

            // Apply weapon material modifier.
            chanceToHitMod += PhysicalCombatAndArmorOverhaul.PhysicalCombatAndArmorOverhaul.CalculateWeaponToHit(weapon);

            // Mod hook for adjusting final hit chance mod. (is a no-op in DFU)
            if (PhysicalCombatAndArmorOverhaul.PhysicalCombatAndArmorOverhaul.archeryModuleCheck)
                chanceToHitMod = AdjustWeaponHitChanceMod(attacker, target, chanceToHitMod, weaponAnimTime, weapon);

            if (CalculateSuccessfulHit(attacker, target, chanceToHitMod, struckBodyPart))
            {
                damage = CalculateWeaponAttackDamage(attacker, target, damageModifiers, weaponAnimTime, weapon);

                damage = CalculateBackstabDamage(damage, backstabChance);
            }

            // Handle poisoned weapons
            if (damage > 0 && weapon.poisonType != Poisons.None)
            {
                FormulaHelper.InflictPoison(attacker, target, weapon.poisonType, false);
                weapon.poisonType = Poisons.None;
            }
        }

        damage = Mathf.Max(0, damage); // I think this is just here to keep damage from outputting a negative value.

        //Debug.LogFormat("4. Here is damage value before crit modifier is applied = {0}", damage);

        if (critSuccess) // Since the critSuccess variable only ever becomes true inside when the module is active, this is always false when that module is disabled.
        {
            damage = (int)Mathf.Round(damage * critDamMulti); // Multiplies 'Final' damage values, before reductions, with the critical damage multiplier.
                                                              //Debug.LogFormat("5. Here is damage value AFTER crit modifier is applied = {0}", damage);
        }

        //if (attacker == player)
        //Debug.LogFormat("2. Here is damage value BEFORE soft material requirement modifier is applied = {0}", damage);

        float damCheckBeforeMatMod = damage;

        damage = (int)Mathf.Round(damage * matReqDamMulti); // Could not find much better place to put there, so here seems fine, right after crit multiplier is taken into account.

        //if (attacker == player)
        //Debug.LogFormat("3. Here is damage value AFTER soft material requirement modifier is applied = {0}", damage);

        float damCheckAfterMatMod = damage;

        if (PhysicalCombatAndArmorOverhaul.PhysicalCombatAndArmorOverhaul.softMatRequireModuleCheck)
        {
            if (attacker == player)
            {
                if (damCheckBeforeMatMod > 0 && (damCheckAfterMatMod / damCheckBeforeMatMod) <= 0.45f)
                    DaggerfallUI.AddHUDText("This Weapon Is Not Very Effective Against This Creature.", 1.00f);
            }
        }

        int targetEndur = target.Stats.LiveEndurance - 50;
        int targetStren = target.Stats.LiveStrength - 50; // Every point of these does something, positive and negative between 50.
        int targetWillp = target.Stats.LiveWillpower - 50;

        float naturalDamResist = (targetEndur * .002f);
        naturalDamResist += (targetStren * .001f);
        naturalDamResist += (targetWillp * .001f);

        Mathf.Clamp(naturalDamResist, -0.2f, 0.2f); // This is to keep other mods that allow over 100 attribute points from allowing damage reduction values to go over 20%. May actually remove this cap for monsters, possibly, since some of the higher level ones have over 100 attribute points.
                                                    //Debug.LogFormat("Natural Damage Resist = {0}", naturalDamResist);

        DaggerfallUnityItem shield = target.ItemEquipTable.GetItem(EquipSlots.LeftHand); // Checks if character is using a shield or not.
        bool shieldStrongSpot = false;
        PhysicalCombatAndArmorOverhaul.PhysicalCombatAndArmorOverhaul.shieldBlockSuccess = false;
        if (shield != null)
        {
            BodyParts[] protectedBodyParts = shield.GetShieldProtectedBodyParts();

            for (int i = 0; (i < protectedBodyParts.Length) && !shieldStrongSpot; i++)
            {
                if (protectedBodyParts[i] == (BodyParts)struckBodyPart)
                    shieldStrongSpot = true;
            }
            PhysicalCombatAndArmorOverhaul.PhysicalCombatAndArmorOverhaul.shieldBlockSuccess = ShieldBlockChanceCalculation(target, shieldStrongSpot, shield);

            if (PhysicalCombatAndArmorOverhaul.PhysicalCombatAndArmorOverhaul.shieldBlockSuccess)
                PhysicalCombatAndArmorOverhaul.PhysicalCombatAndArmorOverhaul.shieldBlockSuccess = PhysicalCombatAndArmorOverhaul.PhysicalCombatAndArmorOverhaul.CompareShieldToUnderArmor(target, struckBodyPart, naturalDamResist);
        }

        if (PhysicalCombatAndArmorOverhaul.PhysicalCombatAndArmorOverhaul.condBasedEffectModuleCheck) // Only runs if "Condition Based Effectiveness" module is active. As well if a weapon is even being used.
        {
            if (attacker == player && weapon != null) // Only the player has weapon damage effected by condition value.
            {
                damage = PhysicalCombatAndArmorOverhaul.PhysicalCombatAndArmorOverhaul.AlterDamageBasedOnWepCondition(damage, bluntWep, weapon);
                //Debug.LogFormat("Damage Multiplier Due To Weapon Condition = {0}", damage);
            }
        }

        if (damage < 1) // Cut off the execution if the damage is still not anything higher than 1 at this point in the method.
        {
            if (target == player && !GameManager.Instance.WeaponManager.ScreenWeapon.IsAttacking())
            {
                DaggerfallUnityItem shieldPlayer = player.ItemEquipTable.GetItem(EquipSlots.LeftHand);
                if (shieldPlayer != null)
                {
                    if (shieldPlayer.IsShield)
                    {
                        if (ShieldWidget.Instance.recoilCondition > 2)                           //recoil shield on unshielded bits
                        {
                            ShieldWidget.Instance.HitShield(damage);
                        }
                        else                                                        //recoil shield only on shielded bits
                        {
                            bool shielded = ShieldWidget.Instance.IsPartShielded(shieldPlayer, struckBodyPart);
                            if (shielded)
                                ShieldWidget.Instance.HitShield(damage);
                        }
                    }
                }
            }
            return damage;
        }

        DamageEquipment(attacker, target, damage, weapon, struckBodyPart); // Might alter this later so that equipment damage is only calculated with the amount that was reduced, not the whole initial amount, will see.

        if (((target != player) && (AITarget.EntityType == EntityTypes.EnemyMonster)))
        {
            monsterArmorCheck = PhysicalCombatAndArmorOverhaul.PhysicalCombatAndArmorOverhaul.ArmorStruckVerification(target, struckBodyPart); // Check for if a monster has a piece of armor/shield hit by an attack, returns true if so.

            if (!monsterArmorCheck)
            {
                //Debug.Log("------------------------------------------------------------------------------------------");
                //Debug.LogFormat("Here is damage value before Monster 'Natural' Damage reduction is applied = {0}", damage);

                damage = PhysicalCombatAndArmorOverhaul.PhysicalCombatAndArmorOverhaul.PercentageReductionCalculationForMonsters(attacker, target, damage, bluntWep, naturalDamResist);

                //Debug.LogFormat("Here is damage value after Monster 'Natural' Damage reduction = {0}", damage);
                //Debug.Log("------------------------------------------------------------------------------------------");
            }
            else
            {
                if (unarmedAttack)
                {
                    //Debug.Log("------------------------------------------------------------------------------------------");
                    //Debug.LogFormat("Here is damage value before armor reduction is applied = {0}", damage);

                    damage = PhysicalCombatAndArmorOverhaul.PhysicalCombatAndArmorOverhaul.CalculateArmorDamageReductionWithUnarmed(attacker, target, damage, struckBodyPart, naturalDamResist); // This will be the method call for armor reduction against unarmed.

                    //Debug.LogFormat("Here is damage value after armor reduction = {0}", damage);
                    //Debug.Log("------------------------------------------------------------------------------------------");
                }
                else if (weaponAttack)
                {
                    //Debug.Log("------------------------------------------------------------------------------------------");
                    //Debug.LogFormat("Here is damage value before armor reduction is applied = {0}", damage);

                    damage = PhysicalCombatAndArmorOverhaul.PhysicalCombatAndArmorOverhaul.CalculateArmorDamageReductionWithWeapon(attacker, target, damage, weapon, struckBodyPart, naturalDamResist); // This will be the method call for armor reduction against weapons.

                    //Debug.LogFormat("Here is damage value after armor reduction = {0}", damage);
                    //Debug.Log("------------------------------------------------------------------------------------------");
                }
            }
        }
        else
        {
            if (unarmedAttack)
            {
                //Debug.Log("------------------------------------------------------------------------------------------");
                //Debug.LogFormat("Here is damage value before armor reduction is applied = {0}", damage);
                int damBefore = damage;

                damage = PhysicalCombatAndArmorOverhaul.PhysicalCombatAndArmorOverhaul.CalculateArmorDamageReductionWithUnarmed(attacker, target, damage, struckBodyPart, naturalDamResist); // This will be the method call for armor reduction against unarmed.

                int damAfter = damage;
                //Debug.LogFormat("Here is damage value after armor reduction = {0}", damage);
                if (damBefore > 0)
                {
                    int damReduPercent = ((100 * damAfter / damBefore) - 100) * -1;
                    //Debug.LogFormat("Here is damage reduction percent = {0}%", damReduPercent);
                }
                //Debug.Log("------------------------------------------------------------------------------------------");
            }
            else if (weaponAttack)
            {
                //Debug.Log("------------------------------------------------------------------------------------------");
                //Debug.LogFormat("Here is damage value before armor reduction is applied = {0}", damage);
                int damBefore = damage;

                damage = PhysicalCombatAndArmorOverhaul.PhysicalCombatAndArmorOverhaul.CalculateArmorDamageReductionWithWeapon(attacker, target, damage, weapon, struckBodyPart, naturalDamResist); // This will be the method call for armor reduction against weapons.

                int damAfter = damage;
                //Debug.LogFormat("Here is damage value after armor reduction = {0}", damage);
                if (damBefore > 0)
                {
                    int damReduPercent = ((100 * damAfter / damBefore) - 100) * -1;
                    //Debug.LogFormat("Here is damage reduction percent = {0}%", damReduPercent);
                }
                //Debug.Log("------------------------------------------------------------------------------------------");
            }
        }

        // Apply Ring of Namira effect
        if (target == player)
        {
            DaggerfallUnityItem[] equippedItems = target.ItemEquipTable.EquipTable;
            DaggerfallUnityItem item = null;
            if (equippedItems.Length != 0)
            {
                if (IsRingOfNamira(equippedItems[(int)EquipSlots.Ring0]) || IsRingOfNamira(equippedItems[(int)EquipSlots.Ring1]))
                {
                    IEntityEffect effectTemplate = GameManager.Instance.EntityEffectBroker.GetEffectTemplate(RingOfNamiraEffect.EffectKey);
                    effectTemplate.EnchantmentPayloadCallback(EnchantmentPayloadFlags.None,
                        targetEntity: AIAttacker.EntityBehaviour,
                        sourceItem: item,
                        sourceDamage: damage);
                }
            }
        }

        //Debug.LogFormat("Damage {0} applied, animTime={1}  ({2})", damage, weaponAnimTime, GameManager.Instance.WeaponManager.ScreenWeapon.WeaponState);

        if (target == player && !GameManager.Instance.WeaponManager.ScreenWeapon.IsAttacking())
        {
            DaggerfallUnityItem shieldPlayer = player.ItemEquipTable.GetItem(EquipSlots.LeftHand);
            if (shieldPlayer != null)
            {
                if (shieldPlayer.IsShield)
                {
                    if (ShieldWidget.Instance.recoilCondition > 2)                           //recoil shield on unshielded bits
                    {
                        ShieldWidget.Instance.HitShield(damage);
                    }
                    else                                                        //recoil shield only on shielded bits
                    {
                        bool shielded = ShieldWidget.Instance.IsPartShielded(shieldPlayer, struckBodyPart);
                        if (shielded)
                            ShieldWidget.Instance.HitShield(damage);
                    }
                }
            }
        }

        return damage;
    }
    private static PhysicalCombatAndArmorOverhaul.PhysicalCombatAndArmorOverhaul.ToHitAndDamageMods CalculateSwingModifiers(FPSWeapon onscreenWeapon) // Make this a setting option obviously. Possibly modify this swing mod formula, so that is works in a similar way to Morrowind, where the attack direction "types" are not universal like here, but each weapon type has a different amount of attacks that are "better" or worse depending. Like a dagger having better damage with a thrusting attacking, rather than slashing, and the same for other weapon types. Possibly as well, make the different attack types depending on the weapon, have some degree of "resistance penetration" or something, like thrusting doing increased damage resistance penatration.
    {
        PhysicalCombatAndArmorOverhaul.PhysicalCombatAndArmorOverhaul.ToHitAndDamageMods mods = new PhysicalCombatAndArmorOverhaul.PhysicalCombatAndArmorOverhaul.ToHitAndDamageMods();
        if (onscreenWeapon != null)
        {
            // The Daggerfall manual groups diagonal slashes to the left and right as if they are the same, but they are different.
            // Classic does not apply swing modifiers to unarmed attacks.
            if (onscreenWeapon.WeaponState == WeaponStates.StrikeUp)
            {
                mods.damageMod = -4;
                mods.toHitMod = 10;
            }
            if (onscreenWeapon.WeaponState == WeaponStates.StrikeDownRight)
            {
                mods.damageMod = -2;
                mods.toHitMod = 5;
            }
            if (onscreenWeapon.WeaponState == WeaponStates.StrikeDownLeft)
            {
                mods.damageMod = 2;
                mods.toHitMod = -5;
            }
            if (onscreenWeapon.WeaponState == WeaponStates.StrikeDown)
            {
                mods.damageMod = 4;
                mods.toHitMod = -10;
            }
        }
        return mods;
    }
    private static int CalculateBackstabChance(PlayerEntity player, DaggerfallEntity target, bool isEnemyFacingAwayFromPlayer)
    {
        // If enemy is facing away from player
        if (isEnemyFacingAwayFromPlayer)
        {
            player.TallySkill(DFCareer.Skills.Backstabbing, 1);
            return player.Skills.GetLiveSkillValue(DFCareer.Skills.Backstabbing);
        }
        return 0;
    }
    private static int CalculateStruckBodyPart()
    {
        //int[] bodyParts = { 0, 0, 1, 1, 1, 2, 2, 2, 3, 3, 3, 3, 4, 4, 4, 4, 5, 5, 5, 6 }; // Default Values.
        int[] bodyParts = { 0, 1, 1, 1, 2, 2, 2, 3, 3, 3, 3, 4, 4, 4, 5, 5, 5, 5, 6, 6 }; // Changed slightly. Head Now 5%, Left-Right Arms 15%, Chest 20%, Hands 15%, Legs 20%, Feet 10%. Plan on doing more with this, making it so when different parts of the body take damage, do different things, like extra damage, or less damage, but attribute drain until health restored or something.
        return bodyParts[UnityEngine.Random.Range(0, bodyParts.Length)];
    }

    /// Calculates whether an attack on a target is successful or not.
    private static bool CalculateSuccessfulHit(DaggerfallEntity attacker, DaggerfallEntity target, int chanceToHitMod, int struckBodyPart)
    {
        PlayerEntity player = GameManager.Instance.PlayerEntity;

        if (attacker == null || target == null)
            return false;

        int chanceToHit = chanceToHitMod;
        //Debug.LogFormat("Starting chanceToHitMod = {0}", chanceToHit);

        // Get armor value for struck body part
        chanceToHit += PhysicalCombatAndArmorOverhaul.PhysicalCombatAndArmorOverhaul.CalculateArmorToHit(target, struckBodyPart);

        // Apply adrenaline rush modifiers.
        chanceToHit += PhysicalCombatAndArmorOverhaul.PhysicalCombatAndArmorOverhaul.CalculateAdrenalineRushToHit(attacker, target);

        // Apply enchantment modifier. 
        chanceToHit += attacker.ChanceToHitModifier;
        //Debug.LogFormat("Attacker Chance To Hit Mod 'Enchantment' = {0}", attacker.ChanceToHitModifier); // No idea what this does, always seeing 0.

        // Apply stat differential modifiers. (default: luck and agility)
        chanceToHit += PhysicalCombatAndArmorOverhaul.PhysicalCombatAndArmorOverhaul.CalculateStatDiffsToHit(attacker, target);

        // Apply skill modifiers. (default: dodge and crit strike)
        chanceToHit += PhysicalCombatAndArmorOverhaul.PhysicalCombatAndArmorOverhaul.CalculateSkillsToHit(attacker, target);
        //Debug.LogFormat("After Dodge = {0}", chanceToHitMod);

        // Apply monster modifier and biography adjustments.
        chanceToHit += CalculateAdjustmentsToHit(attacker, target);
        //Debug.LogFormat("Final chanceToHitMod = {0}", chanceToHitMod);

        Mathf.Clamp(chanceToHit, 3, 97);

        return Dice100.SuccessRoll(chanceToHit);
    }

    private static int CalculateBackstabDamage(int damage, int backstabbingLevel)
    {
        if (backstabbingLevel > 1 && Dice100.SuccessRoll(backstabbingLevel))
        {
            damage *= 3;
            string backstabMessage = TextManager.Instance.GetLocalizedText("successfulBackstab");
            DaggerfallUI.Instance.PopupMessage(backstabMessage);
        }
        return damage;
    }

    private static int AdjustWeaponHitChanceMod(DaggerfallEntity attacker, DaggerfallEntity target, int hitChanceMod, int weaponAnimTime, DaggerfallUnityItem weapon)
    {
        if (weaponAnimTime > 0 && (weapon.TemplateIndex == (int)Weapons.Short_Bow || weapon.TemplateIndex == (int)Weapons.Long_Bow))
        {
            int adjustedHitChanceMod = hitChanceMod;
            if (weaponAnimTime < 200)
                adjustedHitChanceMod -= 40;
            else if (weaponAnimTime < 500)
                adjustedHitChanceMod -= 10;
            else if (weaponAnimTime < 1000)
                adjustedHitChanceMod = hitChanceMod;
            else if (weaponAnimTime < 2000)
                adjustedHitChanceMod += 10;
            else if (weaponAnimTime > 5000)
                adjustedHitChanceMod -= 10;
            else if (weaponAnimTime > 8000)
                adjustedHitChanceMod -= 20;

            //Debug.LogFormat("Adjusted Weapon HitChanceMod for bow drawing from {0} to {1} (t={2}ms)", hitChanceMod, adjustedHitChanceMod, weaponAnimTime);
            return adjustedHitChanceMod;
        }

        return hitChanceMod;
    }

    private static int CalculateWeaponAttackDamage(DaggerfallEntity attacker, DaggerfallEntity target, int damageModifier, int weaponAnimTime, DaggerfallUnityItem weapon)
    {
        int damage = UnityEngine.Random.Range(weapon.GetBaseDamageMin(), weapon.GetBaseDamageMax() + 1) + damageModifier;

        EnemyEntity AITarget = null;
        if (target != GameManager.Instance.PlayerEntity)
        {
            AITarget = target as EnemyEntity;
            if (AITarget.CareerIndex == (int)MonsterCareers.SkeletalWarrior)
            {
                // Apply edged-weapon damage modifier for Skeletal Warrior
                if ((weapon.flags & 0x10) == 0)
                    damage /= 2;

                // Apply silver weapon damage modifier for Skeletal Warrior
                // Arena applies a silver weapon damage bonus for undead enemies, which is probably where this comes from.
                if (weapon.NativeMaterialValue == (int)WeaponMaterialTypes.Silver)
                    damage *= 2;
            }
            // Has most of the "obvious" enemies take extra damage from silver weapons, most of the lower level undead, as well as werebeasts.
            else if (AITarget.CareerIndex == (int)MonsterCareers.Werewolf || AITarget.CareerIndex == (int)MonsterCareers.Ghost || AITarget.CareerIndex == (int)MonsterCareers.Wraith || AITarget.CareerIndex == (int)MonsterCareers.Vampire || AITarget.CareerIndex == (int)MonsterCareers.Mummy || AITarget.CareerIndex == (int)MonsterCareers.Wereboar)
            {
                if (weapon.NativeMaterialValue == (int)WeaponMaterialTypes.Silver)
                    damage *= 2;
            }
        }
        // TODO: Apply strength bonus from Mace of Molag Bal

        // Apply strength modifier
        if (ItemEquipTable.GetItemHands(weapon) == ItemHands.Both && weapon.TemplateIndex != (int)Weapons.Short_Bow && weapon.TemplateIndex != (int)Weapons.Long_Bow)
            damage += (PhysicalCombatAndArmorOverhaul.PhysicalCombatAndArmorOverhaul.DamageModifier(attacker.Stats.LiveStrength)) * 2; // Multiplying by 2, so that two-handed weapons gets double the damage mod from Strength, except bows.
        else
            damage += PhysicalCombatAndArmorOverhaul.PhysicalCombatAndArmorOverhaul.DamageModifier(attacker.Stats.LiveStrength);

        // Apply material modifier.
        // The in-game display in Daggerfall of weapon damages with material modifiers is incorrect. The material modifier is half of what the display suggests.
        damage += weapon.GetWeaponMaterialModifier();
        if (damage < 1)
            damage = 0;

        if (damage >= 1)
            damage += GetBonusOrPenaltyByEnemyType(attacker, target); // Added my own, non-overriden version of this method for modification.

        // Mod hook for adjusting final damage. (is a no-op in DFU)
        if (PhysicalCombatAndArmorOverhaul.PhysicalCombatAndArmorOverhaul.archeryModuleCheck)
            damage = AdjustWeaponAttackDamage(attacker, target, damage, weaponAnimTime, weapon);

        return damage;
    }

    // Checks for if a shield block was successful and returns true if so, false if not.
    public static bool ShieldBlockChanceCalculation(DaggerfallEntity target, bool shieldStrongSpot, DaggerfallUnityItem shield)
    {
        float hardBlockChance = 0f;
        float softBlockChance = 0f;
        int targetAgili = target.Stats.LiveAgility - 50;
        int targetSpeed = target.Stats.LiveSpeed - 50;
        int targetStren = target.Stats.LiveStrength - 50;
        int targetEndur = target.Stats.LiveEndurance - 50;
        int targetWillp = target.Stats.LiveWillpower - 50;
        int targetLuck = target.Stats.LiveLuck - 50;

        switch (shield.TemplateIndex)
        {
            case (int)Armor.Buckler:
                hardBlockChance = 30f;
                softBlockChance = 20f;
                break;
            case (int)Armor.Round_Shield:
                hardBlockChance = 35f;
                softBlockChance = 10f;
                break;
            case (int)Armor.Kite_Shield:
                hardBlockChance = 45f;
                softBlockChance = 5f;
                break;
            case (int)Armor.Tower_Shield:
                hardBlockChance = 55f;
                softBlockChance = -5f;
                break;
            default:
                hardBlockChance = 40f;
                softBlockChance = 0f;
                break;
        }

        if (shieldStrongSpot)
        {
            hardBlockChance += (targetAgili * .3f);
            hardBlockChance += (targetSpeed * .3f);
            hardBlockChance += (targetStren * .3f);
            hardBlockChance += (targetEndur * .2f);
            hardBlockChance += (targetWillp * .1f);
            hardBlockChance += (targetLuck * .1f);

            Mathf.Clamp(hardBlockChance, 7f, 95f);
            int blockChanceInt = (int)Mathf.Round(hardBlockChance);

            if (Dice100.SuccessRoll(blockChanceInt))
            {
                //Debug.LogFormat("$$$. Shield Blocked A Hard-Point, Chance Was {0}%", blockChanceInt);
                return true;
            }
            else
            {
                //Debug.LogFormat("!!!. Shield FAILED To Block A Hard-Point, Chance Was {0}%", blockChanceInt);
                return false;
            }
        }
        else
        {
            softBlockChance += (targetAgili * .3f);
            softBlockChance += (targetSpeed * .2f);
            softBlockChance += (targetStren * .2f);
            softBlockChance += (targetEndur * .1f);
            softBlockChance += (targetWillp * .1f);
            softBlockChance += (targetLuck * .1f);

            Mathf.Clamp(softBlockChance, 0f, 50f);
            int blockChanceInt = (int)Mathf.Round(softBlockChance);

            if (Dice100.SuccessRoll(blockChanceInt))
            {
                //Debug.LogFormat("$$$. Shield Blocked A Soft-Point, Chance Was {0}%", blockChanceInt);
                return true;
            }
            else
            {
                //Debug.LogFormat("!!!. Shield FAILED To Block A Soft-Point, Chance Was {0}%", blockChanceInt);
                return false;
            }
        }
    }

    /// Allocate any equipment damage from a strike, and reduce item condition.
    private static bool DamageEquipment(DaggerfallEntity attacker, DaggerfallEntity target, int damage, DaggerfallUnityItem weapon, int struckBodyPart)
    {
        int atkStrength = attacker.Stats.LiveStrength;
        int tarMatMod = 0;
        int matDifference = 0;
        bool bluntWep = false;
        bool shtbladeWep = false;
        bool missileWep = false;
        int wepEqualize = 1;
        int wepWeight = 1;
        float wepDamResist = 1f;
        float armorDamResist = 1f;
        int startItemCondPer = 0;

        if (!PhysicalCombatAndArmorOverhaul.PhysicalCombatAndArmorOverhaul.armorHitFormulaModuleCheck) // Uses the regular shield formula if the "armorHitFormula" Module is disabled in settings, but the equipment damage module is still active.
        {
            DaggerfallUnityItem shield = target.ItemEquipTable.GetItem(EquipSlots.LeftHand); // Checks if character is using a shield or not.
            PhysicalCombatAndArmorOverhaul.PhysicalCombatAndArmorOverhaul.shieldBlockSuccess = false;
            if (shield != null)
            {
                BodyParts[] protectedBodyParts = shield.GetShieldProtectedBodyParts();

                for (int i = 0; (i < protectedBodyParts.Length) && !PhysicalCombatAndArmorOverhaul.PhysicalCombatAndArmorOverhaul.shieldBlockSuccess; i++)
                {
                    if (protectedBodyParts[i] == (BodyParts)struckBodyPart)
                        PhysicalCombatAndArmorOverhaul.PhysicalCombatAndArmorOverhaul.shieldBlockSuccess = true;
                }
            }
        }

        // If damage was done by a weapon, damage the weapon and armor of the hit body part.
        if (weapon != null && damage > 0)
        {
            int atkMatMod = weapon.GetWeaponMaterialModifier() + 2;
            int wepDam = damage;
            wepEqualize = PhysicalCombatAndArmorOverhaul.PhysicalCombatAndArmorOverhaul.EqualizeMaterialConditions(weapon);
            wepDam *= wepEqualize;

            if (weapon.GetWeaponSkillIDAsShort() == 32) // Checks if the weapon being used is in the Blunt Weapon category, then sets a bool value to true.
            {
                wepDam += (atkStrength / 10);
                wepDamResist = (wepEqualize * .20f) + 1;
                wepDam = (int)Mathf.Ceil(wepDam / wepDamResist);
                bluntWep = true;
                wepWeight = (int)Mathf.Ceil(weapon.EffectiveUnitWeightInKg());

                startItemCondPer = weapon.ConditionPercentage;
                PhysicalCombatAndArmorOverhaul.PhysicalCombatAndArmorOverhaul.ApplyConditionDamageThroughWeaponDamage(weapon, attacker, wepDam, bluntWep, shtbladeWep, missileWep, wepEqualize); // Does condition damage to the attackers weapon.
            }
            else if (weapon.GetWeaponSkillIDAsShort() == 28) // Checks if the weapon being used is in the Short Blade category, then sets a bool value to true.
            {
                if (weapon.TemplateIndex == (int)Weapons.Dagger || weapon.TemplateIndex == (int)Weapons.Tanto)
                {
                    wepDam += (atkStrength / 30);
                    wepDamResist = (wepEqualize * .90f) + 1;
                    wepDam = (int)Mathf.Ceil(wepDam / wepDamResist);
                    shtbladeWep = true;
                }
                else
                {
                    wepDam += (atkStrength / 30);
                    wepDamResist = (wepEqualize * .30f) + 1;
                    wepDam = (int)Mathf.Ceil(wepDam / wepDamResist);
                    shtbladeWep = true;
                }

                startItemCondPer = weapon.ConditionPercentage;
                PhysicalCombatAndArmorOverhaul.PhysicalCombatAndArmorOverhaul.ApplyConditionDamageThroughWeaponDamage(weapon, attacker, wepDam, bluntWep, shtbladeWep, missileWep, wepEqualize); // Does condition damage to the attackers weapon.
            }
            else if (weapon.GetWeaponSkillIDAsShort() == 33) // Checks if the weapon being used is in the Missile Weapon category, then sets a bool value to true.
            {
                missileWep = true;

                startItemCondPer = weapon.ConditionPercentage;
                PhysicalCombatAndArmorOverhaul.PhysicalCombatAndArmorOverhaul.ApplyConditionDamageThroughWeaponDamage(weapon, attacker, wepDam, bluntWep, shtbladeWep, missileWep, wepEqualize); // Does condition damage to the attackers weapon.
            }
            else // If all other weapons categories have not been found, it defaults to this, which currently includes long blades and axes.
            {
                wepDam += (atkStrength / 10);
                wepDamResist = (wepEqualize * .20f) + 1;
                wepDam = (int)Mathf.Ceil(wepDam / wepDamResist);

                startItemCondPer = weapon.ConditionPercentage;
                PhysicalCombatAndArmorOverhaul.PhysicalCombatAndArmorOverhaul.ApplyConditionDamageThroughWeaponDamage(weapon, attacker, wepDam, bluntWep, shtbladeWep, missileWep, wepEqualize); // Does condition damage to the attackers weapon.
            }

            if (attacker == GameManager.Instance.PlayerEntity)
                PhysicalCombatAndArmorOverhaul.PhysicalCombatAndArmorOverhaul.WarningMessagePlayerEquipmentCondition(weapon, startItemCondPer);

            if (PhysicalCombatAndArmorOverhaul.PhysicalCombatAndArmorOverhaul.shieldBlockSuccess)
            {
                DaggerfallUnityItem shield = target.ItemEquipTable.GetItem(EquipSlots.LeftHand);
                int shieldEqualize = PhysicalCombatAndArmorOverhaul.PhysicalCombatAndArmorOverhaul.EqualizeMaterialConditions(shield);
                damage *= shieldEqualize;
                tarMatMod = PhysicalCombatAndArmorOverhaul.PhysicalCombatAndArmorOverhaul.ArmorMaterialModifierFinder(shield);
                matDifference = tarMatMod - atkMatMod;
                damage = PhysicalCombatAndArmorOverhaul.PhysicalCombatAndArmorOverhaul.MaterialDifferenceDamageCalculation(shield, matDifference, atkStrength, damage, bluntWep, wepWeight, PhysicalCombatAndArmorOverhaul.PhysicalCombatAndArmorOverhaul.shieldBlockSuccess);

                startItemCondPer = shield.ConditionPercentage;
                PhysicalCombatAndArmorOverhaul.PhysicalCombatAndArmorOverhaul.ApplyConditionDamageThroughWeaponDamage(shield, target, damage, bluntWep, shtbladeWep, missileWep, wepEqualize);

                if (target == GameManager.Instance.PlayerEntity)
                    PhysicalCombatAndArmorOverhaul.PhysicalCombatAndArmorOverhaul.WarningMessagePlayerEquipmentCondition(shield, startItemCondPer);
            }
            else
            {
                EquipSlots hitSlot = DaggerfallUnityItem.GetEquipSlotForBodyPart((BodyParts)struckBodyPart);
                DaggerfallUnityItem armor = target.ItemEquipTable.GetItem(hitSlot);
                if (armor != null)
                {
                    int armorEqualize = PhysicalCombatAndArmorOverhaul.PhysicalCombatAndArmorOverhaul.EqualizeMaterialConditions(armor);
                    damage *= armorEqualize;
                    tarMatMod = PhysicalCombatAndArmorOverhaul.PhysicalCombatAndArmorOverhaul.ArmorMaterialModifierFinder(armor);
                    matDifference = tarMatMod - atkMatMod;
                    damage = PhysicalCombatAndArmorOverhaul.PhysicalCombatAndArmorOverhaul.MaterialDifferenceDamageCalculation(armor, matDifference, atkStrength, damage, bluntWep, wepWeight, PhysicalCombatAndArmorOverhaul.PhysicalCombatAndArmorOverhaul.shieldBlockSuccess);

                    startItemCondPer = armor.ConditionPercentage;
                    PhysicalCombatAndArmorOverhaul.PhysicalCombatAndArmorOverhaul.ApplyConditionDamageThroughWeaponDamage(armor, target, damage, bluntWep, shtbladeWep, missileWep, wepEqualize);

                    if (target == GameManager.Instance.PlayerEntity)
                        PhysicalCombatAndArmorOverhaul.PhysicalCombatAndArmorOverhaul.WarningMessagePlayerEquipmentCondition(armor, startItemCondPer);
                }
            }
            return false;
        }
        else if (weapon == null && damage > 0) // Handles Unarmed attacks.
        {
            if (PhysicalCombatAndArmorOverhaul.PhysicalCombatAndArmorOverhaul.shieldBlockSuccess)
            {
                DaggerfallUnityItem shield = target.ItemEquipTable.GetItem(EquipSlots.LeftHand);
                int shieldEqualize = PhysicalCombatAndArmorOverhaul.PhysicalCombatAndArmorOverhaul.EqualizeMaterialConditions(shield);
                damage *= shieldEqualize;
                tarMatMod = PhysicalCombatAndArmorOverhaul.PhysicalCombatAndArmorOverhaul.ArmorMaterialModifierFinder(shield);
                atkStrength /= 5;
                armorDamResist = (tarMatMod * .40f) + 1;
                damage = (int)Mathf.Ceil((damage + atkStrength) / armorDamResist);

                startItemCondPer = shield.ConditionPercentage;
                PhysicalCombatAndArmorOverhaul.PhysicalCombatAndArmorOverhaul.ApplyConditionDamageThroughUnarmedDamage(shield, target, damage);

                if (target == GameManager.Instance.PlayerEntity)
                    PhysicalCombatAndArmorOverhaul.PhysicalCombatAndArmorOverhaul.WarningMessagePlayerEquipmentCondition(shield, startItemCondPer);
            }
            else
            {
                EquipSlots hitSlot = DaggerfallUnityItem.GetEquipSlotForBodyPart((BodyParts)struckBodyPart);
                DaggerfallUnityItem armor = target.ItemEquipTable.GetItem(hitSlot);
                if (armor != null)
                {
                    int armorEqualize = PhysicalCombatAndArmorOverhaul.PhysicalCombatAndArmorOverhaul.EqualizeMaterialConditions(armor);
                    damage *= armorEqualize;
                    tarMatMod = PhysicalCombatAndArmorOverhaul.PhysicalCombatAndArmorOverhaul.ArmorMaterialModifierFinder(armor);
                    atkStrength /= 5;
                    armorDamResist = (tarMatMod * .20f) + 1;
                    damage = (int)Mathf.Ceil((damage + atkStrength) / armorDamResist);

                    startItemCondPer = armor.ConditionPercentage;
                    PhysicalCombatAndArmorOverhaul.PhysicalCombatAndArmorOverhaul.ApplyConditionDamageThroughUnarmedDamage(armor, target, damage);

                    if (target == GameManager.Instance.PlayerEntity)
                        PhysicalCombatAndArmorOverhaul.PhysicalCombatAndArmorOverhaul.WarningMessagePlayerEquipmentCondition(armor, startItemCondPer);
                }
            }
            return false;
        }
        return false;
    }
    private static bool IsRingOfNamira(DaggerfallUnityItem item)
    {
        return item != null && item.ContainsEnchantment(DaggerfallConnect.FallExe.EnchantmentTypes.SpecialArtifactEffect, (int)ArtifactsSubTypes.Ring_of_Namira);
    }

    private static int CalculateAdjustmentsToHit(DaggerfallEntity attacker, DaggerfallEntity target)
    {
        PlayerEntity player = GameManager.Instance.PlayerEntity;
        EnemyEntity AITarget = target as EnemyEntity;

        int chanceToHitMod = 0;

        // Apply hit mod from character biography. This gives -5 to player chances to not be hit if they say they have trouble "Fighting and Parrying"
        if (target == player)
        {
            chanceToHitMod -= player.BiographyAvoidHitMod;
        }

        // Apply monster modifier.
        if ((target != player) && (AITarget.EntityType == EntityTypes.EnemyMonster))
        {
            chanceToHitMod += 50; // Changed from 40 to 50, +10, in since i'm going to make dodging have double the effect, as well as nerf weapon material hit mod more.
        }

        // DF Chronicles says -60 is applied at the end, but it actually seems to be -50.
        chanceToHitMod -= 50;

        return chanceToHitMod;
    }

    private static int AdjustWeaponAttackDamage(DaggerfallEntity attacker, DaggerfallEntity target, int damage, int weaponAnimTime, DaggerfallUnityItem weapon)
    {
        if (weaponAnimTime > 0 && (weapon.TemplateIndex == (int)Weapons.Short_Bow || weapon.TemplateIndex == (int)Weapons.Long_Bow))
        {
            double adjustedDamage = damage;
            if (weaponAnimTime < 800)
                adjustedDamage *= (double)weaponAnimTime / 800;
            else if (weaponAnimTime < 5000)
                adjustedDamage = damage;
            else if (weaponAnimTime < 6000)
                adjustedDamage *= 0.85;
            else if (weaponAnimTime < 8000)
                adjustedDamage *= 0.75;
            else if (weaponAnimTime < 9000)
                adjustedDamage *= 0.5;
            else if (weaponAnimTime >= 9000)
                adjustedDamage *= 0.25;

            //Debug.LogFormat("Adjusted Weapon Damage for bow drawing from {0} to {1} (t={2}ms)", damage, (int)adjustedDamage, weaponAnimTime);
            return (int)adjustedDamage;
        }

        return damage;
    }

    static int GetBonusOrPenaltyByEnemyType(DaggerfallEntity attacker, DaggerfallEntity target) // Possibly update at some point like 10.26 did so vampirism of the player is taken into account.
    {
        if (attacker == null || target == null) // So after observing the effects of adding large amounts of weight to an enemy, it does not seem to have that much of an effect on their ability to be stun-locked. As the knock-back/hurt state is probably the real issue here, as well as other parts of the AI choices. So I think this comes down a lot more to AI behavior than creature weight values. So with that, I will mostly likely make an entirely seperate mod to try and deal with this issue and continue on non-AI related stuff in this already large mod. So yeah, start another "proof of concept" mod project where I attempt to change the AI to make it more challenging/smarter.
            return 0;

        int attackerWillpMod = 0;
        int confidenceMod = 0;
        int courageMod = 0;
        EnemyEntity AITarget = null;
        PlayerEntity player = GameManager.Instance.PlayerEntity;

        if (target != player)
            AITarget = target as EnemyEntity;
        else
            player = target as PlayerEntity;

        if (player == attacker) // When attacker is the player
        {
            attackerWillpMod = (int)Mathf.Round((attacker.Stats.LiveWillpower - 50) / 5);
            confidenceMod = Mathf.Max(10 + attackerWillpMod - (target.Level / 2), 0);
            courageMod = Mathf.Max((target.Level / 2) - attackerWillpMod, 0);

            confidenceMod = UnityEngine.Random.Range(0, confidenceMod);
        }
        else // When attacker is anything other than the player // Apparently "32" is the maximum possible level cap for the player without cheating.
        {
            attackerWillpMod = (int)Mathf.Round((attacker.Stats.LiveWillpower - 50) / 5);
            confidenceMod = Mathf.Max(5 + attackerWillpMod + (attacker.Level / 4), 0);
            courageMod = Mathf.Max(target.Level - (attacker.Level + attackerWillpMod), 0);

            confidenceMod = UnityEngine.Random.Range(0, confidenceMod);
        }

        int damage = 0;
        // Apply bonus or penalty by opponent type.
        // In classic this is broken and only works if the attack is done with a weapon that has the maximum number of enchantments.
        if (AITarget != null && AITarget.GetEnemyGroup() == DFCareer.EnemyGroups.Undead)
        {
            if (((int)attacker.Career.UndeadAttackModifier & (int)DFCareer.AttackModifier.Bonus) != 0)
            {
                damage += confidenceMod;
            }
            if (((int)attacker.Career.UndeadAttackModifier & (int)DFCareer.AttackModifier.Phobia) != 0)
            {
                damage -= courageMod;
            }
        }
        else if (AITarget != null && AITarget.GetEnemyGroup() == DFCareer.EnemyGroups.Daedra)
        {
            if (((int)attacker.Career.DaedraAttackModifier & (int)DFCareer.AttackModifier.Bonus) != 0)
            {
                damage += confidenceMod;
            }
            if (((int)attacker.Career.DaedraAttackModifier & (int)DFCareer.AttackModifier.Phobia) != 0)
            {
                damage -= courageMod;
            }
        }
        else if ((AITarget != null && AITarget.GetEnemyGroup() == DFCareer.EnemyGroups.Humanoid) || player == target) // Apparently human npcs already are in the humanoid career, so "|| AITarget.EntityType == EntityTypes.EnemyClass" is unneeded.
        {
            if (((int)attacker.Career.HumanoidAttackModifier & (int)DFCareer.AttackModifier.Bonus) != 0)
            {
                damage += confidenceMod;
            }
            if (((int)attacker.Career.HumanoidAttackModifier & (int)DFCareer.AttackModifier.Phobia) != 0)
            {
                damage -= courageMod;
            }
        }
        else if (AITarget != null && AITarget.GetEnemyGroup() == DFCareer.EnemyGroups.Animals)
        {
            if (((int)attacker.Career.AnimalsAttackModifier & (int)DFCareer.AttackModifier.Bonus) != 0)
            {
                damage += confidenceMod;
            }
            if (((int)attacker.Career.AnimalsAttackModifier & (int)DFCareer.AttackModifier.Phobia) != 0)
            {
                damage -= courageMod;
            }
        }
        return damage;
    }
}
