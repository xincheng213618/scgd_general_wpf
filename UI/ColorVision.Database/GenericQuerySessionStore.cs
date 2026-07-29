using SqlSugar;
using System;
using System.Collections.Generic;
using System.Linq;

namespace ColorVision.Database
{
    internal sealed class GenericQueryConditionState
    {
        public string PropertyName { get; init; } = string.Empty;
        public QueryOperator Operator { get; init; }
        public string? InputText { get; init; }
        public object? Value { get; init; }
    }

    internal sealed class GenericQuerySessionState
    {
        public int Count { get; init; }
        public OrderByType OrderByType { get; init; }
        public IReadOnlyList<GenericQueryConditionState> Conditions { get; init; } = [];
    }

    internal static class GenericQuerySessionStore
    {
        private static readonly object SyncRoot = new();
        private static readonly Dictionary<Type, GenericQuerySessionState> States = [];

        public static GenericQuerySessionState? Load(Type entityType)
        {
            lock (SyncRoot)
            {
                return States.TryGetValue(entityType, out GenericQuerySessionState? state)
                    ? Clone(state)
                    : null;
            }
        }

        public static void Save(Type entityType, IEnumerable<QueryCondition> conditions, GenericQueryBaseConfig config)
        {
            var state = new GenericQuerySessionState
            {
                Count = config.Count,
                OrderByType = config.OrderByType,
                Conditions = conditions.Select(condition => new GenericQueryConditionState
                {
                    PropertyName = condition.Property.Name,
                    Operator = condition.Operator,
                    InputText = condition.InputText,
                    Value = condition.Value
                }).ToArray()
            };

            lock (SyncRoot)
                States[entityType] = state;
        }

        public static void Clear(Type entityType)
        {
            lock (SyncRoot)
                States.Remove(entityType);
        }

        internal static void ClearAll()
        {
            lock (SyncRoot)
                States.Clear();
        }

        private static GenericQuerySessionState Clone(GenericQuerySessionState state)
        {
            return new GenericQuerySessionState
            {
                Count = state.Count,
                OrderByType = state.OrderByType,
                Conditions = state.Conditions.Select(condition => new GenericQueryConditionState
                {
                    PropertyName = condition.PropertyName,
                    Operator = condition.Operator,
                    InputText = condition.InputText,
                    Value = condition.Value
                }).ToArray()
            };
        }
    }
}
