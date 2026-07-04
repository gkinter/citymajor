using Forge.Engine.Core;
using Xunit;

namespace Forge.Engine.Tests;

public class SceneManagerTests
{
    // =========================================================================
    // Push / Pop / Replace
    // =========================================================================

    [Fact]
    public void PushScene_IncreasesCount()
    {
        var mgr = new SceneManager();
        Assert.Equal(0, mgr.SceneCount);
        Assert.Null(mgr.CurrentScene);

        mgr.PushScene(new TestScene("A"));
        Assert.Equal(1, mgr.SceneCount);
        Assert.NotNull(mgr.CurrentScene);
    }

    [Fact]
    public void PushScene_CallsEnter()
    {
        var mgr = new SceneManager();
        var scene = new TestScene("A");
        Assert.False(scene.EnterCalled);

        mgr.PushScene(scene);
        Assert.True(scene.EnterCalled);
    }

    [Fact]
    public void PopScene_RemovesTopAndCallsExitDispose()
    {
        var mgr = new SceneManager();
        var sceneA = new TestScene("A");
        var sceneB = new TestScene("B");

        mgr.PushScene(sceneA);
        mgr.PushScene(sceneB);
        Assert.Equal(2, mgr.SceneCount);

        mgr.PopScene();
        Assert.Equal(1, mgr.SceneCount);
        Assert.True(sceneB.ExitCalled);
        Assert.True(sceneB.DisposeCalled);
        Assert.False(sceneA.ExitCalled); // A was never exited (just covered)
    }

    [Fact]
    public void PopScene_EmptyStack_NoOp()
    {
        var mgr = new SceneManager();
        mgr.PopScene(); // Should not throw
        Assert.Equal(0, mgr.SceneCount);
    }

    [Fact]
    public void ReplaceScene_SwapsTop()
    {
        var mgr = new SceneManager();
        var sceneA = new TestScene("A");
        var sceneB = new TestScene("B");

        mgr.PushScene(sceneA);
        mgr.ReplaceScene(sceneB);

        Assert.Equal(1, mgr.SceneCount);
        Assert.True(sceneA.ExitCalled);
        Assert.True(sceneA.DisposeCalled);
        Assert.True(sceneB.EnterCalled);
    }

    [Fact]
    public void ReplaceScene_OnEmptyStack_JustPushes()
    {
        var mgr = new SceneManager();
        var scene = new TestScene("A");

        mgr.ReplaceScene(scene);
        Assert.Equal(1, mgr.SceneCount);
        Assert.True(scene.EnterCalled);
    }

    // =========================================================================
    // Update propagation
    // =========================================================================

    [Fact]
    public void Update_CallsTopScene()
    {
        var mgr = new SceneManager();
        var scene = new TestScene("A");
        mgr.PushScene(scene);

        mgr.Update(0.016);
        Assert.True(scene.UpdateCalled);
    }

    [Fact]
    public void Update_BlockingScene_BlocksBelow()
    {
        var mgr = new SceneManager();
        var gameplay = new TestScene("Gameplay", blocksUpdate: false);
        var pause = new TestScene("Pause", blocksUpdate: true);

        mgr.PushScene(gameplay);
        mgr.PushScene(pause);

        mgr.Update(0.016);

        // Pause should receive update (it's on top)
        Assert.True(pause.UpdateCalled);
        // Gameplay should NOT receive update (pause blocks it)
        Assert.False(gameplay.UpdateCalled);
    }

    [Fact]
    public void Update_NonBlockingOverlay_UpdatesBoth()
    {
        var mgr = new SceneManager();
        var gameplay = new TestScene("Gameplay", blocksUpdate: false);
        var tooltip = new TestScene("Tooltip", blocksUpdate: false);

        mgr.PushScene(gameplay);
        mgr.PushScene(tooltip);

        mgr.Update(0.016);

        Assert.True(tooltip.UpdateCalled);
        Assert.True(gameplay.UpdateCalled);
    }

    // =========================================================================
    // Render propagation
    // =========================================================================

    [Fact]
    public void Render_BlockingScene_OnlyRendersTop()
    {
        var mgr = new SceneManager();
        var gameplay = new TestScene("Gameplay");
        var loading = new TestScene("Loading", blocksRender: true);

        mgr.PushScene(gameplay);
        mgr.PushScene(loading);

        mgr.Render(0.5);

        Assert.True(loading.RenderCalled);
        Assert.False(gameplay.RenderCalled);
    }

    [Fact]
    public void Render_NonBlockingOverlay_RendersBothBottomUp()
    {
        var mgr = new SceneManager();
        var gameplay = new TestScene("Gameplay");
        var pause = new TestScene("Pause", blocksRender: false);

        mgr.PushScene(gameplay);
        mgr.PushScene(pause);

        mgr.Render(0.5);

        Assert.True(gameplay.RenderCalled);
        Assert.True(pause.RenderCalled);
        // Gameplay should render first (bottom-up)
        Assert.True(gameplay.RenderOrder < pause.RenderOrder);
    }

    [Fact]
    public void RenderUI_PropagatesLikeRender()
    {
        var mgr = new SceneManager();
        var gameplay = new TestScene("Gameplay");
        var pause = new TestScene("Pause", blocksRender: false);

        mgr.PushScene(gameplay);
        mgr.PushScene(pause);

        mgr.RenderUI();

        Assert.True(gameplay.RenderUICalled);
        Assert.True(pause.RenderUICalled);
    }

    // =========================================================================
    // Scene stack ordering
    // =========================================================================

    [Fact]
    public void CurrentScene_IsAlwaysTop()
    {
        var mgr = new SceneManager();
        var a = new TestScene("A");
        var b = new TestScene("B");
        var c = new TestScene("C");

        mgr.PushScene(a);
        Assert.Equal("A", ((TestScene)mgr.CurrentScene!).Name);

        mgr.PushScene(b);
        Assert.Equal("B", ((TestScene)mgr.CurrentScene!).Name);

        mgr.PushScene(c);
        Assert.Equal("C", ((TestScene)mgr.CurrentScene!).Name);

        mgr.PopScene();
        Assert.Equal("B", ((TestScene)mgr.CurrentScene!).Name);
    }

    // =========================================================================
    // Deferred operations during iteration
    // =========================================================================

    [Fact]
    public void PushDuringUpdate_Deferred()
    {
        var mgr = new SceneManager();
        var newScene = new TestScene("New");
        var pushingScene = new DeferredPushScene(mgr, newScene);

        mgr.PushScene(pushingScene);
        Assert.Equal(1, mgr.SceneCount);

        mgr.Update(0.016); // DeferredPushScene pushes during Update
        Assert.Equal(2, mgr.SceneCount);
        Assert.True(newScene.EnterCalled);
    }

    // =========================================================================
    // Concrete scene types (basic sanity)
    // =========================================================================

    [Fact]
    public void MainMenuScene_Properties()
    {
        var mgr = new SceneManager();
        var scene = new MainMenuScene(mgr);
        Assert.True(scene.BlocksUpdate);
        Assert.True(scene.BlocksRender);
        scene.Dispose(); // no-op, should not throw
    }

    [Fact]
    public void PauseMenuScene_Properties()
    {
        var mgr = new SceneManager();
        var scene = new PauseMenuScene(mgr);
        Assert.True(scene.BlocksUpdate);
        Assert.False(scene.BlocksRender); // gameplay visible behind
        scene.Dispose();
    }

    [Fact]
    public void LoadingScene_Properties()
    {
        bool completed = false;
        var mgr = new SceneManager();
        var scene = new LoadingScene(mgr, "Test", 0.5f, () => completed = true);
        Assert.True(scene.BlocksUpdate);
        Assert.True(scene.BlocksRender);
        scene.Dispose();
    }

    [Fact]
    public void GameplayScene_Properties()
    {
        var mgr = new SceneManager();
        var scene = new GameplayScene(mgr);
        Assert.False(scene.BlocksUpdate);
        Assert.False(scene.BlocksRender);
        scene.Dispose();
    }

    [Fact]
    public void LoadingScene_CompletesAfterDuration()
    {
        bool completed = false;
        var mgr = new SceneManager();
        var scene = new LoadingScene(mgr, "Loading", 1.0f, () => completed = true);
        scene.Enter();

        // Not yet done
        scene.Update(0.5);
        Assert.False(completed);

        // Should complete
        scene.Update(0.6);
        Assert.True(completed);

        scene.Dispose();
    }

    [Fact]
    public void GameplayScene_UpdatesState()
    {
        var mgr = new SceneManager();
        var scene = new GameplayScene(mgr);
        scene.Enter();

        // Run many updates
        for (int i = 0; i < 200; i++)
            scene.Update(0.016);

        // Scene should still work without crashing
        scene.Render(0.5);
        scene.Dispose();
    }

    // =========================================================================
    // Test helpers
    // =========================================================================

    private static int _renderCounter;

    private class TestScene : IScene
    {
        public string Name { get; }
        public bool EnterCalled { get; private set; }
        public bool ExitCalled { get; private set; }
        public bool UpdateCalled { get; private set; }
        public bool RenderCalled { get; private set; }
        public bool RenderUICalled { get; private set; }
        public bool DisposeCalled { get; private set; }
        public int RenderOrder { get; private set; }

        public bool BlocksUpdate { get; }
        public bool BlocksRender { get; }

        public TestScene(string name, bool blocksUpdate = false, bool blocksRender = false)
        {
            Name = name;
            BlocksUpdate = blocksUpdate;
            BlocksRender = blocksRender;
        }

        public void Enter() => EnterCalled = true;
        public void Exit() => ExitCalled = true;
        public void Update(double dt) => UpdateCalled = true;
        public void Render(double alpha) { RenderCalled = true; RenderOrder = Interlocked.Increment(ref _renderCounter); }
        public void RenderUI() => RenderUICalled = true;
        public void Dispose() => DisposeCalled = true;
    }

    /// <summary>A scene that pushes another scene during its Update call.</summary>
    private class DeferredPushScene : IScene
    {
        private readonly SceneManager _mgr;
        private readonly IScene _toPush;
        private bool _pushed;

        public bool BlocksUpdate => false;
        public bool BlocksRender => false;

        public DeferredPushScene(SceneManager mgr, IScene toPush)
        {
            _mgr = mgr;
            _toPush = toPush;
        }

        public void Enter() { }
        public void Exit() { }
        public void Update(double dt) { if (!_pushed) { _mgr.PushScene(_toPush); _pushed = true; } }
        public void Render(double alpha) { }
        public void RenderUI() { }
        public void Dispose() { }
    }
}
