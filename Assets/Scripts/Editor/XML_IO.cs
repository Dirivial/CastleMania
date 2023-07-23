using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

[CustomEditor(typeof(XML_IO))]
public class TestEditor : Editor
{
    public override VisualElement CreateInspectorGUI()
    {
        // Create a new VisualElement to be the root of our inspector UI
        VisualElement myInspector = new VisualElement();

        myInspector.Add(new Button(() => { ((XML_IO)target).Export(); }) { text = "Export To File" });
        myInspector.Add(new Button(() => { ((XML_IO)target).Import(); }) { text = "Import Tile Types" });
        myInspector.Add(new Button(() => { ((XML_IO)target).ClearTileTypes(); }) { text = "Clear Tile Types" });

        VisualTreeAsset visualTree = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>("Assets/Scripts/Editor/JSON_io_thing.uxml");
        visualTree.CloneTree(myInspector);

        // Return the finished inspector UI
        return myInspector;
    }
}
