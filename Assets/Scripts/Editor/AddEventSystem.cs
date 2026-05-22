using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
public class AddEventSystem {
    [MenuItem("Tools/Add Event System")]
    public static void Add() {
        if (Object.FindObjectOfType<EventSystem>() == null) {
            var es = new GameObject("EventSystem");
            es.AddComponent<EventSystem>();
            es.AddComponent<StandaloneInputModule>();
        }
    }
}
