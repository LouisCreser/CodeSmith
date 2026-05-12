using UnityEngine;
using UnityEngine.UI;

public readonly struct UIButtonColourSet
{
    public readonly Color Normal;
    public readonly Color Highlighted;
    public readonly Color Pressed;
    public readonly Color Selected;
    public readonly Color Disabled;

    public UIButtonColourSet(Color normal, Color highlighted, Color pressed, Color selected, Color disabled)
    {
        Normal = normal;
        Highlighted = highlighted;
        Pressed = pressed;
        Selected = selected;
        Disabled = disabled;
    }
}

public static class UIColourUtility
{
    // Base text colours, off-white is readable but not too harsh
    public static readonly Color TextPrimary = Hex("#F2F2E8");
    public static readonly Color TextSecondary = Hex("#d8d7cf");
    public static readonly Color TextDisabled = Hex("#bbc2ce");

    // Text colours convey meaning to the player
    public static readonly Color PositiveGreen = Hex("#6BBF59");
    public static readonly Color ValueGold = Hex("#FFD700");
    public static readonly Color ErrorRed = Hex("#c11f1f");

    // The default colour for an unclickable button
    public static readonly Color DisabledButtonColour = Hex("#c8c8c8");

    // Determines how much lighter or darker a colour is made by the respective functions
    private const float VariantLightenAmount = 0.07f;
    private const float VariantDarkenAmount = 0.07f;

    public static string TextPrimaryHex => ColorUtility.ToHtmlStringRGB(TextPrimary);
    public static string PositiveGreenHex => ColorUtility.ToHtmlStringRGB(PositiveGreen);
    public static string ValueGoldHex => ColorUtility.ToHtmlStringRGB(ValueGold);

    // Each colour set aligns with a specific behaviour
    public static UIButtonColourSet InformationBlue => new UIButtonColourSet(
        normal: Hex("#7093CC"),
        highlighted: Hex("#79a0de"),
        pressed: Hex("#273f65"),
        selected: Hex("#7093CC"),
        disabled: DisabledButtonColour);

    public static UIButtonColourSet ActionTeal => new UIButtonColourSet(
        normal: Hex("#4FB3B3"),
        highlighted: Hex("#68C8C8"),
        pressed: Hex("#3C9696"),
        selected: Hex("#68C8C8"),
        disabled: DisabledButtonColour);

    public static UIButtonColourSet BuildOrange => new UIButtonColourSet(
        normal: Hex("#DEAA58"),
        highlighted: Hex("#EDBC6B"),
        pressed: Hex("#C08C44"),
        selected: Hex("#DEAA58"),
        disabled: DisabledButtonColour);

    public static UIButtonColourSet SuccessGreen => new UIButtonColourSet(
        normal: Hex("#6BBF59"),
        highlighted: Hex("#84D672"),
        pressed: Hex("#4E9A40"),
        selected: Hex("#84D672"),
        disabled: DisabledButtonColour);

    public static UIButtonColourSet DangerRed => new UIButtonColourSet(
        normal: Hex("#C65A5A"),
        highlighted: Hex("#D97878"),
        pressed: Hex("#A94747"),
        selected: Hex("#D97878"),
        disabled: DisabledButtonColour);

    public static UIButtonColourSet DisabledGrey => new UIButtonColourSet(
        normal: DisabledButtonColour,
        highlighted: Lighten(DisabledButtonColour, 0.08f),
        pressed: Darken(DisabledButtonColour, 0.08f),
        selected: DisabledButtonColour,
        disabled: DisabledButtonColour);

    // Creates a lighter variant of a colour set
    public static UIButtonColourSet Lightened(UIButtonColourSet source)
    {
        return Lightened(source, VariantLightenAmount);
    }

    public static UIButtonColourSet Lightened(UIButtonColourSet source, float amount)
    {
        return new UIButtonColourSet(
            normal: Lighten(source.Normal, amount),
            highlighted: Lighten(source.Highlighted, amount),
            pressed: Lighten(source.Pressed, amount),
            selected: Lighten(source.Selected, amount),
            disabled: source.Disabled);
    }

    // Creates a darker variant of a colour set
    public static UIButtonColourSet Darkened(UIButtonColourSet source)
    {
        return Darkened(source, VariantDarkenAmount);
    }

    public static UIButtonColourSet Darkened(UIButtonColourSet source, float amount)
    {
        return new UIButtonColourSet(
            normal: Darken(source.Normal, amount),
            highlighted: Darken(source.Highlighted, amount),
            pressed: Darken(source.Pressed, amount),
            selected: Darken(source.Selected, amount),
            disabled: source.Disabled);
    }

    public static void ApplySelectableColours(Selectable selectable, UIButtonColourSet colours)
    {
        if (selectable == null)
            return;

        ColorBlock colorBlock = selectable.colors;
        colorBlock.colorMultiplier = 1f;
        colorBlock.fadeDuration = 0f;
        colorBlock.normalColor = colours.Normal;
        colorBlock.highlightedColor = colours.Highlighted;
        colorBlock.pressedColor = colours.Pressed;
        colorBlock.selectedColor = colours.Selected;
        colorBlock.disabledColor = colours.Disabled;
        selectable.colors = colorBlock;

        if (selectable.targetGraphic != null)
            selectable.targetGraphic.color = selectable.interactable ? colours.Normal : colours.Disabled;
    }

    public static Color Hex(string value)
    {
        return ColorUtility.TryParseHtmlString(value, out Color colour) ? colour : Color.white;
    }

    private static Color Lighten(Color colour, float amount)
    {
        return new Color(
            Mathf.Clamp01(colour.r + amount),
            Mathf.Clamp01(colour.g + amount),
            Mathf.Clamp01(colour.b + amount),
            colour.a);
    }

    private static Color Darken(Color colour, float amount)
    {
        return new Color(
            Mathf.Clamp01(colour.r - amount),
            Mathf.Clamp01(colour.g - amount),
            Mathf.Clamp01(colour.b - amount),
            colour.a);
    }
}