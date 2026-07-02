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
        { InputActionType.MoveUp, "\uc704\ub85c \uc774\ub3d9\u000d" },
        { InputActionType.MoveDown, "\uc544\ub798\ub85c\u000d \uc774\ub3d9\u000d" },
        { InputActionType.MoveLeft, "\uc67c\ucabd\uc73c\ub85c\u000d \uc774\ub3d9\u000d" },
        { InputActionType.MoveRight, "\uc624\ub978\ucabd\uc73c\ub85c\u000d \uc774\ub3d9\u000d" },
        { InputActionType.Run, "\ub2ec\ub9ac\uae30\u000d" },
        { InputActionType.Jump, "\uc810\ud504\u000d" },
        { InputActionType.Interact, "\uc0c1\ud638\uc791\uc6a9" }
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
                return "Left Shift";
            case KeyCode.RightShift:
                return "Right Shift";
            case KeyCode.LeftControl:
                return "Left Control";
            case KeyCode.RightControl:
                return "Right Control";
            case KeyCode.LeftAlt:
                return "Left Alt";
            case KeyCode.RightAlt:
                return "Right Alt";
            case KeyCode.UpArrow:
                return "Up Arrow";
            case KeyCode.DownArrow:
                return "Down Arrow";
            case KeyCode.LeftArrow:
                return "Left Arrow";
            case KeyCode.RightArrow:
                return "Right Arrow";
            case KeyCode.Return:
                return "Enter";
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
