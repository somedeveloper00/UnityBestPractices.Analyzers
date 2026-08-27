namespace UnityBestPractices.Analyzers.Testing;

public static class TestSources
{
    public const string UnityEngine = """
        namespace UnityEngine
        {
            public sealed class SerializeField : System.Attribute { }
            public class Object { }
            public class ScriptableObject : Object { }
            public class Component : Object { }
            public class MonoBehaviour : Component { }
        }
        """;

    public const string Jobs = """
        namespace Unity.Burst
        {
            public sealed class BurstCompileAttribute : System.Attribute { }
        }
        namespace Unity.Jobs
        {
            public struct JobHandle { }
            public interface IJob { void Execute(); }
            public static class JobExtensions
            {
                public static JobHandle Schedule<T>(this T job) where T : struct, IJob => default;
            }
        }
        """;

    public const string Collections = """
        namespace Unity.Collections
        {
            public enum Allocator { Temp, TempJob, Persistent }
            public struct NativeArray<T> : System.IDisposable where T : struct
            {
                public NativeArray(int length, Allocator allocator) { }
                public int Length => 0;
                public T this[int index] { get => default; set { } }
                public void Dispose() { }
            }
        }
        """;

    public const string Shader = """
        namespace UnityEngine
        {
            public static class Shader
            {
                public static int PropertyToID(string propertyName) => 0;
            }
        }
        """;

    public const string Transform = """
        namespace UnityEngine
        {
            public struct Vector2 { public Vector2(float x, float y) { } }
            public struct Vector3
            {
                public Vector3(float x, float y, float z = 0) { }
                public static implicit operator Vector3(Vector2 value) => default;
                public static Vector3 operator +(Vector3 left, Vector3 right) => default;
            }
            public struct Quaternion { public static Quaternion Euler(float x, float y, float z) => default; }
            public class Transform
            {
                public Vector3 localPosition { get; set; }
                public Quaternion localRotation { get; set; }
                public void SetLocalPositionAndRotation(Vector3 position, Quaternion rotation) { }
            }
            public class RectTransform : Transform { }
        }
        """;
}
