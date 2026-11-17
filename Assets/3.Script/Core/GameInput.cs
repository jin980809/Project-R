using UnityEngine;
using UnityEngine.InputSystem;

// 키보드·마우스 입력. 액션을 코드로 만들어 두고 매 프레임 값만 읽는다(할당 없음).
public class GameInput
{
    private readonly InputAction move;
    private readonly InputAction point;
    private readonly InputAction attack;
    private readonly InputAction offhand;
    private readonly InputAction roll;
    private readonly InputAction interact;
    private readonly InputAction skill1;
    private readonly InputAction skill2;
    private readonly InputAction skill3;
    private readonly InputAction menu;
    private readonly InputAction inventory;
    private readonly InputAction potion;
    private readonly InputAction skillTree;
    // 로비에서 숫자 키 1~9, 0으로 시작 직업을 고른다(PlayerSkills의 직업 순서).
    private readonly InputAction[] weaponKeys = new InputAction[PlayerSkills.JobCount];
    // 제단에서 두 번째 직업(완드의 사제)으로 전직
    private readonly InputAction interactAlt;

    public GameInput()
    {
        for (int i = 0; i < weaponKeys.Length; i++)
        {
            weaponKeys[i] = new InputAction("Job" + i, InputActionType.Button, "<Keyboard>/" + ((i + 1) % 10));
        }

        move = new InputAction("Move", InputActionType.Value);
        move.AddCompositeBinding("2DVector")
            .With("Up", "<Keyboard>/w")
            .With("Down", "<Keyboard>/s")
            .With("Left", "<Keyboard>/a")
            .With("Right", "<Keyboard>/d");

        point = new InputAction("Point", InputActionType.PassThrough, "<Mouse>/position");
        attack = new InputAction("Attack", InputActionType.Button, "<Mouse>/leftButton");
        offhand = new InputAction("Offhand", InputActionType.Button, "<Mouse>/rightButton");
        roll = new InputAction("Roll", InputActionType.Button, "<Keyboard>/space");
        interact = new InputAction("Interact", InputActionType.Button, "<Keyboard>/f");
        interactAlt = new InputAction("InteractAlt", InputActionType.Button, "<Keyboard>/g");
        skill1 = new InputAction("Skill1", InputActionType.Button, "<Keyboard>/q");
        skill2 = new InputAction("Skill2", InputActionType.Button, "<Keyboard>/e");
        skill3 = new InputAction("Skill3", InputActionType.Button, "<Keyboard>/r");
        menu = new InputAction("Menu", InputActionType.Button, "<Keyboard>/escape");
        inventory = new InputAction("Inventory", InputActionType.Button, "<Keyboard>/tab");
        potion = new InputAction("Potion", InputActionType.Button, "<Keyboard>/h");
        skillTree = new InputAction("SkillTree", InputActionType.Button, "<Keyboard>/k");
    }

    public Vector2 Move => move.ReadValue<Vector2>();
    public Vector2 PointerPosition => point.ReadValue<Vector2>();
    public bool AttackPressed => attack.WasPressedThisFrame();
    public bool AttackHeld => attack.IsPressed();
    public bool OffhandHeld => offhand.IsPressed();
    public bool RollPressed => roll.WasPressedThisFrame();
    public bool InteractPressed => interact.WasPressedThisFrame();
    public bool InteractAltPressed => interactAlt.WasPressedThisFrame();
    public bool Skill1Pressed => skill1.WasPressedThisFrame();
    public bool Skill2Pressed => skill2.WasPressedThisFrame();
    public bool Skill3Pressed => skill3.WasPressedThisFrame();
    public bool MenuPressed => menu.WasPressedThisFrame();
    public bool InventoryPressed => inventory.WasPressedThisFrame();
    public bool PotionPressed => potion.WasPressedThisFrame();
    public bool SkillTreePressed => skillTree.WasPressedThisFrame();

    // 이번 프레임에 눌린 스킬 키 번호(0 Q, 1 E, 2 R). 없으면 -1.
    public int SkillKeyPressed
    {
        get
        {
            int pressed = -1;
            if (skill1.WasPressedThisFrame())
            {
                pressed = 0;
            }
            else if (skill2.WasPressedThisFrame())
            {
                pressed = 1;
            }
            else if (skill3.WasPressedThisFrame())
            {
                pressed = 2;
            }
            return pressed;
        }
    }

    // 이번 프레임에 눌린 직업 키 번호(0~9). 없으면 -1.
    public int JobKeyPressed
    {
        get
        {
            int pressed = -1;
            for (int i = 0; i < weaponKeys.Length && pressed < 0; i++)
            {
                if (weaponKeys[i].WasPressedThisFrame())
                {
                    pressed = i;
                }
            }
            return pressed;
        }
    }

    public void Enable()
    {
        for (int i = 0; i < weaponKeys.Length; i++)
        {
            weaponKeys[i].Enable();
        }
        move.Enable();
        point.Enable();
        attack.Enable();
        offhand.Enable();
        roll.Enable();
        interact.Enable();
        interactAlt.Enable();
        skill1.Enable();
        skill2.Enable();
        skill3.Enable();
        menu.Enable();
        inventory.Enable();
        potion.Enable();
        skillTree.Enable();
    }

    public void Dispose()
    {
        for (int i = 0; i < weaponKeys.Length; i++)
        {
            weaponKeys[i].Dispose();
        }
        move.Dispose();
        point.Dispose();
        attack.Dispose();
        offhand.Dispose();
        roll.Dispose();
        interact.Dispose();
        interactAlt.Dispose();
        skill1.Dispose();
        skill2.Dispose();
        skill3.Dispose();
        menu.Dispose();
        inventory.Dispose();
        potion.Dispose();
        skillTree.Dispose();
    }
}
