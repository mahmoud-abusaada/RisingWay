using System.Text;
using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

/// <summary>
/// P2-07 owner task 4.1, done as a tool: adds the "Privacy Options" button to the Settings panel
/// and wires it up. Google requires users in regulated regions to be able to change their ad
/// consent from inside the app, and the published consent form tells them to look for it.
///
/// Menu:  Rising Way / Add Privacy Options Button
/// Batch: Unity.exe -batchmode -quit -projectPath &lt;project&gt; -executeMethod PrivacyOptionsButtonTool.AddInBatch
///
/// The button is styled as a copy of the panel's Back button and placed as a new row at the
/// bottom of the "Other" section (the one holding the Tutorials toggle). At runtime
/// SettingsMenu shows it only to users in regulated regions, and grows that section and the
/// scroll content by one row only while it is shown, so everyone else sees the menu unchanged.
/// Running the tool twice does nothing the second time.
/// </summary>
public static class PrivacyOptionsButtonTool
{
    private const string SCENE = "Assets/Scenes/SampleScene.unity";
    private const string BUTTON_NAME = "PrivacyOptionsButton";
    private const string LABEL = "PRIVACY OPTIONS";   // the panel's buttons are upper case ("BACK")
    private const float ROW_HEIGHT = 120f;
    private static readonly Vector2 BUTTON_SIZE = new Vector2(560f, 96f);
    private const float FONT_SIZE = 40f;

    [MenuItem("Rising Way/Add Privacy Options Button")]
    public static void AddFromMenu()
    {
        string result = Add();
        EditorUtility.DisplayDialog("Privacy Options button", result, "OK");
    }

    public static void AddInBatch()
    {
        EditorSceneManager.OpenScene(SCENE);
        Debug.Log("[PrivacyButton] " + Add());
    }

    public static void DumpInBatch()
    {
        EditorSceneManager.OpenScene(SCENE);
        SettingsMenu menu = Object.FindAnyObjectByType<SettingsMenu>(FindObjectsInactive.Include);
        StringBuilder sb = new StringBuilder();
        Dump(menu.transform, 0, sb);
        Debug.Log("[PrivacyButton] Settings panel:\n" + sb);
    }

    private static void Dump(Transform t, int depth, StringBuilder sb)
    {
        RectTransform rt = t as RectTransform;
        sb.Append(new string(' ', depth * 2)).Append(t.name).Append(t.gameObject.activeSelf ? "" : " [inactive]");
        if (rt != null)
            sb.Append("  pos=").Append(rt.anchoredPosition).Append(" size=").Append(rt.sizeDelta)
              .Append(" anchors=").Append(rt.anchorMin).Append('-').Append(rt.anchorMax).Append(" pivot=").Append(rt.pivot);
        foreach (Component c in t.GetComponents<Component>())
            if (c is LayoutGroup || c is ContentSizeFitter || c is LayoutElement)
                sb.Append("  ").Append(c.GetType().Name);
        Button b = t.GetComponent<Button>();
        if (b != null)
        {
            sb.Append("  BUTTON(");
            for (int i = 0; i < b.onClick.GetPersistentEventCount(); i++)
                sb.Append(b.onClick.GetPersistentTarget(i) != null ? b.onClick.GetPersistentTarget(i).GetType().Name : "null")
                  .Append('.').Append(b.onClick.GetPersistentMethodName(i)).Append(' ');
            sb.Append(')');
        }
        TMP_Text tmp = t.GetComponent<TMP_Text>();
        if (tmp != null)
            sb.Append("  TEXT=\"").Append(tmp.text).Append("\" size=").Append(tmp.fontSize);
        sb.Append('\n');
        if (depth < 6)
            foreach (Transform c in t)
                Dump(c, depth + 1, sb);
    }

    private static string Add()
    {
        SettingsMenu menu = Object.FindAnyObjectByType<SettingsMenu>(FindObjectsInactive.Include);
        if (menu == null)
            return "No SettingsMenu found. Open SampleScene first.";

        SerializedObject so = new SerializedObject(menu);
        SerializedProperty buttonField = so.FindProperty("privacyOptionsButton");
        if (buttonField.objectReferenceValue != null)
            return "Already done: SettingsMenu already has a Privacy Options button (" +
                   buttonField.objectReferenceValue.name + ").";

        Button back = FindButton(menu.transform, "Back");
        Toggle tutorials = (Toggle)so.FindProperty("tutorialsToggle").objectReferenceValue;
        if (back == null || tutorials == null)
            return "Could not find the Back button or the Tutorials toggle in the Settings panel. " +
                   "Add the button by hand (docs/owner-actions.md, 4.1).";

        RectTransform section = (RectTransform)tutorials.transform.parent;   // "Other"
        RectTransform content = section.parent as RectTransform;             // scroll content
        if (content != null && (content.GetComponent<LayoutGroup>() != null || content.GetComponent<ContentSizeFitter>() != null))
            content = null; // a layout component already sizes it

        GameObject copy = Object.Instantiate(back.gameObject, section);
        copy.name = BUTTON_NAME;
        Undo.RegisterCreatedObjectUndo(copy, "Add Privacy Options button");
        copy.SetActive(false); // SettingsMenu decides at runtime

        // A new row directly under the section's current bottom edge.
        RectTransform rect = (RectTransform)copy.transform;
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 1f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = BUTTON_SIZE;
        rect.anchoredPosition = new Vector2(0f, -(section.rect.height + ROW_HEIGHT * 0.5f));

        foreach (TMP_Text t in copy.GetComponentsInChildren<TMP_Text>(true))
        {
            t.text = LABEL;
            t.fontSize = FONT_SIZE;
        }

        Button button = copy.GetComponent<Button>();
        Object soundTarget = FindListenerTarget(back, "PlayBack");
        button.onClick = new Button.ButtonClickedEvent();
        UnityEventTools.AddVoidPersistentListener(button.onClick, new UnityAction(menu.OpenPrivacyOptions));
        if (soundTarget is SoundManager)
            UnityEventTools.AddVoidPersistentListener(button.onClick, new UnityAction(((SoundManager)soundTarget).PlayMenu));

        buttonField.objectReferenceValue = button;
        so.FindProperty("privacyOptionsSection").objectReferenceValue = section;
        so.FindProperty("privacyOptionsScrollContent").objectReferenceValue = content;
        so.FindProperty("privacyOptionsRowHeight").floatValue = ROW_HEIGHT;
        so.ApplyModifiedProperties();

        EditorSceneManager.MarkSceneDirty(menu.gameObject.scene);
        bool saved = EditorSceneManager.SaveScene(menu.gameObject.scene);

        return "Added '" + LABEL + "' as a new row in the '" + section.name + "' section of Settings, wired to " +
               "SettingsMenu.OpenPrivacyOptions." + (saved ? " Scene saved." : " Save the scene.") +
               " It only appears for users in regions that require it.";
    }

    private static Button FindButton(Transform root, string settingsMenuMethod)
    {
        foreach (Button b in root.GetComponentsInChildren<Button>(true))
            if (FindListenerTarget(b, settingsMenuMethod) is SettingsMenu)
                return b;
        return null;
    }

    private static Object FindListenerTarget(Button b, string method)
    {
        for (int i = 0; i < b.onClick.GetPersistentEventCount(); i++)
            if (b.onClick.GetPersistentMethodName(i) == method)
                return b.onClick.GetPersistentTarget(i);
        return null;
    }
}
