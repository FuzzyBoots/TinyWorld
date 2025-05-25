using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace JamTrackerItchio.Editor
{
    public static class EditorCoroutineUtility
    {
        public class EditorCoroutine
        {
            public IEnumerator Routine;
            public object Owner;

            public EditorCoroutine(IEnumerator routine, object owner)
            {
                Routine = routine;
                Owner = owner;
            }
        }

        private static List<EditorCoroutine> _coroutines = new List<EditorCoroutine>();

        static EditorCoroutineUtility()
        {
            EditorApplication.update += Update;
        }

        public static EditorCoroutine StartCoroutine(IEnumerator routine, object owner)
        {
            var coroutine = new EditorCoroutine(routine, owner);
            _coroutines.Add(coroutine);
            return coroutine;
        }

        private static void Update()
        {
            for (int i = _coroutines.Count - 1; i >= 0; i--)
            {
                EditorCoroutine coroutine = _coroutines[i];

                // Check if the owner has been destroyed (if it's a Unity Object)
                if (coroutine.Owner is Object unityObj && unityObj == null)
                {
                    _coroutines.RemoveAt(i);
                    continue;
                }

                bool moveNext;
                try
                {
                    moveNext = coroutine.Routine.MoveNext();
                }
                catch (System.Exception ex)
                {
                    Debug.LogError($"Exception in editor coroutine: {ex}");
                    _coroutines.RemoveAt(i);
                    continue;
                }

                if (!moveNext)
                {
                    _coroutines.RemoveAt(i);
                }
            }
        }
    }
}
