using Forge.Engine.Data;
using Xunit;

namespace Forge.Engine.Tests;

public class EntityPoolTests
{
    private struct TestEntity
    {
        public int Id;
        public float Value;
    }

    [Fact]
    public void NewPool_HasZeroAlive()
    {
        var pool = new EntityPool<TestEntity>(128);
        Assert.Equal(0, pool.AliveCount);
        Assert.Equal(128, pool.Capacity);
    }

    [Fact]
    public void Allocate_ReturnsValidHandle()
    {
        var pool = new EntityPool<TestEntity>(16);
        var handle = pool.Allocate(new TestEntity { Id = 42, Value = 3.14f });

        Assert.True(handle.IsValid);
        Assert.True(pool.IsAlive(handle));
        Assert.Equal(1, pool.AliveCount);
    }

    [Fact]
    public void Get_ReturnsCorrectData()
    {
        var pool = new EntityPool<TestEntity>(16);
        var handle = pool.Allocate(new TestEntity { Id = 99, Value = 1.5f });

        ref var entity = ref pool.Get(handle);
        Assert.Equal(99, entity.Id);
        Assert.Equal(1.5f, entity.Value);
    }

    [Fact]
    public void Get_AllowsMutation()
    {
        var pool = new EntityPool<TestEntity>(16);
        var handle = pool.Allocate(new TestEntity { Id = 1, Value = 0f });

        ref var entity = ref pool.Get(handle);
        entity.Value = 42f;

        Assert.Equal(42f, pool.Get(handle).Value);
    }

    [Fact]
    public void Free_MarksSlotDead()
    {
        var pool = new EntityPool<TestEntity>(16);
        var handle = pool.Allocate(new TestEntity { Id = 1 });
        pool.Free(handle);

        Assert.False(pool.IsAlive(handle));
        Assert.Equal(0, pool.AliveCount);
    }

    [Fact]
    public void Free_StaleHandle_Get_Throws()
    {
        var pool = new EntityPool<TestEntity>(16);
        var handle = pool.Allocate(new TestEntity { Id = 1 });
        pool.Free(handle);

        Assert.Throws<InvalidOperationException>(() => pool.Get(handle));
    }

    [Fact]
    public void Free_StaleHandle_FreeTwice_Throws()
    {
        var pool = new EntityPool<TestEntity>(16);
        var handle = pool.Allocate(new TestEntity { Id = 1 });
        pool.Free(handle);

        Assert.Throws<InvalidOperationException>(() => pool.Free(handle));
    }

    [Fact]
    public void Allocate_ReusesFreedSlot_WithNewGeneration()
    {
        var pool = new EntityPool<TestEntity>(4);
        var h1 = pool.Allocate(new TestEntity { Id = 1 });
        pool.Free(h1);

        var h2 = pool.Allocate(new TestEntity { Id = 2 });

        // Same index reused, but generation is different
        Assert.Equal(h1.Index, h2.Index);
        Assert.NotEqual(h1.Generation, h2.Generation);

        // Old handle is dead, new handle is alive
        Assert.False(pool.IsAlive(h1));
        Assert.True(pool.IsAlive(h2));

        // Old handle can't access data
        Assert.Throws<InvalidOperationException>(() => pool.Get(h1));

        // New handle returns new data
        Assert.Equal(2, pool.Get(h2).Id);
    }

    [Fact]
    public void Allocate_BeyondCapacity_GrowsAutomatically()
    {
        var pool = new EntityPool<TestEntity>(4);

        var handles = new EntityHandle[8];
        for (int i = 0; i < 8; i++)
            handles[i] = pool.Allocate(new TestEntity { Id = i });

        Assert.Equal(8, pool.AliveCount);
        Assert.True(pool.Capacity >= 8);

        // All handles still valid
        for (int i = 0; i < 8; i++)
        {
            Assert.True(pool.IsAlive(handles[i]));
            Assert.Equal(i, pool.Get(handles[i]).Id);
        }
    }

    [Fact]
    public void ForEach_VisitsAllAlive()
    {
        var pool = new EntityPool<TestEntity>(8);
        pool.Allocate(new TestEntity { Id = 10 });
        var h2 = pool.Allocate(new TestEntity { Id = 20 });
        pool.Allocate(new TestEntity { Id = 30 });
        pool.Free(h2); // Remove middle one

        var visited = new List<int>();
        pool.ForEach((int index, ref TestEntity e) => visited.Add(e.Id));

        Assert.Equal(2, visited.Count);
        Assert.Contains(10, visited);
        Assert.Contains(30, visited);
        Assert.DoesNotContain(20, visited);
    }

    [Fact]
    public void ForEach_AllowsMutation()
    {
        var pool = new EntityPool<TestEntity>(4);
        var h1 = pool.Allocate(new TestEntity { Id = 1, Value = 0f });
        var h2 = pool.Allocate(new TestEntity { Id = 2, Value = 0f });

        pool.ForEach((int index, ref TestEntity e) => e.Value = e.Id * 10f);

        Assert.Equal(10f, pool.Get(h1).Value);
        Assert.Equal(20f, pool.Get(h2).Value);
    }

    [Fact]
    public void ParallelForEach_VisitsAllAlive()
    {
        var pool = new EntityPool<TestEntity>(1024);
        var handles = new EntityHandle[500];
        for (int i = 0; i < 500; i++)
            handles[i] = pool.Allocate(new TestEntity { Id = i, Value = 0f });

        // Free every 5th
        for (int i = 0; i < 500; i += 5)
            pool.Free(handles[i]);

        int expectedAlive = 500 - 100; // 400
        Assert.Equal(expectedAlive, pool.AliveCount);

        int visitCount = 0;
        pool.ParallelForEach((int index, ref TestEntity e) =>
        {
            Interlocked.Increment(ref visitCount);
        }, batchSize: 64);

        Assert.Equal(expectedAlive, visitCount);
    }

    [Fact]
    public void AsSpan_ReturnsCorrectLength()
    {
        var pool = new EntityPool<TestEntity>(32);
        pool.Allocate(new TestEntity { Id = 1 });

        var span = pool.AsSpan();
        Assert.Equal(32, span.Length);
    }

    [Fact]
    public void IsAliveAt_CorrectForActiveAndDead()
    {
        var pool = new EntityPool<TestEntity>(8);
        var h = pool.Allocate(new TestEntity { Id = 1 });

        Assert.True(pool.IsAliveAt(h.Index));
        pool.Free(h);
        Assert.False(pool.IsAliveAt(h.Index));
    }

    [Fact]
    public void InvalidHandle_IsNotAlive()
    {
        var pool = new EntityPool<TestEntity>(8);
        Assert.False(pool.IsAlive(EntityHandle.Invalid));
    }

    [Fact]
    public void Constructor_InvalidCapacity_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new EntityPool<TestEntity>(0));
        Assert.Throws<ArgumentOutOfRangeException>(() => new EntityPool<TestEntity>(-1));
    }

    [Fact]
    public void AllocateAll_FreeAll_ReallocateAll()
    {
        var pool = new EntityPool<TestEntity>(16);
        var handles = new EntityHandle[16];

        for (int i = 0; i < 16; i++)
            handles[i] = pool.Allocate(new TestEntity { Id = i });

        Assert.Equal(16, pool.AliveCount);

        for (int i = 0; i < 16; i++)
            pool.Free(handles[i]);

        Assert.Equal(0, pool.AliveCount);

        // Reallocate — all should get bumped generations
        var newHandles = new EntityHandle[16];
        for (int i = 0; i < 16; i++)
            newHandles[i] = pool.Allocate(new TestEntity { Id = i + 100 });

        Assert.Equal(16, pool.AliveCount);

        for (int i = 0; i < 16; i++)
        {
            Assert.False(pool.IsAlive(handles[i]));
            Assert.True(pool.IsAlive(newHandles[i]));
            Assert.Equal(i + 100, pool.Get(newHandles[i]).Id);
        }
    }
}
