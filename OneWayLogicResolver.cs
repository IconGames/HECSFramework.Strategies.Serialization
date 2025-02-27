﻿using System;
using System.Collections.Generic;
using System.Linq;
using HECSFramework.Serialize;
using MessagePack;
using Strategies;

namespace HECSFramework.Core
{
    [MessagePackObject()]
    [HECSManualResolver(typeof(OneWayLogic))]
    public struct OneWayLogicResolver : IResolver<OneWayLogicResolver, OneWayLogic>
    {
        [Key(0)]
        public List<NodeContext> Nodes;

        [Key(1)]
        public string Name;
        public OneWayLogicResolver In(ref OneWayLogic data)
        {
            Nodes = new List<NodeContext>();
            Dictionary<BaseDecisionNode, int> nodeIndices = new();
            
            for (var i = 0; i < data.nodes.Count; i++)
            {
                var node = data.nodes[i];
                nodeIndices.Add(node, i);
            }

            foreach (var node in data.nodes)
            {
                var nodeData = EntityManager.ResolversMap.SerializeCustom(node);
                
                var typeIndexSpan = new ReadOnlySpan<byte>(nodeData, 0, 4);
                var typeIndex = BitConverter.ToInt32(typeIndexSpan);

                Type nodeType = EntityManager.ResolversMap.GetTypeByIndex(typeIndex);
                
                var nodeContext = StrategyResolverHelper.SerializeNode(nodeType, node, nodeData, nodeIndices);
                Nodes.Add(nodeContext); 
            }

            Name = data.name;
            return this;
        }

        public void Out(ref OneWayLogic data)
        {
            var nodeMap = new Dictionary<int, BaseDecisionNode>();

            foreach (var nodeContext in Nodes)
            {
                var node = EntityManager.ResolversMap.Deserialize<BaseDecisionNode>(nodeContext.NodeData);
                nodeMap.Add(nodeContext.Index, node);
            }

            foreach (var nodeContext in Nodes)
            {
                var typeIndexSpan = new ReadOnlySpan<byte>(nodeContext.NodeData, 0, 4);
                var typeIndex = BitConverter.ToInt32(typeIndexSpan);

                Type nodeType = EntityManager.ResolversMap.GetTypeByIndex(typeIndex);
                var connectionFields = StrategyResolverHelper.GetConnectionFields(nodeType).ToList();

                var node = nodeMap[nodeContext.Index];
                foreach (var connection in nodeContext.Connections)
                {
                    var field = connectionFields.FirstOrDefault(f => f.Name.Equals(connection.Name));
                    if (field != null)
                        field.SetValue(node, nodeMap[connection.NodeIndex]);
                }
            }

            data.nodes = nodeMap.Values.ToList();

            data.name = Name;
        }
    }
}