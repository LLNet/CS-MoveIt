﻿using UnityEngine;

using System;
using System.Threading;
using System.Collections.Generic;

using ColossalFramework;
using ColossalFramework.Math;

namespace MoveIt
{
    internal class Moveable
    {
        public InstanceID id;
        public List<Moveable> subInstances;

        private Vector3 m_startPosition;
        private float m_startAngle;
        private Vector3[] m_startNodeDirections;

        public Vector3 position
        {
            get
            {
                switch (id.Type)
                {
                    case InstanceType.Building:
                        {
                            return BuildingManager.instance.m_buildings.m_buffer[id.Building].m_position;
                        }
                    case InstanceType.Prop:
                        {
                            return PropManager.instance.m_props.m_buffer[id.Prop].Position;
                        }
                    case InstanceType.Tree:
                        {
                            return TreeManager.instance.m_trees.m_buffer[id.Tree].Position;
                        }
                    case InstanceType.NetNode:
                        {
                            return NetManager.instance.m_nodes.m_buffer[id.NetNode].m_position;
                        }
                }

                return Vector3.zero;
            }
        }

        public override bool Equals(object obj)
        {
            Moveable instance = obj as Moveable;
            if (instance != null)
            {
                return instance.id == id;
            }

            return base.Equals(obj);
        }

        public override int GetHashCode()
        {
            return id.GetHashCode();
        }

        public Moveable(InstanceID instance)
        {
            id = instance;

            switch (id.Type)
            {
                case InstanceType.Building:
                    {
                        Building[] buildingBuffer = BuildingManager.instance.m_buildings.m_buffer;
                        NetNode[] nodeBuffer = NetManager.instance.m_nodes.m_buffer;

                        m_startPosition = buildingBuffer[id.Building].m_position;
                        m_startAngle = buildingBuffer[id.Building].m_angle;

                        subInstances = new List<Moveable>();

                        ushort node = buildingBuffer[id.Building].m_netNode;
                        while (node != 0)
                        {
                            ItemClass.Layer layer = nodeBuffer[node].Info.m_class.m_layer;
                            if (layer != ItemClass.Layer.PublicTransport)
                            {
                                InstanceID nodeID = default(InstanceID);
                                nodeID.NetNode = node;
                                subInstances.Add(new Moveable(nodeID));
                            }

                            node = nodeBuffer[node].m_nextBuildingNode;
                        }

                        ushort building = buildingBuffer[id.Building].m_subBuilding;
                        while (building != 0)
                        {
                            Moveable subBuilding = new Moveable(InstanceID.Empty);
                            subBuilding.id.Building = building;
                            subBuilding.m_startPosition = buildingBuffer[building].m_position;
                            subBuilding.m_startAngle = buildingBuffer[building].m_angle;
                            subInstances.Add(subBuilding);

                            node = buildingBuffer[building].m_netNode;
                            while (node != 0)
                            {
                                ItemClass.Layer layer = nodeBuffer[node].Info.m_class.m_layer;

                                if (layer != ItemClass.Layer.PublicTransport)
                                {
                                    InstanceID nodeID = default(InstanceID);
                                    nodeID.NetNode = node;
                                    subInstances.Add(new Moveable(nodeID));
                                }

                                node = nodeBuffer[node].m_nextBuildingNode;
                            }

                            building = buildingBuffer[building].m_subBuilding;
                        }

                        if (subInstances.Count == 0)
                        {
                            subInstances = null;
                        }
                        break;
                    }
                case InstanceType.Prop:
                    {
                        m_startPosition = PropManager.instance.m_props.m_buffer[id.Prop].Position;
                        m_startAngle = PropManager.instance.m_props.m_buffer[id.Prop].m_angle;
                        break;
                    }
                case InstanceType.Tree:
                    {
                        m_startPosition = TreeManager.instance.m_trees.m_buffer[id.Tree].Position;
                        break;
                    }
                case InstanceType.NetNode:
                    {
                        NetManager netManager = NetManager.instance;

                        m_startPosition = netManager.m_nodes.m_buffer[id.NetNode].m_position;

                        m_startNodeDirections = new Vector3[8];
                        for (int i = 0; i < 8; i++)
                        {
                            ushort segment = netManager.m_nodes.m_buffer[id.NetNode].GetSegment(i);
                            if (segment != 0)
                            {
                                m_startNodeDirections[i] = netManager.m_segments.m_buffer[segment].GetDirection(id.NetNode);
                            }
                        }

                        break;
                    }
            }
        }

        public void Transform(Vector3 deltaPosition, ushort deltaAngle, Vector3 center)
        {
            float fAngle = deltaAngle * 9.58738E-05f;

            Matrix4x4 matrix4x = default(Matrix4x4);
            matrix4x.SetTRS(center, Quaternion.AngleAxis(fAngle * 57.29578f, Vector3.down), Vector3.one);

            Vector3 newPosition = matrix4x.MultiplyPoint(m_startPosition - center) + deltaPosition;

            Move(newPosition, deltaAngle);

            if (subInstances != null)
            {
                matrix4x.SetTRS(newPosition, Quaternion.AngleAxis(fAngle * 57.29578f, Vector3.down), Vector3.one);

                foreach (Moveable subInstance in subInstances)
                {
                    Vector3 subPosition = subInstance.m_startPosition - m_startPosition;
                    subPosition = matrix4x.MultiplyPoint(subPosition);

                    subInstance.Move(subPosition, deltaAngle);
                }
            }
        }

        public Bounds GetBounds()
        {
            Bounds bounds = GetBounds(id);

            if (subInstances != null)
            {
                foreach (Moveable subInstance in subInstances)
                {
                    bounds.Encapsulate(subInstance.GetBounds());
                }
            }

            return bounds;
        }

        private Bounds GetBounds(InstanceID instance)
        {
            switch (instance.Type)
            {
                case InstanceType.Building:
                    {
                        Building[] buildingBuffer = BuildingManager.instance.m_buildings.m_buffer;
                        NetNode[] nodeBuffer = NetManager.instance.m_nodes.m_buffer;

                        ushort id = instance.Building;
                        BuildingInfo info = buildingBuffer[id].Info;

                        float radius = Mathf.Max(info.m_cellWidth * 4f, info.m_cellLength * 4f);
                        Bounds bounds = new Bounds(buildingBuffer[id].m_position, new Vector3(radius, 0, radius));

                        return bounds;
                    }
                case InstanceType.Prop:
                    {
                        PropInstance[] buffer = PropManager.instance.m_props.m_buffer;
                        ushort id = instance.Prop;
                        PropInfo info = buffer[id].Info;

                        Randomizer randomizer = new Randomizer(id);
                        float scale = info.m_minScale + (float)randomizer.Int32(10000u) * (info.m_maxScale - info.m_minScale) * 0.0001f;
                        float radius = Mathf.Max(info.m_generatedInfo.m_size.x, info.m_generatedInfo.m_size.z) * scale;

                        return new Bounds(buffer[id].Position, new Vector3(radius, 0, radius));
                    }
                case InstanceType.Tree:
                    {
                        TreeInstance[] buffer = TreeManager.instance.m_trees.m_buffer;
                        uint id = instance.Tree;
                        TreeInfo info = buffer[id].Info;

                        Randomizer randomizer = new Randomizer(id);
                        float scale = info.m_minScale + (float)randomizer.Int32(10000u) * (info.m_maxScale - info.m_minScale) * 0.0001f;
                        float radius = Mathf.Max(info.m_generatedInfo.m_size.x, info.m_generatedInfo.m_size.z) * scale;

                        return new Bounds(buffer[id].Position, new Vector3(radius, 0, radius));
                    }
                case InstanceType.NetNode:
                    {
                        return NetManager.instance.m_nodes.m_buffer[instance.NetNode].m_bounds;
                    }
            }

            return default(Bounds);
        }

        private void Move(Vector3 location, ushort deltaAngle)
        {
            switch (id.Type)
            {
                case InstanceType.Building:
                    {
                        BuildingManager buildingManager = BuildingManager.instance;
                        ushort building = id.Building;

                        RelocateBuilding(building, ref buildingManager.m_buildings.m_buffer[building], location, m_startAngle + deltaAngle * 9.58738E-05f);
                        break;
                    }
                case InstanceType.Prop:
                    {
                        ushort prop = id.Prop;
                        PropManager.instance.m_props.m_buffer[prop].m_angle = (ushort)((ushort)m_startAngle + deltaAngle);
                        PropManager.instance.MoveProp(prop, location);
                        PropManager.instance.UpdatePropRenderer(prop, true);
                        break;
                    }
                case InstanceType.Tree:
                    {
                        uint tree = id.Tree;
                        TreeManager.instance.MoveTree(tree, location);
                        TreeManager.instance.UpdateTreeRenderer(tree, true);
                        break;
                    }
                case InstanceType.NetNode:
                    {
                        NetManager netManager = NetManager.instance;
                        ushort node = id.NetNode;

                        float netAngle = deltaAngle * 9.58738E-05f;

                        Matrix4x4 matrix4x = default(Matrix4x4);
                        matrix4x.SetTRS(m_startPosition, Quaternion.AngleAxis(netAngle * 57.29578f, Vector3.down), Vector3.one);

                        for (int i = 0; i < 8; i++)
                        {
                            ushort segment = netManager.m_nodes.m_buffer[node].GetSegment(i);
                            if (segment != 0)
                            {
                                Vector3 newDirection = matrix4x.MultiplyVector(m_startNodeDirections[i]);

                                if (netManager.m_segments.m_buffer[segment].m_startNode == node)
                                {
                                    netManager.m_segments.m_buffer[segment].m_startDirection = newDirection;
                                }
                                else
                                {
                                    netManager.m_segments.m_buffer[segment].m_endDirection = newDirection;
                                }

                                netManager.UpdateSegmentRenderer(segment, true);
                            }
                        }

                        netManager.MoveNode(node, location);
                        netManager.UpdateNodeRenderer(node, true);
                        break;
                    }
            }
        }

        private void RelocateBuilding(ushort building, ref Building data, Vector3 position, float angle)
        {
            BuildingManager buildingManager = BuildingManager.instance;

            BuildingInfo info = data.Info;
            RemoveFromGrid(building, ref data);
            data.UpdateBuilding(building);
            buildingManager.UpdateBuildingRenderer(building, true);
            if (info.m_hasParkingSpaces)
            {
                buildingManager.UpdateParkingSpaces(building, ref data);
            }
            data.m_position = position;
            data.m_angle = angle;

            AddToGrid(building, ref data);
            data.CalculateBuilding(building);
            data.UpdateBuilding(building);
            buildingManager.UpdateBuildingRenderer(building, true);
        }

        private static void AddToGrid(ushort building, ref Building data)
        {
            BuildingManager buildingManager = BuildingManager.instance;

            int num = Mathf.Clamp((int)(data.m_position.x / 64f + 135f), 0, 269);
            int num2 = Mathf.Clamp((int)(data.m_position.z / 64f + 135f), 0, 269);
            int num3 = num2 * 270 + num;
            while (!Monitor.TryEnter(buildingManager.m_buildingGrid, SimulationManager.SYNCHRONIZE_TIMEOUT))
            {
            }
            try
            {
                buildingManager.m_buildings.m_buffer[(int)building].m_nextGridBuilding = buildingManager.m_buildingGrid[num3];
                buildingManager.m_buildingGrid[num3] = building;
            }
            finally
            {
                Monitor.Exit(buildingManager.m_buildingGrid);
            }
        }

        private static void RemoveFromGrid(ushort building, ref Building data)
        {
            BuildingManager buildingManager = BuildingManager.instance;

            BuildingInfo info = data.Info;
            int num = Mathf.Clamp((int)(data.m_position.x / 64f + 135f), 0, 269);
            int num2 = Mathf.Clamp((int)(data.m_position.z / 64f + 135f), 0, 269);
            int num3 = num2 * 270 + num;
            while (!Monitor.TryEnter(buildingManager.m_buildingGrid, SimulationManager.SYNCHRONIZE_TIMEOUT))
            {
            }
            try
            {
                ushort num4 = 0;
                ushort num5 = buildingManager.m_buildingGrid[num3];
                int num6 = 0;
                while (num5 != 0)
                {
                    if (num5 == building)
                    {
                        if (num4 == 0)
                        {
                            buildingManager.m_buildingGrid[num3] = data.m_nextGridBuilding;
                        }
                        else
                        {
                            buildingManager.m_buildings.m_buffer[(int)num4].m_nextGridBuilding = data.m_nextGridBuilding;
                        }
                        break;
                    }
                    num4 = num5;
                    num5 = buildingManager.m_buildings.m_buffer[(int)num5].m_nextGridBuilding;
                    if (++num6 > 49152)
                    {
                        CODebugBase<LogChannel>.Error(LogChannel.Core, "Invalid list detected!\n" + Environment.StackTrace);
                        break;
                    }
                }
                data.m_nextGridBuilding = 0;
            }
            finally
            {
                Monitor.Exit(buildingManager.m_buildingGrid);
            }
            if (info != null)
            {
                Singleton<RenderManager>.instance.UpdateGroup(num * 45 / 270, num2 * 45 / 270, info.m_prefabDataLayer);
            }
        }
    }
}