using System;
using System.Collections.Generic;
using UnityEditor;

namespace MeshyWorkspace.Editor
{
    /// <summary>
    /// Marshals background-thread callbacks back to the Unity main thread.
    /// </summary>
    public static class MeshyUiDispatcher
    {
        private static readonly object Lock = new object();
        private static readonly Queue<Action> Queue = new Queue<Action>();
        private static bool hooked;

        public static void Capture()
        {
            if (hooked)
            {
                return;
            }

            hooked = true;
            EditorApplication.update += Drain;
        }

        public static void Post(Action action)
        {
            if (action == null)
            {
                return;
            }

            lock (Lock)
            {
                Queue.Enqueue(action);
            }
        }

        private static void Drain()
        {
            Action[] batch;
            lock (Lock)
            {
                if (Queue.Count == 0)
                {
                    return;
                }

                batch = Queue.ToArray();
                Queue.Clear();
            }

            foreach (var action in batch)
            {
                action();
            }
        }
    }
}
