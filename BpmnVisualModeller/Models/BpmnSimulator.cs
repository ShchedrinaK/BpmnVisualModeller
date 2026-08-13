using System;
using System.Collections.Generic;
using System.Linq;

namespace BpmnVisualModeller.Models
{
    public class BpmnSimulator
    {
        private BpmnProcess _process;
        private List<Token> _activeTokens;

        private Dictionary<string, GatewayJoinState> _gatewayJoinStates = new Dictionary<string, GatewayJoinState>();
        private Dictionary<string, int> _forkInstanceCounts = new Dictionary<string, int>();
        private Dictionary<string, int> _inclusiveForkCounts = new Dictionary<string, int>();

        public event Action<string, string> OnTokenMoved;
        public event Action<string, string> OnDecision;
        public event Action<string, List<string>> OnParallelSplit;
        public event Action<string, string> OnError;
        public event Action<List<Token>> OnTokensUpdated;

        public Dictionary<string, string> UserSelectedFlows { get; set; } = new Dictionary<string, string>();
        public Dictionary<string, HashSet<string>> UserSelectedInclusiveFlows { get; set; } = new Dictionary<string, HashSet<string>>();
        public HashSet<int> PausedInstanceIds { get; set; } = new HashSet<int>();
        public bool IsGloballyPaused { get; set; }

        public BpmnSimulator(BpmnProcess process)
        {
            _process = process;
            _activeTokens = new List<Token>();
        }

        public class GatewayJoinState
        {
            public int ReceivedTokenCount { get; set; } = 0;
            public int ExpectedTokenCount { get; set; }
            public bool HasPassed { get; set; } = false;
            public Token MainToken { get; set; }
        }

        public Token StartProcess(Dictionary<string, object> initialVariables = null)
        {
            var startNode = _process.Nodes.Values.FirstOrDefault(n => n.Type == NodeType.StartEvent);
            if (startNode == null)
            {
                OnError?.Invoke(null, "Стартовое событие не найдено");
                throw new Exception("Стартовое событие не найдено");
            }

            var token = new Token(startNode.Id);
            if (initialVariables != null)
            {
                foreach (var kvp in initialVariables)
                    token.Variables[kvp.Key] = kvp.Value;
            }

            _activeTokens.Add(token);

            OnTokenMoved?.Invoke(null, token.CurrentNodeId);
            OnTokensUpdated?.Invoke(_activeTokens);
            return token;
        }

        public bool Step()
        {
            if (_activeTokens.Count == 0) return false;
            if (IsGloballyPaused) return _activeTokens.Any(t => !t.IsCompleted);

            CleanupWaitingTokens();

            var tokensToProcess = _activeTokens
                .Where(t => !t.IsCompleted && !t.IsPaused && !PausedInstanceIds.Contains(t.InstanceId))
                .ToList();

            if (tokensToProcess.Count == 0)
            {
                OnTokensUpdated?.Invoke(_activeTokens);
                return _activeTokens.Any(t => !t.IsCompleted);
            }
            var newTokens = new List<Token>();
            var completedTokens = new List<Token>();

            foreach (var token in tokensToProcess)
            {
                if (token.IsCompleted)
                {
                    completedTokens.Add(token);
                    continue;
                }

                var currentNode = _process.Nodes[token.CurrentNodeId];
                token.VisitedNodes.Add(currentNode.Id);

                bool tokenCompleted = false;

                switch (currentNode.Type)
                {
                    case NodeType.StartEvent:
                    case NodeType.Task:
                        MoveToken(token, currentNode, newTokens);
                        break;

                    case NodeType.ExclusiveGateway:
                        ProcessExclusiveGateway(token, (ExclusiveGateway)currentNode, newTokens);
                        break;

                    case NodeType.ParallelGateway:
                        ProcessParallelGateway(token, (ParallelGateway)currentNode, newTokens);
                        break;

                    case NodeType.InclusiveGateway:
                        ProcessInclusiveGateway(token, (InclusiveGateway)currentNode, newTokens);
                        break;

                    case NodeType.EndEvent:
                        token.IsCompleted = true;
                        token.EndTime = DateTime.Now;
                        OnTokenMoved?.Invoke(currentNode.Id, null);
                        tokenCompleted = true;
                        break;

                    default:
                        OnError?.Invoke(currentNode.Id, $"Неподдерживаемый тип узла: {currentNode.Type}");
                        break;
                }

                if (tokenCompleted)
                {
                    completedTokens.Add(token);
                }
            }

            foreach (var token in completedTokens)
            {
                _activeTokens.Remove(token);

                foreach (var kvp in _gatewayJoinStates.ToList())
                {
                    var state = kvp.Value;
                    if (state.MainToken?.InstanceId == token.InstanceId)
                    {
                        state.MainToken = null;
                    }

                    if (state.ReceivedTokenCount == 0 && state.MainToken == null && !state.HasPassed)
                    {
                        _gatewayJoinStates.Remove(kvp.Key);
                    }
                }
            }

            foreach (var token in newTokens)
            {
                _activeTokens.Add(token);
            }

            var emptyGateways = _gatewayJoinStates
                .Where(kvp => kvp.Value.ReceivedTokenCount == 0 && kvp.Value.MainToken == null && !kvp.Value.HasPassed)
                .Select(kvp => kvp.Key)
                .ToList();

            foreach (var gatewayId in emptyGateways)
            {
                _gatewayJoinStates.Remove(gatewayId);
            }

            OnTokensUpdated?.Invoke(_activeTokens);
            return _activeTokens.Count > 0;
        }

        private void MoveToken(Token token, BpmnNode currentNode, List<Token> newTokens)
        {
            var outgoingFlows = _process.OutgoingFlows.ContainsKey(currentNode.Id)
                ? _process.OutgoingFlows[currentNode.Id]
                : new List<SequenceFlow>();

            if (outgoingFlows.Any())
            {
                token.CurrentNodeId = outgoingFlows[0].TargetRef;
                OnTokenMoved?.Invoke(currentNode.Id, token.CurrentNodeId);
            }
        }

        private void ProcessExclusiveGateway(Token token, ExclusiveGateway gateway, List<Token> newTokens)
        {
            var outgoingFlows = _process.OutgoingFlows.ContainsKey(gateway.Id)
                ? _process.OutgoingFlows[gateway.Id]
                : new List<SequenceFlow>();

            if (outgoingFlows.Count == 0) return;

            SequenceFlow selectedFlow = null;

            if (UserSelectedFlows.ContainsKey(gateway.Id))
            {
                string selectedFlowId = UserSelectedFlows[gateway.Id];
                selectedFlow = outgoingFlows.FirstOrDefault(f => f.Id == selectedFlowId);
            }

            if (selectedFlow == null)
            {
                foreach (var flow in outgoingFlows)
                {
                    if (EvaluateCondition(flow.ConditionExpression, token))
                    {
                        selectedFlow = flow;
                        break;
                    }
                }
            }

            if (selectedFlow == null)
            {
                selectedFlow = outgoingFlows.FirstOrDefault(f => string.IsNullOrEmpty(f.ConditionExpression));
            }

            if (selectedFlow != null)
            {
                OnDecision?.Invoke(gateway.Id, selectedFlow.Id);
                token.CurrentNodeId = selectedFlow.TargetRef;
                OnTokenMoved?.Invoke(gateway.Id, token.CurrentNodeId);
            }
            else
            {
                var error = $"Нет подходящего условия в шлюзе {gateway.Id}";
                OnError?.Invoke(gateway.Id, error);
                throw new Exception(error);
            }
        }

        public void PauseInstance(int instanceId)
        {
            PausedInstanceIds.Add(instanceId);
            foreach (var t in _activeTokens.Where(t => t.InstanceId == instanceId))
                t.IsPaused = true;
            OnTokensUpdated?.Invoke(_activeTokens);
        }

        public void ResumeInstance(int instanceId)
        {
            PausedInstanceIds.Remove(instanceId);
            foreach (var t in _activeTokens.Where(t => t.InstanceId == instanceId))
                t.IsPaused = false;
            OnTokensUpdated?.Invoke(_activeTokens);
        }

        public void PauseAllInstances()
        {
            IsGloballyPaused = true;
            foreach (var t in _activeTokens.Where(t => !t.IsCompleted))
            {
                PausedInstanceIds.Add(t.InstanceId);
                t.IsPaused = true;
            }
            OnTokensUpdated?.Invoke(_activeTokens);
        }

        public void ResumeAllInstances()
        {
            IsGloballyPaused = false;
            PausedInstanceIds.Clear();
            foreach (var t in _activeTokens)
                t.IsPaused = false;
            OnTokensUpdated?.Invoke(_activeTokens);
        }

        private void ProcessInclusiveGateway(Token token, InclusiveGateway gateway, List<Token> newTokens)
        {
            var outgoingFlows = _process.OutgoingFlows.ContainsKey(gateway.Id)
                ? _process.OutgoingFlows[gateway.Id]
                : new List<SequenceFlow>();

            var incomingFlows = GetIncomingFlows(gateway.Id);
            int incomingCount = incomingFlows.Count;
            int outgoingCount = outgoingFlows.Count;

            if (incomingCount > 1 && outgoingCount <= 1)
            {
                ProcessJoinGateway(token, gateway.Id, incomingFlows, outgoingFlows, newTokens, _inclusiveForkCounts);
            }
            else if (outgoingCount > 1)
            {
                var activeFlows = GetInclusiveActiveFlows(gateway.Id, outgoingFlows, token);
                ProcessForkGateway(token, gateway.Id, activeFlows, newTokens, _inclusiveForkCounts);
            }
            else if (outgoingFlows.Any())
            {
                token.CurrentNodeId = outgoingFlows[0].TargetRef;
                OnTokenMoved?.Invoke(gateway.Id, token.CurrentNodeId);
            }
        }

        private List<SequenceFlow> GetParallelForkFlows(string gatewayId, List<SequenceFlow> outgoingFlows)
        {
            if (UserSelectedFlows.TryGetValue(gatewayId, out var selectedFlowId))
            {
                var selected = outgoingFlows.FirstOrDefault(f => f.Id == selectedFlowId);
                if (selected != null)
                    return new List<SequenceFlow> { selected };
            }

            return outgoingFlows;
        }

        private List<SequenceFlow> GetInclusiveActiveFlows(string gatewayId, List<SequenceFlow> outgoingFlows, Token token)
        {
            if (UserSelectedInclusiveFlows.TryGetValue(gatewayId, out var selected) && selected != null && selected.Count > 0)
            {
                return outgoingFlows.Where(f => selected.Contains(f.Id)).ToList();
            }

            var byCondition = outgoingFlows
                .Where(f => string.IsNullOrEmpty(f.ConditionExpression) || EvaluateCondition(f.ConditionExpression, token))
                .ToList();

            if (byCondition.Any())
                return byCondition;

            return new List<SequenceFlow> { outgoingFlows.First() };
        }

        private void ProcessParallelGateway(Token token, ParallelGateway gateway, List<Token> newTokens)
        {
            var outgoingFlows = _process.OutgoingFlows.ContainsKey(gateway.Id)
                ? _process.OutgoingFlows[gateway.Id]
                : new List<SequenceFlow>();

            var incomingFlows = GetIncomingFlows(gateway.Id);
            int incomingCount = incomingFlows.Count;
            int outgoingCount = outgoingFlows.Count;

            AddLogMessage($"🔀 Параллельный шлюз {gateway.Name ?? gateway.Id}: входящих={incomingCount}, исходящих={outgoingCount}");

            if (incomingCount > 1 && outgoingCount == 1)
            {
                ProcessJoinGateway(token, gateway.Id, incomingFlows, outgoingFlows, newTokens, _forkInstanceCounts);
            }
            else if (outgoingCount > 1 && incomingCount <= 1)
            {
                var forkFlows = GetParallelForkFlows(gateway.Id, outgoingFlows);
                ProcessForkGateway(token, gateway.Id, forkFlows, newTokens, _forkInstanceCounts);
            }
            else if (incomingCount > 1 && outgoingCount > 1)
            {
                AddLogMessage($"⚠️ Шлюз {gateway.Name ?? gateway.Id}: сложный случай - сначала JOIN, потом FORK");
                ProcessJoinGateway(token, gateway.Id, incomingFlows, outgoingFlows, newTokens, _forkInstanceCounts);
            }

            else if (outgoingFlows.Any())
            {
                token.CurrentNodeId = outgoingFlows[0].TargetRef;
                OnTokenMoved?.Invoke(gateway.Id, token.CurrentNodeId);
            }
        }

        private void ProcessJoinGateway(Token token, string gatewayId,
            List<SequenceFlow> incomingFlows, List<SequenceFlow> outgoingFlows,
            List<Token> newTokens, Dictionary<string, int> forkCountsRegistry)
        {
            string gatewayName = _process.Nodes.TryGetValue(gatewayId, out var gn) ? (gn.Name ?? gatewayId) : gatewayId;
            AddLogMessage($"🔀 JOIN-шлюз: {gatewayName}, токен #{token.InstanceId}");

            if (token.IsCompleted)
            {
                return;
            }

            string stateKey = $"{gatewayId}_{token.InstanceId}";

            if (!_gatewayJoinStates.ContainsKey(stateKey))
            {
                _gatewayJoinStates[stateKey] = new GatewayJoinState();
                AddLogMessage($"🔀 Создано состояние JOIN для экземпляра #{token.InstanceId}");
            }

            var state = _gatewayJoinStates[stateKey];

            if (state.ExpectedTokenCount == 0)
            {
                state.ExpectedTokenCount = FindExpectedTokenCount(gatewayId, token.InstanceId, forkCountsRegistry);
                AddLogMessage($"🔀 Ожидается {state.ExpectedTokenCount} токенов для JOIN");
            }

            state.ReceivedTokenCount++;

            if (state.MainToken == null)
            {
                state.MainToken = token;
            }

            AddLogMessage($"📊 Получено {state.ReceivedTokenCount}/{state.ExpectedTokenCount} токенов");

            if (state.ReceivedTokenCount >= state.ExpectedTokenCount)
            {
                AddLogMessage($"✅ Все {state.ExpectedTokenCount} токенов собрались, продолжаем выполнение");

                var mainToken = state.MainToken;

                foreach (var t in _activeTokens.Where(t => t.InstanceId == token.InstanceId && t != mainToken))
                {
                    t.IsCompleted = true;
                }

                if (outgoingFlows.Any())
                {
                    mainToken.CurrentNodeId = outgoingFlows[0].TargetRef;
                    OnTokenMoved?.Invoke(gatewayId, mainToken.CurrentNodeId);
                }

                _gatewayJoinStates.Remove(stateKey);
            }
            else
            {
                AddLogMessage($"⏳ Ожидание, не хватает {state.ExpectedTokenCount - state.ReceivedTokenCount} токенов");
            }
        }

        private void ProcessForkGateway(Token token, string gatewayId,
            List<SequenceFlow> outgoingFlows, List<Token> newTokens, Dictionary<string, int> forkCountsRegistry)
        {
            string gatewayName = _process.Nodes.TryGetValue(gatewayId, out var gn) ? (gn.Name ?? gatewayId) : gatewayId;
            AddLogMessage($"🔀 FORK-шлюз: {gatewayName}, токен #{token.InstanceId}, исходящих={outgoingFlows.Count}");

            string forkKey = $"{gatewayId}_{token.InstanceId}";

            if (forkCountsRegistry.ContainsKey(forkKey))
            {
                AddLogMessage($"⚠️ Повторное разветвление предотвращено");
                if (outgoingFlows.Any())
                {
                    token.CurrentNodeId = outgoingFlows[0].TargetRef;
                    OnTokenMoved?.Invoke(gatewayId, token.CurrentNodeId);
                }
                return;
            }

            OnParallelSplit?.Invoke(gatewayId, outgoingFlows.Select(f => f.TargetRef).ToList());

            if (outgoingFlows.Count > 1)
            {
                forkCountsRegistry[forkKey] = outgoingFlows.Count;
                AddLogMessage($"✅ Параллельное разделение: создано {outgoingFlows.Count} веток");

                for (int i = 0; i < outgoingFlows.Count; i++)
                {
                    var flow = outgoingFlows[i];
                    if (i == 0)
                    {
                        AddLogMessage($"🔀 Ветка 0: экземпляр #{token.InstanceId} → {flow.TargetRef}");
                        token.CurrentNodeId = flow.TargetRef;
                        OnDecision?.Invoke(gatewayId, flow.Id);
                        OnTokenMoved?.Invoke(gatewayId, token.CurrentNodeId);
                    }
                    else
                    {
                        var newToken = token.Clone();
                        newToken.CurrentNodeId = flow.TargetRef;
                        newTokens.Add(newToken);
                        AddLogMessage($"🔀 Ветка {i}: ветка экземпляра #{token.InstanceId} → {flow.TargetRef}");
                        OnDecision?.Invoke(gatewayId, flow.Id);
                        OnTokenMoved?.Invoke(gatewayId, newToken.CurrentNodeId);
                    }
                }
            }
            else if (outgoingFlows.Any())
            {
                AddLogMessage($"🔀 Только один выход: {outgoingFlows[0].TargetRef}");
                token.CurrentNodeId = outgoingFlows[0].TargetRef;
                OnTokenMoved?.Invoke(gatewayId, token.CurrentNodeId);
            }
        }

        private int FindExpectedTokenCount(string joinGatewayId, int instanceId, Dictionary<string, int> forkCountsRegistry)
        {
            string joinName = _process.Nodes.TryGetValue(joinGatewayId, out var gn) ? (gn.Name ?? joinGatewayId) : joinGatewayId;
            AddLogMessage($"🔍 Поиск FORK для JOIN-шлюза {joinName}");

            var queue = new Queue<string>();
            var visited = new HashSet<string>();

            var incomingNodes = GetIncomingNodes(joinGatewayId);
            foreach (var nodeId in incomingNodes)
            {
                queue.Enqueue(nodeId);
            }

            while (queue.Count > 0)
            {
                var currentId = queue.Dequeue();
                if (visited.Contains(currentId)) continue;
                visited.Add(currentId);

                if (_process.Nodes.TryGetValue(currentId, out var currentNode))
                {
                    if (currentNode.Type == NodeType.ParallelGateway ||
                        currentNode.Type == NodeType.InclusiveGateway)
                    {
                        string forkKey = $"{currentId}_{instanceId}";
                        if (forkCountsRegistry.ContainsKey(forkKey))
                        {
                            int count = forkCountsRegistry[forkKey];
                            AddLogMessage($"✅ Найден FORK-шлюз {currentId}, создал {count} токенов");
                            return count;
                        }
                    }
                }

                var previousNodes = GetPreviousNodes(currentId);
                foreach (var prevId in previousNodes)
                {
                    if (!visited.Contains(prevId))
                        queue.Enqueue(prevId);
                }
            }

            int fallbackCount = GetIncomingFlows(joinGatewayId).Count;
            AddLogMessage($"⚠️ FORK не найден, используем fallback = {fallbackCount}");
            return fallbackCount;
        }

        private List<string> GetIncomingNodes(string nodeId)
        {
            var result = new List<string>();
            foreach (var kvp in _process.OutgoingFlows)
            {
                foreach (var flow in kvp.Value)
                {
                    if (flow.TargetRef == nodeId)
                    {
                        result.Add(flow.SourceRef);
                    }
                }
            }
            return result;
        }

        private List<string> GetPreviousNodes(string nodeId)
        {
            return GetIncomingNodes(nodeId);
        }

        private List<SequenceFlow> GetIncomingFlows(string gatewayId)
        {
            var incomingFlows = new List<SequenceFlow>();
            foreach (var kvp in _process.OutgoingFlows)
            {
                foreach (var flow in kvp.Value)
                {
                    if (flow.TargetRef == gatewayId)
                    {
                        incomingFlows.Add(flow);
                    }
                }
            }
            return incomingFlows;
        }

        private void CleanupWaitingTokens()
        {
            var activeInstanceIds = _activeTokens.Where(t => !t.IsCompleted).Select(t => t.InstanceId).ToHashSet();
            var gatewaysToRemove = new List<string>();

            foreach (var kvp in _gatewayJoinStates)
            {
                var state = kvp.Value;

                if (state.MainToken != null && (state.MainToken.IsCompleted || !activeInstanceIds.Contains(state.MainToken.InstanceId)))
                {
                    gatewaysToRemove.Add(kvp.Key);
                }

                if (state.ExpectedTokenCount == 0 && state.ReceivedTokenCount == 0)
                {
                    gatewaysToRemove.Add(kvp.Key);
                }
            }

            foreach (var gwId in gatewaysToRemove)
            {
                _gatewayJoinStates.Remove(gwId);
            }
        }

        private bool EvaluateCondition(string expression, Token token)
        {
            if (string.IsNullOrEmpty(expression)) return true;

            try
            {
                if (expression.Contains("${") && expression.Contains("}"))
                {
                    var expr = expression.Replace("${", "").Replace("}", "");

                    string[] operators = { "==", "!=", ">=", "<=", ">", "<" };
                    foreach (var op in operators)
                    {
                        if (expr.Contains(op))
                        {
                            var parts = expr.Split(new[] { op }, StringSplitOptions.None);
                            var varName = parts[0].Trim();
                            var expectedValue = parts[1].Trim().Replace("'", "");

                            if (token.Variables.ContainsKey(varName))
                            {
                                var actualValue = token.Variables[varName];

                                switch (op)
                                {
                                    case "==":
                                        return actualValue.ToString() == expectedValue;
                                    case "!=":
                                        return actualValue.ToString() != expectedValue;
                                    case ">":
                                        if (decimal.TryParse(actualValue.ToString(), out decimal actualNum) &&
                                            decimal.TryParse(expectedValue, out decimal expectedNum))
                                            return actualNum > expectedNum;
                                        break;
                                    case "<":
                                        if (decimal.TryParse(actualValue.ToString(), out actualNum) &&
                                            decimal.TryParse(expectedValue, out expectedNum))
                                            return actualNum < expectedNum;
                                        break;
                                    case ">=":
                                        if (decimal.TryParse(actualValue.ToString(), out actualNum) &&
                                            decimal.TryParse(expectedValue, out expectedNum))
                                            return actualNum >= expectedNum;
                                        break;
                                    case "<=":
                                        if (decimal.TryParse(actualValue.ToString(), out actualNum) &&
                                            decimal.TryParse(expectedValue, out expectedNum))
                                            return actualNum <= expectedNum;
                                        break;
                                }
                            }
                            break;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                OnError?.Invoke(null, $"Ошибка вычисления условия '{expression}': {ex.Message}");
            }

            return false;
        }

        private void AddLogMessage(string message)
        {
            OnError?.Invoke(null, message);
        }

        public void UpdateUserSelections(
            Dictionary<string, string> exclusiveSelections,
            Dictionary<string, HashSet<string>> inclusiveSelections = null)
        {
            UserSelectedFlows = exclusiveSelections ?? new Dictionary<string, string>();
            UserSelectedInclusiveFlows = inclusiveSelections ?? new Dictionary<string, HashSet<string>>();
        }

        public List<Token> GetAllActiveTokens()
        {
            return _activeTokens.ToList();
        }

        public void Reset()
        {
            _activeTokens.Clear();
            _gatewayJoinStates.Clear();
            _forkInstanceCounts.Clear();
            _inclusiveForkCounts.Clear();
            PausedInstanceIds.Clear();
            IsGloballyPaused = false;
            OnTokensUpdated?.Invoke(_activeTokens);
        }

        public static void ResetTokenIds()
        {
            Token.ResetInstanceIdCounter();
        }
    }
}