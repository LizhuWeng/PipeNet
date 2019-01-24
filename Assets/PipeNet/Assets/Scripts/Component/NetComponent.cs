using UnityEngine;
using System.Linq;
using System.Collections.Generic;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace PipeNet
{
    /// <summary>
    /// Net component attached to game object
    /// </summary>
    public class NetComponent : MonoBehaviour
    {
        //pipe line scale according to line width
        public float pipeLineScale = 3f;
        public float lineWidth = 0.5f;

        //attach to ground
        public bool attachToGround = true;

        //show sign
        public bool showSign = true;
        public bool showFlowLine = true;

        //uv options 
        public bool swapUV = true;
        public bool flipU = false;
        public bool flipV = false;
        public Vector2 uvScale = new Vector2(0.4f, 1f);
        public Vector2 uvOffset = Vector2.zero;

        // materials
        public Material mainMat;
        public Material blockMat;
        public Material pipeLineMat;

        // net data
        public Net data = new Net();

        // current selected index by Editor
        private int selectedIndex;

        /// <summary>
        /// Refresh the net
        /// </summary>
        /// <param name="includeValveState">should reset valve state also</param>
        /// <param name="refreshAll">need to refresh the whole childs</param>
        public void Refresh(bool includeValveState = false, bool refreshAll = false)
        {
            transform.localScale = Vector3.one;

            data.Reset(false, includeValveState);
            data.minGap = lineWidth * 2f;
            var nodes = data.GetNodeList();

            // if attach to ground
            if (attachToGround)
            {
                foreach(var node in nodes)
                {
                    node.position = PipeNetUtils.GroundHeight(node.position) - transform.position;
                }
            }

            // not enough nodes
            if (nodes.Length < 2)
            {
                gameObject.GetComponent<MeshFilter>().sharedMesh = null;
                gameObject.GetComponent<MeshCollider>().sharedMesh = null;
                return;
            }

            //generate block node list
            var blockList = new List<Node>();
            var sourceNodes = data.StartFlow();
            if (sourceNodes.Length < 1)
                blockList.AddRange(nodes);
            else
                blockList.AddRange(nodes.Where(node => { return node.pressure == 0; }));

            //we remove all the middle connected nodes to join edges
            blockList.RemoveAll(node =>
            {
                int i = 0;
                foreach (var id in node.neighbors)
                {
                    var neighbor = data.GetNode(id);
                    if (neighbor != null && neighbor.pressure == 0)
                        i++;

                    if (i > 1) return true;
                } return false;
            });

            //Mesh component initialization
            if (!gameObject.GetComponent<MeshFilter>())
                gameObject.AddComponent<MeshFilter>();
            else if (gameObject.GetComponent<MeshFilter>().sharedMesh != null)
                DestroyImmediate(gameObject.GetComponent<MeshFilter>().sharedMesh);

            if (!gameObject.GetComponent<MeshRenderer>())
                gameObject.AddComponent<MeshRenderer>();

            //generate meshes
            var mainMesh = GenerateMesh(sourceNodes, false);
            data.Reset(true);
            var blockMesh = GenerateMesh(blockList.ToArray(), true);
            if (mainMesh == null && blockMesh == null)
                return;

            var meshList = new List<Mesh>();
            var matList = new List<Material>();
            if (mainMesh != null)
            {
                meshList.Add(mainMesh);
                matList.Add(mainMat);
            }
            if (blockMesh != null)
            {
                meshList.Add(blockMesh);
                matList.Add(blockMat);
            }

            //combine mesh
            var combinedMesh = meshList[0];
            if (meshList.Count > 1)
            {
                combinedMesh = new Mesh();
                var combines = new CombineInstance[meshList.Count];
                for (int i = 0; i < combines.Length; i++)
                {
                    combines[i].mesh = meshList[i];
                    combines[i].transform = transform.localToWorldMatrix;
                }
                combinedMesh.CombineMeshes(combines, false);
            }

            gameObject.GetComponent<MeshFilter>().sharedMesh = combinedMesh;
            gameObject.GetComponent<MeshRenderer>().sharedMaterials = matList.ToArray();
            gameObject.GetComponent<MeshCollider>().sharedMesh = combinedMesh;
#if UNITY_EDITOR
            Unwrapping.GenerateSecondaryUVSet(gameObject.GetComponent<MeshFilter>().sharedMesh);
#endif
            if (refreshAll)
            {
                foreach(var node in nodes)
                {
                    UpdateLineNode(node);
                    UpdateNodeSign(node);
                }
            }
        }

        /// <summary>
        /// show flow line
        /// </summary>
        /// <param name="show">show line</param>
        public void ShowFlowLine(bool show)
        {
            GetComponent<MeshRenderer>().enabled = show;
        }

        /// <summary>
        /// generate mesh data from node list
        /// </summary>
        /// <param name="nodeList">node list</param>
        /// <param name="bBlock">are they block nodes</param>
        /// <returns></returns>
        private Mesh GenerateMesh(Node[] nodeList, bool bBlock)
        {
            var meshData = new MeshData();
            if (nodeList.Length > 0)
            {
                foreach (var node in nodeList)
                {
                    FlowNodeData(node, ref meshData, bBlock);
                }
            }

            CalculateUV(ref meshData);
            return meshData.GenerateMesh();
        }

        /// <summary>
        /// flow node to generate mesh data
        /// </summary>
        /// <param name="source">source</param>
        /// <param name="meshData">mesh data</param>
        /// <param name="bBlock">is block node</param>
        /// <param name="lastIndex">last triangle index</param>
        private void FlowNodeData(Node source, ref MeshData meshData, bool bBlock, int lastIndex = -1)
        {
            source.searched = true;
            var v = meshData.v;
            var t = meshData.t;

            var node1 = source;
            var searchList = bBlock ? source.neighbors : source.downstreams;
            foreach (var nextID in searchList)
            {
                var node2 = data.GetNode(nextID);
                if (bBlock)
                {
                    if ( (node2.pressure != 0 && !node2 .closed ) || node2.searched)
                        continue;
                }

                /*************** Calculate vertices *****************/
                Vector2 a = node1.position.ToXZVector2();
                Vector2 b = node2.position.ToXZVector2();
                Vector3 rght = new Vector3(0, 0, 1);
                Vector3 lft = new Vector3(0, 0, -1);

                var theta = PipeNetMath.AngleRadian(a, b);
                meshData.thetas.Add(theta);

                // add two segments
                v.Add(node1.position + rght * lineWidth);
                v.Add(node1.position + lft * lineWidth);
                v.Add(node2.position + rght * lineWidth);
                v.Add(node2.position + lft * lineWidth);

                // apply angular rotation to points
                int l = v.Count - 4;
                v[l + 0] = v[l + 0].RotateAroundPoint(node1.position, -theta);
                v[l + 1] = v[l + 1].RotateAroundPoint(node1.position, -theta);
                v[l + 2] = v[l + 2].RotateAroundPoint(node2.position, -theta);
                v[l + 3] = v[l + 3].RotateAroundPoint(node2.position, -theta);

                // add triangles
                t.AddRange(new int[6]{meshData.tri_index + 2,meshData.tri_index + 1,meshData.tri_index + 0,
				                      meshData.tri_index + 2, meshData.tri_index + 3, meshData.tri_index + 1});
                meshData.tri_index += 4;

                /*************** Calculate edges *****************/
                if (lastIndex >= 0)
                {
                    int p0 = lastIndex;
                    int p1 = lastIndex + 1;
                    int p2 = lastIndex + 2;
                    int p3 = lastIndex + 3;
                    int p4 = meshData.tri_index - 4;
                    int p5 = meshData.tri_index - 3;
                    int p6 = meshData.tri_index - 2;
                    int p7 = meshData.tri_index - 1;

                    var leftInterceptY = (v[p2].y + v[p3].y) / 2f;
                    var rightInterceptY = (v[p4].y + v[p5].y) / 2f;

                    Vector2 leftIntercept;
                    if (!PipeNetMath.InterceptPoint(v[p0].ToXZVector2(), v[p2].ToXZVector2(),
                        v[p4].ToXZVector2(), v[p6].ToXZVector2(), out leftIntercept))
                        Debug.LogWarning("Parallel pipe lines!");

                    Vector2 rightIntercept;
                    if (!PipeNetMath.InterceptPoint(v[p1].ToXZVector2(), v[p3].ToXZVector2(),
                       v[p5].ToXZVector2(), v[p7].ToXZVector2(), out rightIntercept))
                        Debug.LogWarning("Parallel pipe lines!");

                    v[p2] = leftIntercept.ToVector3(leftInterceptY);
                    v[p4] = leftIntercept.ToVector3(leftInterceptY);

                    v[p3] = rightIntercept.ToVector3(rightInterceptY);
                    v[p5] = rightIntercept.ToVector3(rightInterceptY);
                }
                //continue flow node
                FlowNodeData(node2, ref meshData, bBlock, meshData.tri_index - 4);
            }
        }

        /// <summary>
        /// Calculate all the uvs
        /// </summary>
        private void CalculateUV(ref MeshData meshData)
        {
            var vertices = meshData.v;
            if (vertices.Count == 0)
                return;

            var uvs = new Vector2[vertices.Count];
            var topLeft = Vector2.zero;

            float scale = 1f;
            int v = 0; // vertex iterator
            int segments = meshData.tri_index / 4;
            for (int i = 0; i < segments; i++)
            {
                Vector3 segCenter = (vertices[v + 0] + vertices[v + 1] + vertices[v + 2] + vertices[v + 3]) / 4f;

                Vector2 u0 = vertices[v + 0].RotateAroundPoint(segCenter, meshData.thetas[i] + (90f * Mathf.Deg2Rad)).ToXZVector2();
                Vector2 u1 = vertices[v + 1].RotateAroundPoint(segCenter, meshData.thetas[i] + (90f * Mathf.Deg2Rad)).ToXZVector2();
                Vector2 u2 = vertices[v + 2].RotateAroundPoint(segCenter, meshData.thetas[i] + (90f * Mathf.Deg2Rad)).ToXZVector2();
                Vector2 u3 = vertices[v + 3].RotateAroundPoint(segCenter, meshData.thetas[i] + (90f * Mathf.Deg2Rad)).ToXZVector2();

                // normalizes uv scale
                uvs[v + 0] = u0 * scale;
                uvs[v + 1] = u1 * scale;
                uvs[v + 2] = u2 * scale;
                uvs[v + 3] = u3 * scale;

                var delta = topLeft - uvs[v + 0];
                uvs[v + 0] += delta;
                uvs[v + 1] += delta;
                uvs[v + 2] += delta;
                uvs[v + 3] += delta;

                topLeft = uvs[v + 2];
                v += 4;
            }

            // Normalize X axis, apply to Y
            scale = 1f / uvs[1].x - uvs[0].x;
            for (int i = 0; i < uvs.Length; i++)
            {
                uvs[i] *= scale;
            }

            // optional uv modifications
            if (swapUV)
            {
                for (int i = 0; i < uvs.Length; i++)
                    uvs[i] = new Vector2(uvs[i].y, uvs[i].x);
            }
            if (flipU)
            {
                for (int i = 0; i < uvs.Length; i++)
                    uvs[i] = new Vector2(-uvs[i].x, uvs[i].y);
            }
            if (flipV)
            {
                for (int i = 0; i < uvs.Length; i++)
                    uvs[i] = new Vector2(uvs[i].x, -uvs[i].y);
            }
            for (int i = 0; i < uvs.Length; i++)
            {
                uvs[i] += uvOffset;
                uvs[i] = Vector2.Scale(uvs[i], uvScale);
            }
            meshData.uv = uvs;
        }

        /// <summary>
        /// Close node at position
        /// </summary>
        /// <param name="pos">right position at the mesh collider</param>
        public void CloseNodeAt(Vector3 pos)
        {
           var nodes = PipeNetUtils.GetNodeAtPoint(data, pos);
           data.AnalyseNodeClosed(nodes.ToArray());
           Refresh();
        }

        #region EDITOR_UPDATE
#if UNITY_EDITOR
        /// <summary>
        /// update line node objects
        /// </summary>
        /// <param name="node">Node</param>
        public void UpdateLineNode(Node node)
        {
            var root = PipeLineTrans;
            foreach (var neighborID in node.neighbors)
            {
                var neighborNode = data.GetNode(neighborID);
                var lineName = string.Format("line {0}-{1}", Mathf.Min(node.id, neighborID), Mathf.Max(node.id, neighborID));
                var nodeLine = root.Find(lineName);
                if (nodeLine == null)      // create a new line
                {
                    nodeLine = GameObject.CreatePrimitive(PrimitiveType.Cylinder).transform;
                    nodeLine.name = lineName;
                    nodeLine.GetComponent<Renderer>().sharedMaterial = pipeLineMat;
                    DestroyImmediate(nodeLine.GetComponent<Collider>());
                    nodeLine.transform.parent = root;
                }
                // update line position
                var lineDis = Vector3.Distance(neighborNode.position, node.position);
                nodeLine.transform.localScale = new Vector3(lineWidth * pipeLineScale, lineDis / 2, lineWidth * pipeLineScale);
                nodeLine.transform.position = transform.position + (neighborNode.position + node.position) / 2;
                nodeLine.transform.LookAt(transform.position + node.position, Vector3.right);
                nodeLine.transform.Rotate(Vector3.right, 90f);
                nodeLine.transform.SetSiblingIndex(Mathf.Min(node.id, neighborID));
            }

            CheckLineNode(node);
        }

        /// <summary>
        /// delete line node object
        /// </summary>
        /// <param name="node">Node</param>
        public void DeleteLineNode(Node node)
        {
            var nodeName = "node " + node.id;
            var nodeSphere = PipeNodeTrans.Find(nodeName);
            if (nodeSphere)
                DestroyImmediate(nodeSphere.gameObject);

            foreach (var neighborID in node.neighbors)
            {
                var lineName = string.Format("line {0}-{1}", Mathf.Min(node.id, neighborID), Mathf.Max(node.id, neighborID));
                var nodeLine = PipeLineTrans.Find(lineName);

                if (nodeLine != null)
                    DestroyImmediate(nodeLine.gameObject);
            }
        }

        /// <summary>
        /// add a line node object
        /// </summary>
        /// <param name="node">Node</param>
        public void CheckLineNode(Node node)
        {
            var nodeName = "node " + node.id;
            var root = PipeNodeTrans;
            var nodeSphere = root.Find(nodeName);
            if (!nodeSphere && node.neighbors.Count > 1)
            {
                nodeSphere = GameObject.CreatePrimitive(PrimitiveType.Sphere).transform;
                nodeSphere.name = nodeName;
                nodeSphere.GetComponent<Renderer>().sharedMaterial = pipeLineMat;
                DestroyImmediate(nodeSphere.GetComponent<SphereCollider>()); //We don't need collider
                nodeSphere.parent = root;
                nodeSphere.localScale = Vector3.one * lineWidth * pipeLineScale;
                nodeSphere.transform.SetSiblingIndex(node.id);
            }

            if (nodeSphere)
                nodeSphere.position = transform.position + node.position;
        }

        /// <summary>
        /// update node sign position and appearance
        /// </summary>
        /// <param name="node">Node</param>
        /// <param name="regenerate">regenerate signs</param>
        public void UpdateNodeSign(Node node, bool regenerate = false)
        {
            var root = NodeSignRoot;
            var nodeName = "node " + node.id;
            var nodeSign = root.Find(nodeName);
            if (nodeSign != null)
            {
                nodeSign.gameObject.SetActive(true);
                if (regenerate)
                    DestroyImmediate(nodeSign.gameObject);
            }

            if (node.type != NodeType.Common)
            {
                if (nodeSign == null)
                {
                    var typePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(PipeNetUtils.RootAssetPath + "/Prefabs/" + node.type + ".prefab");
                    if (typePrefab)
                    {
                        nodeSign = Instantiate<GameObject>(typePrefab).transform;
                        nodeSign.transform.parent = root;
                        nodeSign.localScale = Vector3.one * lineWidth * pipeLineScale;
                        nodeSign.name = nodeName;
                        nodeSign.transform.SetSiblingIndex(node.id);
                    }
                }
            }
            if (nodeSign != null)
                nodeSign.position = transform.position + node.position + Vector3.up * lineWidth * pipeLineScale / 2f;
        }

        /// <summary>
        /// delete a node sign
        /// </summary>
        /// <param name="node">Node</param>
        public void DeleteNodeSign(Node node)
        {
            var nodeName = "node " + node.id;
            var nodeSign = NodeSignRoot.Find(nodeName);
            if (nodeSign != null)
                DestroyImmediate(nodeSign.gameObject);
        }

        /// <summary>
        /// update all the line objects' scale
        /// </summary>
        public void UpdateLineScale()
        {
            var root = PipeNodeTrans;
            for (int i = 0; i < root.childCount; i++)
            {
                var child = root.GetChild(i);
                child.localScale = Vector3.one * lineWidth * pipeLineScale;
            }

            root = PipeLineTrans;
            for (int i = 0; i < root.childCount; i++)
            {
                var child = root.GetChild(i);
                child.localScale = new Vector3(lineWidth * pipeLineScale, child.localScale.y, lineWidth * pipeLineScale);
            }

            root = NodeSignRoot;
            for (int i = 0; i < root.childCount; i++)
            {
                var child = root.GetChild(i);
                var nodeID = int.Parse(child.name.Replace("node ", ""));
                var node = data.GetNode(nodeID);
                if (node != null)
                {
                    child.localScale = Vector3.one * lineWidth * pipeLineScale;
                    child.position = transform.position + node.position + Vector3.up * lineWidth * pipeLineScale / 2f;
                }
            }
        }

        /// <summary>
        /// Current selected node's index
        /// </summary>
        public int SelectedIndex
        {
            get
            {
                if (data.GetNode(selectedIndex) != null)
                {
                    return selectedIndex;
                }
                else
                {
                    var firstNode = data.GetFirstNode();
                    if (firstNode != null)
                        selectedIndex = firstNode.id;

                    return selectedIndex;
                }
            }
            set { selectedIndex = value; }
        }

        // node signs transform root
        public Transform NodeSignRoot
        {
            get
            {
                var root = transform.Find("NodeSign");
                if (root == null)
                {
                    var go = new GameObject("NodeSign");
                    go.transform.parent = transform;
                    go.transform.localPosition = Vector3.zero;
                    root = go.transform;
                }
                return root;
            }
        }

        // pipe line transform root
        public Transform PipeLineRoot
        {
            get
            {
                var root = transform.Find("PipeLine");
                if (root == null)
                {
                    var go = new GameObject("PipeLine");
                    go.transform.parent = transform;
                    go.transform.localPosition = Vector3.zero;
                    root = go.transform;
                }
                return root;
            }
        }

        public Transform PipeLineTrans
        {
            get
            {
                var root = transform.Find("PipeLine/Lines");
                if (root == null)
                {
                    var go = new GameObject("Lines");
                    go.transform.parent = PipeLineRoot;
                    go.transform.localPosition = Vector3.zero;
                    root = go.transform;
                }
                return root;
            }
        }

        public Transform PipeNodeTrans
        {
            get
            {
                var root = transform.Find("PipeLine/Nodes");
                if (root == null)
                {
                    var go = new GameObject("Nodes");
                    go.transform.parent = PipeLineRoot;
                    go.transform.localPosition = Vector3.zero;
                    root = go.transform;
                }
                return root;
            }
        }
#endif
        #endregion

        /// <summary>
        /// Mesh data for generating mesh
        /// </summary>
        class MeshData
        {
            public List<Vector3> v = new List<Vector3>();
            public List<int> t = new List<int>();
            public Vector2[] uv = new Vector2[] { };
            public List<float> thetas = new List<float>();
            public int tri_index = 0;

            /// <summary>
            /// Generate mesh
            /// </summary>
            /// <returns>Mesh</returns>
            public Mesh GenerateMesh()
            {
                if (v.Count == 0)
                    return null;

                var m = new Mesh();
                m.vertices = v.ToArray();
                m.triangles = t.ToArray();
                m.uv = uv;
                m.RecalculateNormals();
                return m;
            }
        }
    }
}

