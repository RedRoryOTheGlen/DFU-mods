using System;
using System.IO;
using UnityEngine;
using DaggerfallWorkshop;
using DaggerfallWorkshop.Game;
using DaggerfallWorkshop.Game.Serialization;
using DaggerfallWorkshop.Game.Entity;
using DaggerfallWorkshop.Game.Formulas;
using DaggerfallWorkshop.Game.Items;
using DaggerfallWorkshop.Game.Utility.ModSupport;
using DaggerfallWorkshop.Game.Utility.ModSupport.ModSettings;
using DaggerfallWorkshop.Game.MagicAndEffects;
using DaggerfallConnect;
using DaggerfallWorkshop.Game.MagicAndEffects.MagicEffects;

public class ShieldWidget : MonoBehaviour
{
    static Mod mod;

    [Invoke(StateManager.StateTypes.Start, 0)]

    public static void Init(InitParams initParams)
    {
        mod = initParams.Mod;
        var go = new GameObject(mod.Title);
        go.AddComponent<ShieldWidget>();
    }

    public static ShieldWidget Instance;

    DaggerfallAudioSource audioSource;

    const int nativeScreenWidth = 320;
    const int nativeScreenHeight = 200;

    public Texture2D shieldTexture;
    public Texture2D[] shieldTextures;
    Rect shieldPositionCurrent;
    Rect shieldPositionTarget;
    Rect curAnimRect;

    Rect screenRect;
    Rect screenRectLast;

    float weaponScaleX;
    float weaponScaleY;
    float weaponOffsetHeight;
    float weaponOffsetHeightLast;

    //FPSWeaponMovement.GetSettings

    float offsetX = 0.8f;           //distance from the texture's left edge the center of the screen
    float offsetY = 0.6f;           //distance from the texture's bottom edge to the bottom of the screen
    float scale = 1f;
    float stanceSpeed = 100f;
    int whenSheathed;
    int whenCasting;

    bool bob;
    float bobLength = 1.0f;
    float bobSizeXMod = 0.5f;      //RECOMMEND RANGE OF 0.5-1.5
    float bobSizeYMod = 1.5f;      //RECOMMEND RANGE OF 0.5-1.5
    float moveSmoothSpeed = 1;        //RECOMMEND RANGE OF 1-3
    float bobSmoothSpeed = 1;         //RECOMMEND RANGE OF UP TO 1-3
    float bobShape = 0;
    bool bobWhileIdle;

    bool inertia;
    float inertiaScale = 500;
    float inertiaSpeed = 50;
    float inertiaSpeedMod = 1;
    float inertiaForwardSpeed = 1;
    float inertiaForwardScale = 1;

    bool recoil;
    float recoilScale = 1;
    float recoilSpeed = 1;
    public int recoilCondition = 0;        //SHIELD HIT, SHIELD MISS, SHIELD ATTACK, ANY HIT, ANY MISS, ANY ATTACK

    //PERSISTENT VARIABLES FOR THE SMOOTHING
    float moveSmooth = 0;
    Vector2 bobSmooth = Vector2.zero;

    Vector2 inertiaCurrent = Vector2.zero;
    Vector2 inertiaTarget;

    Vector2 inertiaForwardCurrent = Vector2.zero;
    Vector2 inertiaForwardTarget;

    Vector2 recoilCurrent = Vector2.zero;

    public Color Tint { get; set; } = Color.white;
    public Vector2 Offset { get; set; } = Vector2.zero;
    public Vector2 Scale { get; set; } = Vector2.one;

    bool attacked;
    bool sheathed;
    bool spelled;
    int lastIndex = -1;

    bool flipped;

    void Awake()
    {
        mod.LoadSettingsCallback = LoadSettings;
        mod.LoadSettings();

        if (recoil)
            FormulaHelper.RegisterOverride(mod, "CalculateAttackDamage", (Func<DaggerfallEntity, DaggerfallEntity, bool, int, DaggerfallUnityItem, int>)CalculateAttackDamage);

        if (Instance == null)
            Instance = this;

        if (audioSource == null)
            audioSource = GameManager.Instance.WeaponManager.ScreenWeapon.gameObject.GetComponent<DaggerfallAudioSource>();

        if (DaggerfallUI.Instance.CustomScreenRect != null)
            screenRect = DaggerfallUI.Instance.CustomScreenRect.Value;
        else
            screenRect = new Rect(0, 0, Screen.width, Screen.height);
        screenRectLast = screenRect;

        InitializeShieldTextures();

        RefreshShield();
        shieldPositionCurrent = shieldPositionTarget;
        attacked = false;
        sheathed = false;
        spelled = false;

        flipped = GameManager.Instance.WeaponManager.ScreenWeapon.FlipHorizontal;
        if (flipped)
            curAnimRect = new Rect(1, 0, -1, 1);
        else
            curAnimRect = new Rect(0, 0, 1, 1);

        mod.IsReady = true;
    }

    private void LoadSettings(ModSettings settings, ModSettingsChange change)
    {
        if (change.HasChanged("Shield"))
        {
            offsetX = settings.GetValue<float>("Shield", "OffsetHorizontal") * -1f + 1;
            offsetY = settings.GetValue<float>("Shield", "OffsetVertical") * 0.5f + 0.5f;
            scale = settings.GetValue<float>("Shield", "Scale");
            stanceSpeed = settings.GetValue<float>("Shield", "StanceSpeed") * 1000;
            whenSheathed = settings.GetValue<int>("Shield", "WhenSheathed");
            whenCasting = settings.GetValue<int>("Shield", "WhenCasting");
        }
        if (change.HasChanged("Bob"))
        {
            bob = settings.GetValue<bool>("Bob", "EnableBob");
            bobLength = settings.GetValue<float>("Bob", "Length");
            bobSizeXMod = settings.GetValue<float>("Bob", "SizeX");
            bobSizeYMod = settings.GetValue<float>("Bob", "SizeY");
            moveSmoothSpeed = settings.GetValue<float>("Bob", "SpeedMove")*2;
            bobSmoothSpeed = settings.GetValue<float>("Bob", "SpeedState")*250;
            bobShape = settings.GetValue<int>("Bob", "Shape") * 0.5f;
            bobWhileIdle = settings.GetValue<bool>("Bob", "BobWhileIdle");
        }
        if (change.HasChanged("Inertia"))
        {
            inertia = settings.GetValue<bool>("Inertia", "EnableInertia");
            inertiaScale = settings.GetValue<float>("Inertia", "Scale") * 250;
            inertiaSpeed = settings.GetValue<float>("Inertia", "Speed") * 250;
            inertiaForwardScale = settings.GetValue<float>("Inertia", "ForwardDepth");
            inertiaForwardSpeed = settings.GetValue<float>("Inertia", "ForwardSpeed")*0.1f;
        }
        if (change.HasChanged("Recoil"))
        {
            recoil = settings.GetValue<bool>("Recoil", "EnableRecoil");
            recoilScale = settings.GetValue<float>("Recoil", "Scale")*2f;
            recoilSpeed = settings.GetValue<float>("Recoil", "Speed")*0.5f;
            recoilCondition = settings.GetValue<int>("Recoil", "Condition");
        }

        //if off-hand is shield
        DaggerfallUnityItem itemLeftHand = GameManager.Instance.PlayerEntity.ItemEquipTable.GetItem(EquipSlots.LeftHand);
        if (itemLeftHand != null)
            RefreshShield();
    }

    //Fill in the shieldTextures array with all the textures
    void InitializeShieldTextures()
    {
        shieldTextures = new Texture2D[40];
        int archive = 700;
        int record = 0;
        int frame = 0;
        for (int i = 0; i < 40; i++)
        {
            Texture2D texture;
            DaggerfallWorkshop.Utility.AssetInjection.TextureReplacement.TryImportTexture(archive, record, frame, out texture);
            shieldTextures[i] = texture;

            if (frame == 9)
            {
                frame = 0;
                record++;
            } else
                frame++;
        }
        shieldTexture = shieldTextures[0];
    }

    //Updates the sprite to match the currently equipped shield
    //Only run when the shield has changed
    void UpdateShieldTextures(DaggerfallUnityItem shield)
    {
        //shield textures are placed in array alphabetically so we have to work with that
        int shieldType = 0;
        int shieldMaterial = 0;
        switch (shield.TemplateIndex)
        {
            case (int)Armor.Buckler:
                shieldType = 0;
                break;
            case (int)Armor.Round_Shield:
                shieldType = 10;
                break;
            case (int)Armor.Kite_Shield:
                shieldType = 20;
                break;
            case (int)Armor.Tower_Shield:
                shieldType = 30;
                break;
        }
        switch (shield.NativeMaterialValue)
        {
            case (int)ArmorMaterialTypes.Leather:
                shieldMaterial = 0;
                break;
            case (int)ArmorMaterialTypes.Chain:
                shieldMaterial = 0;
                break;
            case (int)ArmorMaterialTypes.Iron:
                shieldMaterial = 1;
                break;
            case (int)ArmorMaterialTypes.Steel:
                shieldMaterial = 2;
                break;
            case (int)ArmorMaterialTypes.Silver:
                shieldMaterial = 0;
                break;
            case (int)ArmorMaterialTypes.Elven:
                shieldMaterial = 3;
                break;
            case (int)ArmorMaterialTypes.Dwarven:
                shieldMaterial = 4;
                break;
            case (int)ArmorMaterialTypes.Mithril:
                shieldMaterial = 5;
                break;
            case (int)ArmorMaterialTypes.Adamantium:
                shieldMaterial = 6;
                break;
            case (int)ArmorMaterialTypes.Ebony:
                shieldMaterial = 7;
                break;
            case (int)ArmorMaterialTypes.Orcish:
                shieldMaterial = 8;
                break;
            case (int)ArmorMaterialTypes.Daedric:
                shieldMaterial = 9;
                break;
        }

        shieldTexture = shieldTextures[shieldType+shieldMaterial];
    }

    private void OnGUI()
    {
        GUI.depth = 0;

        if (shieldTexture == null || GameManager.Instance.PlayerEntity == null)
            return;

        //if off-hand is shield
        DaggerfallUnityItem itemLeftHand = GameManager.Instance.PlayerEntity.ItemEquipTable.GetItem(EquipSlots.LeftHand);
        if (itemLeftHand != null)
        {
            if (!itemLeftHand.IsShield || GameManager.Instance.WeaponManager.EquipCountdownLeftHand > 0 || GameManager.Instance.ClimbingMotor.IsClimbing || GameManager.IsGamePaused || SaveLoadManager.Instance.LoadInProgress)
                return;

            if (whenSheathed == 0 && GameManager.Instance.WeaponManager.Sheathed)
                return;

            if (whenCasting == 0 && (GameManager.Instance.PlayerSpellCasting.IsPlayingAnim || GameManager.Instance.PlayerEffectManager.HasReadySpell))
                return;

            if (DaggerfallUI.Instance.CustomScreenRect != null)
                screenRect = DaggerfallUI.Instance.CustomScreenRect.Value;
            else
                screenRect = new Rect(0, 0, Screen.width, Screen.height);

            weaponOffsetHeight = 0;
            if (DaggerfallUI.Instance.DaggerfallHUD != null &&
                DaggerfallUnity.Settings.LargeHUD &&
                (DaggerfallUnity.Settings.LargeHUDUndockedOffsetWeapon || DaggerfallUnity.Settings.LargeHUDDocked))
            {
                weaponOffsetHeight = (int)DaggerfallUI.Instance.DaggerfallHUD.LargeHUD.ScreenHeight;
            }

            if (screenRect != screenRectLast || weaponOffsetHeight != weaponOffsetHeightLast)
                RefreshShield();

            screenRectLast = screenRect;
            weaponOffsetHeightLast = weaponOffsetHeight;

            //change shield texture when a new shield type or material is equipped
            int index = itemLeftHand.TemplateIndex + itemLeftHand.NativeMaterialValue;
            if (lastIndex != index)
            {
                UpdateShieldTextures(itemLeftHand);

                RefreshShield();
                lastIndex = index;

                attacked = false;
                sheathed = false;
                spelled = false;
            }

            //adjust shield position when sheathed
            if (GameManager.Instance.WeaponManager.Sheathed)
            {
                if (!sheathed)
                {
                    sheathed = true;
                    if (whenSheathed == 1)
                        SetAttack();
                    else
                        SetGuard();
                }
            }
            else
            {
                if (sheathed)
                {
                    sheathed = false;
                    RefreshShield();
                }
            }

            //adjust shield position when spellcasting
            if (GameManager.Instance.PlayerSpellCasting.IsPlayingAnim || GameManager.Instance.PlayerEffectManager.HasReadySpell)
            {
                if (!spelled)
                {
                    spelled = true;
                    if (whenCasting == 1)
                        SetAttack();
                    else
                        SetGuard();
                }
            }
            else
            {
                if (spelled)
                {
                    spelled = false;
                    RefreshShield(); 
                }
            }

            //adjust shield position when attacking
            if (GameManager.Instance.WeaponManager.ScreenWeapon.IsAttacking())
            {
                if (!attacked)
                {
                    attacked = true;
                    SetAttack();
                }
            } else
            {
                if (attacked)
                {
                    attacked = false;
                    RefreshShield();
                }
            }

            Vector3 current = new Vector3(shieldPositionCurrent.x,shieldPositionCurrent.y,0);
            Vector3 target = new Vector3(shieldPositionTarget.x,shieldPositionTarget.y,0);
            //current = Vector3.RotateTowards(current, target, Time.deltaTime,0);
            current = Vector3.MoveTowards(current, target, Time.deltaTime*stanceSpeed);
            shieldPositionCurrent = new Rect(current.x, current.y, shieldPositionTarget.width, shieldPositionTarget.height);

            Offset = Vector2.zero;
            Scale = Vector2.one;

            //start of weapon bob code
            if (bob && shieldPositionCurrent == shieldPositionTarget)
            {
                //SHAPE OF MOVE BOB
                float bobYOffset = bobShape;

                //BOB ONLY WHEN GROUNDED
                float move = 1;
                if (!GameManager.Instance.PlayerMotor.IsGrounded)
                    move = 0;
                moveSmooth = Mathf.MoveTowards(moveSmooth, move, Time.deltaTime * moveSmoothSpeed);

                //SCALE BOB TO SPEED AND MOVEMENT
                float currentSpeed = GameManager.Instance.PlayerMotor.Speed;
                float baseSpeed = GameManager.Instance.SpeedChanger.GetBaseSpeed();
                float bob = currentSpeed / baseSpeed;

                //DAMPEN BOB WHEN CROUCHED
                if (GameManager.Instance.PlayerMotor.IsCrouching)
                    bob *= 0.5f;

                //DAMPEN BOB WHEN RIDING A HORSE OR CART
                if (GameManager.Instance.PlayerMotor.IsRiding)
                    bob *= 0.5f;

                //DAMPEN BOB WHEN NOT MOVING
                if (GameManager.Instance.PlayerMotor.IsStandingStill)
                {
                    if (bobWhileIdle)
                        bob *= 0.2f;
                    else
                        bob = 0;
                }

                //HORIZONTAL BOB
                float bobXSpeed = baseSpeed * 1.25f * bob * bobLength; //SYNC IT WITH THE FOOTSTEP SOUNDS
                float bobYSpeed = bobXSpeed * 2f; //MAKE IT A MULTIPLE OF THE HORIZONTAL BOB SPEED
                Vector2 bobSize = new Vector2(screenRect.width * 0.1f * bob * bobSizeXMod, screenRect.height * 0.05f * bob * bobSizeYMod);

                //GET CURRENT BOB VALUES
                Vector2 bobRaw = new Vector2(Mathf.Sin(0.5f + Time.time * bobXSpeed) * -bobSize.x, (1f - Mathf.Sin(0.5f + bobYOffset + Time.time * bobYSpeed)) * bobSize.y);

                //SMOOTH TRANSITIONS BETWEEN WALKING, RUNNING, CROUCHING, ETC
                bobSmooth = Vector2.MoveTowards(bobSmooth, bobRaw, Time.deltaTime * bobSmoothSpeed) * moveSmooth;

                Offset += bobSmooth;
            }

            //inertia
            if (inertia)
            {
                float mod = 1;

                inertiaTarget = new Vector2(-(InputManager.Instance.LookX + InputManager.Instance.Horizontal)* 0.5f * inertiaScale, InputManager.Instance.LookY * inertiaScale);
                inertiaSpeedMod = Vector2.Distance(inertiaCurrent,inertiaTarget) / inertiaScale;

                if (inertiaTarget != Vector2.zero)
                    mod = 3;

                inertiaCurrent = Vector2.MoveTowards(inertiaCurrent, inertiaTarget, Time.deltaTime * inertiaSpeed * inertiaSpeedMod * mod);

                Offset += inertiaCurrent;

                mod = 1;

                if (inertiaForwardTarget != Vector2.zero)
                    mod = 3;

                inertiaForwardTarget = new Vector2(InputManager.Instance.Vertical, InputManager.Instance.Vertical) * 0.05f * inertiaForwardScale * (GameManager.Instance.PlayerMotor.Speed/ GameManager.Instance.SpeedChanger.GetBaseSpeed());
                inertiaForwardCurrent = Vector2.MoveTowards(inertiaForwardCurrent, inertiaForwardTarget, Time.deltaTime * inertiaForwardSpeed * mod);

                Scale += inertiaForwardCurrent;
            }

            if (recoil)
            {
                if (recoilCurrent != Vector2.zero)
                {
                    float mod = recoilCurrent.magnitude / 0.5f;
                    recoilCurrent = Vector2.MoveTowards(recoilCurrent, Vector2.zero, Time.deltaTime * recoilSpeed * mod);
                    //recoilCurrent = Vector2.MoveTowards(recoilCurrent, Vector2.zero, Time.deltaTime * recoilSpeed);
                }

                Vector2 recoilFinal = recoilCurrent * recoilScale;

                recoilFinal = new Vector2(Mathf.Clamp(recoilFinal.x,0,0.5f), Mathf.Clamp(recoilFinal.y, 0, 0.5f));

                Scale += recoilFinal;

                Vector2 centerOffset = new Vector2(shieldTexture.width* recoilFinal.x,shieldTexture.height* recoilFinal.y);
                Offset -= centerOffset;
            }

            DaggerfallUI.DrawTextureWithTexCoords(GetShieldRect(), shieldTexture, curAnimRect, true, GameManager.Instance.WeaponManager.ScreenWeapon.Tint);
        }
    }
    public Rect GetShieldRect()
    {
        if (Offset != Vector2.zero || Scale != Vector2.one)
        {
            Rect shieldPositionOffset = shieldPositionCurrent;

            shieldPositionOffset.x += Offset.x;
            shieldPositionOffset.y += Offset.y;
            shieldPositionOffset.width *= Scale.x;
            shieldPositionOffset.height *= Scale.y;

            //stop the texture from going higher than its bottom edge
            shieldPositionOffset.y = Mathf.Clamp(shieldPositionOffset.y,screenRect.height-shieldPositionOffset.height,screenRect.height);

            return shieldPositionOffset;

        }
        else
            return shieldPositionCurrent;
    }

    public void RefreshShield()
    {
        if (GameManager.Instance.WeaponManager.Sheathed || GameManager.Instance.WeaponManager.ScreenWeapon.IsAttacking())
            SetAttack();
        else
            SetGuard();
    }

    public void SetGuard()
    {
        weaponScaleX = (float)screenRect.width / (float)nativeScreenWidth;
        weaponScaleY = weaponScaleX;

        if (flipped)
        {
            shieldPositionTarget = new Rect(
                screenRect.x + screenRect.width - (shieldTexture.width * scale) * (2-offsetX) * weaponScaleX,
                screenRect.y + screenRect.height - (shieldTexture.height * scale) * offsetY * weaponScaleY - weaponOffsetHeight,
                shieldTexture.width * scale * weaponScaleX,
                shieldTexture.height * scale * weaponScaleY
                );
        }
        else
        {
            shieldPositionTarget = new Rect(
                screenRect.x + screenRect.width * 0.5f - (shieldTexture.width * scale) * offsetX * weaponScaleX,
                screenRect.y + screenRect.height - (shieldTexture.height * scale) * offsetY * weaponScaleY - weaponOffsetHeight,
                shieldTexture.width * scale * weaponScaleX,
                shieldTexture.height * scale * weaponScaleY
                );
        }
    }

    public void SetAttack()
    {
        weaponScaleX = (float)screenRect.width / (float)nativeScreenWidth;
        weaponScaleY = weaponScaleX;


        if (flipped)
        {
            shieldPositionTarget = new Rect(
                screenRect.x + screenRect.width - (shieldTexture.width * scale) * 0.5f * weaponScaleX,
                screenRect.y + screenRect.height - (shieldTexture.height * scale) * 0.5f * weaponScaleY - weaponOffsetHeight,
                shieldTexture.width * scale * weaponScaleX,
                shieldTexture.height * scale * weaponScaleY
            );
        }
        else
        {
            shieldPositionTarget = new Rect(
                screenRect.x + screenRect.width * 0.5f - (shieldTexture.width * scale) * 1.5f * weaponScaleX,
                screenRect.y + screenRect.height - (shieldTexture.height * scale) * 0.5f * weaponScaleY - weaponOffsetHeight,
                shieldTexture.width * scale * weaponScaleX,
                shieldTexture.height * scale * weaponScaleY
            );
        }
    }

    //by blessed3220
    //texture loading method. Grabs the string path the developer inputs, finds the file, if exists, loads it,
    //then resizes it for use. If not, outputs error message.
    public Texture2D LoadPNG(string filePath)
    {
        Texture2D tex = null;
        byte[] fileData;

        if (File.Exists(filePath))
        {
            fileData = File.ReadAllBytes(filePath);
            tex = new Texture2D(2, 2);
            tex.filterMode = FilterMode.Point;
            tex.name = Path.GetFileName(filePath);
            tex.LoadImage(fileData); //..this will auto-resize the texture dimensions.
        }
        else
            Debug.Log("FilePath Broken!");


        return tex;
    }

    /*
    private void Update()
    {
        if (Input.GetMouseButtonDown(3))
            HitShield();
    }
    */

    public void HitShield(int damage)
    {
        if (!recoil)
            return;

        if (recoilCondition == 0 || recoilCondition == 3) //HITS ONLY
        {
            if (damage > 0)
                recoilCurrent += Vector2.one * (0.1f + (damage * 0.01f));
            else
                PlayImpactSound();
        }
        else if (recoilCondition == 1 || recoilCondition == 4) //MISSES ONLY
        {
            if (damage < 1)
            {
                recoilCurrent += Vector2.one * (UnityEngine.Random.Range(1, 2) * 0.1f);
                PlayImpactSound();
            }
        }
        else
        {
            recoilCurrent += Vector2.one * (0.1f + (damage * 0.01f));
            PlayImpactSound();
        }
    }

    public void PlaySound()
    {
        int sound;
        if (UnityEngine.Random.value > 0.5f)
        {
            sound = (int)SoundClips.Hit1 + UnityEngine.Random.Range(0, 5);
        }
        else
        {
            sound = (int)SoundClips.Parry1 + UnityEngine.Random.Range(0, 9);
        }
        audioSource.PlayOneShot(sound, 1, 1.1f);
    }

    public void PlayImpactSound()
    {
        int sound = (int)SoundClips.Parry1 + UnityEngine.Random.Range(0, 9);
        audioSource.PlayOneShot(sound, 1, 1.1f);
    }

    public bool IsPartShielded(DaggerfallUnityItem shield, int struckBodyPart)
    {
        BodyParts[] parts = shield.GetShieldProtectedBodyParts();
        bool shielded = false;
        foreach (BodyParts part in parts)
        {
            if (part == (BodyParts)struckBodyPart)
            {
                shielded = true;
                break;
            }
        }
        return shielded;
    }

    /// <summary>
    /// Calculate the damage caused by an attack.
    /// </summary>
    /// <param name="attacker">Attacking entity</param>
    /// <param name="target">Target entity</param>
    /// <param name="isEnemyFacingAwayFromPlayer">Whether enemy is facing away from player, used for backstabbing</param>
    /// <param name="weaponAnimTime">Time the weapon animation lasted before the attack in ms, used for bow drawing </param>
    /// <param name="weapon">The weapon item being used</param>
    /// <returns>Damage inflicted to target, can be 0 for a miss or ineffective hit</returns>
    public static int CalculateAttackDamage(DaggerfallEntity attacker, DaggerfallEntity target, bool isEnemyFacingAwayFromPlayer, int weaponAnimTime, DaggerfallUnityItem weapon)
    {
        if (attacker == null || target == null)
            return 0;

        int damageModifiers = 0;
        int damage = 0;
        int chanceToHitMod = 0;
        int backstabChance = 0;
        PlayerEntity player = GameManager.Instance.PlayerEntity;
        short skillID = 0;

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
        }
        else
        {
            skillID = (short)DFCareer.Skills.HandToHand;
        }

        chanceToHitMod = attacker.Skills.GetLiveSkillValue(skillID);

        if (attacker == player)
        {
            // Apply swing modifiers
            FormulaHelper.ToHitAndDamageMods swingMods = FormulaHelper.CalculateSwingModifiers(GameManager.Instance.WeaponManager.ScreenWeapon);
            damageModifiers += swingMods.damageMod;
            chanceToHitMod += swingMods.toHitMod;

            // Apply proficiency modifiers
            FormulaHelper.ToHitAndDamageMods proficiencyMods = FormulaHelper.CalculateProficiencyModifiers(attacker, weapon);
            damageModifiers += proficiencyMods.damageMod;
            chanceToHitMod += proficiencyMods.toHitMod;

            // Apply racial bonuses
            FormulaHelper.ToHitAndDamageMods racialMods = FormulaHelper.CalculateRacialModifiers(attacker, weapon, player);
            damageModifiers += racialMods.damageMod;
            chanceToHitMod += racialMods.toHitMod;

            backstabChance = FormulaHelper.CalculateBackstabChance(player, null, isEnemyFacingAwayFromPlayer);
            chanceToHitMod += backstabChance;
        }

        // Choose struck body part
        int struckBodyPart = FormulaHelper.CalculateStruckBodyPart();

        // Get damage for weaponless attacks
        if (skillID == (short)DFCareer.Skills.HandToHand)
        {
            if (attacker == player || (AIAttacker != null && AIAttacker.EntityType == EntityTypes.EnemyClass))
            {
                if (FormulaHelper.CalculateSuccessfulHit(attacker, target, chanceToHitMod, struckBodyPart))
                {
                    damage = FormulaHelper.CalculateHandToHandAttackDamage(attacker, target, damageModifiers, attacker == player);

                    damage = FormulaHelper.CalculateBackstabDamage(damage, backstabChance);
                }
            }
            else if (AIAttacker != null) // attacker is a monster
            {
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

                    int hitDamage = 0;
                    if (DFRandom.rand() % 100 < reflexesChance && minBaseDamage > 0 && FormulaHelper.CalculateSuccessfulHit(attacker, target, chanceToHitMod, struckBodyPart))
                    {
                        hitDamage = UnityEngine.Random.Range(minBaseDamage, maxBaseDamage + 1);
                        // Apply special monster attack effects
                        if (hitDamage > 0)
                            FormulaHelper.OnMonsterHit(AIAttacker, target, hitDamage);

                        damage += hitDamage;
                    }

                    // Apply bonus damage only when monster has actually hit, or they will accumulate bonus damage even for missed attacks and zero-damage attacks
                    if (hitDamage > 0)
                        damage += FormulaHelper.GetBonusOrPenaltyByEnemyType(attacker, target);

                    ++attackNumber;
                }
            }
        }
        // Handle weapon attacks
        else if (weapon != null)
        {
            // Apply weapon material modifier.
            chanceToHitMod += FormulaHelper.CalculateWeaponToHit(weapon);

            // Mod hook for adjusting final hit chance mod and adding new elements to calculation. (no-op in DFU)
            chanceToHitMod = FormulaHelper.AdjustWeaponHitChanceMod(attacker, target, chanceToHitMod, weaponAnimTime, weapon);

            if (FormulaHelper.CalculateSuccessfulHit(attacker, target, chanceToHitMod, struckBodyPart))
            {
                damage = FormulaHelper.CalculateWeaponAttackDamage(attacker, target, damageModifiers, weaponAnimTime, weapon);

                damage = FormulaHelper.CalculateBackstabDamage(damage, backstabChance);
            }

            // Handle poisoned weapons
            if (damage > 0 && weapon.poisonType != Poisons.None)
            {
                FormulaHelper.InflictPoison(attacker, target, weapon.poisonType, false);
                weapon.poisonType = Poisons.None;
            }
        }

        damage = Mathf.Max(0, damage);

        FormulaHelper.DamageEquipment(attacker, target, damage, weapon, struckBodyPart);

        // Apply Ring of Namira effect
        if (target == player)
        {
            DaggerfallUnityItem[] equippedItems = target.ItemEquipTable.EquipTable;
            DaggerfallUnityItem item = null;
            if (equippedItems.Length != 0)
            {
                if (Instance.IsRingOfNamira(equippedItems[(int)EquipSlots.Ring0]) || Instance.IsRingOfNamira(equippedItems[(int)EquipSlots.Ring1]))
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
            DaggerfallUnityItem shield = player.ItemEquipTable.GetItem(EquipSlots.LeftHand);
            if (shield != null)
            {
                if (shield.IsShield)
                {
                    if (Instance.recoilCondition > 2)                           //recoil shield on unshielded bits
                    {
                        Instance.HitShield(damage);
                    }
                    else                                                        //recoil shield only on shielded bits
                    {
                        bool shielded = Instance.IsPartShielded(shield,struckBodyPart);
                        if (shielded)
                            Instance.HitShield(damage);
                    }
                }
            }
        }

        return damage;
    }

    public bool IsRingOfNamira(DaggerfallUnityItem item)
    {
        return item != null && item.ContainsEnchantment(DaggerfallConnect.FallExe.EnchantmentTypes.SpecialArtifactEffect, (int)ArtifactsSubTypes.Ring_of_Namira);
    }

}
