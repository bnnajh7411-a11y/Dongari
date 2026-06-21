using System;
using System.Collections.Generic;
using UnityEngine;

public enum InputActionType
{
    MoveUp,
    MoveDown,
    MoveLeft,
    MoveRight,
    Run,
    Jump,
    Interact
}

public static class PlayerInputBindings
{
    private const string ConfiguredPrefKey = "PlayerInputBindings.Configured";
    private const string BindingPrefPrefix = "PlayerInputBindings.";

    private static readonly InputActionType[] OrderedActions =
    {
        InputActionType.MoveUp,
        InputActionType.MoveDown,
        InputActionType.MoveLeft,
        InputActionType.MoveRight,
        InputActionType.Run,
        InputActionType.Jump,
        InputActionType.Interact
    };

    private static readonly Dictionary<InputActionType, string> ActionLabels = new Dictionary<InputActionType, string>
    {
        { InputActionType.MoveUp, "위로 이동" },
        { InputActionType.MoveDown, "아래로 이동" },
        { InputActionType.MoveLeft, "왼쪽 이동" },
        { InputActionType.MoveRight, "오른쪽 이동" },
        { InputActionType.Run, "달리기" },
        { InputActionType.Jump, "점프" },
        { InputActionType.Interact, "상호작용" }
    };

    private static readonly Dictionary<InputActionType, KeyCode> DefaultBindings = new Dictionary<InputActionType, KeyCode>
    {
        { InputActionType.MoveUp, KeyCode.W },
        { InputActionType.MoveDown, KeyCode.S },
        { InputActionType.MoveLeft, KeyCode.A },
        { InputActionType.MoveRight, KeyCode.D },
        { InputActionType.Run, KeyCode.LeftShift },
        { InputActionType.Jump, KeyCode.Space },
        { InputActionType.Interact, KeyCode.Z }
    };

    private static readonly Dictionary<InputActionType, KeyCode> CachedBindings = new Dictionary<InputActionType, KeyCode>();

    private static bool hasLoadedBindings;

    public static IReadOnlyList<InputActionType> Actions => OrderedActions;

    public static bool IsConfigured => PlayerPrefs.GetInt(ConfiguredPrefKey, 0) == 1;

    public static string GetActionLabel(InputActionType action)
    {
        return ActionLabels[action];
    }

    public static KeyCode GetKey(InputActionType action)
    {
        EnsureBindingsLoaded();
        return CachedBindings[action];
    }

    public static string GetKeyDisplayName(InputActionType action)
    {
        return GetKeyDisplayName(GetKey(action));
    }

    public static string GetKeyDisplayName(KeyCode key)
    {
        switch (key)
        {
            case KeyCode.LeftShift:
                return "왼쪽 시프트";
            case KeyCode.RightShift:
                return "오른쪽 시프트";
            case KeyCode.LeftControl:
                return "왼쪽 컨트롤";
            case KeyCode.RightControl:
                return "오른쪽 컨트롤";
            case KeyCode.LeftAlt:
                return "왼쪽 알트";
            case KeyCode.RightAlt:
                return "오른쪽 알트";
            case KeyCode.UpArrow:
                return "위 방향키";
            case KeyCode.DownArrow:
                return "아래 방향키";
            case KeyCode.LeftArrow:
                return "왼쪽 방향키";
            case KeyCode.RightArrow:
                return "오른쪽 방향키";
            case KeyCode.Return:
                return "엔터";
        }

        string keyName = key.ToString();
        if (keyName.StartsWith("Alpha", StringComparison.Ordinal))
        {
            return keyName.Substring("Alpha".Length);
        }

        return keyName;
    }

    public static void SetKey(InputActionType action, KeyCode key)
    {
        EnsureBindingsLoaded();
        CachedBindings[action] = key;
    }

    public static void SaveAndMarkConfigured()
    {
        EnsureBindingsLoaded();

        for (int i = 0; i < OrderedActions.Length; i++)
        {
            InputActionType action = OrderedActions[i];
            PlayerPrefs.SetInt(GetBindingPrefKey(action), (int)CachedBindings[action]);
        }

        PlayerPrefs.SetInt(ConfiguredPrefKey, 1);
        PlayerPrefs.Save();
    }

    public static float GetHorizontalInput()
    {
        return GetAxisValue(InputActionType.MoveLeft, InputActionType.MoveRight);
    }

    public static float GetVerticalInput()
    {
        return GetAxisValue(InputActionType.MoveDown, InputActionType.MoveUp);
    }

    public static bool IsRunPressed()
    {
        return Input.GetKey(GetKey(InputActionType.Run));
    }

    public static bool WasJumpPressedThisFrame()
    {
        return Input.GetKeyDown(GetKey(InputActionType.Jump));
    }

    public static bool WasInteractPressedThisFrame()
    {
        return Input.GetKeyDown(GetKey(InputActionType.Interact));
    }

    private static float GetAxisValue(InputActionType negativeAction, InputActionType positiveAction)
    {
        bool isNegativePressed = Input.GetKey(GetKey(negativeAction));
        bool isPositivePressed = Input.GetKey(GetKey(positiveAction));

        if (isNegativePressed == isPositivePressed)
        {
            return 0f;
        }

        return isPositivePressed ? 1f : -1f;
    }

    private static void EnsureBindingsLoaded()
    {
        if (hasLoadedBindings)
        {
            return;
        }

        CachedBindings.Clear();

        for (int i = 0; i < OrderedActions.Length; i++)
        {
            InputActionType action = OrderedActions[i];
            CachedBindings[action] = LoadBindingOrDefault(action);
        }

        hasLoadedBindings = true;
    }

    private static KeyCode LoadBindingOrDefault(InputActionType action)
    {
        string prefKey = GetBindingPrefKey(action);
        if (!PlayerPrefs.HasKey(prefKey))
        {
            return DefaultBindings[action];
        }

        int savedValue = PlayerPrefs.GetInt(prefKey, (int)DefaultBindings[action]);
        if (!Enum.IsDefined(typeof(KeyCode), savedValue))
        {
            return DefaultBindings[action];
        }

        return (KeyCode)savedValue;
    }

    private static string GetBindingPrefKey(InputActionType action)
    {
        return $"{BindingPrefPrefix}{action}";
    }
}
