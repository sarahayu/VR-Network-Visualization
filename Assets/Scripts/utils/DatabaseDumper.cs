using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
namespace VidiGraph
{
    public class DatabaseDumper
    {
        StreamWriter _sFile;
        StreamWriter _cFile;
        StreamWriter _nFile;
        StreamWriter _n2nFile;
        NetworkFileData _networkFile;
        IEnumerable<string> _nodeProps;
        IEnumerable<string> _linkProps;
        bool _onlyDirty;
        bool _dumpProps;
        MultiLayoutContext _networkContext;
        NetworkGlobal _networkGlobal;

        public DatabaseDumper(
            StreamWriter sFile,
            StreamWriter cFile,
            StreamWriter nFile,
            StreamWriter n2nFile,
            NetworkFileData networkFile,
            IEnumerable<string> nodeProps,
            IEnumerable<string> linkProps,
            bool onlyDirty,
            bool dumpProps,
            MultiLayoutContext networkContext,
            NetworkGlobal networkGlobal)
        {
            _sFile = sFile;
            _cFile = cFile;
            _nFile = nFile;
            _n2nFile = n2nFile;
            _networkFile = networkFile;
            _nodeProps = nodeProps;
            _linkProps = linkProps;
            _onlyDirty = onlyDirty;
            _dumpProps = dumpProps;
            _networkContext = networkContext;
            _networkGlobal = networkGlobal;
        }

        public void Dump()
        {
            DumpSubnetwork();
            DumpCommunities();
            DumpNodes();
            DumpLinks();
        }

        void DumpSubnetwork()
        {
            _sFile.WriteLine(_networkContext.SubnetworkID);
        }

        void DumpCommunities()
        {
            foreach (var (commID, commContext) in _networkContext.Communities)
            {
                var commGlobal = _networkGlobal.Communities[commID];

                if (commGlobal == null) continue;
                if (_onlyDirty && !commGlobal.Dirty && !commContext.Dirty) continue;

                var commId = commContext.ID;
                var selected = false; //commGlobal.Selected;
                var subnetworkId = _networkContext.SubnetworkID; //commGlobal.Selected;
                var render_UUID = commContext.UUID;
                var render_mass = commContext.Mass;
                var render_massCenter = commContext.MassCenter;
                var render_size = commContext.Size;
                var render_state = commContext.State;

                _cFile.WriteLine($"{commId};{selected};{subnetworkId};{render_UUID};{render_mass};{render_massCenter};{render_size};{render_state}");
            }
        }

        void DumpNodes()
        {
            foreach (var (nodeID, nodeContext) in _networkContext.Nodes)
            {
                var nodeGlobal = _networkGlobal.Nodes[nodeID];

                if (nodeGlobal.IsVirtualNode) continue;
                if (_onlyDirty && !nodeGlobal.Dirty && !nodeContext.Dirty) continue;

                var nodeId = nodeID;
                var label = nodeGlobal.Label;
                var degree = nodeGlobal.Degree;
                var selected = false; //nodeGlobal.Selected;
                var commRenderUUID = _networkContext.Communities[nodeContext.CommunityID].UUID;
                var render_UUID = nodeContext.UUID;
                var render_size = nodeContext.Size;
                var render_pos = nodeContext.Position.ToString();
                var render_color = nodeContext.Color.ToString();

                string values = $"{nodeId};{label};{degree};{selected};{commRenderUUID};{render_UUID};{render_size};{render_pos};{render_color}";
                if (_dumpProps)
                {
                    var props = ObjectUtils.AsDictionary(_networkFile.nodes[nodeGlobal.IdxProcessed].props);
                    values += ";" + string.Join(";", _nodeProps.Select(p => (props[p] ?? "").ToString()));
                }

                _nFile.WriteLine(values);
            }
        }

        void DumpLinks()
        {


            foreach (var (linkID, linkContext) in _networkContext.Links)
            {
                var linkGlobal = _networkGlobal.Links[linkID];

                if (linkGlobal.SourceNode.IsVirtualNode || linkGlobal.TargetNode.IsVirtualNode) continue;
                if (_onlyDirty && !linkGlobal.Dirty && !linkContext.Dirty) continue;

                var linkId = linkID;
                var sourceRenderUUID = _networkContext.Nodes[_networkGlobal.Links[linkID].SourceNodeID].UUID;
                var targetRenderUUID = _networkContext.Nodes[_networkGlobal.Links[linkID].TargetNodeID].UUID;
                var selected = linkGlobal.Selected;
                var render_UUID = linkContext.UUID;
                var render_bundlingStrength = linkContext.BundlingStrength;
                var render_width = linkContext.Width;
                var render_colorStart = linkContext.ColorStart;
                var render_colorEnd = linkContext.ColorEnd;
                var render_alpha = linkContext.Alpha;

                string values = $"{linkId};{sourceRenderUUID};{targetRenderUUID};{selected};{render_UUID};{render_bundlingStrength};{render_width};{render_colorStart};{render_colorEnd};{render_alpha}";
                if (_dumpProps)
                {
                    var props = ObjectUtils.AsDictionary(_networkFile.links[linkGlobal.IdxProcessed].props);

                    values += ";" + string.Join(";", _linkProps.Select(p => (props[p] ?? "").ToString()));
                }

                _n2nFile.WriteLine(values);
            }
        }
    }
}