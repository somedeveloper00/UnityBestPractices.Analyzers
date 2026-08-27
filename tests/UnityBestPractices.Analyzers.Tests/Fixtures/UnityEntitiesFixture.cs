internal static class UnityEntitiesFixture
{
    internal const string Source = """
        namespace Unity.Entities
        {
            public interface IComponentData { }
            public interface IBufferElementData { }
            public interface IJobEntity { }
            public struct Entity { }

            public struct ComponentLookup<T> where T : struct, IComponentData
            {
                public bool HasComponent(Entity entity) => false;
            }

            public struct EntityStorageInfoLookup
            {
                public bool Exists(Entity entity) => false;
            }

            public struct DynamicBuffer<T> where T : struct, IBufferElementData
            {
                private T[] _items;
                public int Length => 0;
                public void Clear() { }
                public T this[int index]
                {
                    get => _items[index];
                    set => _items[index] = value;
                }

                public ref T ElementAt(int index) => ref _items[index];
            }

            public enum EntityQueryOptions { Default, IncludeDisabledEntities }

            public sealed class WithAllAttribute : System.Attribute
            {
                public WithAllAttribute(params System.Type[] types) { }
            }

            public sealed class WithAnyAttribute : System.Attribute
            {
                public WithAnyAttribute(params System.Type[] types) { }
            }

            public sealed class WithNoneAttribute : System.Attribute
            {
                public WithNoneAttribute(params System.Type[] types) { }
            }

            public sealed class WithChangeFilterAttribute : System.Attribute
            {
                public WithChangeFilterAttribute(params System.Type[] types) { }
            }

            public sealed class WithOptionsAttribute : System.Attribute
            {
                public WithOptionsAttribute(EntityQueryOptions options) { }
            }

            public sealed class EntityIndexInQueryAttribute : System.Attribute { }

            public struct TimeData
            {
                public double ElapsedTime => 0d;
                public float DeltaTime => 0f;
            }

            public struct EntityManager
            {
                public void AddComponent<T>(Entity entity)
                    where T : struct, IComponentData { }
                public T GetComponentData<T>(Entity entity)
                    where T : struct, IComponentData => default;
                public void RemoveComponent<T>(Entity entity)
                    where T : struct, IComponentData { }
                public void DestroyEntity(Entity entity) { }
            }

            public sealed class World
            {
                public TimeData Time => default;
            }

            public struct EntityCommandBuffer : System.IDisposable
            {
                public EntityCommandBuffer(Unity.Collections.Allocator allocator) { }
                public ParallelWriter AsParallelWriter() => default;
                public Entity CreateEntity() => default;
                public void AddComponent<T>(Entity entity, T component)
                    where T : struct, IComponentData { }
                public void RemoveComponent<T>(Entity entity)
                    where T : struct, IComponentData { }
                public void DestroyEntity(Entity entity) { }
                public void Playback(EntityManager entityManager) { }
                public void Dispose() { }

                public struct ParallelWriter
                {
                    public Entity CreateEntity(int sortKey) => default;
                    public void AddComponent<T>(int sortKey, Entity entity, T component)
                        where T : struct, IComponentData { }
                }
            }

            public sealed class RefRW<T> where T : struct, IComponentData
            {
                private T _value;
                public ref T ValueRW => ref _value;
            }

            public sealed class RefRO<T> where T : struct, IComponentData
            {
                private T _value;
                public ref readonly T ValueRO => ref _value;
            }

            public abstract class SystemBase
            {
                protected EntitiesBuilder Entities => default;
                protected EntityManager EntityManager => default;
                protected World World => default;
                protected Unity.Jobs.JobHandle Dependency { get; set; }
            }

            public delegate void RefAction<T>(ref T value) where T : struct, IComponentData;
            public delegate void InAction<T>(in T value) where T : struct, IComponentData;
            public delegate void OutAction<T>(out T value) where T : struct, IComponentData;
            public delegate void EntityAction(Entity entity);
            public delegate void EntityRefAction<T>(Entity entity, ref T value)
                where T : struct, IComponentData;
            public delegate void RefInAction<T1, T2>(ref T1 first, in T2 second)
                where T1 : struct, IComponentData
                where T2 : struct, IComponentData;
            public delegate void EntityRefInAction<T1, T2>(Entity entity, ref T1 first, in T2 second)
                where T1 : struct, IComponentData
                where T2 : struct, IComponentData;
            public delegate void EntityInInAction<T1, T2>(Entity entity, in T1 first, in T2 second)
                where T1 : struct, IComponentData
                where T2 : struct, IComponentData;
            public delegate void EntityIndexInAction<T>(
                Entity entity,
                int entityInQueryIndex,
                in T value)
                where T : struct, IComponentData;
            public delegate void EntityIndexSixInAction<T1, T2, T3, T4, T5, T6>(
                Entity entity, int entityInQueryIndex, in T1 first, in T2 second,
                in T3 third, in T4 fourth, in T5 fifth, in T6 sixth)
                where T1 : struct, IComponentData where T2 : struct, IComponentData
                where T3 : struct, IComponentData where T4 : struct, IComponentData
                where T5 : struct, IComponentData where T6 : struct, IComponentData;
            public delegate void EntityIndexComponentsBufferAction<T1, T2, TBuffer, T3, T4>(
                Entity entity,
                int entityInQueryIndex,
                ref T1 first,
                ref T2 second,
                ref DynamicBuffer<TBuffer> buffer,
                in T3 third,
                in T4 fourth)
                where T1 : struct, IComponentData
                where T2 : struct, IComponentData
                where TBuffer : struct, IBufferElementData
                where T3 : struct, IComponentData
                where T4 : struct, IComponentData;
            public delegate void EntityRefInBufferThreeInAction<T1, TBuffer, T2, T3, T4>(
                Entity entity,
                ref T1 first,
                in DynamicBuffer<TBuffer> buffer,
                in T2 second,
                in T3 third,
                in T4 fourth)
                where T1 : struct, IComponentData
                where TBuffer : struct, IBufferElementData
                where T2 : struct, IComponentData
                where T3 : struct, IComponentData
                where T4 : struct, IComponentData;
            public delegate void EntityFourBuffersThreeComponentsAction<TBuffer1, TBuffer2, TBuffer3, TBuffer4, T1, T2, T3>(
                Entity entity,
                ref DynamicBuffer<TBuffer1> buffer1,
                ref DynamicBuffer<TBuffer2> buffer2,
                ref DynamicBuffer<TBuffer3> buffer3,
                ref DynamicBuffer<TBuffer4> buffer4,
                ref T1 first,
                ref T2 second,
                in T3 third)
                where TBuffer1 : struct, IBufferElementData
                where TBuffer2 : struct, IBufferElementData
                where TBuffer3 : struct, IBufferElementData
                where TBuffer4 : struct, IBufferElementData
                where T1 : struct, IComponentData
                where T2 : struct, IComponentData
                where T3 : struct, IComponentData;

            public struct EntitiesBuilder
            {
                public EntitiesBuilder WithAll<T>() where T : struct, IComponentData => this;
                public EntitiesBuilder WithAll<T1, T2>()
                    where T1 : struct, IComponentData
                    where T2 : struct, IComponentData => this;
                public EntitiesBuilder WithAny<T>() where T : struct, IComponentData => this;
                public EntitiesBuilder WithNone<T>() where T : struct, IComponentData => this;
                public EntitiesBuilder WithChangeFilter<T>() where T : struct, IComponentData => this;
                public EntitiesBuilder WithEntityQueryOptions(EntityQueryOptions options) => this;
                public EntitiesBuilder WithStructuralChanges() => this;
                public EntitiesBuilder WithoutBurst() => this;
                public EntitiesBuilder WithReadOnly<T>(T value) where T : struct => this;
                public EntitiesBuilder WithDisposeOnCompletion<T>(T value) where T : struct => this;
                public ForEachDescription ForEach(EntityAction action) => default;
                public ForEachDescription ForEach<T>(RefAction<T> action) where T : struct, IComponentData => default;
                public ForEachDescription ForEach<T>(InAction<T> action) where T : struct, IComponentData => default;
                public ForEachDescription ForEach<T>(OutAction<T> action) where T : struct, IComponentData => default;
                public ForEachDescription ForEach<T>(EntityRefAction<T> action)
                    where T : struct, IComponentData => default;
                public ForEachDescription ForEach<T1, T2>(RefInAction<T1, T2> action)
                    where T1 : struct, IComponentData
                    where T2 : struct, IComponentData => default;
                public ForEachDescription ForEach<T1, T2>(EntityRefInAction<T1, T2> action)
                    where T1 : struct, IComponentData
                    where T2 : struct, IComponentData => default;
                public ForEachDescription ForEach<T1, T2>(EntityInInAction<T1, T2> action)
                    where T1 : struct, IComponentData
                    where T2 : struct, IComponentData => default;
                public ForEachDescription ForEach<T>(EntityIndexInAction<T> action)
                    where T : struct, IComponentData => default;
                public ForEachDescription ForEach<T1, T2, T3, T4, T5, T6>(
                    EntityIndexSixInAction<T1, T2, T3, T4, T5, T6> action)
                    where T1 : struct, IComponentData where T2 : struct, IComponentData
                    where T3 : struct, IComponentData where T4 : struct, IComponentData
                    where T5 : struct, IComponentData where T6 : struct, IComponentData => default;
                public ForEachDescription ForEach<T1, T2, TBuffer, T3, T4>(
                    EntityIndexComponentsBufferAction<T1, T2, TBuffer, T3, T4> action)
                    where T1 : struct, IComponentData
                    where T2 : struct, IComponentData
                    where TBuffer : struct, IBufferElementData
                    where T3 : struct, IComponentData
                    where T4 : struct, IComponentData => default;
                public ForEachDescription ForEach<T1, TBuffer, T2, T3, T4>(
                    EntityRefInBufferThreeInAction<T1, TBuffer, T2, T3, T4> action)
                    where T1 : struct, IComponentData
                    where TBuffer : struct, IBufferElementData
                    where T2 : struct, IComponentData
                    where T3 : struct, IComponentData
                    where T4 : struct, IComponentData => default;
                public ForEachDescription ForEach<TBuffer1, TBuffer2, TBuffer3, TBuffer4, T1, T2, T3>(
                    EntityFourBuffersThreeComponentsAction<TBuffer1, TBuffer2, TBuffer3, TBuffer4, T1, T2, T3> action)
                    where TBuffer1 : struct, IBufferElementData
                    where TBuffer2 : struct, IBufferElementData
                    where TBuffer3 : struct, IBufferElementData
                    where TBuffer4 : struct, IBufferElementData
                    where T1 : struct, IComponentData
                    where T2 : struct, IComponentData
                    where T3 : struct, IComponentData => default;
            }

            public struct ForEachDescription
            {
                public void Run() { }
                public Unity.Jobs.JobHandle Schedule() => default;
                public Unity.Jobs.JobHandle Schedule(Unity.Jobs.JobHandle dependency) => default;
                public Unity.Jobs.JobHandle ScheduleParallel() => default;
                public Unity.Jobs.JobHandle ScheduleParallel(Unity.Jobs.JobHandle dependency) => default;
            }

            public static class IJobEntityExtensions
            {
                public static void Run<T>(this T job) where T : struct, IJobEntity { }
                public static Unity.Jobs.JobHandle Schedule<T>(this T job) where T : struct, IJobEntity => default;
                public static Unity.Jobs.JobHandle Schedule<T>(this T job, Unity.Jobs.JobHandle dependency)
                    where T : struct, IJobEntity => default;
                public static Unity.Jobs.JobHandle ScheduleParallel<T>(this T job) where T : struct, IJobEntity => default;
                public static Unity.Jobs.JobHandle ScheduleParallel<T>(this T job, Unity.Jobs.JobHandle dependency)
                    where T : struct, IJobEntity => default;
            }

            public static class SystemAPI
            {
                public static TimeData Time => default;
                public static bool HasComponent<T>(Entity entity)
                    where T : struct, IComponentData => false;
                public static bool Exists(Entity entity) => false;
                public static EntityStorageInfoLookup GetEntityStorageInfoLookup() => default;
                public static ComponentLookup<T> GetComponentLookup<T>(bool isReadOnly = false)
                    where T : struct, IComponentData => default;
                public static RefRW<T> GetComponentRW<T>(Entity entity)
                    where T : struct, IComponentData => default;
                public static RefRO<T> GetComponentRO<T>(Entity entity)
                    where T : struct, IComponentData => default;
                public static DynamicBuffer<T> GetBuffer<T>(Entity entity)
                    where T : struct, IBufferElementData => default;
                public static QueryEnumerable<T1> Query<T1>() => default;
                public static QueryEnumerable<T1, T2> Query<T1, T2>() => default;
                public static QueryEnumerable<T1, T2, T3, T4> Query<T1, T2, T3, T4>() => default;
                public static QueryEnumerable<T1, T2, T3, T4, T5> Query<T1, T2, T3, T4, T5>() =>
                    default;
                public static SystemAPIQueryBuilder QueryBuilder() => default;
            }

            public struct SystemAPIQueryBuilder
            {
                public SystemAPIQueryBuilder WithAll<T>() => this;
                public SystemAPIQueryBuilder WithAny<T>() => this;
                public SystemAPIQueryBuilder WithNone<T>() => this;
                public EntityQuery Build() => default;
            }

            public struct EntityQuery
            {
                public Unity.Collections.NativeArray<Entity> ToEntityArray(Unity.Collections.Allocator allocator) =>
                    default;
            }

            public struct QueryEnumerable<T1>
            {
                public QueryEnumerable<T1> WithAll<T>() => this;
                public QueryEnumerable<T1> WithAny<T>() => this;
                public QueryEnumerable<T1> WithNone<T>() => this;
                public QueryEnumerable<T1> WithChangeFilter<T>() => this;
                public QueryEnumerable<T1> WithOptions(EntityQueryOptions options) => this;
                public QueryEnumerableWithEntity<T1> WithEntityAccess() => default;
                public Enumerator GetEnumerator() => default;
                public struct Enumerator
                {
                    public T1 Current => default;
                    public bool MoveNext() => false;
                }
            }

            public struct QueryEnumerable<T1, T2>
            {
                public QueryEnumerable<T1, T2> WithAll<T>() => this;
                public QueryEnumerable<T1, T2> WithAny<T>() => this;
                public QueryEnumerable<T1, T2> WithNone<T>() => this;
                public QueryEnumerable<T1, T2> WithChangeFilter<T>() => this;
                public QueryEnumerable<T1, T2> WithOptions(EntityQueryOptions options) => this;
                public QueryEnumerableWithEntity<T1, T2> WithEntityAccess() => default;
                public Enumerator GetEnumerator() => default;
                public struct Enumerator
                {
                    public (T1, T2) Current => default;
                    public bool MoveNext() => false;
                }
            }

            public struct QueryEnumerableWithEntity<T1>
            {
                public Enumerator GetEnumerator() => default;
                public struct Enumerator
                {
                    public (T1, Entity) Current => default;
                    public bool MoveNext() => false;
                }
            }

            public struct QueryEnumerableWithEntity<T1, T2>
            {
                public Enumerator GetEnumerator() => default;
                public struct Enumerator
                {
                    public (T1, T2, Entity) Current => default;
                    public bool MoveNext() => false;
                }
            }

            public struct QueryEnumerable<T1, T2, T3, T4, T5>
            {
                public QueryEnumerable<T1, T2, T3, T4, T5> WithAll<T>() => this;
                public QueryEnumerable<T1, T2, T3, T4, T5> WithAny<T>() => this;
                public QueryEnumerable<T1, T2, T3, T4, T5> WithNone<T>() => this;
                public QueryEnumerable<T1, T2, T3, T4, T5> WithChangeFilter<T>() => this;
                public QueryEnumerable<T1, T2, T3, T4, T5> WithOptions(EntityQueryOptions options) =>
                    this;
                public QueryEnumerableWithEntity<T1, T2, T3, T4, T5> WithEntityAccess() => default;
                public Enumerator GetEnumerator() => default;
                public struct Enumerator
                {
                    public (T1, T2, T3, T4, T5) Current => default;
                    public bool MoveNext() => false;
                }
            }

            public struct QueryEnumerable<T1, T2, T3, T4>
            {
                public QueryEnumerable<T1, T2, T3, T4> WithAll<T>() => this;
                public QueryEnumerable<T1, T2, T3, T4> WithAny<T>() => this;
                public QueryEnumerable<T1, T2, T3, T4> WithNone<T>() => this;
                public QueryEnumerable<T1, T2, T3, T4> WithChangeFilter<T>() => this;
                public QueryEnumerable<T1, T2, T3, T4> WithOptions(EntityQueryOptions options) => this;
                public QueryEnumerableWithEntity<T1, T2, T3, T4> WithEntityAccess() => default;
                public Enumerator GetEnumerator() => default;
                public struct Enumerator
                {
                    public (T1, T2, T3, T4) Current => default;
                    public bool MoveNext() => false;
                }
            }

            public struct QueryEnumerableWithEntity<T1, T2, T3, T4>
            {
                public Enumerator GetEnumerator() => default;
                public struct Enumerator
                {
                    public (T1, T2, T3, T4, Entity) Current => default;
                    public bool MoveNext() => false;
                }
            }

            public struct QueryEnumerableWithEntity<T1, T2, T3, T4, T5>
            {
                public Enumerator GetEnumerator() => default;
                public struct Enumerator
                {
                    public (T1, T2, T3, T4, T5, Entity) Current => default;
                    public bool MoveNext() => false;
                }
            }
        }
        """;
}
