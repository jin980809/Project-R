using System;
using UnityEngine;
using UnityEngine.InputSystem;

// 키보드·마우스 입력. 액션을 코드로 만들어 두고 매 프레임 값만 읽는다(할당 없음).
// 키보드 키는 설정 화면(PauseMenuUi)에서 바꿀 수 있다(마우스 공격·막기와 Esc는 고정). 바꾼 키는 MetaProgress에 저장한다.
public class GameInput
{
    // 바꿀 수 있는 키 순서. 설정 화면의 줄 순서이자 저장 번호다.
    public const int KeyUp = 0;
    public const int KeyDown = 1;
    public const int KeyLeft = 2;
    public const int KeyRight = 3;
    public const int KeyRoll = 4;
    public const int KeyInteract = 5;
    public const int KeySkill1 = 6;
    public const int KeySkill2 = 7;
    public const int KeySkill3 = 8;
    public const int KeyPotion1 = 9;
    public const int KeyPotion2 = 10;
    public const int KeyInventory = 11;
    public const int KeySkillTree = 12;
    public const int KeyRide = 13;
    public const int KeyMap = 14;
    public const int RebindCount = 15;
    public const int PotionSlotCount = 2;

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
    private readonly InputAction potion1;
    private readonly InputAction potion2;
    private readonly InputAction skillTree;
    // Shift: 말에 타고 내린다
    private readonly InputAction ride;
    // M: 큰 지도(던전)
    private readonly InputAction map;
    // 로비에서 숫자 키 1~3으로 시작 계열을 고른다(PlayerSkills의 계열 순서).
    private readonly InputAction[] weaponKeys = new InputAction[PlayerSkills.ClassCount];

    // 바꿀 수 있는 키: 액션과 그 안의 바인딩 번호(이동은 2DVector 합성의 위·아래·왼쪽·오른쪽 칸)
    private readonly InputAction[] rebindActions = new InputAction[RebindCount];
    private readonly int[] rebindBindings = new int[RebindCount];
    private readonly string[] rebindNames =
    {
        "위로 이동", "아래로 이동", "왼쪽 이동", "오른쪽 이동", "구르기", "상호작용", "스킬 1", "스킬 2", "스킬 3",
        "포션 1", "포션 2", "가방", "스킬 트리", "탈것", "지도"
    };
    // 화면에 보여 줄 키 이름. 키가 바뀔 때만 다시 만든다.
    private readonly string[] keyNames = new string[RebindCount];
    private readonly Action<InputActionRebindingExtensions.RebindingOperation> onRebindComplete;
    private readonly Action<InputActionRebindingExtensions.RebindingOperation> onRebindCancel;
    private InputActionRebindingExtensions.RebindingOperation rebinding;
    private int rebindIndex = -1;
    private string rebindOldPath;
    private int keysRevision;
    private int rebindEndFrame = -10;

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
        skill1 = new InputAction("Skill1", InputActionType.Button, "<Keyboard>/q");
        skill2 = new InputAction("Skill2", InputActionType.Button, "<Keyboard>/e");
        skill3 = new InputAction("Skill3", InputActionType.Button, "<Keyboard>/r");
        menu = new InputAction("Menu", InputActionType.Button, "<Keyboard>/escape");
        inventory = new InputAction("Inventory", InputActionType.Button, "<Keyboard>/tab");
        potion1 = new InputAction("Potion1", InputActionType.Button, "<Keyboard>/1");
        potion2 = new InputAction("Potion2", InputActionType.Button, "<Keyboard>/2");
        skillTree = new InputAction("SkillTree", InputActionType.Button, "<Keyboard>/k");
        ride = new InputAction("Ride", InputActionType.Button, "<Keyboard>/shift");
        map = new InputAction("Map", InputActionType.Button, "<Keyboard>/m");

        SetRebind(KeyUp, move, 1);
        SetRebind(KeyDown, move, 2);
        SetRebind(KeyLeft, move, 3);
        SetRebind(KeyRight, move, 4);
        SetRebind(KeyRoll, roll, 0);
        SetRebind(KeyInteract, interact, 0);
        SetRebind(KeySkill1, skill1, 0);
        SetRebind(KeySkill2, skill2, 0);
        SetRebind(KeySkill3, skill3, 0);
        SetRebind(KeyPotion1, potion1, 0);
        SetRebind(KeyPotion2, potion2, 0);
        SetRebind(KeyInventory, inventory, 0);
        SetRebind(KeySkillTree, skillTree, 0);
        SetRebind(KeyRide, ride, 0);
        SetRebind(KeyMap, map, 0);
        onRebindComplete = OnRebindComplete;
        onRebindCancel = OnRebindCancel;
        RefreshKeyNames();
    }

    public Vector2 Move => move.ReadValue<Vector2>();
    public Vector2 PointerPosition => point.ReadValue<Vector2>();
    public bool AttackPressed => attack.WasPressedThisFrame();
    public bool AttackHeld => attack.IsPressed();
    public bool OffhandHeld => offhand.IsPressed();
    public bool RollPressed => roll.WasPressedThisFrame();
    public bool InteractPressed => interact.WasPressedThisFrame();
    public bool Skill1Pressed => skill1.WasPressedThisFrame();
    public bool Skill2Pressed => skill2.WasPressedThisFrame();
    public bool Skill3Pressed => skill3.WasPressedThisFrame();
    public bool MenuPressed => menu.WasPressedThisFrame();
    public bool InventoryPressed => inventory.WasPressedThisFrame();
    public bool SkillTreePressed => skillTree.WasPressedThisFrame();
    public bool RidePressed => ride.WasPressedThisFrame();
    public bool MapPressed => map.WasPressedThisFrame();
    // 키를 바꾸고 있는 중(설정 화면이 Esc를 무시한다)
    public bool IsRebinding => rebinding != null;
    // 방금(이번이나 지난 프레임) 키 바꾸기가 끝났다. 취소에 쓴 Esc가 메뉴를 닫지 않게 한다.
    public bool RebindJustEnded => Time.frameCount - rebindEndFrame <= 1;
    // 키가 바뀔 때마다 1씩 늘어난다(키 이름을 보여 주는 화면이 다시 그리도록).
    public int KeysRevision => keysRevision;

    // 이번 프레임에 눌린 포션 칸 번호(0, 1). 없으면 -1.
    public int PotionKeyPressed
    {
        get
        {
            int pressed = -1;
            if (potion1.WasPressedThisFrame())
            {
                pressed = 0;
            }
            else if (potion2.WasPressedThisFrame())
            {
                pressed = 1;
            }
            return pressed;
        }
    }

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

    // 스킬 키(0 Q, 1 E, 2 R)를 누르고 있는지. 커서 자리에 놓는 스킬의 조준에 쓴다.
    public bool IsSkillKeyHeld(int slot)
    {
        bool held = false;
        if (slot == 0)
        {
            held = skill1.IsPressed();
        }
        else if (slot == 1)
        {
            held = skill2.IsPressed();
        }
        else if (slot == 2)
        {
            held = skill3.IsPressed();
        }
        return held;
    }

    public bool OffhandPressed => offhand.WasPressedThisFrame();

    // 이번 프레임에 눌린 계열 키 번호(0~2). 없으면 -1.
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

    // ------------------------------------------------------------ 키 바꾸기

    public string GetRebindName(int index)
    {
        return rebindNames[index];
    }

    // 지금 그 칸에 쓰이는 키의 이름(예: "F", "Space")
    public string GetKeyName(int index)
    {
        return keyNames[index];
    }

    // 기본값에서 바뀐 키 경로. 바뀌지 않았으면 빈 문자열(저장용).
    public string GetOverridePath(int index)
    {
        string path = rebindActions[index].bindings[rebindBindings[index]].overridePath;
        if (path == null)
        {
            path = string.Empty;
        }
        return path;
    }

    // 저장해 둔 키를 되살린다. 빈 문자열이면 기본값.
    public void ApplyOverridePath(int index, string path)
    {
        if (!string.IsNullOrEmpty(path))
        {
            rebindActions[index].ApplyBindingOverride(rebindBindings[index], path);
            RefreshKeyNames();
        }
    }

    // 다음에 누르는 키보드 키로 바꾼다. Esc는 취소. 이미 다른 칸이 쓰던 키면 그 칸과 서로 바꾼다.
    public void StartRebind(int index)
    {
        CancelRebind();
        rebindIndex = index;
        InputAction action = rebindActions[index];
        int bindingIndex = rebindBindings[index];
        rebindOldPath = action.bindings[bindingIndex].effectivePath;
        action.Disable();
        rebinding = action.PerformInteractiveRebinding(bindingIndex)
            .WithControlsHavingToMatchPath("<Keyboard>")
            .WithCancelingThrough("<Keyboard>/escape")
            .OnComplete(onRebindComplete)
            .OnCancel(onRebindCancel)
            .Start();
    }

    public void CancelRebind()
    {
        if (rebinding != null)
        {
            rebinding.Cancel();
        }
    }

    public void ResetBindings()
    {
        CancelRebind();
        for (int i = 0; i < RebindCount; i++)
        {
            rebindActions[i].RemoveBindingOverride(rebindBindings[i]);
        }
        RefreshKeyNames();
    }

    private void OnRebindComplete(InputActionRebindingExtensions.RebindingOperation operation)
    {
        string newPath = rebindActions[rebindIndex].bindings[rebindBindings[rebindIndex]].effectivePath;
        for (int i = 0; i < RebindCount; i++)
        {
            if (i != rebindIndex && rebindActions[i].bindings[rebindBindings[i]].effectivePath == newPath)
            {
                rebindActions[i].ApplyBindingOverride(rebindBindings[i], rebindOldPath);
            }
        }
        FinishRebind(operation);
    }

    private void OnRebindCancel(InputActionRebindingExtensions.RebindingOperation operation)
    {
        FinishRebind(operation);
    }

    private void FinishRebind(InputActionRebindingExtensions.RebindingOperation operation)
    {
        InputAction action = rebindActions[rebindIndex];
        operation.Dispose();
        rebinding = null;
        rebindIndex = -1;
        rebindEndFrame = Time.frameCount;
        action.Enable();
        RefreshKeyNames();
    }

    private void SetRebind(int index, InputAction action, int bindingIndex)
    {
        rebindActions[index] = action;
        rebindBindings[index] = bindingIndex;
    }

    private void RefreshKeyNames()
    {
        for (int i = 0; i < RebindCount; i++)
        {
            string path = rebindActions[i].bindings[rebindBindings[i]].effectivePath;
            keyNames[i] = InputControlPath.ToHumanReadableString(path, InputControlPath.HumanReadableStringOptions.OmitDevice);
        }
        keysRevision++;
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
        skill1.Enable();
        skill2.Enable();
        skill3.Enable();
        menu.Enable();
        inventory.Enable();
        potion1.Enable();
        potion2.Enable();
        skillTree.Enable();
        ride.Enable();
        map.Enable();
    }

    public void Dispose()
    {
        CancelRebind();
        for (int i = 0; i < weaponKeys.Length; i++)
        {
            weaponKeys[i].Dispose();
        }
        move.Dispose();
        point.Dispose();
        attack.Dispose();
        offhand.Dispose();
        ride.Dispose();
        map.Dispose();
        roll.Dispose();
        interact.Dispose();
        skill1.Dispose();
        skill2.Dispose();
        skill3.Dispose();
        menu.Dispose();
        inventory.Dispose();
        potion1.Dispose();
        potion2.Dispose();
        skillTree.Dispose();
    }
}
