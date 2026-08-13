using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;

namespace BpmnVisualModeller.Models
{
    public static class BpmnPoolLayout
    {
        private const int DefaultPoolWidth = 1100;
        private const int DefaultLaneHeight = 160;
        private const int PoolHeaderHeight = 45;
        private const int PoolPadding = 40;

        public static void Normalize(
            List<BpmnPool> pools,
            List<BpmnLane> lanes,
            BpmnProcess process,
            Dictionary<string, Rectangle> shapeBounds = null)
        {
            if (pools == null || lanes == null || process == null)
                return;

            EnsureDefaultPoolForOrphanLanes(pools, lanes, process);
            LinkLanesToPools(pools, lanes);
            ApplyDefaultBounds(pools);
            AssignUnplacedNodes(pools, lanes, process, shapeBounds);
        }

        private static void EnsureDefaultPoolForOrphanLanes(
            List<BpmnPool> pools,
            List<BpmnLane> lanes,
            BpmnProcess process)
        {
            if (!lanes.Any())
                return;

            var orphanLanes = lanes.Where(l => string.IsNullOrEmpty(l.ParentPoolId) ||
                pools.All(p => p.Id != l.ParentPoolId)).ToList();

            if (!orphanLanes.Any() && pools.Any())
                return;

            if (!pools.Any() || orphanLanes.Any())
            {
                string poolId = "Pool_" + (process.Id ?? "default");
                var existing = pools.FirstOrDefault(p => p.Id == poolId);
                if (existing == null)
                {
                    existing = new BpmnPool
                    {
                        Id = poolId,
                        Name = string.IsNullOrEmpty(process.Name) ? "Процесс" : process.Name,
                        ProcessRef = process.Id
                    };
                    pools.Add(existing);
                }

                foreach (var lane in orphanLanes)
                {
                    lane.ParentPoolId = existing.Id;
                    if (!existing.Lanes.Contains(lane))
                        existing.Lanes.Add(lane);
                }
            }
        }

        private static void LinkLanesToPools(List<BpmnPool> pools, List<BpmnLane> lanes)
        {
            foreach (var pool in pools)
            {
                if (pool.Lanes == null)
                    pool.Lanes = new List<BpmnLane>();
            }

            foreach (var lane in lanes)
            {
                var pool = pools.FirstOrDefault(p => p.Id == lane.ParentPoolId);
                if (pool != null && !pool.Lanes.Contains(lane))
                    pool.Lanes.Add(lane);
            }

            foreach (var pool in pools)
            {
                int order = 0;
                foreach (var lane in pool.Lanes.OrderBy(l => l.Order).ThenBy(l => l.Id))
                    lane.Order = order++;
            }
        }

        public static void ApplyDefaultBounds(List<BpmnPool> pools)
        {
            int poolY = PoolPadding;

            foreach (var pool in pools)
            {
                bool poolNeedsBounds = pool.Bounds.Width <= 0 || pool.Bounds.Height <= 0;

                if (poolNeedsBounds)
                {
                    int laneCount = Math.Max(pool.Lanes.Count, 1);
                    int poolHeight = PoolHeaderHeight + laneCount * DefaultLaneHeight + 25;
                    pool.Bounds = new Rectangle(PoolPadding, poolY, DefaultPoolWidth, poolHeight);
                    poolY += poolHeight + 30;
                }

                int laneY = pool.Bounds.Y + PoolHeaderHeight;
                int laneOrder = 0;

                foreach (var lane in pool.Lanes.OrderBy(l => l.Order))
                {
                    if (lane.Bounds.Width <= 0 || lane.Bounds.Height <= 0)
                    {
                        lane.Bounds = new Rectangle(
                            pool.Bounds.X + 5,
                            laneY,
                            pool.Bounds.Width - 10,
                            DefaultLaneHeight);
                        laneY += DefaultLaneHeight + 5;
                    }

                    lane.Order = laneOrder++;
                }

                if (poolNeedsBounds && pool.Lanes.Any())
                {
                    int maxBottom = pool.Lanes.Max(l => l.Bounds.Bottom);
                    pool.Bounds = new Rectangle(
                        pool.Bounds.X,
                        pool.Bounds.Y,
                        pool.Bounds.Width,
                        maxBottom - pool.Bounds.Y + 15);
                }
            }
        }

        public static void AssignUnplacedNodes(
            List<BpmnPool> pools,
            List<BpmnLane> lanes,
            BpmnProcess process,
            Dictionary<string, Rectangle> shapeBounds)
        {
            foreach (var node in process.Nodes.Values)
            {
                if (!string.IsNullOrEmpty(node.LaneId))
                    continue;

                if (shapeBounds != null && shapeBounds.Count > 0)
                {
                    var nodeRect = shapeBounds.ContainsKey(node.Id) ? shapeBounds[node.Id] : default;
                    if (nodeRect.Width > 0)
                    {
                        var lane = lanes
                            .Where(l => l.Bounds.Contains(nodeRect.X + nodeRect.Width / 2,
                                nodeRect.Y + nodeRect.Height / 2))
                            .OrderBy(l => l.Bounds.Height)
                            .FirstOrDefault();

                        if (lane != null)
                        {
                            AssignNodeToLane(node, lane, pools);
                            continue;
                        }
                    }
                }

                var pool = pools.FirstOrDefault();
                if (pool == null)
                    continue;

                var targetLane = pool.Lanes.OrderBy(l => l.Order).FirstOrDefault();
                if (targetLane == null)
                {
                    targetLane = new BpmnLane
                    {
                        Id = pool.Id + "_lane_1",
                        Name = "Дорожка 1",
                        ParentPoolId = pool.Id,
                        Order = 0
                    };
                    lanes.Add(targetLane);
                    pool.Lanes.Add(targetLane);
                }

                AssignNodeToLane(node, targetLane, pools);
            }
        }

        private static void AssignNodeToLane(BpmnNode node, BpmnLane lane, List<BpmnPool> pools)
        {
            node.LaneId = lane.Id;
            node.PoolId = lane.ParentPoolId;

            if (!lane.ChildNodeIds.Contains(node.Id))
                lane.ChildNodeIds.Add(node.Id);

            var pool = pools.FirstOrDefault(p => p.Id == lane.ParentPoolId);
            if (pool != null && !pool.ChildNodeIds.Contains(node.Id))
                pool.ChildNodeIds.Add(node.Id);
        }
    }
}
