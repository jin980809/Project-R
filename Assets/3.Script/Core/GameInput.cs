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

    public GameInput()
    {
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

    public void Enable()
    {
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
    }

    public void Dispose()
    {
        move.Dispose();
        point.Dispose();
        attack.Dispose();
        offhand.Dispose();
        roll.Dispose();
        interact.Dispose();
        skill1.Dispose();
        skill2.Dispose();
        skill3.Dispose();
        menu.Dispose();
    }
}
