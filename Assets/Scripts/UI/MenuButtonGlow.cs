using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// The red-rimmed plate (MenuButton_Red) shown over a menu button while the mouse is on it, it is
// pressed or it is selected. Its metal plate is drawn a little bigger than the button's own,
// with the red glow around it. The picture is 9-sliced so it stretches to the button's shape
// without squashing the rivets. Works out its size from the button, so buttons can be resized.
[RequireComponent(typeof(Button))]
public class MenuButtonGlow : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, ISelectHandler, IDeselectHandler
{
    public Sprite glow;
    [Tooltip("How much bigger the red plate is than the plain one (1 = same size)")]
    public float grow = 1.06f;

    // where the metal plate sits in the pictures (pixels, of 2172 x 724)
    static readonly Rect PlainPlate = Rect.MinMaxRect(67f, 184f, 2105f, 537f);
    static readonly Rect GlowPlate = Rect.MinMaxRect(127f, 250f, 2044f, 482f);
    const float PictureWidth = 2172f;

    Image hover;
    bool pointerOver, selected;

    void Awake()
    {
        var button = GetComponent<Button>();
        button.transition = Selectable.Transition.None;   // the glow replaces the sprite swap

        var go = new GameObject("Glow", typeof(RectTransform), typeof(Image));
        go.transform.SetParent(transform, false);
        go.transform.SetSiblingIndex(0);                   // over the button, under its label
        hover = go.GetComponent<Image>();
        hover.sprite = glow;
        hover.type = Image.Type.Sliced;
        hover.raycastTarget = false;
        hover.enabled = false;
    }

    void OnRectTransformDimensionsChange() => Layout();
    void Start() => Layout();

    void Layout()
    {
        if (hover == null)
            return;
        var size = ((RectTransform)transform).rect.size;
        if (size.x <= 0f)
            return;
        float k = size.x / PictureWidth;                   // screen units per picture pixel
        hover.pixelsPerUnitMultiplier = 1f / k;            // corners keep the picture's scale

        // the plain plate's rectangle on the button, grown...
        Vector2 center = (PlainPlate.center - new Vector2(PictureWidth, 724f) / 2f) * k;
        Vector2 plate = PlainPlate.size * k * grow;
        // ...plus the glow picture's margins around its own plate
        float left = GlowPlate.xMin * k, right = (PictureWidth - GlowPlate.xMax) * k;
        float bottom = (724f - GlowPlate.yMax) * k, top = GlowPlate.yMin * k;
        var rt = hover.rectTransform;
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = new Vector2(plate.x + left + right, plate.y + top + bottom);
        rt.anchoredPosition = new Vector2(center.x, -center.y);
    }

    void Refresh() => hover.enabled = pointerOver || selected;

    public void OnPointerEnter(PointerEventData e) { pointerOver = true; Refresh(); }
    public void OnPointerExit(PointerEventData e) { pointerOver = false; Refresh(); }
    public void OnSelect(BaseEventData e) { selected = true; Refresh(); }
    public void OnDeselect(BaseEventData e) { selected = false; Refresh(); }

    void OnDisable()
    {
        pointerOver = selected = false;
        if (hover != null) hover.enabled = false;
    }
}
