using UnityEngine;
using System;
using System.Collections.Generic;
using System.Xml;
using System.IO;
using System.Text;

namespace PipeNet
{
    public static class PipeNetUtils
    {
        /// <summary>
        /// default pressure of the source node
        /// </summary>
        private static float SOURCE_PRESSURE = 100f;

        /// <summary>
        /// return node list at nearby point 
        ///       if two nodes returned ,then denotes that it's inside a segment
        /// </summary>
        /// <param name="net">net</param>
        /// <param name="pos">position</param>
        /// <returns>node list</returns>
        public static List<Node> GetNodeAtPoint(Net net, Vector3 pos)
        {
            var pointNodes = new List<Node>();
            var allNodes = net.GetNodeList();

            foreach (var node in allNodes)
            {
                //closed to a node
                if (Vector3.Distance(node.position, pos) < net.minGap * 2)
                {
                    pointNodes.Add(node);
                    return pointNodes;
                }
            }

            foreach (var node in allNodes)
            {
                //loop to find the segment
                foreach (var neighborID in node.neighbors)
                {
                    var neighborNode = net.GetNode(neighborID);
                    if (neighborNode == null)
                        continue;

                    var ab = neighborNode.position - node.position;
                    var ac = pos - node.position;
                    var bc = pos - neighborNode.position;

                    //check if it's on a line segment
                    if (Vector3.Dot(ab, ac) >= 0 && Vector3.Dot(ab, bc) <= 0)
                    {
                        var projectDis = (Vector3.Cross(ab, ac) / ab.magnitude).magnitude;
                        if (projectDis < net.minGap)
                        {
                            pointNodes.Add(node);
                            pointNodes.Add(neighborNode);
                            return pointNodes;
                        }
                    }
                }
            }
            return pointNodes;
        }

        /// <summary>
        /// analyze to get all the nodes affected by the closed list
        /// </summary>
        /// <param name="net">net</param>
        /// <param name="closedNodes">closed nodes</param>
        /// <returns>related node list</returns>
        public static Node[] AnalyseNodeClosed(this Net net, Node[] closedNodes)
        {
            var closeList = new List<Node>();
            net.Reset();
            //loop all closed nodes to set the state closed
            foreach (var closedNode in closedNodes)
            {
                if (closedNode != null)
                    closedNode.closed = true;
            }

            //start flow to set the pressure
            net.StartFlow();

            var allNodes = net.GetNodeList();
            foreach (var node in allNodes)
            {
                if (node.pressure == 0)
                    closeList.Add(node);
            }
            return closeList.ToArray();
        }

        /// <summary>
        /// start flow through all nodes
        /// </summary>
        /// <param name="net">net</param>
        /// <returns>source node array</returns>
        public static Node[] StartFlow(this Net net)
        {
            //firstly find the source node of the net and start search from that
            var sources = net.GetNodeList(NodeType.Source);
            foreach (var source in sources)
            {
                //if source is open but pressure is not set, we set that
                if (!source.closed && source.pressure <= 0)
                {
                    source.pressure = SOURCE_PRESSURE;
                }
                FlowNode(net, source);
            }
            return sources;
        }

        /// <summary>
        /// get all related control nodes from the breakdown position
        /// </summary>
        /// <param name="net">net</param>
        /// <param name="nodes">breakdown node list</param>
        /// <returns>control node array</returns>
        public static Node[] GetRelatedControlNodes(this Net net, params Node[] nodes)
        {
            net.Reset(true);
            List<Node> relatedControlNodes = new List<Node>();
            foreach (var node in nodes)
            {
                TrackRelatedControlNodes(net, node, ref relatedControlNodes);
            }
            return relatedControlNodes.ToArray();
        }

        /// <summary>
        /// loop flow node to set pressure and downstream list
        /// </summary>
        /// <param name="net">net</param>
        /// <param name="node">start node</param>
        private static void FlowNode(Net net, Node node)
        {
            foreach (var neighbor in node.neighbors)
            {
                var neighborNode = net.GetNode(neighbor);
                //if we had not flow to that node, then check that
                if (!neighborNode.downstreams.Contains(node.id))
                {
                    //if current node's pressure is larger than neighbor node, then we flow to 
                    if (node.pressure > neighborNode.pressure)
                    {
                        node.downstreams.Add(neighbor);
                        if (!neighborNode.closed)
                        {
                            neighborNode.pressure = node.pressure;
                            FlowNode(net, neighborNode);
                        }
                    }
                }
            }
        }

        /// <summary>
        /// track related control nodes
        /// </summary>
        /// <param name="net">net</param>
        /// <param name="node">node</param>
        /// <param name="controlNodes">control node list</param>
        private static void TrackRelatedControlNodes(Net net, Node node, ref List<Node> controlNodes)
        {
            node.searched = true;
            if (node.type != NodeType.Common && !node.closed)
            {
                if (!controlNodes.Contains(node))
                    controlNodes.Add(node);

                return;
            }

            foreach (var neighbor in node.neighbors)
            {
                var neighborNode = net.GetNode(neighbor);
                if (!neighborNode.searched)
                    TrackRelatedControlNodes(net, neighborNode, ref controlNodes);
            }
        }

        public static Vector3 ToVector3Y(this Vector2 vec, float y)
        {
            return new Vector3(vec.x, y, vec.y);
        }

        /**
         *	Move the vector to the nearest point below (then) above.  If no mesh found, no change is made.
         */
        public static Vector3 GroundHeight(Vector3 v)
        {
            RaycastHit ground = new RaycastHit();

            // try casting from really high up next
            if (Physics.Raycast(v + Vector3.up * 50, Vector3.down, out ground, 200, ~(1 << 4)))  //water layer
                v.y = ground.point.y;
            else if (Physics.Raycast(v, Vector3.down, out ground, 200, ~(1 << 4)))
                v.y = ground.point.y;
            else if (Physics.Raycast(v, Vector3.up, out ground, 200, ~(1 << 4)))
                v.y = ground.point.y;

            return v;
        }

        /// <summary>
        /// generate data from a XML
        /// </summary>
        /// <param name="net">Net</param>
        /// <param name="XMLPath">URL</param>
        public static void FromXML(this Net net, string XMLPath)
        {
            Debug.Log("Import Pipe Net XML " + XMLPath);
            XmlDocument xml = new XmlDocument();
            using (StreamReader sr = new StreamReader(XMLPath))
            {
                xml.LoadXml(sr.ReadToEnd());
            }

            net.RemoveNodes();
            foreach (XmlNode xmlNode in xml.SelectNodes("pipenet/nodes/node"))
            {
                var node = new Node();
                node.id = int.Parse(xmlNode["id"].FirstChild.Value);
                node.type = (NodeType)Enum.Parse(typeof(NodeType), xmlNode["type"].FirstChild.Value);
                node.position.x = float.Parse(xmlNode["positionX"].FirstChild.Value);
                node.position.y = float.Parse(xmlNode["positionY"].FirstChild.Value);
                node.position.z = float.Parse(xmlNode["positionZ"].FirstChild.Value);

                var neighbors = xmlNode["neighbors"];
                if (neighbors != null)
                {
                    node.neighbors = new List<int>();
                    foreach (XmlNode neighborXMLNode in neighbors)
                    {
                        node.neighbors.Add(int.Parse(neighborXMLNode.InnerText));
                    }
                }
                net.AddNode(node, false);
            }
        }

        /// <summary>
        /// output data to XML
        /// </summary>
        /// <param name="net">Net</param>
        /// <returns>XML</returns>
        public static string ToXML(this Net net)
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("<?xml version='1.0' encoding='ISO-8859-15'?>");
            sb.AppendLine("<!-- Pipe Net Data XML Exporter -->");
            sb.AppendLine("<pipenet>");

            sb.AppendLine("<nodes>");
            var allNodes = net.GetNodeList();
            foreach (var node in allNodes)
            {
                sb.AppendLine("<node>");
                sb.AppendLine("<id>" + node.id + "</id>");
                sb.AppendLine("<type>" + node.type + "</type>");
                sb.AppendLine("<positionX>" + node.position.x + "</positionX>");
                sb.AppendLine("<positionY>" + node.position.y + "</positionY>");
                sb.AppendLine("<positionZ>" + node.position.z + "</positionZ>");

                sb.AppendLine("<neighbors>");
                foreach (var neighborID in node.neighbors)
                {
                    sb.AppendLine("<nodeID>" + neighborID + "</nodeID>");
                }
                sb.AppendLine("</neighbors>");
                sb.AppendLine("</node>");
            }
            sb.AppendLine("</nodes>");
            sb.AppendLine("</pipenet>");
            return sb.ToString();
        }

#if UNITY_EDITOR
        static string rootAssetPath = "Assets/PipeNet";
        /// <summary>
        /// Return root asset path
        /// </summary>
        public static string RootAssetPath
        {
            get
            {
                if (!UnityEditor.AssetDatabase.IsValidFolder(rootAssetPath))
                {
                    var path = GetRootFolder("Assets", "PipeNet");
                    if (path != null)
                        rootAssetPath = path;
                }
                return rootAssetPath;
            }
        }

        static string GetRootFolder(string path, string checkStr)
        {
            var subFolders = UnityEditor.AssetDatabase.GetSubFolders(path);
            foreach (var subfolder in subFolders)
            {
                if (subfolder.Contains(checkStr))
                    return subfolder;
                else
                {
                    var url = GetRootFolder(subfolder, checkStr);
                    if (url != null)
                        return url;
                }
            }
            return null;
        }
#endif

    }
}

