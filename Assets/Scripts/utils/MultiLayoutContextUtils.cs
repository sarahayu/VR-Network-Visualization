using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Neo4j.Driver;
using UnityEngine;

namespace VidiGraph
{
    public class MultiLayoutContextUtils
    {
        MultiLayoutContext _context;
        NetworkManager _manager;
        NetworkFileData _fileData;

        IDictionary<string, object> _sampleGlobalNodeProps;
        IDictionary<string, object> _sampleContextNodeProps;
        IDictionary<string, object> _sampleFileNodeProps;

        public MultiLayoutContextUtils(MultiLayoutContext context, NetworkManager manager)
        {
            _context = context;
            _manager = manager;
            _fileData = manager.FileLoader.SphericalLayout;

            _sampleGlobalNodeProps = ObjectUtils.AsDictionary(manager.NetworkGlobal.Nodes.NodeArray.First());
            _sampleContextNodeProps = ObjectUtils.AsDictionary(context.Nodes.First().Value);
            _sampleFileNodeProps = ObjectUtils.AsDictionary(manager.FileLoader.SphericalLayout.nodes.First().props);
        }

        public bool TryCastNodeProp<T>(string propname)
        {
            try
            {
                if (_sampleGlobalNodeProps.ContainsKey(propname))
                {
                    var p = _sampleGlobalNodeProps[propname].As<T>();
                }
                else if (_sampleContextNodeProps.ContainsKey(propname))
                {
                    var p = _sampleContextNodeProps[propname].As<T>();

                }
                else if (_sampleFileNodeProps.ContainsKey(propname))
                {
                    var p = _sampleFileNodeProps[propname].As<T>();
                }
                else
                {
                    throw new InvalidCastException();
                }
            }
            catch (InvalidCastException e)
            {
                Debug.LogError($"Could not cast property {propname} to requested type: {e.Message}");
                return false;
            }

            return true;
        }

        public T CastNodeProp<T>(int ID, string propname)
        {

            if (_sampleGlobalNodeProps.ContainsKey(propname))
            {
                return ObjectUtils.AsDictionary(_manager.NetworkGlobal.Nodes[ID])[propname].As<T>();
            }
            else if (_sampleContextNodeProps.ContainsKey(propname))
            {
                return ObjectUtils.AsDictionary(_context.Nodes[ID])[propname].As<T>();
            }
            else
            {
                var idx = _manager.NetworkGlobal.Nodes[ID].IdxProcessed;
                return ObjectUtils.AsDictionary(_fileData.nodes[idx].props)[propname].As<T>();
            }
        }

        public U GetNodeProp<T, U>(int ID, string propname,
            Dictionary<T, U> valToEncoding, U defaultEncoded)
        {
            var propVal = CastNodeProp<T>(ID, propname);

            if (propVal == null) return defaultEncoded;
            if (valToEncoding.ContainsKey(propVal)) return valToEncoding[propVal];

            return defaultEncoded;
        }
    }
}