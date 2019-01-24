using UnityEngine;
using System;
using System.Collections.Generic;

namespace PipeNet
{
    /// <summary>
    /// Pipe net data structure
    /// </summary>
    [Serializable]
    public class Net
    {
        //Point dictionary <ID, Node>
        [SerializeField]
        private NodeDictionary nodes;

        //The unique id for generating nodes (auto-added)
        [SerializeField]
        private int uniqueID = 1;

        //Removed id list(for recycle)
        [SerializeField]
        private List<int> removedIDs = new List<int>();

        //Minimum gap between nodes
        public float minGap = 1f;

        public Net()
        {
            nodes = new NodeDictionary();
        }

        public Net(Node[] data)
        {
            nodes = new NodeDictionary();
            AddNodes(data);
        }

        /// <summary>
        /// add node to net 
        /// </summary>
        /// <param name="node">node</param>
        /// <param name="generateID">generate a new ID?</param>
        public void AddNode(Node node, bool generateID = true)
        {
            if (generateID)
                node.id = GenerateNodeID();
            else
                uniqueID = Mathf.Max(uniqueID, node.id + 1);

            nodes.Add(node.id, node);
        }

        /// <summary>
        /// add a node list to the net
        /// </summary>
        /// <param name="data">node list</param>
        /// <param name="generateID">generate a new ID?</param>
        public void AddNodes(Node[] datas, bool generateID = true)
        {
            foreach(var node in datas)
            {
                AddNode(node, generateID);
            }
        }

        /// <summary>
        /// remove node from the net
        /// </summary>
        /// <param name="datas"></param>
        public void RemoveNodes(params Node[] datas)
        {
            if (datas.Length == 0)
            {
                nodes = new NodeDictionary();
                removedIDs = new List<int>();
                uniqueID = 1;
                return;
            }

            foreach (var node in datas)
            {
                if (nodes.ContainsKey(node.id))
                {
                    // disconnect all the neighbors
                    foreach(var neighborID in node.neighbors)
                    {
                        var neighborNode = GetNode(neighborID);
                        if (neighborNode != null && neighborNode.neighbors.Contains(node.id))
                            neighborNode.neighbors.Remove(node.id);
                    }
                    //reset downstream connection 
                    foreach (var nodeID in node.downstreams)   //TODO  move to neighbor check
                    {
                        var checkNode = GetNode(nodeID);
                        if (checkNode != null && checkNode.downstreams != null && checkNode.downstreams.Contains(node.id))
                            checkNode.downstreams.Remove(node.id);
                    }

                    //Recycle ID
                    if (!removedIDs.Contains(node.id))
                        removedIDs.Add(node.id);

                    //Remove node
                    nodes.Remove(node.id);
                }
            }
        }


        /// <summary>
        /// connect two node
        /// </summary>
        /// <param name="node1">node</param>
        /// <param name="node2">node</param>
        /// <returns>need update view</returns>
        public bool ConnectNode(int node1, int node2)
        {
            if (node1 == node2) return false;

            bool dirty = false;
            var p1 = GetNode(node1);
            var p2 = GetNode(node2);
            if (p1 != null && p2 != null)
            {
                if (!p1.neighbors.Contains(node2))
                {
                    p1.neighbors.Add(node2);
                    dirty = true;
                }
                if (!p2.neighbors.Contains(node1))
                {
                    p2.neighbors.Add(node1);
                    dirty = true;
                }
            }
            return dirty;
        }

        /// <summary>
        /// return all node list
        /// </summary>
        /// <returns>node list</returns>
        public Node[] GetNodeList()
        {
            var nodeList = new List<Node>(nodes.Values);
            return nodeList.ToArray();
        }

        /// <summary>
        /// return a node list by type
        /// </summary>
        /// <param name="type">Node Type</param>
        /// <returns>node list</returns>
        public Node[] GetNodeList(NodeType type)
        {
            var nodeList = new List<Node>();
            foreach (var node in nodes.Values)
            {
                if (node.type == type)
                    nodeList.Add(node);
            }
            return nodeList.ToArray();
        }

        /// <summary>
        /// reset the net
        /// </summary>
        /// <param name="justSearch">just reset search</param>
        /// <param name="includeValveState">include valve state</param>
        public void Reset(bool justSearch = false, bool includeValveState = false)
        {
            foreach (var node in nodes.Values)
            {
                if(node != null)
                {
                    node.Reset(justSearch, includeValveState);
                    if (!justSearch)
                        node.neighbors.RemoveAll(neighborID => !nodes.ContainsKey(neighborID));
                }
            }
        }

        /// <summary>
        /// get node by id
        /// </summary>
        /// <param name="id">node ID</param>
        /// <returns></returns>
        public Node GetNode(int id)
        {
            if (nodes.ContainsKey(id))
                return nodes[id];
       //     else
       //         Debug.Log("Net does not contain node [ID]:" + id);
            return null;
        }

        /// <summary>
        /// get the first node of the net
        /// </summary>
        /// <returns>Node</returns>
        public Node GetFirstNode()
        {
            var enumerator = nodes.GetEnumerator();
            enumerator.MoveNext();
            return enumerator.Current.Value;
        }

        /// <summary>
        /// generate unique ID for new created node
        /// </summary>
        /// <returns></returns>
        private int GenerateNodeID()
        {
            if (removedIDs.Count > 0)
            {
                var lastID = removedIDs[0];
                removedIDs.RemoveAt(0);
                return lastID;
            }
            return uniqueID++;
        }
    }

    [Serializable]
    public class NodeDictionary : SerializableDictionary<int, Node> { }
}
