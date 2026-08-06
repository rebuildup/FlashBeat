using UnityEngine;

namespace NoteEditor.Utility
{
    public class SingletonMonoBehaviour<T> : MonoBehaviour where T : MonoBehaviour
    {
        static T instance_;
        public static T Instance
        {
            get
            {
                if (instance_ == null)
                {
                    instance_ = FindAnyObjectByType<T>();
                }

                return instance_ ?? new GameObject(typeof(T).FullName).AddComponent<T>();
            }
        }
    }
}
