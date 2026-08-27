internal static class UnityCollectionsFixture
{
    internal const string Source = """
        namespace Unity.Collections
        {
            public sealed class ReadOnlyAttribute : System.Attribute { }
            public enum Allocator { Temp, TempJob, Persistent }
            public enum NativeArrayOptions { UninitializedMemory, ClearMemory }

            public struct NativeArray<T> : System.IDisposable where T : struct
            {
                private T[] _items;
                public NativeArray(
                    int length,
                    Allocator allocator,
                    NativeArrayOptions options = NativeArrayOptions.ClearMemory)
                {
                    _items = new T[length];
                }

                public NativeArray(T[] array, Allocator allocator)
                {
                    _items = array;
                }

                public int Length => _items.Length;
                public T this[int index]
                {
                    get => _items[index];
                    set => _items[index] = value;
                }

                public System.Span<T> AsSpan() => _items;
                public void Dispose() { }
                public Enumerator GetEnumerator() => default;
                public struct Enumerator
                {
                    public T Current => default;
                    public bool MoveNext() => false;
                }
            }

            public struct NativeList<T> : System.IDisposable where T : struct
            {
                private T[] _items;
                public NativeList(Allocator allocator) { _items = default; }
                public void Add(T item) { }
                public T this[int index]
                {
                    get => _items[index];
                    set => _items[index] = value;
                }

                public ref T ElementAt(int index) => ref _items[index];
                public void Dispose() { }
                public Enumerator GetEnumerator() => default;
                public struct Enumerator
                {
                    public T Current => default;
                    public bool MoveNext() => false;
                }
            }
        }
        """;
}
