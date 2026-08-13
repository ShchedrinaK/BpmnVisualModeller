using System;
using System.Collections.Generic;
using System.Drawing;

namespace BpmnVisualModeller.Models
{
    public enum NodeType
    {
        StartEvent,
        EndEvent,
        Task,
        ExclusiveGateway,
        ParallelGateway,
        InclusiveGateway,
        IntermediateEvent
    }

    public abstract class BpmnNode
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public abstract NodeType Type { get; }
        public string LaneId { get; set; }
        public string PoolId { get; set; }
    }

    public class StartEvent : BpmnNode
    {
        public override NodeType Type => NodeType.StartEvent;
    }

    public class EndEvent : BpmnNode
    {
        public override NodeType Type => NodeType.EndEvent;
    }

    public enum BpmnTaskKind
    {
        Generic,
        User,
        Service,
        Send,
        Receive,
        Manual,
        BusinessRule,
        Script,
        CallActivity,
        SubProcess
    }

    public class Task : BpmnNode
    {
        public override NodeType Type => NodeType.Task;
        public string Implementation { get; set; }
        public BpmnTaskKind TaskKind { get; set; } = BpmnTaskKind.Generic;
        public bool IsExpandedSubProcess { get; set; }
    }

    public class ExclusiveGateway : BpmnNode
    {
        public override NodeType Type => NodeType.ExclusiveGateway;
        public Dictionary<string, Func<Token, bool>> Conditions { get; set; }

        public ExclusiveGateway()
        {
            Conditions = new Dictionary<string, Func<Token, bool>>();
        }
    }

    public class ParallelGateway : BpmnNode
    {
        public override NodeType Type => NodeType.ParallelGateway;
    }

    public class InclusiveGateway : BpmnNode
    {
        public override NodeType Type => NodeType.InclusiveGateway;
    }

    public class IntermediateEvent : BpmnNode
    {
        public override NodeType Type => NodeType.IntermediateEvent;
        public string EventType { get; set; }
    }

    public class SequenceFlow
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public string SourceRef { get; set; }
        public string TargetRef { get; set; }
        public string ConditionExpression { get; set; }
    }

    public class BpmnPool
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public string ProcessRef { get; set; }
        public Rectangle Bounds { get; set; }
        public List<BpmnLane> Lanes { get; set; } = new List<BpmnLane>();
        public List<string> ChildNodeIds { get; set; } = new List<string>();
    }

    public class BpmnLane
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public string ParentPoolId { get; set; }
        public Rectangle Bounds { get; set; }
        public List<string> ChildNodeIds { get; set; } = new List<string>();
        public int Order { get; set; }
    }

    public class BpmnProcess
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public Dictionary<string, BpmnNode> Nodes { get; set; }
        public Dictionary<string, List<SequenceFlow>> OutgoingFlows { get; set; }

        public BpmnProcess()
        {
            Nodes = new Dictionary<string, BpmnNode>();
            OutgoingFlows = new Dictionary<string, List<SequenceFlow>>();
        }
    }

    public class Token
    {
        private static int _nextInstanceId = 1;

        public int InstanceId { get; private set; }
        public int? ParentInstanceId { get; private set; }
        public string CurrentNodeId { get; set; }
        public Dictionary<string, object> Variables { get; set; }
        public List<string> VisitedNodes { get; set; }
        public bool IsCompleted { get; set; }
        public bool IsPaused { get; set; }
        public DateTime StartTime { get; set; }
        public DateTime? EndTime { get; set; }

        public Token(string startNodeId, int? instanceId = null, int? parentInstanceId = null)
        {
            if (instanceId.HasValue)
                InstanceId = instanceId.Value;
            else
                InstanceId = _nextInstanceId++;

            ParentInstanceId = parentInstanceId;
            CurrentNodeId = startNodeId;
            Variables = new Dictionary<string, object>();
            VisitedNodes = new List<string>();
            IsCompleted = false;
            StartTime = DateTime.Now;
        }

        public Token Clone()
        {
            var newToken = new Token(this.CurrentNodeId, this.InstanceId, this.InstanceId);

            foreach (var kvp in this.Variables)
                newToken.Variables[kvp.Key] = kvp.Value;

            newToken.VisitedNodes = new List<string>(this.VisitedNodes);
            newToken.IsCompleted = this.IsCompleted;

            return newToken;
        }

        public static void ResetInstanceIdCounter()
        {
            _nextInstanceId = 1;
        }

        public TimeSpan GetExecutionTime()
        {
            return (EndTime ?? DateTime.Now) - StartTime;
        }
    }
}
