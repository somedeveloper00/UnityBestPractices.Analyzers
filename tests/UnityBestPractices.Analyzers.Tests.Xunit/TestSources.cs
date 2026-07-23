internal static class TestSources
{
    internal const string UnityEngine = """
        namespace UnityEngine
        {
            public sealed class SerializeField : System.Attribute { }
            public class Object { }
            public class ScriptableObject : Object { }
            public class Component : Object { }
            public class MonoBehaviour : Component { }
        }
        """;

    internal const string Jobs = """
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

    internal const string Collections = """
        namespace Unity.Collections
        {
            public enum Allocator { Temp, TempJob, Persistent }
            public struct NativeArray<T> where T : struct
            {
                public NativeArray(int length, Allocator allocator) { }
                public int Length => 0;
                public void Dispose() { }
            }
        }
        """;

    internal const string Shader = """
        namespace UnityEngine
        {
            public static class Shader
            {
                public static int PropertyToID(string propertyName) => 0;
            }
        }
        """;
}
