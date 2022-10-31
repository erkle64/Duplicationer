using Microsoft.SqlServer.Server;
using static SystemManager;
using System.Data;
using System.Xml.Linq;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using System;
using Il2CppSystem.Runtime.Remoting.Messaging;

namespace Duplicationer
{
    public class ConstructionTaskGroup
    {
        public delegate void TaskGroupCompleteDelegate(ConstructionTaskGroup taskGroup);

        public int Count => tasksById.Count;
        public int Remaining => sortedTasks.Count;

        private TaskGroupCompleteDelegate taskGroupComplete;
        private Dictionary<ulong, ConstructionTask> tasksById = new Dictionary<ulong, ConstructionTask>();
        private Dictionary<ulong, TaskSortInfo> unsortedTasks = new Dictionary<ulong, TaskSortInfo>();
        private Queue<ConstructionTask> sortedTasks = new Queue<ConstructionTask>();
        private Stack<ConstructionTask> stack = new Stack<ConstructionTask>();

        public ConstructionTaskGroup(TaskGroupCompleteDelegate taskGroupComplete)
        {
            this.taskGroupComplete = taskGroupComplete;
        }

        public ConstructionTask GetTask(ulong id)
        {
            ConstructionTask task;
            return tasksById.TryGetValue(id, out task) ? task : null;
        }

        public ConstructionTask AddTask(ulong id, ConstructionTask.TaskActionDelegate taskAction)
        {
            var task = new ConstructionTask(id, taskAction);
            tasksById[id] = task;
            return task;
        }

        public void SortTasks()
        {
            unsortedTasks.Clear();
            foreach (var kv in tasksById) unsortedTasks[kv.Key] = new TaskSortInfo(kv.Value);

            foreach (var kv in tasksById)
            {
                var task = kv.Value;
                if (task.dependencies != null)
                {
                    foreach (var dependency in task.dependencies)
                    {
                        if (unsortedTasks.ContainsKey(dependency.id))
                        {
                            unsortedTasks[dependency.id].afters.Add(task);
                            unsortedTasks[task.id].befores.Add(dependency);
                        }
                    }
                }
            }

            sortedTasks.Clear();
            stack.Clear();
            foreach (var kv in unsortedTasks) if (kv.Value.befores.Count == 0) stack.Push(kv.Value.task);

            while (stack.Count > 0)
            {
                var task = stack.Pop();
                var sortInfo = unsortedTasks[task.id];
                sortedTasks.Enqueue(task);
                unsortedTasks.Remove(task.id);
                foreach (var after in sortInfo.afters)
                {
                    var befores = unsortedTasks[after.id].befores;
                    befores.Remove(task);
                    if (befores.Count == 0) stack.Push(after);
                }
            }

            if (unsortedTasks.Count > 0)
            {
                BepInExLoader.log.LogError("Cyclic dependency loop in construction tasks");
                sortedTasks.Clear();
                unsortedTasks.Clear();
                return;
            }

            unsortedTasks.Clear();
        }

        public void InvokeNextTask()
        {
            if (sortedTasks.Count > 0) sortedTasks.Dequeue().Invoke(this);
            else taskGroupComplete?.Invoke(this);
        }

        private struct TaskSortInfo
        {
            public ConstructionTask task;
            internal List<ConstructionTask> befores;
            internal List<ConstructionTask> afters;

            public TaskSortInfo(ConstructionTask task)
            {
                this.task = task;
                befores = new List<ConstructionTask>();
                afters = new List<ConstructionTask>();
            }
        }


        public class ConstructionTask
        {
            public delegate void TaskActionDelegate(ConstructionTaskGroup taskGroup, ConstructionTask task);

            public readonly ulong id;
            internal readonly TaskActionDelegate taskAction;
            internal ConstructionTask[] dependencies = null;
            internal ulong entityId = 0;

            public ConstructionTask(ulong id, TaskActionDelegate taskAction)
            {
                this.id = id;
                this.taskAction = taskAction;
            }

            public void Invoke(ConstructionTaskGroup taskGroup)
            {
                taskAction.Invoke(taskGroup, this);
            }
        }
    }
}
