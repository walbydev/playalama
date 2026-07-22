using Lama.WebApp.ViewModels;

namespace Lama.WebApp.UnitTests;

public sealed class GamePlayViewModelKeyboardTests
{
    private static GamePlayViewModel CreateVm()
    {
        var vm = new GamePlayViewModel();
        vm.Initialize("test-game", "player1");
        return vm;
    }

    // ── StartKeyboardMode ──────────────────────────────────────────────────

    [Fact]
    public void StartKeyboardMode_Default_Direction_Is_Horizontal()
    {
        var vm = CreateVm();
        vm.StartKeyboardMode(7, 7);

        Assert.True(vm.KeyboardModeActive);
        Assert.Equal(7, vm.KeyboardCursorRow);
        Assert.Equal(7, vm.KeyboardCursorCol);
        Assert.True(vm.KeyboardIsHorizontal);
    }

    // ── MoveCursor ─────────────────────────────────────────────────────────

    [Fact]
    public void MoveCursor_Right_Sets_Horizontal()
    {
        var vm = CreateVm();
        vm.StartKeyboardMode(5, 5);

        vm.MoveCursor(0, 1);

        Assert.Equal(5, vm.KeyboardCursorRow);
        Assert.Equal(6, vm.KeyboardCursorCol);
        Assert.True(vm.KeyboardIsHorizontal);
    }

    [Fact]
    public void MoveCursor_Left_Sets_Horizontal()
    {
        var vm = CreateVm();
        vm.StartKeyboardMode(5, 5);

        vm.MoveCursor(0, -1);

        Assert.Equal(4, vm.KeyboardCursorCol);
        Assert.True(vm.KeyboardIsHorizontal);
    }

    [Fact]
    public void MoveCursor_Down_Sets_Vertical()
    {
        var vm = CreateVm();
        vm.StartKeyboardMode(5, 5);

        vm.MoveCursor(1, 0);

        Assert.Equal(6, vm.KeyboardCursorRow);
        Assert.False(vm.KeyboardIsHorizontal);
    }

    [Fact]
    public void MoveCursor_Up_Sets_Vertical()
    {
        var vm = CreateVm();
        vm.StartKeyboardMode(5, 5);

        vm.MoveCursor(-1, 0);

        Assert.Equal(4, vm.KeyboardCursorRow);
        Assert.False(vm.KeyboardIsHorizontal);
    }

    [Fact]
    public void MoveCursor_Clamps_At_Boundaries()
    {
        var vm = CreateVm();
        vm.StartKeyboardMode(0, 0);

        vm.MoveCursor(-1, -1);

        Assert.Equal(0, vm.KeyboardCursorRow);
        Assert.Equal(0, vm.KeyboardCursorCol);
    }

    [Fact]
    public void MoveCursor_Clamps_At_14()
    {
        var vm = CreateVm();
        vm.StartKeyboardMode(14, 14);

        vm.MoveCursor(1, 1);

        Assert.Equal(14, vm.KeyboardCursorRow);
        Assert.Equal(14, vm.KeyboardCursorCol);
    }

    // ── ToggleKeyboardDirection ────────────────────────────────────────────

    [Fact]
    public void ToggleKeyboardDirection_Horizontal_To_Vertical()
    {
        var vm = CreateVm();
        vm.StartKeyboardMode(7, 7);
        Assert.True(vm.KeyboardIsHorizontal);

        vm.ToggleKeyboardDirection();

        Assert.False(vm.KeyboardIsHorizontal);
    }

    [Fact]
    public void ToggleKeyboardDirection_Vertical_To_Horizontal()
    {
        var vm = CreateVm();
        vm.StartKeyboardMode(7, 7);
        vm.ToggleKeyboardDirection();
        Assert.False(vm.KeyboardIsHorizontal);

        vm.ToggleKeyboardDirection();

        Assert.True(vm.KeyboardIsHorizontal);
    }

    // ── HandleKeyboardInsert ───────────────────────────────────────────────

    [Fact]
    public void HandleKeyboardInsert_Advances_Horizontal_Cursor()
    {
        var vm = CreateVm();
        vm.StartKeyboardMode(3, 3);

        vm.HandleKeyboardInsert();

        Assert.Equal(3, vm.KeyboardCursorRow);
        Assert.Equal(4, vm.KeyboardCursorCol);
    }

    [Fact]
    public void HandleKeyboardInsert_Advances_Vertical_Cursor()
    {
        var vm = CreateVm();
        vm.StartKeyboardMode(3, 3);
        vm.ToggleKeyboardDirection();

        vm.HandleKeyboardInsert();

        Assert.Equal(4, vm.KeyboardCursorRow);
        Assert.Equal(3, vm.KeyboardCursorCol);
    }

    [Fact]
    public void HandleKeyboardInsert_Creates_Gap_Between_Placements()
    {
        var vm = CreateVm();
        vm.StartKeyboardMode(3, 3);

        vm.PlaceTile(0, 'A', 3, 3);
        Assert.Single(vm.PendingPlacements);

        vm.HandleKeyboardInsert();

        vm.PlaceTile(1, 'B', 3, 5);
        Assert.Equal(2, vm.PendingPlacements.Count);

        Assert.Equal(3, vm.PendingPlacements[0].Col);
        Assert.Equal(5, vm.PendingPlacements[1].Col);
    }

    [Fact]
    public void HandleKeyboardInsert_NoOp_When_NotActive()
    {
        var vm = CreateVm();

        vm.HandleKeyboardInsert();

        Assert.Equal(0, vm.KeyboardCursorRow);
        Assert.Equal(0, vm.KeyboardCursorCol);
    }

    // ── HandleKeyboardDelete ───────────────────────────────────────────────

    [Fact]
    public void HandleKeyboardDelete_Removes_PendingTile_At_Cursor()
    {
        var vm = CreateVm();
        vm.StartKeyboardMode(5, 5);
        vm.PlaceTile(0, 'C', 5, 5);

        Assert.Single(vm.PendingPlacements);

        var removed = vm.HandleKeyboardDelete();

        Assert.True(removed);
        Assert.Empty(vm.PendingPlacements);
    }

    [Fact]
    public void HandleKeyboardDelete_ReturnsFalse_When_NoTile_At_Cursor()
    {
        var vm = CreateVm();
        vm.StartKeyboardMode(5, 5);

        var removed = vm.HandleKeyboardDelete();

        Assert.False(removed);
    }

    [Fact]
    public void HandleKeyboardDelete_NoOp_When_NotActive()
    {
        var vm = CreateVm();

        var removed = vm.HandleKeyboardDelete();

        Assert.False(removed);
    }

    // ── StopKeyboardMode ───────────────────────────────────────────────────

    [Fact]
    public void StopKeyboardMode_Deactivates()
    {
        var vm = CreateVm();
        vm.StartKeyboardMode(7, 7);
        Assert.True(vm.KeyboardModeActive);

        vm.StopKeyboardMode();

        Assert.False(vm.KeyboardModeActive);
    }
}
