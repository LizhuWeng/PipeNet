using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System;
using System.Linq;
using System.IO;

namespace PipeNet
{
    [CustomEditor(typeof(NetComponent))]
    public class NetComponentEditor : Editor
    {
        NetComponent net;
        static bool acceptInput = false;
        bool showUVOptions = false;

        #region Constant
        const float HANDLE_SCALE = 0.3f;

        const float MIN_LINE_WIDTH = 0.3f;
        const float MAX_LINE_WIDTH = 20f;

        const float MIN_LINE_SCALE = 1f;
        const float MAX_LINE_SCALE = 10f;

        const float MIN_GROUND_OFFSET = 0.001f;
        const float MAX_GROUND_OFFSET = 1f;
        #endregion

        EditMode editmode;
        int lastAddNodeID = -1;
        Handles.DrawCapFunction[] caps = new Handles.DrawCapFunction[3] { Handles.SphereCap, Handles.CubeCap, Handles.CylinderCap };

        #region Shortcut
        public bool earlyOut
        {
            get
            {
                return (
                    Event.current.alt ||Tools.current == Tool.View ||GUIUtility.hotControl > 0 ||
                    (Event.current.isMouse ? Event.current.button > 1 : false) ||
                    Tools.viewTool == ViewTool.FPS ||Tools.viewTool == ViewTool.Orbit);
            }
        }
        #endregion

        #region Initalization
        [MenuItem("GameObject/Create Other/PipeNet %#n")]
        public static void Init()
        {
            // create new
            var go = new GameObject();
            var n = go.AddComponent<NetComponent>();
            n.mainMat = AssetDatabase.LoadAssetAtPath<Material>(PipeNetUtils.RootAssetPath + "/Assets/Materials/WaterFlow.mat");
            n.blockMat = AssetDatabase.LoadAssetAtPath<Material>(PipeNetUtils.RootAssetPath + "/Assets/Materials/Block.mat");
            n.pipeLineMat = AssetDatabase.LoadAssetAtPath<Material>(PipeNetUtils.RootAssetPath + "/Assets/Materials/PipeLine.mat");
            go.name = "PipeNet " + go.GetInstanceID();
            go.AddComponent<MeshFilter>();
            go.AddComponent<MeshCollider>();

            //renderer setting
            var renderer = go.AddComponent<MeshRenderer>();
            renderer.receiveShadows = false;
            renderer.useLightProbes = false;
            renderer.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

            go.transform.position = Vector3.zero;
            Selection.activeObject = go;

            // create a horizontal plane (Collider)
            var plane = GameObject.Find("Horizontal Plane");
            if(plane == null)
            {
                plane = GameObject.CreatePrimitive(PrimitiveType.Plane);
                plane.transform.position = Vector3.zero;
                plane.transform.localScale = new Vector3(20f,1f,20f);
                plane.GetComponent<MeshRenderer>().material = AssetDatabase.LoadAssetAtPath<Material>(PipeNetUtils.RootAssetPath + "/Assets/Materials/PipeNetPlane.mat");
                plane.name = "Horizontal Plane";
            }
            acceptInput = true;
        }

        public void OnEnable()
        {
            net = (NetComponent)target;
            lastAddNodeID = -1;
        }
        #endregion

        #region Draw GUI
        public override void OnInspectorGUI()
        {
            var titleLabelStyle = new GUIStyle(GUI.skin.label);
            titleLabelStyle.fontSize = 16;
            titleLabelStyle.fontStyle = FontStyle.Bold;

            // XML import and export
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Import", GUILayout.Height(20f)))
            {
                string xmlpath = EditorUtility.OpenFilePanel("Import Data from XML", PipeNetUtils.RootAssetPath, "xml");
                if (xmlpath != "")
                {
                    net.data.FromXML(xmlpath);
                    while(net.transform.childCount > 0)
                    {
                        DestroyImmediate(net.transform.GetChild(0).gameObject);
                    }
                    net.Refresh(false, true);
                }  
            }
            if (GUILayout.Button("Export", GUILayout.Height(20f)))
            {
                ExportXML();
            }
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.HelpBox("Remember always saving to XML data to avoid crashing.", MessageType.Warning);

            GUI.changed = false;

            // Global Setting
            GUILayout.Label("Global Setting", titleLabelStyle);

            // Show flow line
            var showFlowLine = net.showFlowLine;
            net.showFlowLine = EditorGUILayout.Toggle("Show Flow Line", net.showFlowLine);
            if (showFlowLine != net.showFlowLine)
            {
                net.ShowFlowLine(net.showFlowLine);
            }
            // switch showing signs
            var showSign = net.showSign;
            net.showSign = EditorGUILayout.Toggle("Show Sign", net.showSign);
            if (showSign != net.showSign)
            {
                var nodeSignRoot = net.NodeSignRoot;
                for (int i = 0; i < nodeSignRoot.childCount; i++)
                {
                    nodeSignRoot.GetChild(i).gameObject.SetActive(net.showSign);
                }
            }

            net.attachToGround = EditorGUILayout.Toggle("Attach To Ground", net.attachToGround);
            net.lineWidth = EditorGUILayout.Slider("Line Width", net.lineWidth, MIN_LINE_WIDTH, MAX_LINE_WIDTH);

            //line objects' scale
            var pipeLineScale = net.pipeLineScale;
            net.pipeLineScale = EditorGUILayout.Slider("Pipe Scale", net.pipeLineScale, MIN_LINE_SCALE, MAX_LINE_SCALE);
            if (pipeLineScale != net.pipeLineScale)
                net.UpdateLineScale();

            //show UV options
            showUVOptions = EditorGUILayout.Foldout(showUVOptions, "UV Options");
            if (showUVOptions)
            {
                net.swapUV = EditorGUILayout.Toggle("Swap UV", net.swapUV);
                net.flipU = EditorGUILayout.Toggle("Flip U", net.flipU);
                net.flipV = EditorGUILayout.Toggle("Flip V", net.flipV);

                net.uvScale = EditorGUILayout.Vector2Field("Scale", net.uvScale);
                net.uvOffset = EditorGUILayout.Vector2Field("Offset", net.uvOffset);
            }

            net.mainMat = (Material)EditorGUILayout.ObjectField("Main Material", net.mainMat, typeof(Material), true);
            net.blockMat = (Material)EditorGUILayout.ObjectField("Block Material", net.blockMat, typeof(Material), true);
            net.pipeLineMat = (Material)EditorGUILayout.ObjectField("Pipe Material", net.pipeLineMat, typeof(Material), true);

            //current selected
            var selectedNode = net.data.GetNode(net.SelectedIndex);
            if (selectedNode != null)
            {
                EditorGUILayout.TextArea("", GUI.skin.horizontalSlider);

                GUILayout.BeginHorizontal();
                GUILayout.Label("Current Node :", titleLabelStyle);
                GUILayout.Label("Node" + selectedNode.id);
                GUILayout.EndHorizontal();

                EditorGUILayout.BeginHorizontal();
                var nodeType = selectedNode.type;
                selectedNode.type = (NodeType)ShowToolBar<NodeType>((int)selectedNode.type);
                if (nodeType != selectedNode.type)
                    net.UpdateNodeSign(selectedNode, true);
                EditorGUILayout.EndHorizontal();
            }

            EditorGUILayout.TextArea("", GUI.skin.horizontalSlider);
            GUILayout.Label("Edit Mode", titleLabelStyle);
            editmode = (EditMode)ShowToolBar<EditMode>((int)editmode, 30);
            EditorGUILayout.Separator();

            var desc = editmode == EditMode.Transform ? "Click on a plane collider to add nodes. With 'CTRL' key down do a continuous connection" : "Drag a node to the other one to connect them";
            EditorGUILayout.HelpBox(desc, MessageType.Info);

            //edit mode
            if (acceptInput)
            {
                GUI.backgroundColor = Color.red;
                if (GUILayout.Button("Edit: On", GUILayout.Height(35f)))
                {
                    acceptInput = false;
                    net.Refresh(false, true);
                    SceneView.RepaintAll();
                    return;
                }
            }
            else
            {
                GUI.backgroundColor = Color.green;
                if (GUILayout.Button("Edit: Off", GUILayout.Height(35f)))
                {
                    acceptInput = true;
                    SceneView.RepaintAll();
                }
            }

            GUI.backgroundColor = Color.white;
            if (GUI.changed)
            {
                EditorUtility.SetDirty(net);
                net.Refresh();
                SceneView.RepaintAll();
            }
        }
        #endregion

        #region Scene GUI

        Vector3 groundPoint = Vector3.zero;
        Vector3 tp;
        public void OnSceneGUI()
        {
            if (acceptInput == false)
                return;

            Event e = Event.current;
            if (e.type == EventType.ValidateCommand)
            {
                net.Refresh();
                SceneView.RepaintAll();
            }

            if (e.isKey && (e.keyCode == KeyCode.Escape || e.keyCode == KeyCode.Return))
            {
                acceptInput = false;
                SceneView.RepaintAll();
            }

            Handles.BeginGUI();
            GUILayout.Window(2, new Rect(Screen.width - 250, Screen.height - 80, 100, 60), (id) =>
            {
                var desc = editmode == EditMode.Transform ? "Click on a plane collider to add nodes" : "Drag a node to the other one to connect";
                GUILayout.Label(desc);
            }, "PipeNet");
            Handles.EndGUI();

            // Existing point handles
            DrawHandleGUI(net.SelectedIndex);
            DrawSceneNodesGUI();

            if (earlyOut)
                return;

            // New point placement from here down 
            int controlID = GUIUtility.GetControlID(FocusType.Passive);
            HandleUtility.AddDefaultControl(controlID);

            if ((e.modifiers != 0 && !e.control)|| Tools.current != Tool.Move)
            {
                if (e.type == EventType.MouseUp && e.button == 0 && Tools.current != Tool.Move && e.modifiers == 0)
                {
                    FindSceneView().ShowNotification(new GUIContent("Tool must be set to 'Move' to place points!", ""));
                    SceneView.RepaintAll();
                }
                return;
            }

            // Listen for mouse input
            if (editmode == EditMode.Transform && e.type == EventType.MouseUp && e.button == 0)
            {
                Ray ray = HandleUtility.GUIPointToWorldRay(e.mousePosition);
                RaycastHit ground = new RaycastHit();

                if (Physics.Raycast(ray.origin, ray.direction, out ground))
                {
                    groundPoint = ground.point;
                    var node = AddNode(groundPoint - net.transform.position);

                    if(e.control)  // if click with control key , we automatically connect it with last node
                    {
                        var lastnode = net.data.GetNode(lastAddNodeID);
                        if(lastnode != null)
                        {
                            net.data.ConnectNode(lastAddNodeID, node.id);
                            net.CheckLineNode(node);
                            net.CheckLineNode(lastnode);
                            net.UpdateLineNode(node);
                            net.Refresh();
                        }
                    }
                    lastAddNodeID = node.id;
                }
            }
        }

        /// <summary>
        /// Show tool bar from a Enum
        /// </summary>
        /// <typeparam name="T">Enum</typeparam>
        /// <param name="val">value</param>
        /// <param name="height">toolbar height</param>
        /// <returns></returns>
        private int ShowToolBar<T>(int val, float height = 20) where T : struct
        {
            var nodeTypes = new List<string>();
            var values = Enum.GetValues(typeof(T));
            foreach (var v in values)
            {
                nodeTypes.Add(v.ToString());
            }
            return GUILayout.Toolbar(val, nodeTypes.ToArray(), GUILayout.Height(height));
        }

        private void DrawSceneNodesGUI()
        {
            Camera sceneCamera = Camera.current;
            var allNodes = net.data.GetNodeList();
            bool dirty = false;
            foreach (var node in allNodes)
            {
                var nodePositon = node.position + net.transform.position;
                if (Vector3.Dot(sceneCamera.transform.forward, nodePositon - sceneCamera.transform.position) < 0)
                    continue;

                int selectedIndex = net.SelectedIndex;
                // Undo.RecordObject(node, "Modifying node");
                Handles.Label(nodePositon, "Node" + node.id);

                var pointHandleSize = HandleUtility.GetHandleSize(nodePositon) * HANDLE_SCALE;
                Handles.color = (node.id == selectedIndex) ? Color.red : Color.green;

                MyHandles.DragHandleResult dhResult;
                Vector3 newPosition = MyHandles.DragHandle(nodePositon, pointHandleSize, caps[(int)node.type], Color.blue, out dhResult);

                switch (dhResult)
                {
                    case MyHandles.DragHandleResult.LMBClick:
                        ChangeSelectedPointIndex(node.id);
                        GUI.changed = true;
                        break;
                    case MyHandles.DragHandleResult.LMBDrag:

                        if (editmode == EditMode.ConnectLine)
                        {
                            Vector3 position2 = Camera.current.WorldToScreenPoint(Handles.matrix.MultiplyPoint(newPosition));
                            var dragNodeTo = allNodes.Where(n =>
                            {
                                return n != node &&
                                    Vector3.Distance(Camera.current.WorldToScreenPoint(Handles.matrix.MultiplyPoint(n.position + net.transform.position)), position2) < 30;
                            }).FirstOrDefault();

                            if (dragNodeTo != null)
                            {
                                if (net.data.ConnectNode(node.id, dragNodeTo.id))
                                {
                                    net.CheckLineNode(node);
                                    net.CheckLineNode(dragNodeTo);
                                    net.UpdateLineNode(node);
                                }
                            }
                        }
                        break;
                }

                // Move node position
                if (editmode == EditMode.Transform)
                {
                    if (node.id == selectedIndex)
                    {
                        var currentPosition = tp = node.position + net.transform.position;
                        currentPosition = Handles.DoPositionHandle(currentPosition, Quaternion.identity);

                        if (tp != currentPosition)
                        {
                            node.position = net.attachToGround ? PipeNetUtils.GroundHeight(currentPosition) : currentPosition - net.transform.position;
                            net.UpdateNodeSign(node);
                            net.UpdateLineNode(node);
                            dirty = true;
                        }
                    }
                }
            }

            if (dirty)
            {
                Undo.RecordObject(net, "Refresh");
                net.Refresh();
            }
        }

        private void ExportXML()
        {
            string filepath = EditorUtility.SaveFilePanel("Export Pipe Net Data to XML", PipeNetUtils.RootAssetPath, net.name, "xml");
            if (filepath != "")
            {
                using (StreamWriter sw = new StreamWriter(filepath))
                {
                    sw.Write(net.data.ToXML());
                }
                AssetDatabase.Refresh();
            }
        }

        private void ChangeSelectedPointIndex(int newPointSelected)
        {
            net.SelectedIndex = newPointSelected;
            this.Repaint();
        }

        public static SceneView FindSceneView()
        {
            return SceneView.lastActiveSceneView == null ? EditorWindow.GetWindow<SceneView>() : SceneView.lastActiveSceneView;
        }
        #endregion

        #region Handles

        public void DrawHandleGUI(int nodeID)
        {
            var node = net.data.GetNode(nodeID);
            if (node == null)
                return;

            Handles.BeginGUI();
            GUI.backgroundColor = Color.red;
            Vector2 p = HandleUtility.WorldToGUIPoint(node.position + net.transform.position);
            if (GUI.Button(new Rect(p.x + 10, p.y - 50, 25, 25), "x"))
                DeleteNode(nodeID);

            GUI.Label(new Rect(p.x + 45, p.y - 50, 200, 25), "Node: " + nodeID.ToString());
            GUI.backgroundColor = Color.white;
            Handles.EndGUI();
        }
        #endregion

        #region Node Management

        /// <summary>
        /// add node
        /// </summary>
        /// <param name="v">node position</param>
        public Node AddNode(Vector3 v)
        {
            Undo.RecordObject(net, "Add Node");
            var node = new Node(v);
            net.data.AddNode(node);
            net.Refresh();
            net.UpdateNodeSign(node, true);
            SceneView.RepaintAll();
            return node;
        }

        /// <summary>
        /// delete node
        /// </summary>
        /// <param name="nodeID">node ID</param>
        public void DeleteNode(int nodeID)
        {
            Undo.RecordObject(net, "Delete Node");
            var node = net.data.GetNode(nodeID);
            if (node != null)
            {
                // delete already existing node objects
                net.DeleteLineNode(node);
                net.DeleteNodeSign(node);

                net.data.RemoveNodes(node);
                net.Refresh();
                SceneView.RepaintAll();
            }
        }
        #endregion
    }

    /// <summary>
    /// edit mode
    /// </summary>
    public enum EditMode
    {
        Transform,
        ConnectLine
    }
}
