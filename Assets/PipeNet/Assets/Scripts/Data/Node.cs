using System;
using UnityEngine;
using System.Collections.Generic;

namespace PipeNet
{
    /// <summary>
    /// Node data structure
    /// </summary>
    [Serializable]
    public class Node
    {
        public int id;

        /// <summary>
        /// point position
        /// </summary>
        public Vector3 position;

        /// <summary>
        /// neighbor points connected to self
        /// </summary>
        public List<int> neighbors = new List<int>();

        /// <summary>
        /// point type <Enum PointType>
        /// </summary>
        public NodeType type;

        /// <summary>
        /// current pressure
        /// </summary>
        public float pressure;

        /// <summary>
        /// closed or not
        /// </summary>
        public bool closed;

        /// <summary>
        /// has been searched
        /// </summary>
        internal bool searched;

        /// <summary>
        /// downstream list
        /// </summary>
        internal List<int> downstreams = new List<int>();

        public Node() { }

        /// <summary>
        /// New node
        /// </summary>
        /// <param name="pos">node position</param>
        public Node(Vector3 pos)
        {
            position = pos;
        }

        /// <summary>
        /// reset self state and pressure
        /// </summary>
        /// <param name="justSearch">just reset search</param>
        /// <param name="includeValveState">include valve state</param>
        public void Reset(bool justSearch = false, bool includeValveState = false)
        {
            searched = false;
            if (justSearch)
                return;

            downstreams = new List<int>();
            if (type != NodeType.Source)
            {
                pressure = 0f;
                if (includeValveState || type == NodeType.Common)
                    closed = false;
            }
        }
    }

    /// <summary>
    /// Node type
    /// </summary>
    public enum NodeType
    {
        /// <summary>
        /// Default node
        /// </summary>
        Common,

        /// <summary>
        /// Node that can be closed and affect connected nodes
        /// </summary>
        Valve,

        /// <summary>
        /// Node source which has some pressure (like water source .etc)
        /// </summary>
        Source
    }

}
