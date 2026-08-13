using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Xml.Linq;

namespace BpmnVisualModeller.Models
{
    public class BpmnParser
    {
        public List<BpmnPool> Pools { get; private set; } = new List<BpmnPool>();
        public List<BpmnLane> Lanes { get; private set; } = new List<BpmnLane>();
        public Dictionary<string, Rectangle> ShapeBounds { get; private set; } = new Dictionary<string, Rectangle>();
        public Dictionary<string, List<Point>> EdgeWaypoints { get; private set; } = new Dictionary<string, List<Point>>();

        public bool HasDiagramLayout(BpmnProcess process)
        {
            if (process == null || ShapeBounds.Count == 0)
                return false;

            return process.Nodes.Keys.All(id =>
                ShapeBounds.TryGetValue(id, out var r) && r.Width > 0 && r.Height > 0);
        }

        public (BpmnProcess process, List<BpmnPool> pools, List<BpmnLane> lanes) ParseWithPools(string xmlFilePath)
        {
            var process = Parse(xmlFilePath);
            return (process, Pools, Lanes);
        }

        public BpmnProcess Parse(string xmlFilePath)
        {
            Pools.Clear();
            Lanes.Clear();
            ShapeBounds.Clear();
            EdgeWaypoints.Clear();

            var process = new BpmnProcess();
            XDocument doc = XDocument.Load(xmlFilePath);

            XNamespace bpmn = "http://www.omg.org/spec/BPMN/20100524/MODEL";
            XNamespace bpmndi = "http://www.omg.org/spec/BPMN/20100524/DI";
            XNamespace dc = "http://www.omg.org/spec/DD/20100524/DC";
            XNamespace di = "http://www.omg.org/spec/DD/20100524/DI";

            var processElement = doc.Descendants(bpmn + "process").FirstOrDefault();
            if (processElement == null)
                throw new Exception("BPMN process element not found");

            process.Id = processElement.Attribute("id")?.Value;
            process.Name = processElement.Attribute("name")?.Value;

            ParsePoolsAndLanes(doc, bpmn, bpmndi, dc, di, process);

            ParseStartEvents(process, doc, bpmn);
            ParseEndEvents(process, doc, bpmn);
            ParseTasks(process, doc, bpmn);
            ParseExclusiveGateways(process, doc, bpmn);
            ParseParallelGateways(process, doc, bpmn);
            ParseInclusiveGateways(process, doc, bpmn);
            ParseIntermediateEvents(process, doc, bpmn);
            ParseSequenceFlows(process, doc, bpmn);

            AssignNodesToLanes(process);
            SortLanesByVerticalPosition();
            BpmnPoolLayout.Normalize(Pools, Lanes, process, ShapeBounds);

            return process;
        }

        private void ParsePoolsAndLanes(
            XDocument doc,
            XNamespace bpmn,
            XNamespace bpmndi,
            XNamespace dc,
            XNamespace di,
            BpmnProcess process)
        {
            var plane = doc.Descendants(bpmndi + "BPMNPlane").FirstOrDefault();

            if (plane != null)
            {
                foreach (var shape in plane.Descendants(bpmndi + "BPMNShape"))
                {
                    var bpmnElement = shape.Attribute("bpmnElement")?.Value;
                    if (string.IsNullOrEmpty(bpmnElement))
                        continue;

                    var bounds = shape.Descendants(dc + "Bounds").FirstOrDefault();
                    if (bounds == null)
                        continue;

                    ShapeBounds[bpmnElement] = new Rectangle(
                        int.Parse(bounds.Attribute("x")?.Value ?? "0"),
                        int.Parse(bounds.Attribute("y")?.Value ?? "0"),
                        int.Parse(bounds.Attribute("width")?.Value ?? "0"),
                        int.Parse(bounds.Attribute("height")?.Value ?? "0"));
                }

                foreach (var edge in plane.Descendants(bpmndi + "BPMNEdge"))
                {
                    var flowId = edge.Attribute("bpmnElement")?.Value;
                    if (string.IsNullOrEmpty(flowId))
                        continue;

                    var points = edge.Descendants(di + "waypoint")
                        .Select(wp => new Point(
                            (int)double.Parse(wp.Attribute("x")?.Value ?? "0", System.Globalization.CultureInfo.InvariantCulture),
                            (int)double.Parse(wp.Attribute("y")?.Value ?? "0", System.Globalization.CultureInfo.InvariantCulture)))
                        .ToList();

                    if (points.Count >= 2)
                        EdgeWaypoints[flowId] = points;
                }
            }

            var processToPool = new Dictionary<string, string>();

            int poolOrder = 0;
            foreach (var element in doc.Descendants(bpmn + "participant"))
            {
                var pool = new BpmnPool
                {
                    Id = element.Attribute("id")?.Value,
                    Name = element.Attribute("name")?.Value ?? $"Пул {poolOrder + 1}",
                    ProcessRef = element.Attribute("processRef")?.Value
                };

                if (ShapeBounds.TryGetValue(pool.Id, out var poolRect))
                    pool.Bounds = poolRect;

                Pools.Add(pool);

                if (!string.IsNullOrEmpty(pool.ProcessRef))
                    processToPool[pool.ProcessRef] = pool.Id;

                poolOrder++;
            }

            foreach (var laneSet in doc.Descendants(bpmn + "laneSet"))
            {
                string processId = FindOwningProcessId(laneSet, bpmn);
                string poolId = null;

                if (!string.IsNullOrEmpty(processId) && processToPool.TryGetValue(processId, out var mappedPoolId))
                    poolId = mappedPoolId;

                int laneOrder = 0;
                foreach (var element in laneSet.Elements(bpmn + "lane"))
                {
                    var lane = ParseLaneElement(element, bpmn, poolId, laneOrder);
                    Lanes.Add(lane);
                    laneOrder++;
                }
            }

            foreach (var lane in Lanes)
            {
                if (string.IsNullOrEmpty(lane.ParentPoolId))
                    lane.ParentPoolId = Pools.FirstOrDefault()?.Id;

                var pool = Pools.FirstOrDefault(p => p.Id == lane.ParentPoolId);
                if (pool != null && !pool.Lanes.Contains(lane))
                    pool.Lanes.Add(lane);
            }

            foreach (var pool in Pools)
            {
                if (pool.Lanes.Any() && (pool.Bounds.Width <= 0 || pool.Bounds.Height <= 0))
                {
                    var minX = pool.Lanes.Min(l => l.Bounds.X);
                    var minY = pool.Lanes.Min(l => l.Bounds.Y);
                    var maxX = pool.Lanes.Max(l => l.Bounds.Right);
                    var maxY = pool.Lanes.Max(l => l.Bounds.Bottom);
                    pool.Bounds = new Rectangle(minX, minY, maxX - minX, maxY - minY);
                }
            }
        }

        private BpmnLane ParseLaneElement(XElement element, XNamespace bpmn, string poolId, int order)
        {
            var lane = new BpmnLane
            {
                Id = element.Attribute("id")?.Value,
                Name = element.Attribute("name")?.Value ?? $"Дорожка {order + 1}",
                ParentPoolId = poolId,
                Order = order
            };

            if (ShapeBounds.TryGetValue(lane.Id, out var laneRect))
                lane.Bounds = laneRect;

            foreach (var nodeRef in element.Descendants(bpmn + "flowNodeRef"))
            {
                if (!string.IsNullOrEmpty(nodeRef.Value))
                    lane.ChildNodeIds.Add(nodeRef.Value);
            }

            return lane;
        }

        private void SortLanesByVerticalPosition()
        {
            foreach (var pool in Pools)
            {
                if (!pool.Lanes.Any(l => l.Bounds.Height > 0))
                    continue;

                int order = 0;
                foreach (var lane in pool.Lanes.OrderBy(l => l.Bounds.Y).ThenBy(l => l.Id))
                    lane.Order = order++;
            }
        }

        private static string FindOwningProcessId(XElement element, XNamespace bpmn)
        {
            var current = element.Parent;
            while (current != null)
            {
                if (current.Name == bpmn + "process")
                    return current.Attribute("id")?.Value;
                current = current.Parent;
            }

            return null;
        }

        private void AssignNodesToLanes(BpmnProcess process)
        {
            foreach (var lane in Lanes)
            {
                foreach (var nodeId in lane.ChildNodeIds)
                {
                    if (!process.Nodes.ContainsKey(nodeId))
                        continue;

                    process.Nodes[nodeId].LaneId = lane.Id;
                    process.Nodes[nodeId].PoolId = lane.ParentPoolId;

                    var pool = Pools.FirstOrDefault(p => p.Id == lane.ParentPoolId);
                    if (pool != null && !pool.ChildNodeIds.Contains(nodeId))
                        pool.ChildNodeIds.Add(nodeId);
                }
            }
        }

        private void ParseStartEvents(BpmnProcess process, XDocument doc, XNamespace bpmn)
        {
            foreach (var element in doc.Descendants(bpmn + "startEvent"))
            {
                process.Nodes[element.Attribute("id")?.Value] = new StartEvent
                {
                    Id = element.Attribute("id")?.Value,
                    Name = element.Attribute("name")?.Value
                };
            }
        }

        private void ParseEndEvents(BpmnProcess process, XDocument doc, XNamespace bpmn)
        {
            foreach (var element in doc.Descendants(bpmn + "endEvent"))
            {
                process.Nodes[element.Attribute("id")?.Value] = new EndEvent
                {
                    Id = element.Attribute("id")?.Value,
                    Name = element.Attribute("name")?.Value
                };
            }
        }

        private void ParseTasks(BpmnProcess process, XDocument doc, XNamespace bpmn)
        {
            foreach (var element in doc.Descendants(bpmn + "task"))
            {
                process.Nodes[element.Attribute("id")?.Value] = CreateTaskFromElement(element, BpmnTaskKind.Generic);
            }

            var typedTasks = new[]
            {
                ("userTask", BpmnTaskKind.User),
                ("serviceTask", BpmnTaskKind.Service),
                ("sendTask", BpmnTaskKind.Send),
                ("receiveTask", BpmnTaskKind.Receive),
                ("manualTask", BpmnTaskKind.Manual),
                ("businessRuleTask", BpmnTaskKind.BusinessRule),
                ("scriptTask", BpmnTaskKind.Script),
                ("callActivity", BpmnTaskKind.CallActivity),
            };

            foreach (var (tag, kind) in typedTasks)
            {
                foreach (var element in doc.Descendants(bpmn + tag))
                {
                    process.Nodes[element.Attribute("id")?.Value] = CreateTaskFromElement(element, kind);
                }
            }

            foreach (var element in doc.Descendants(bpmn + "subProcess"))
            {
                bool expanded = string.Equals(
                    element.Attribute("isExpanded")?.Value, "true",
                    StringComparison.OrdinalIgnoreCase);
                var task = CreateTaskFromElement(element, BpmnTaskKind.SubProcess);
                task.IsExpandedSubProcess = expanded;
                process.Nodes[task.Id] = task;
            }
        }

        private static Task CreateTaskFromElement(XElement element, BpmnTaskKind kind)
        {
            return new Task
            {
                Id = element.Attribute("id")?.Value,
                Name = element.Attribute("name")?.Value,
                Implementation = kind == BpmnTaskKind.Generic
                    ? element.Attribute("implementation")?.Value
                    : element.Name.LocalName,
                TaskKind = kind
            };
        }

        private void ParseExclusiveGateways(BpmnProcess process, XDocument doc, XNamespace bpmn)
        {
            foreach (var element in doc.Descendants(bpmn + "exclusiveGateway"))
            {
                process.Nodes[element.Attribute("id")?.Value] = new ExclusiveGateway
                {
                    Id = element.Attribute("id")?.Value,
                    Name = element.Attribute("name")?.Value
                };
            }
        }

        private void ParseParallelGateways(BpmnProcess process, XDocument doc, XNamespace bpmn)
        {
            foreach (var element in doc.Descendants(bpmn + "parallelGateway"))
            {
                process.Nodes[element.Attribute("id")?.Value] = new ParallelGateway
                {
                    Id = element.Attribute("id")?.Value,
                    Name = element.Attribute("name")?.Value
                };
            }
        }

        private void ParseInclusiveGateways(BpmnProcess process, XDocument doc, XNamespace bpmn)
        {
            foreach (var element in doc.Descendants(bpmn + "inclusiveGateway"))
            {
                process.Nodes[element.Attribute("id")?.Value] = new InclusiveGateway
                {
                    Id = element.Attribute("id")?.Value,
                    Name = element.Attribute("name")?.Value
                };
            }
        }

        private void ParseIntermediateEvents(BpmnProcess process, XDocument doc, XNamespace bpmn)
        {
            foreach (var element in doc.Descendants(bpmn + "intermediateCatchEvent"))
            {
                process.Nodes[element.Attribute("id")?.Value] = new IntermediateEvent
                {
                    Id = element.Attribute("id")?.Value,
                    Name = element.Attribute("name")?.Value,
                    EventType = "Catch"
                };
            }

            foreach (var element in doc.Descendants(bpmn + "intermediateThrowEvent"))
            {
                process.Nodes[element.Attribute("id")?.Value] = new IntermediateEvent
                {
                    Id = element.Attribute("id")?.Value,
                    Name = element.Attribute("name")?.Value,
                    EventType = "Throw"
                };
            }
        }

        private void ParseSequenceFlows(BpmnProcess process, XDocument doc, XNamespace bpmn)
        {
            foreach (var element in doc.Descendants(bpmn + "sequenceFlow"))
            {
                var flow = new SequenceFlow
                {
                    Id = element.Attribute("id")?.Value,
                    Name = element.Attribute("name")?.Value,
                    SourceRef = element.Attribute("sourceRef")?.Value,
                    TargetRef = element.Attribute("targetRef")?.Value,
                };

                var conditionExpression = element.Descendants(bpmn + "conditionExpression").FirstOrDefault();
                if (conditionExpression != null)
                    flow.ConditionExpression = conditionExpression.Value;

                if (!process.OutgoingFlows.ContainsKey(flow.SourceRef))
                    process.OutgoingFlows[flow.SourceRef] = new List<SequenceFlow>();

                process.OutgoingFlows[flow.SourceRef].Add(flow);
            }
        }
    }
}
