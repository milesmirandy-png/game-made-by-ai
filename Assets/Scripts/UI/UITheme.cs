using System.Collections.Generic;
using UnityEngine;

namespace Swat
{
    // The game's look (dark navy, black, muted blue and gray, with restrained
    // emergency colors) and the small set of drawing helpers every screen
    // uses. All UI is drawn with Unity's immediate-mode GUI in a virtual
    // 1080-pixel-high space that scales with the window.
    public static class UITheme
    {
        public static readonly Color Background = new Color(0.035f, 0.05f, 0.08f);
        public static readonly Color PanelColor = new Color(0.04f, 0.06f, 0.1f, 0.92f);
        public static readonly Color PanelLight = new Color(0.08f, 0.11f, 0.17f, 0.95f);
        public static readonly Color PanelHover = new Color(0.12f, 0.17f, 0.26f, 0.97f);
        public static readonly Color Line = new Color(0.2f, 0.28f, 0.4f, 1f);
        public static readonly Color Accent = new Color(0.36f, 0.62f, 0.95f);
        public static readonly Color AccentDim = new Color(0.18f, 0.32f, 0.55f);
        public static readonly Color TextColor = new Color(0.88f, 0.91f, 0.95f);
        public static readonly Color Dim = new Color(0.58f, 0.64f, 0.72f);
        public static readonly Color Faint = new Color(0.38f, 0.43f, 0.5f);
        // Status colors. The colorblind-friendly palette swaps red/green for orange/blue.
        public static Color Good { get { return SaveManager.Settings.colorblindMode ? new Color(0.35f, 0.65f, 1f) : new Color(0.4f, 0.85f, 0.55f); } }
        public static Color Warn { get { return SaveManager.Settings.colorblindMode ? new Color(1f, 0.92f, 0.35f) : new Color(1f, 0.76f, 0.3f); } }
        public static Color Bad { get { return SaveManager.Settings.colorblindMode ? new Color(1f, 0.55f, 0.1f) : new Color(0.95f, 0.35f, 0.3f); } }

        public static readonly string[] CrosshairColorNames = { "White", "Green", "Cyan", "Yellow", "Magenta" };
        public static readonly Color[] CrosshairColors = { Color.white, new Color(0.4f, 1f, 0.45f), new Color(0.35f, 0.95f, 1f), new Color(1f, 0.95f, 0.3f), new Color(1f, 0.4f, 0.95f) };
        public static readonly Color AlertRed = new Color(0.9f, 0.15f, 0.12f);
        public static readonly Color AlertBlue = new Color(0.2f, 0.4f, 1f);

        // UI scale setting on top of the resolution-based scale.
        // (Capped so the layout always has at least 1440 virtual pixels of width.)
        public static float Scale
        {
            get
            {
                float scale = Mathf.Max(0.5f, Screen.height / 1080f) * Mathf.Clamp(SaveManager.Settings.uiScale, 0.75f, 1.5f);
                return Mathf.Max(0.4f, Mathf.Min(scale, Screen.width / 1440f));
            }
        }
        public static float Width { get { return Screen.width / Scale; } }
        public static float Height { get { return Screen.height / Scale; } }

        static readonly Dictionary<int, GUIStyle> styles = new Dictionary<int, GUIStyle>();
        static Texture2D circle, gradient;
        static GUIStyle invisible;

        public static Texture2D CircleTexture
        {
            get
            {
                if (circle == null) circle = MakeCircle(64);
                return circle;
            }
        }

        // Dark screen edges, used as the vignette when the Built-in post-processing isn't available (URP).
        public static Texture2D Vignette
        {
            get
            {
                if (vignette == null)
                {
                    const int size = 64;
                    vignette = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, hideFlags = HideFlags.DontSave };
                    for (int y = 0; y < size; y++)
                        for (int x = 0; x < size; x++)
                        {
                            float d = Vector2.Distance(new Vector2((x + 0.5f) / size, (y + 0.5f) / size), new Vector2(0.5f, 0.5f)) * 1.414f;
                            float a = Mathf.Clamp01((d - 0.55f) / 0.45f);
                            vignette.SetPixel(x, y, new Color(0f, 0f, 0f, a * a));
                        }
                    vignette.Apply();
                }
                return vignette;
            }
        }

        static Texture2D vignette;

        public static Texture2D Gradient
        {
            get
            {
                if (gradient == null)
                {
                    gradient = new Texture2D(1, 32, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, hideFlags = HideFlags.DontSave };
                    for (int y = 0; y < 32; y++) gradient.SetPixel(0, y, new Color(1f, 1f, 1f, y / 31f));
                    gradient.Apply();
                }
                return gradient;
            }
        }

        static Texture2D MakeCircle(int size)
        {
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, hideFlags = HideFlags.DontSave };
            float r = size * 0.5f;
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(r, r));
                    texture.SetPixel(x, y, new Color(1f, 1f, 1f, Mathf.Clamp01(r - d)));
                }
            texture.Apply();
            return texture;
        }

        // Call at the start of OnGUI.
        public static void Begin()
        {
            float s = Scale;
            GUI.matrix = Matrix4x4.Scale(new Vector3(s, s, 1f));
            if (invisible == null) invisible = new GUIStyle();
        }

        public static GUIStyle Style(int size, TextAnchor anchor, bool bold, bool wrap = true)
        {
            size = Mathf.RoundToInt(size * Mathf.Clamp(SaveManager.Settings.textSize, 0.85f, 1.4f));
            int key = size * 1000 + (int)anchor * 10 + (bold ? 1 : 0) + (wrap ? 5000000 : 0);
            GUIStyle style;
            if (!styles.TryGetValue(key, out style) || style == null)
            {
                style = new GUIStyle(GUI.skin.label)
                {
                    fontSize = size,
                    alignment = anchor,
                    fontStyle = bold ? FontStyle.Bold : FontStyle.Normal,
                    wordWrap = wrap,
                    richText = true,
                    clipping = TextClipping.Clip,
                };
                style.padding = new RectOffset(0, 0, 0, 0);
                style.normal.textColor = Color.white;
                styles[key] = style;
            }
            return style;
        }

        // ---- Primitives ----

        public static void Fill(Rect rect, Color color)
        {
            if (Event.current.type != EventType.Repaint) return;
            var old = GUI.color;
            GUI.color = color;
            GUI.DrawTexture(rect, Texture2D.whiteTexture);
            GUI.color = old;
        }

        public static void Frame(Rect rect, Color color, float thickness = 1f)
        {
            Fill(new Rect(rect.x, rect.y, rect.width, thickness), color);
            Fill(new Rect(rect.x, rect.yMax - thickness, rect.width, thickness), color);
            Fill(new Rect(rect.x, rect.y, thickness, rect.height), color);
            Fill(new Rect(rect.xMax - thickness, rect.y, thickness, rect.height), color);
        }

        public static void Shade(Rect rect, Color color, bool upward)
        {
            if (Event.current.type != EventType.Repaint) return;
            var old = GUI.color;
            GUI.color = color;
            if (upward) GUI.DrawTexture(rect, Gradient);
            else GUI.DrawTextureWithTexCoords(rect, Gradient, new Rect(0f, 1f, 1f, -1f));
            GUI.color = old;
        }

        public static void Dot(Vector2 center, float radius, Color color)
        {
            if (Event.current.type != EventType.Repaint) return;
            var old = GUI.color;
            GUI.color = color;
            GUI.DrawTexture(new Rect(center.x - radius, center.y - radius, radius * 2f, radius * 2f), CircleTexture);
            GUI.color = old;
        }

        // A ring drawn as one textured quad (textures cached per thickness ratio).
        public static void Ring(Vector2 center, float radius, Color color, float thickness)
        {
            if (Event.current.type != EventType.Repaint || radius <= 0.5f) return;
            int ratio = Mathf.Clamp(Mathf.RoundToInt(thickness / radius * 20f), 1, 10);
            Texture2D texture;
            if (!rings.TryGetValue(ratio, out texture) || texture == null)
            {
                texture = MakeRing(64, ratio / 20f);
                rings[ratio] = texture;
            }
            var old = GUI.color;
            GUI.color = color;
            float r = radius + thickness * 0.5f;
            GUI.DrawTexture(new Rect(center.x - r, center.y - r, r * 2f, r * 2f), texture);
            GUI.color = old;
        }

        static readonly Dictionary<int, Texture2D> rings = new Dictionary<int, Texture2D>();

        static Texture2D MakeRing(int size, float thicknessRatio)
        {
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, hideFlags = HideFlags.DontSave };
            float outer = size * 0.5f;
            float width = outer * thicknessRatio / (1f + thicknessRatio * 0.5f);
            float inner = outer - width;
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(outer, outer));
                    float a = Mathf.Clamp01(outer - d) * Mathf.Clamp01(d - inner);
                    texture.SetPixel(x, y, new Color(1f, 1f, 1f, a));
                }
            texture.Apply();
            return texture;
        }

        public static void LineTo(Vector2 a, Vector2 b, Color color, float thickness)
        {
            if (Event.current.type != EventType.Repaint) return;
            Vector2 d = b - a;
            float length = d.magnitude;
            if (length < 0.5f) return;
            var matrix = GUI.matrix;
            float angle = Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg;
            // Rotate about 'a' in virtual space, then apply the UI scale.
            Vector3 pivot = new Vector3(a.x, a.y, 0f);
            GUI.matrix = matrix * Matrix4x4.TRS(pivot, Quaternion.Euler(0f, 0f, angle), Vector3.one) * Matrix4x4.TRS(-pivot, Quaternion.identity, Vector3.one);
            Fill(new Rect(a.x, a.y - thickness * 0.5f, length, thickness), color);
            GUI.matrix = matrix;
        }

        public static void Text(Rect rect, string text, int size, Color color, TextAnchor anchor = TextAnchor.UpperLeft, bool bold = false)
        {
            if (string.IsNullOrEmpty(text)) return;
            var style = Style(size, anchor, bold);
            var old = GUI.color;
            GUI.color = color;
            GUI.Label(rect, text, style);
            GUI.color = old;
        }

        public static void ShadowText(Rect rect, string text, int size, Color color, TextAnchor anchor = TextAnchor.UpperLeft, bool bold = false)
        {
            Text(new Rect(rect.x + 1.5f, rect.y + 1.5f, rect.width, rect.height), text, size, new Color(0f, 0f, 0f, color.a * 0.85f), anchor, bold);
            Text(rect, text, size, color, anchor, bold);
        }

        public static float TextHeight(string text, int size, float width)
        {
            return Style(size, TextAnchor.UpperLeft, false).CalcHeight(new GUIContent(text), width);
        }

        public static void Panel(Rect rect, bool accentTop = true)
        {
            Fill(rect, PanelColor);
            Frame(rect, new Color(Line.r, Line.g, Line.b, 0.6f));
            if (accentTop) Fill(new Rect(rect.x, rect.y, rect.width, 2f), Accent);
        }

        public static void Header(Rect rect, string title, string subtitle = null)
        {
            Text(new Rect(rect.x, rect.y, rect.width, 40f), title.ToUpperInvariant(), 30, TextColor, TextAnchor.UpperLeft, true);
            Fill(new Rect(rect.x, rect.y + 42f, 70f, 3f), Accent);
            if (!string.IsNullOrEmpty(subtitle)) Text(new Rect(rect.x + 84f, rect.y + 34f, rect.width - 84f, 22f), subtitle, 16, Dim);
        }

        public static void Bar(Rect rect, float fraction, Color color)
        {
            Fill(rect, new Color(0f, 0f, 0f, 0.5f));
            Fill(new Rect(rect.x, rect.y, rect.width * Mathf.Clamp01(fraction), rect.height), color);
        }

        public static void StatBar(Rect rect, string label, float fraction, string value)
        {
            Text(new Rect(rect.x, rect.y, rect.width * 0.38f, rect.height), label, 15, Dim, TextAnchor.MiddleLeft);
            Bar(new Rect(rect.x + rect.width * 0.4f, rect.y + rect.height * 0.35f, rect.width * 0.42f, rect.height * 0.3f), fraction, Accent);
            Text(new Rect(rect.x + rect.width * 0.84f, rect.y, rect.width * 0.16f, rect.height), value, 15, TextColor, TextAnchor.MiddleRight);
        }

        public static bool Hover(Rect rect)
        {
            return GUI.enabled && rect.Contains(Event.current.mousePosition);
        }

        public static bool Button(Rect rect, string label, bool enabled = true, bool selected = false, int size = 19)
        {
            bool hover = enabled && Hover(rect);
            Color fill = selected ? new Color(AccentDim.r, AccentDim.g, AccentDim.b, 0.95f) : hover ? PanelHover : PanelLight;
            Fill(rect, fill);
            Frame(rect, selected ? Accent : hover ? new Color(Accent.r, Accent.g, Accent.b, 0.8f) : new Color(Line.r, Line.g, Line.b, 0.7f));
            if (hover || selected) Fill(new Rect(rect.x, rect.y, 3f, rect.height), Accent);
            Text(new Rect(rect.x + 12f, rect.y, rect.width - 20f, rect.height), label, size, enabled ? TextColor : Faint, TextAnchor.MiddleLeft, selected);
            if (!enabled) return false;
            if (GUI.Button(rect, GUIContent.none, invisible))
            {
                AudioManager.Ui(Sound.UiSelect, 0.45f);
                return true;
            }
            return false;
        }

        // A button with a second line of small text.
        public static bool BigButton(Rect rect, string label, string detail, bool enabled = true)
        {
            bool clicked = Button(rect, string.Empty, enabled);
            Text(new Rect(rect.x + 16f, rect.y + 6f, rect.width - 24f, 28f), label, 22, enabled ? TextColor : Faint, TextAnchor.UpperLeft, true);
            if (!string.IsNullOrEmpty(detail)) Text(new Rect(rect.x + 16f, rect.y + rect.height - 24f, rect.width - 24f, 20f), detail, 14, enabled ? Dim : Faint, TextAnchor.UpperLeft);
            return clicked;
        }

        public static bool Toggle(Rect rect, string label, bool value)
        {
            bool hover = Hover(rect);
            Rect box = new Rect(rect.x, rect.y + (rect.height - 22f) * 0.5f, 22f, 22f);
            Fill(box, hover ? PanelHover : PanelLight);
            Frame(box, hover ? Accent : Line);
            if (value) Fill(new Rect(box.x + 5f, box.y + 5f, 12f, 12f), Accent);
            Text(new Rect(rect.x + 32f, rect.y, rect.width - 32f, rect.height), label, 17, TextColor, TextAnchor.MiddleLeft);
            if (GUI.Button(rect, GUIContent.none, invisible))
            {
                AudioManager.Ui(Sound.UiSelect, 0.4f);
                return !value;
            }
            return value;
        }

        public static float Slider(Rect rect, string label, float value, float min, float max, string valueText)
        {
            Text(new Rect(rect.x, rect.y, rect.width * 0.36f, rect.height), label, 17, TextColor, TextAnchor.MiddleLeft);
            Rect track = new Rect(rect.x + rect.width * 0.38f, rect.y + rect.height * 0.5f - 3f, rect.width * 0.46f, 6f);
            Fill(track, new Color(0f, 0f, 0f, 0.6f));
            float t = Mathf.InverseLerp(min, max, value);
            Fill(new Rect(track.x, track.y, track.width * t, track.height), Accent);
            Dot(new Vector2(track.x + track.width * t, track.center.y), 9f, TextColor);
            Text(new Rect(rect.x + rect.width * 0.86f, rect.y, rect.width * 0.14f, rect.height), valueText, 16, Dim, TextAnchor.MiddleRight);

            Rect hit = new Rect(track.x - 8f, rect.y, track.width + 16f, rect.height);
            var e = Event.current;
            if (!GUI.enabled) return value;
            int id = GUIUtility.GetControlID(FocusType.Passive, hit);
            if (e.type == EventType.MouseDown && hit.Contains(e.mousePosition) && e.button == 0)
            {
                GUIUtility.hotControl = id;
                e.Use();
            }
            if (GUIUtility.hotControl == id)
            {
                if (e.type == EventType.MouseDrag || e.type == EventType.MouseDown || e.type == EventType.MouseUp)
                {
                    value = Mathf.Lerp(min, max, Mathf.InverseLerp(track.x, track.xMax, e.mousePosition.x));
                    GUI.changed = true;
                    if (e.type == EventType.MouseUp) GUIUtility.hotControl = 0;
                    e.Use();
                }
            }
            return value;
        }

        // Left/right arrows around a value.
        public static int Stepper(Rect rect, string label, int index, string[] options)
        {
            Text(new Rect(rect.x, rect.y, rect.width * 0.36f, rect.height), label, 17, TextColor, TextAnchor.MiddleLeft);
            float x = rect.x + rect.width * 0.38f;
            float w = rect.width * 0.62f;
            if (Button(new Rect(x, rect.y + 2f, 34f, rect.height - 4f), "<", true, false, 18)) index = (index - 1 + options.Length) % options.Length;
            Text(new Rect(x + 40f, rect.y, w - 80f, rect.height), options[Mathf.Clamp(index, 0, options.Length - 1)], 17, TextColor, TextAnchor.MiddleCenter, true);
            if (Button(new Rect(x + w - 34f, rect.y + 2f, 34f, rect.height - 4f), ">", true, false, 18)) index = (index + 1) % options.Length;
            return index;
        }

        public static string KeyFor(InputAction action)
        {
            return GameInput.KeyName(GameInput.Binding(action));
        }

        // Converts a world position to virtual GUI coordinates. Returns false if behind the camera.
        public static bool WorldToGui(Camera cam, Vector3 world, out Vector2 gui)
        {
            Vector3 v = cam.WorldToViewportPoint(world);
            gui = new Vector2(v.x * Width, (1f - v.y) * Height);
            return v.z > 0f;
        }

        public static Color RoleColor(OfficerRole role)
        {
            switch (role)
            {
                case OfficerRole.Leader: return new Color(0.36f, 0.62f, 0.95f);
                case OfficerRole.Shield: return new Color(0.6f, 0.65f, 0.75f);
                case OfficerRole.Breacher: return new Color(0.95f, 0.65f, 0.25f);
                case OfficerRole.Medic: return new Color(0.4f, 0.85f, 0.55f);
                case OfficerRole.Recon: return new Color(0.7f, 0.5f, 0.95f);
                default: return new Color(0.95f, 0.85f, 0.35f);
            }
        }

        public static string RoleName(OfficerRole role)
        {
            switch (role)
            {
                case OfficerRole.Leader: return "Team Leader";
                case OfficerRole.Shield: return "Shield Officer";
                case OfficerRole.Breacher: return "Breacher";
                case OfficerRole.Medic: return "Medic";
                case OfficerRole.Recon: return "Recon Officer";
                default: return "Tactical Officer";
            }
        }

        // Role icon: a colored disc with a simple glyph.
        public static void RoleIcon(Rect rect, OfficerRole role)
        {
            var color = RoleColor(role);
            Vector2 c = rect.center;
            float r = Mathf.Min(rect.width, rect.height) * 0.5f;
            Dot(c, r, new Color(0.03f, 0.04f, 0.07f, 0.95f));
            Ring(c, r - 1f, color, 2f);
            float u = r * 0.18f;
            switch (role)
            {
                case OfficerRole.Leader: // chevrons
                    LineTo(c + new Vector2(-3f * u, 0f), c + new Vector2(0f, -2.5f * u), color, u);
                    LineTo(c + new Vector2(0f, -2.5f * u), c + new Vector2(3f * u, 0f), color, u);
                    LineTo(c + new Vector2(-3f * u, 2.5f * u), c + new Vector2(0f, 0f), color, u);
                    LineTo(c, c + new Vector2(3f * u, 2.5f * u), color, u);
                    break;
                case OfficerRole.Shield:
                    Fill(new Rect(c.x - 2.5f * u, c.y - 3f * u, 5f * u, 4.5f * u), color);
                    Dot(c + new Vector2(0f, 1.5f * u), 2.5f * u, color);
                    break;
                case OfficerRole.Breacher: // door with burst
                    Frame(new Rect(c.x - 2f * u, c.y - 3f * u, 4f * u, 6f * u), color, u * 0.8f);
                    Dot(c + new Vector2(0.8f * u, 0f), u * 0.6f, color);
                    break;
                case OfficerRole.Medic: // cross
                    Fill(new Rect(c.x - u, c.y - 3f * u, 2f * u, 6f * u), color);
                    Fill(new Rect(c.x - 3f * u, c.y - u, 6f * u, 2f * u), color);
                    break;
                case OfficerRole.Recon: // eye
                    Ring(c, 2.6f * u, color, u * 0.8f);
                    Dot(c, u * 1.1f, color);
                    break;
                default: // bolt
                    LineTo(c + new Vector2(1.5f * u, -3f * u), c + new Vector2(-1.5f * u, 0.3f * u), color, u);
                    LineTo(c + new Vector2(-1.5f * u, 0.3f * u), c + new Vector2(1.5f * u, -0.3f * u), color, u);
                    LineTo(c + new Vector2(1.5f * u, -0.3f * u), c + new Vector2(-1.5f * u, 3f * u), color, u);
                    break;
            }
        }
    }
}
