using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;
using PipeNet;

/// <summary>
/// Pipe net analysis (UI)
/// </summary>
public class UI_PipiNetAnalysis : MonoBehaviour
{
    /// <summary>
    /// clear all marks
    /// </summary>
    [SerializeField]
    private Button btnReset;

    /// <summary>
    /// close a valve
    /// </summary>
    [SerializeField]
    private Button btnCloseValve;

    /// <summary>
    /// mark a trouble point
    /// </summary>
    [SerializeField]
    private Button btnMarkTroublePoint;

    /// <summary>
    /// mark icon image
    /// </summary>
    [SerializeField]
    private Image imgMarkIcon;

    /// <summary>
    /// net component
    /// </summary>
    [SerializeField]
    private NetComponent net;

    // mark objects (been created)
    private List<Transform> markObjs = new List<Transform>();

    enum AnalyseMode
    {
        TroublePoint,
        CloseValve,
    }
    //current analyze mode
    private AnalyseMode curAnalyseMode;
    //mark effect resource url list
    private string[] effectURLs = new string[2] { "PipeNetEffect/Trouble Point", "PipeNetEffect/Closed Valve" };

    // control objects 
    private List<Transform> controlObjs = new List<Transform>();
    private string controlEffectURL = "PipeNetEffect/Repair Point";

    //minimum insert distance
    private float minInsert = 3f;

    void Awake()
    {
        btnReset.onClick.AddListener(() => ClearMarks());

        btnCloseValve.onClick.AddListener(() => Analyse(AnalyseMode.CloseValve, btnCloseValve.image.sprite));
        btnMarkTroublePoint.onClick.AddListener(() => Analyse(AnalyseMode.TroublePoint, btnMarkTroublePoint.image.sprite));
    }

    /// <summary>
    /// analyze mode 
    /// </summary>
    /// <param name="mode">analyse mode</param>
    /// <param name="icon">icon</param>
    void Analyse(AnalyseMode mode, Sprite icon)
    {
        if (curAnalyseMode != mode)
        {
            ClearMarks();
            curAnalyseMode = mode;
        }

        imgMarkIcon.enabled = true;
        imgMarkIcon.sprite = icon;
    }

    /// <summary>
    /// clear marks
    /// </summary>
    void ClearMarks()
    {
        foreach (var trans in markObjs)
        {
            if (trans != null)
                GameObject.Destroy(trans.gameObject);
        }
        foreach (var trans in controlObjs)
        {
            if (trans != null)
                GameObject.Destroy(trans.gameObject);
        }
        net.Refresh(true);
        markObjs.Clear();
        controlObjs.Clear();
        imgMarkIcon.enabled = false;
    }

    void Update()
    {
        if (imgMarkIcon.enabled)
        {
            Vector2 localPoint;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(imgMarkIcon.transform.parent.GetComponent<RectTransform>(),
                                        Input.mousePosition, null, out localPoint);
            imgMarkIcon.transform.localPosition = localPoint;

            if (Input.GetMouseButtonUp(0))
            {
                RaycastHit hit;
                if (Physics.Raycast(Camera.main.ScreenPointToRay(Input.mousePosition), out hit, 1000, 1 << 4))//water layer
                {
                    AddMark(hit.point);
                }
            }
        }
    }

    /// <summary>
    /// Add a mark at the position
    /// </summary>
    /// <param name="pos">position</param>
    void AddMark(Vector3 pos)
    {
        //check if too close to already existing marks
        foreach(var obj in markObjs)
        {
            if (Vector3.Distance(obj.position, pos) < minInsert)
                return;
        }

        // check if clicked at a right node
        var pointNodes = PipeNetUtils.GetNodeAtPoint(net.data, pos);
        if (curAnalyseMode == AnalyseMode.CloseValve)
        {
            if (pointNodes.Count != 1 || pointNodes[0].type == NodeType.Common)
                return;
        }

        // add mark nodes to the list
        var mark = Instantiate((GameObject)Resources.Load(effectURLs[(int)curAnalyseMode]));
        markObjs.Add(mark.transform);
        mark.transform.position = pos;

        var nodes = new List<Node>();
        foreach (var obj in markObjs)
        {
            var node = PipeNetUtils.GetNodeAtPoint(net.data, obj.transform.position);
            if (node != null)
                nodes.AddRange(node);
        }

        // start analyze
        switch(curAnalyseMode)
        {
            case AnalyseMode.TroublePoint:
                // clear control objects
                foreach (var trans in controlObjs)
                {
                    if (trans != null)
                        GameObject.Destroy(trans.gameObject);
                }
                controlObjs.Clear();
                //generate new
                var controlNodes = net.data.GetRelatedControlNodes(nodes.ToArray());
                foreach (var node in controlNodes)
                {
                    var repairMark = Instantiate((GameObject)Resources.Load(controlEffectURL));
                    repairMark.transform.position = node.position;
                    controlObjs.Add(repairMark.transform);
                }
                break;
            case AnalyseMode.CloseValve:
                net.data.AnalyseNodeClosed(nodes.ToArray());
                net.Refresh();
                break;
        }

    }

}
