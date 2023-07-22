using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

[CustomEditor(typeof(JSON_io))]
public class TestEditor : Editor
{
    public override VisualElement CreateInspectorGUI()
    {
        // Create a new VisualElement to be the root of our inspector UI
        VisualElement myInspector = new VisualElement();

        myInspector.Add(new Button(() => { ((JSON_io)target).Export(); }) { text = "Export To File" });

        VisualTreeAsset visualTree = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>("Assets/Scripts/Editor/JSON_io_thing.uxml");
        visualTree.CloneTree(myInspector);

        // Return the finished inspector UI
        return myInspector;
    }
}
