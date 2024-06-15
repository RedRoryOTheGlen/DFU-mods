using UnityEngine;
using DaggerfallWorkshop;
using DaggerfallWorkshop.Game;
using DaggerfallWorkshop.Game.Utility.ModSupport;
using DaggerfallWorkshop.Game.Utility.ModSupport.ModSettings;

namespace FPSWeaponMovementMod
{
    public class FPSWeaponMovement : MonoBehaviour
    {
        static Mod mod;

        DaggerfallAudioSource audioSource;

        [Invoke(StateManager.StateTypes.Start, 0)]
        public static void Init(InitParams initParams)
        {
            mod = initParams.Mod;
            var go = new GameObject(mod.Title);
            go.AddComponent<FPSWeaponMovement>();
        }

        Vector2 offsetCurrent;
        Vector2 scaleCurrent;

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

        Rect screenRect;

        bool flipped;

        void Awake()
        {
            mod.LoadSettingsCallback = LoadSettings;
            mod.LoadSettings();

            if (DaggerfallUI.Instance.CustomScreenRect != null)
                screenRect = DaggerfallUI.Instance.CustomScreenRect.Value;
            else
                screenRect = new Rect(0, 0, Screen.width, Screen.height);

            if (audioSource == null)
                audioSource = GameManager.Instance.WeaponManager.ScreenWeapon.gameObject.GetComponent<DaggerfallAudioSource>();

            flipped = GameManager.Instance.WeaponManager.ScreenWeapon.FlipHorizontal;

            mod.IsReady = true;
        }

        private void LoadSettings(ModSettings settings, ModSettingsChange change)
        {
            if (change.HasChanged("Bob"))
            {
                bob = settings.GetValue<bool>("Bob", "EnableBob");
                bobLength = settings.GetValue<float>("Bob", "Length");
                bobSizeXMod = settings.GetValue<float>("Bob", "SizeX");
                bobSizeYMod = settings.GetValue<float>("Bob", "SizeY");
                moveSmoothSpeed = settings.GetValue<float>("Bob", "SpeedMove") * 2;
                bobSmoothSpeed = settings.GetValue<float>("Bob", "SpeedState") * 250;
                bobShape = settings.GetValue<int>("Bob", "Shape") * 0.5f;
                bobWhileIdle = settings.GetValue<bool>("Bob", "BobWhileIdle");
            }
            if (change.HasChanged("Inertia"))
            {
                inertia = settings.GetValue<bool>("Inertia", "EnableInertia");
                inertiaScale = settings.GetValue<float>("Inertia", "Scale") * 250;
                inertiaSpeed = settings.GetValue<float>("Inertia", "Speed") * 250;
                inertiaForwardScale = settings.GetValue<float>("Inertia", "ForwardDepth");
                inertiaForwardSpeed = settings.GetValue<float>("Inertia", "ForwardSpeed") * 0.1f;
            }
        }

        void OnGUI()
        {
            offsetCurrent = Vector2.zero;
            scaleCurrent = Vector2.one;

            GameManager.Instance.WeaponManager.ScreenWeapon.Offset = Vector2.zero;
            GameManager.Instance.WeaponManager.ScreenWeapon.Scale = Vector2.one;

            bool attacking = GameManager.Instance.WeaponManager.ScreenWeapon.IsAttacking();
            bool usingBow = GameManager.Instance.WeaponManager.ScreenWeapon.WeaponType == WeaponTypes.Bow;

            //SCALE  TO SPEED AND MOVEMENT
            float currentSpeed = GameManager.Instance.PlayerMotor.Speed;
            float baseSpeed = GameManager.Instance.SpeedChanger.GetBaseSpeed();
            float speed = currentSpeed / baseSpeed;

            //bob
            if (bob && (!attacking || usingBow))
            {
                //SHAPE OF MOVE BOB
                float bobYOffset = bobShape;

                //BOB ONLY WHEN GROUNDED
                float move = 1;
                if (!GameManager.Instance.PlayerMotor.IsGrounded)
                    move = 0;
                moveSmooth = Mathf.MoveTowards(moveSmooth, move, Time.deltaTime * moveSmoothSpeed);

                //SCALE BOB TO SPEED AND MOVEMENT
                float bob = speed;

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

                //DAMPEN BOB SIZE SIGNIFICANTLY WHEN BOW IS DRAWN AND HELD
                float drawn = 1;
                if (usingBow && attacking)
                    drawn *= 0.5f;

                //HORIZONTAL BOB
                float bobXSpeed = baseSpeed * 1.25f * bob * bobLength; //SYNC IT WITH THE FOOTSTEP SOUNDS
                float bobYSpeed = bobXSpeed * 2f; //MAKE IT A MULTIPLE OF THE HORIZONTAL BOB SPEED
                Vector2 bobSize = new Vector2(screenRect.width * 0.1f * bob * bobSizeXMod, screenRect.height * 0.05f * bob * bobSizeYMod);

                //GET CURRENT BOB VALUES
                float screenOffset = 1f;
                if (flipped)
                    screenOffset = -1f;
                Vector2 bobRaw = new Vector2((screenOffset - Mathf.Sin(Time.time * bobXSpeed)) * (bobSize.x*drawn), (screenOffset - Mathf.Sin(bobYOffset + Time.time * bobYSpeed)) * (bobSize.y*drawn));

                //SMOOTH TRANSITIONS BETWEEN WALKING, RUNNING, CROUCHING, ETC
                bobSmooth = Vector2.MoveTowards(bobSmooth, bobRaw, Time.deltaTime * bobSmoothSpeed) * moveSmooth;

                GameManager.Instance.WeaponManager.ScreenWeapon.Offset += bobSmooth;
            }

            //inertia
            if (inertia && (!attacking || usingBow))
            {
                float mod = 1;

                inertiaTarget = new Vector2(-(InputManager.Instance.LookX + InputManager.Instance.Horizontal) * 0.5f * inertiaScale, InputManager.Instance.LookY * inertiaScale);
                inertiaSpeedMod = Vector2.Distance(inertiaCurrent, inertiaTarget) / inertiaScale;

                if (inertiaTarget != Vector2.zero)
                    mod = 3;

                inertiaCurrent = Vector2.MoveTowards(inertiaCurrent, inertiaTarget, Time.deltaTime * inertiaSpeed * inertiaSpeedMod * mod);

                GameManager.Instance.WeaponManager.ScreenWeapon.Offset += inertiaCurrent;

                mod = 1;

                if (inertiaForwardTarget != Vector2.zero)
                    mod = 3;

                inertiaForwardTarget = new Vector2(InputManager.Instance.Vertical, InputManager.Instance.Vertical) * 0.05f * inertiaForwardScale * speed;
                inertiaForwardCurrent = Vector2.MoveTowards(inertiaForwardCurrent, inertiaForwardTarget, Time.deltaTime * inertiaForwardSpeed * mod);

                GameManager.Instance.WeaponManager.ScreenWeapon.Scale += inertiaForwardCurrent;
            }
        }

    }
}
