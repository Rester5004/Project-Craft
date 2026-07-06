using System;
using UnityEngine;
using UnityEngine.InputSystem;

public class InputActionManager : Singleton<InputActionManager>
{
    private const string BindingOverridesKey = "InputBindingOverrides";

    private InputActionMap playerMap;
    private InputAction moveAction;
    private InputAction useAction;
    private InputAction hitAction;
    private InputAction toggleInventoryAction;

    public event Action<Vector2> OnMove;
    public event Action OnUsePerformed;
    public event Action OnHitPerformed;
    public event Action OnToggleInventoryPerformed;

    public Vector2 MoveValue => moveAction?.ReadValue<Vector2>() ?? Vector2.zero;

    protected override void Awake()
    {
        base.Awake();
        BuildActions();
        LoadBindingOverrides();
        playerMap.Enable();
    }

    protected override void OnDestroy()
    {
        base.OnDestroy();
        playerMap?.Disable();
        playerMap?.Dispose();
    }

    private void BuildActions()
    {
        playerMap = new InputActionMap("Player");

        moveAction = playerMap.AddAction("Move", type: InputActionType.Value, expectedControlLayout: "Vector2");
        moveAction.AddCompositeBinding("2DVector")
            .With("Up", "<Keyboard>/w")
            .With("Down", "<Keyboard>/s")
            .With("Left", "<Keyboard>/a")
            .With("Right", "<Keyboard>/d");

        hitAction = playerMap.AddAction("Hit", type: InputActionType.Button, binding: "<Mouse>/leftButton");
        useAction = playerMap.AddAction("Use", type: InputActionType.Button, binding: "<Mouse>/rightButton");

        toggleInventoryAction = playerMap.AddAction("ToggleInventory", type: InputActionType.Button, binding: "<Keyboard>/i");

        moveAction.performed += HandleMovePerformed;
        moveAction.canceled += HandleMovePerformed;
        useAction.performed += HandleUsePerformed;
        hitAction.performed += HandleHitPerformed;
        toggleInventoryAction.performed += HandleToggleInventoryPerformed;
    }

    private void HandleMovePerformed(InputAction.CallbackContext ctx) => OnMove?.Invoke(ctx.ReadValue<Vector2>());
    private void HandleUsePerformed(InputAction.CallbackContext ctx) => OnUsePerformed?.Invoke();
    private void HandleHitPerformed(InputAction.CallbackContext ctx) => OnHitPerformed?.Invoke();
    private void HandleToggleInventoryPerformed(InputAction.CallbackContext ctx) => OnToggleInventoryPerformed?.Invoke();

    public InputAction GetAction(string actionName) => playerMap?.FindAction(actionName);

    /// <summary>
    /// 지정한 액션을 새 입력으로 다시 바인딩합니다. 완료/취소 시 액션을 자동으로 재활성화합니다.
    /// </summary>
    public void StartRebind(string actionName, Action onComplete = null)
    {
        InputAction action = playerMap?.FindAction(actionName);
        if (action == null)
        {
            Debug.LogWarning($"[InputActionManager] '{actionName}' 액션을 찾을 수 없습니다.");
            return;
        }

        action.Disable();
        action.PerformInteractiveRebinding()
            .WithControlsExcluding("Mouse/position")
            .WithControlsExcluding("Mouse/delta")
            .OnMatchWaitForAnother(0.1f)
            .OnComplete(operation =>
            {
                operation.Dispose();
                action.Enable();
                SaveBindingOverrides();
                onComplete?.Invoke();
            })
            .OnCancel(operation =>
            {
                operation.Dispose();
                action.Enable();
            })
            .Start();
    }

    public void ResetBindings()
    {
        foreach (InputAction action in playerMap.actions)
        {
            action.RemoveAllBindingOverrides();
        }
        PlayerPrefs.DeleteKey(BindingOverridesKey);
    }

    private void SaveBindingOverrides()
    {
        string json = playerMap.SaveBindingOverridesAsJson();
        PlayerPrefs.SetString(BindingOverridesKey, json);
    }

    private void LoadBindingOverrides()
    {
        if (PlayerPrefs.HasKey(BindingOverridesKey))
        {
            playerMap.LoadBindingOverridesFromJson(PlayerPrefs.GetString(BindingOverridesKey));
        }
    }
}
