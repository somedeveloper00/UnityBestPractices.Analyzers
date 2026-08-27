internal static class UnityEngineFixture
{
    internal const string Source = """
        namespace UnityEngine
        {
            public sealed class SerializeField : System.Attribute { }
            public enum FindObjectsSortMode { None, InstanceID }
            public class Object
            {
                public static T FindObjectOfType<T>() => default;
                public static Object FindObjectOfType(System.Type type) => default;
                public static T[] FindObjectsOfType<T>() => default;
                public static Object[] FindObjectsOfType(System.Type type) => default;
                public static T FindFirstObjectByType<T>() => default;
                public static Object FindFirstObjectByType(System.Type type) => default;
                public static T[] FindObjectsByType<T>(FindObjectsSortMode sortMode) => default;
                public static Object[] FindObjectsByType(System.Type type, FindObjectsSortMode sortMode) => default;
            }
            public class ScriptableObject : Object { }
            public class Component : Object { }
            public class MonoBehaviour : Component { }
            public class Camera : Component
            {
                public static Camera main => new Camera();
                public float fieldOfView { get; set; }
                public int cullingMask { get; set; }
            }

            public static class Mathf
            {
                public static float Pow(float value, float power) => 0f;
                public static float Clamp(float value, float minimum, float maximum) => 0f;
                public static float Clamp01(float value) => 0f;
                public static float Sqrt(float value) => 0f;
                public static float Floor(float value) => 0f;
                public static float Ceil(float value) => 0f;
                public static float Round(float value) => 0f;
                public static int FloorToInt(float value) => 0;
                public static int CeilToInt(float value) => 0;
                public static int RoundToInt(float value) => 0;
            }

            public struct Vector2
            {
                public Vector2(float x, float y) { }
                public static implicit operator Vector3(Vector2 value) => default;
                public static Vector2 zero => default;
                public static Vector2 one => default;
                public static Vector2 up => default;
                public static Vector2 down => default;
                public static Vector2 left => default;
                public static Vector2 right => default;
                public float magnitude => 0f;
                public float sqrMagnitude => 0f;
            }

            public struct Vector3
            {
                public Vector3(float x, float y) { }
                public Vector3(float x, float y, float z) { }
                public static Vector3 zero => default;
                public static Vector3 one => default;
                public static Vector3 up => default;
                public static Vector3 down => default;
                public static Vector3 left => default;
                public static Vector3 right => default;
                public static Vector3 forward => default;
                public static Vector3 back => default;
                public float magnitude => 0f;
                public float sqrMagnitude => 0f;
            }

            public struct Quaternion
            {
                public Quaternion(float x, float y, float z, float w) { }
                public static Quaternion identity => default;
                public static Quaternion Euler(float x, float y, float z) => default;
            }

            public class Transform : Component
            {
                public Vector3 localPosition { get; set; }
                public Quaternion localRotation { get; set; }
                public void SetLocalPositionAndRotation(Vector3 position, Quaternion rotation) { }
            }

            public struct Color
            {
                public Color(float r, float g, float b, float a) { }
                public static Color clear => default;
                public static Color black => default;
                public static Color white => default;
                public static Color red => default;
                public static Color green => default;
                public static Color blue => default;
                public static Color yellow => default;
                public static Color cyan => default;
                public static Color magenta => default;
            }

            public static class Shader
            {
                public static int PropertyToID(string propertyName) => 0;
            }
        }
        """;
}
