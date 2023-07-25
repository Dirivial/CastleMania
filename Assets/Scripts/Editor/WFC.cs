using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Search;
using UnityEngine;
using UnityEngine.UIElements;

[CustomEditor(typeof(WFC))]
public class WFCEditor : Editor
{
    public override VisualElement CreateInspectorGUI()
    {
        // Create a new VisualElement to be the root of our inspector UI
        VisualElement myInspector = new VisualElement();

        myInspector.Add(new Vector3IntField() { bindingPath = "dimensions" });
        myInspector.Add(new Vector3IntField() { bindingPath = "tileScaling" });
        myInspector.Add(new IntegerField() { bindingPath = "tileSize" });
        myInspector.Add(new ObjectField() { bindingPath = "XML_IO" });

        myInspector.Add(new Button(() => { ((WFC)target).GenerateFull(); }) { text = "Generate Full" });
        myInspector.Add(new Button(() => { ((WFC)target).TakeStep(); }) { text = "Step" });
        myInspector.Add(new Button(() => { ((WFC)target).Clear(); }) { text = "Clear" });

        // Return the finished inspector UI
        return myInspector;
    }
}
