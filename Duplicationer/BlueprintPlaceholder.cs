using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using System;
using Unfoundry;

namespace Duplicationer
{
    public class BlueprintPlaceholder {
        public BuildableObjectTemplate Template { get; private set; }
        public Vector3 Position { get; private set; }
        public Quaternion Rotation { get; private set; }
        public BuildingManager.BuildOrientation Orientation { get; private set; }
        public State CurrentState { get; private set; }
        public BatchRenderingHandle[] BatchRenderingHandles { get; set; }
        private static int[] stateCounts = new int[Enum.GetValues(typeof(State)).Length - 1];
        private static Dictionary<ulong, int[]> stateCountsByTemplateId = new Dictionary<ulong, int[]>();

        internal static Color[] stateColours = new Color[] {
            new Color(1.0f, 0.0f, 1.0f, 0.5f),
            new Color(1.0f, 1.0f, 1.0f, 0.5f),
            new Color(0.8f, 0.9f, 1.0f, 0.5f),
            new Color(1.0f, 0.2f, 0.1f, 0.5f),
            new Color(0.0f, 0.0f, 0.0f, 0.0f)
        };

        internal static LazyMaterial[] stateSimplePlaceholderMaterial = new LazyMaterial[] {
            null,
            new LazyMaterial(() =>
            {
                var material = new Material(ResourceDB.material_glow_purple);
                material.SetColor("_Color", new Color(1.0f, 1.0f, 1.0f, 1.0f));
                return material;
            }),
            new LazyMaterial(() =>
            {
                var material = new Material(ResourceDB.material_glow_blue);
                material.SetColor("_Color", new Color(0.8f, 0.9f, 1.0f, 1.0f));
                return material;
            }),
            new LazyMaterial(() =>
            {
                var material = new Material(ResourceDB.material_glow_red);
                material.SetColor("_Color", new Color(1.0f, 0.2f, 0.1f, 1.0f));
                return material;
            }),
            null
        };

        public BlueprintPlaceholder(BuildableObjectTemplate template, Vector3 position, Quaternion rotation, BuildingManager.BuildOrientation orientation, BatchRenderingHandle[] batchRenderingHandles, State state = State.Untested)
        {
            Template = template;
            Position = position;
            Rotation = rotation;
            Orientation = orientation;
            BatchRenderingHandles = batchRenderingHandles;
            CurrentState = State.Invalid;
            SetState(state);
        }

        public static IEnumerable<KeyValuePair<ulong, int[]>> GetStateCounts()
        {
            yield return new KeyValuePair<ulong, int[]>(0, stateCounts);
            foreach(var kv in stateCountsByTemplateId)
            {
                yield return kv;
            }
        }

        public static IEnumerable<KeyValuePair<string, int[]>> GetNamedStateCounts()
        {
            yield return new KeyValuePair<string, int[]>("Total", stateCounts);
            foreach(var kv in stateCountsByTemplateId)
            {
                var template = ItemTemplateManager.getBuildableObjectTemplate(kv.Key);
                if(template != null) yield return new KeyValuePair<string, int[]>(template.name, kv.Value);
            }
        }

        public void SetState(State state)
        {
            if (state == CurrentState) return;

            var counts = (Template != null && Template.parentItemTemplate != null) ? ForceStateCount(Template.parentItemTemplate.id) : null;

            if (CurrentState != State.Invalid)
            {
                stateCounts[(int)CurrentState - 1]--;
                if (counts != null) counts[(int)CurrentState - 1]--;
            }

            CurrentState = state;

            if (CurrentState != State.Invalid)
            {
                stateCounts[(int)CurrentState - 1]++;
                if (counts != null) counts[(int)CurrentState - 1]++;

                SetColor(CurrentStateColor);
            }
        }

        private Color CurrentStateColor => stateColours[(int)CurrentState];

        public void SetColor(Color color)
        {
            if (BatchRenderingHandles == null) return;
            foreach(var handle in BatchRenderingHandles) handle.SetColor(color);
        }

        public static int GetStateCount(State state)
        {
            return (state == State.Invalid) ? 0 : stateCounts[(int)state - 1];
        }

        public static int GetStateCount(ulong templateId, State state)
        {
            if (state == State.Invalid) return 0;

            var counts = GetStateCounts(templateId);
            if (counts == null) return 0;

            return counts[(int)state - 1];
        }

        public static int GetStateCountTotal()
        {
            return stateCounts.Sum();
        }

        public static int GetStateCountTotal(ulong templateId)
        {
            var counts = GetStateCounts(templateId);
            if (counts == null) return 0;

            return counts.Sum();
        }

        private static int[] GetStateCounts(ulong templateId)
        {
            int[] counts;
            return stateCountsByTemplateId.TryGetValue(templateId, out counts) ? counts : null;
        }

        private static int[] ForceStateCount(ulong templateId)
        {
            int[] counts;
            return stateCountsByTemplateId.TryGetValue(templateId, out counts) ? counts : (stateCountsByTemplateId[templateId] = new int[Enum.GetValues(typeof(State)).Length - 1]);
        }

        public void Move(Vector3 offset)
        {
            Position += offset;

            if (BatchRenderingHandles == null) return;
            foreach (var handle in BatchRenderingHandles) handle.Move(offset);
        }

        public void Moved(Vector3 offset)
        {
            Position += offset;
        }

        public enum State
        {
            Invalid,
            Untested,
            Clear,
            Blocked,
            Done
        }
    }
}