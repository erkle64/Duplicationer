using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using static Duplicationer.BepInExLoader;

namespace Duplicationer
{
    internal static class ActionManager
    {
        public delegate void BuildEventDelegate(ulong entityId);
        private static Dictionary<Vector3Int, BuildEventDelegate> buildEvents = new Dictionary<Vector3Int, BuildEventDelegate>();

        public static int MaxQueuedEventsPerFrame = 20;
        public delegate void QueuedEventDelegate();
        private static Queue<QueuedEventDelegate> queuedEvents = new Queue<QueuedEventDelegate>();

        private static TimedAction timedActions = null;
        private static TimedAction timedActionFreeList = null;

        public static string StatusText { get; private set; } = "";

        public static void AddBuildEvent(BuildEntityEvent target, BuildEventDelegate handler)
        {
            Vector3Int worldPos = new Vector3Int(target.worldBuildPos[0], target.worldBuildPos[1], target.worldBuildPos[2]);
            if (buildEvents.ContainsKey(worldPos))
            {
                log.LogWarning((string)$"Build event already exists at {worldPos}");
                return;
            }

            buildEvents[worldPos] = handler;
        }

        public static void RemoveBuildEvent(BuildEntityEvent target)
        {
            Vector3Int worldPos = new Vector3Int(target.worldBuildPos[0], target.worldBuildPos[1], target.worldBuildPos[2]);
            if (!buildEvents.ContainsKey(worldPos))
            {
                return;
            }

            buildEvents.Remove(worldPos);
        }

        public static void InvokeBuildEvent(BuildEntityEvent target, ulong entityId)
        {
            Vector3Int worldPos = new Vector3Int(target.worldBuildPos[0], target.worldBuildPos[1], target.worldBuildPos[2]);
            BuildEventDelegate handler;
            if (buildEvents.TryGetValue(worldPos, out handler))
            {
                handler(entityId);
            }
        }

        public static void InvokeAndRemoveBuildEvent(BuildEntityEvent target, ulong entityId)
        {
            Vector3Int worldPos = new Vector3Int(target.worldBuildPos[0], target.worldBuildPos[1], target.worldBuildPos[2]);
            BuildEventDelegate handler;
            if (buildEvents.TryGetValue(worldPos, out handler))
            {
                handler(entityId);
                buildEvents.Remove(worldPos);
            }
        }

        public static void AddQueuedEvent(QueuedEventDelegate queuedEvent)
        {
            queuedEvents.Enqueue(queuedEvent);
        }

        public static void AddTimedAction(float time, float randomAdd, Action action)
        {
            AddTimedAction(time + UnityEngine.Random.value * randomAdd, action);
        }

        public static void AddTimedAction(float time, Action action)
        {
            if (timedActionFreeList == null)
            {
                BubbleTimedAction(new TimedAction(time, action, null));
            }
            else
            {
                var timedAction = timedActionFreeList;
                timedActionFreeList = timedActionFreeList.next;

                timedAction.time = time;
                timedAction.action = action;
                timedAction.next = null;
                BubbleTimedAction(timedAction);
            }
        }

        private static void BubbleTimedAction(TimedAction node)
        {
            if (timedActions == null)
            {
                timedActions = node;
                return;
            }

            if (timedActions.time >= node.time)
            {
                node.next = timedActions;
                timedActions = node;
                return;
            }

            var prev = timedActions;
            while (prev.next != null && prev.next.time < node.time) prev = prev.next;
            node.next = prev.next;
            prev.next = node;
        }

        internal static void Update()
        {

            int toProcess = queuedEvents.Count;
            if (toProcess > MaxQueuedEventsPerFrame) toProcess = MaxQueuedEventsPerFrame;
            //if (toProcess > 0) log.LogInfo(string.Format("Processing {0} events", toProcess));
            while (toProcess-- > 0)
            {
                queuedEvents.Dequeue().Invoke();
            }

            var time = Time.time;
            while (timedActions != null && timedActions.time <= time)
            {
                var timedAction = timedActions;
                timedActions = timedActions.next;

                timedAction.action.Invoke();

                timedAction.next = timedActionFreeList;
                timedActionFreeList = timedAction;
                timedAction.action = null;
            }
        }

        internal static void OnGameInitializationDone()
        {
            buildEvents.Clear();

            queuedEvents.Clear();

            timedActions = null;
            timedActionFreeList = null;

            StatusText = "";
        }

        public class TimedAction
        {
            public float time;
            public Action action;
            public TimedAction next;

            public TimedAction(float time, Action action, TimedAction next)
            {
                this.time = time;
                this.action = action;
                this.next = next;
            }
        }
    }
}