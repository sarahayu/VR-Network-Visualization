using System.Collections;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using VidiGraph;

public class Tooltip : MonoBehaviour
{
    [SerializeField] TextMeshProUGUI _infoCol1;
    [SerializeField] TextMeshProUGUI _infoCol2;

    Canvas _canvas;

    NetworkManager _networkManager;

    int _lastHoveredNode = -1;

    void OnEnable()
    {
        _canvas = GetComponent<Canvas>();
        _networkManager = GameObject.Find("/Network Manager").GetComponent<NetworkManager>();
    }

    void Start()
    {
        Unshow();
    }

    void Update()
    {
        int curHoveredNode = _networkManager.NetworkGlobal.HoveredNode?.ID ?? -1;

        if (_lastHoveredNode != curHoveredNode)
        {
            _lastHoveredNode = curHoveredNode;

            UpdateTooltip(curHoveredNode);
        }
    }

    public void Show()
    {
        _canvas.enabled = true;
    }

    public void Unshow()
    {
        _canvas.enabled = false;
    }

    void UpdateTooltip(int hoveredNode)
    {
        if (hoveredNode == -1)
        {
            Unshow();
            return;
        }

        var halves = GetPropsStr(_networkManager.NetworkGlobal.Nodes[hoveredNode], 2);

        _infoCol1.SetText(halves.Length >= 1 ? halves[0] : "");
        _infoCol2.SetText(halves.Length >= 2 ? halves[1] : "");

        Show();
    }

    string[] GetPropsStr(Node node, int split)
    {
        var filenodes = _networkManager.FileLoader.SphericalLayout.nodes;

        Dictionary<string, object> labelAndID = new Dictionary<string, object>()
            {
                {"label", node.Label},
                {"id", node.ID},
            };

        var props = labelAndID.Concat(ObjectUtils.AsDictionary(filenodes[node.IdxProcessed].props)).ToDictionary(k => k.Key, k => k.Value);

        int counter = 0;

        var splitProps = props.GroupBy(_ => counter++ % split).Select(d => d.ToDictionary(e => e.Key, e => e.Value));

        return splitProps.Select(splitProp =>
            splitProp.Aggregate("", (propStr, propPair) =>
            {
                return propStr += "<b><size=70%>" + propPair.Key + "</size></b>\n"
                    + (propPair.Value ?? "<i>no info</i>") + "\n"
                    + "<size=50%> </size>\n";
            })
        ).ToArray();
    }
}
