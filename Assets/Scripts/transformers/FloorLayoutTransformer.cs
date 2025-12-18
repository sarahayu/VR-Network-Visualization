using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Assertions;

namespace VidiGraph
{
	public class FloorLayoutTransformer : NetworkContextTransformer
	{
		public Transform FloorPosition;

		NetworkGlobal _networkGlobal;
		MultiLayoutContext _networkContext;

		// TODO remove this when we are able to calc at runtime
		NetworkFilesLoader _fileLoader;
		TransformInfo _floorTransform;
		HashSet<int> _commsToUpdate = new HashSet<int>();

		public override void Initialize(NetworkGlobal networkGlobal, NetworkContext networkContext)
		{
			_networkContext = (MultiLayoutContext)networkContext;

			var manager = GameObject.Find("/Network Manager").GetComponent<NetworkManager>();
			_networkGlobal = manager.NetworkGlobal;
			_fileLoader = manager.FileLoader;
			_floorTransform = new TransformInfo(FloorPosition);
		}

		public override void ApplyTransformation()
		{
			// TODO calculate at runtime
			var floorNodes = _fileLoader.FlatLayout.nodes;

			foreach (var commID in _commsToUpdate)
			{
				var commContext = _networkContext.Communities[commID];
				commContext.Dirty = true;

				foreach (var nodeID in commContext.Nodes)
				{
					var nodeGlobal = _networkGlobal.Nodes[nodeID];

					if (nodeGlobal.IsVirtualNode) continue;

					var nodeContext = _networkContext.Nodes[nodeID];

					var floorPos = floorNodes[nodeGlobal.IdxProcessed]._position3D;
					nodeContext.Position = _floorTransform.TransformPoint(new Vector3(floorPos.x, floorPos.y, floorPos.z));
					nodeContext.Dirty = true;

					foreach (var link in _networkGlobal.NodeLinkMatrixUndir[nodeID])
					{
						var linkContext = _networkContext.Links[link.ID];
						linkContext.BundlingStrength = _networkContext.ContextSettings.EdgeBundlingStrength;
						linkContext.Alpha = _networkContext.ContextSettings.LinkContext2FocusAlphaFactor;

						if (link.SourceNodeID == nodeID)
						{
							linkContext.BundleStart = false;

							if (nodeContext.CommunityID == _networkContext.Nodes[link.TargetNodeID].CommunityID)
							{
								_networkContext.Links[link.ID].BundlingStrength = 0f;
								_networkContext.Links[link.ID].Alpha = _networkContext.ContextSettings.LinkNormalAlphaFactor;
							}
						}
						else
						{
							linkContext.BundleEnd = false;

							if (nodeContext.CommunityID == _networkContext.Nodes[link.SourceNodeID].CommunityID)
							{
								_networkContext.Links[link.ID].BundlingStrength = 0f;
								_networkContext.Links[link.ID].Alpha = _networkContext.ContextSettings.LinkNormalAlphaFactor;
							}
						}

						link.Dirty = true;
					}
				}
			}

			_commsToUpdate.Clear();
		}

		public override TransformInterpolator GetInterpolator()
		{
			return new FloorLayoutInterpolator(_floorTransform, _networkGlobal, _networkContext, _fileLoader, _commsToUpdate);
		}

		public void UpdateOnNextApply(int commID)
		{
			_commsToUpdate.Add(commID);
		}

		public void UpdateOnNextApply(IEnumerable<int> commIDs)
		{
			_commsToUpdate.UnionWith(commIDs);
		}
	}

	public class FloorLayoutInterpolator : TransformInterpolator
	{
		MultiLayoutContext _networkContext;
		Dictionary<int, Vector3> _startPositions = new Dictionary<int, Vector3>();
		Dictionary<int, Vector3> _endPositions = new Dictionary<int, Vector3>();

		public FloorLayoutInterpolator(TransformInfo endingContextTransform, NetworkGlobal networkGlobal, MultiLayoutContext networkContext,
			NetworkFilesLoader fileLoader, HashSet<int> toUpdate)
		{
			_networkContext = networkContext;

			var floorNodes = fileLoader.FlatLayout.nodes;

			foreach (var commID in toUpdate)
			{
				networkGlobal.Communities[commID].Dirty = true;

				foreach (var node in networkGlobal.Communities[commID].Nodes)
				{
					var floorPos = floorNodes[node.IdxProcessed]._position3D;

					_startPositions[node.ID] = networkContext.Nodes[node.ID].Position;
					_endPositions[node.ID] = endingContextTransform.TransformPoint(new Vector3(floorPos.x, floorPos.y, floorPos.z));
					_networkContext.Nodes[node.ID].Dirty = true;

					foreach (var link in networkGlobal.NodeLinkMatrixUndir[node.ID])
					{
						var linkContext = _networkContext.Links[link.ID];
						linkContext.BundlingStrength = _networkContext.ContextSettings.EdgeBundlingStrength;
						linkContext.Alpha = _networkContext.ContextSettings.LinkContext2FocusAlphaFactor;

						if (link.SourceNodeID == node.ID) linkContext.BundleStart = false;
						else linkContext.BundleEnd = false;
						link.Dirty = true;
					}
				}

				foreach (var link in networkGlobal.Communities[commID].InnerLinks)
				{
					_networkContext.Links[link.ID].BundlingStrength = 0f;
					_networkContext.Links[link.ID].Alpha = _networkContext.ContextSettings.LinkNormalAlphaFactor;
					link.Dirty = true;
				}
			}

			toUpdate.Clear();
		}

		public override void Interpolate(float t)
		{
			foreach (var nodeID in _startPositions.Keys)
			{
				_networkContext.Nodes[nodeID].Position
					= Vector3.Lerp(_startPositions[nodeID], _endPositions[nodeID], Mathf.SmoothStep(0f, 1f, t));
				_networkContext.Nodes[nodeID].Dirty = true;
				_networkContext.Communities[_networkContext.Nodes[nodeID].CommunityID].Dirty = true;
			}
		}
	}

}