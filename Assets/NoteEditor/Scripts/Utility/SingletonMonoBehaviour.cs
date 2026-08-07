using UnityEngine;

namespace NoteEditor.Utility
{
    public class SingletonMonoBehaviour<T> : MonoBehaviour where T : MonoBehaviour
    {
        static T instance_;
        // When true, Instance getter returns instance_ directly without consulting
        // FindAnyObjectByType or the AddComponent fallback. Required for EditMode
        // tests of editor-only T types because AddComponent<T> rejects editor
        // scripts ("Can't add script behaviour 'X' because it is an editor script").
        static bool externallySet_;

        public static T Instance
        {
            get
            {
                if (externallySet_)
                {
                    return instance_;
                }

                if (instance_ == null)
                {
                    instance_ = FindAnyObjectByType<T>();
                }

                return instance_ ?? new GameObject(typeof(T).FullName).AddComponent<T>();
            }
        }

        // Test-only injection point. See externallySet_ comment for rationale.
        // Call from [SetUp] and reset (null) from [TearDown].
        public static void SetInstanceForTesting(T instance)
        {
            instance_ = instance;
            externallySet_ = !ReferenceEquals(instance, null);
        }
    }
}
