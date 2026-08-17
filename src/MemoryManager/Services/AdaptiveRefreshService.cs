using Ray.MemoryManager.Models;

namespace Ray.MemoryManager.Services;

public sealed class AdaptiveRefreshService
{
    public int ChooseUiIntervalMs(bool windowVisible, bool windowActive, bool gameMode, MemorySample sample)
    {
        if (!windowVisible) return 1000;
        if (sample.CommitUsedPercent >= 95 || sample.AvailablePhysical < 768UL * 1024 * 1024) return 33;
        if (gameMode) return windowActive ? 100 : 500;
        if (!windowActive) return 500;
        if (sample.CommitUsedPercent >= 85 || sample.AvailablePhysical < 2UL * 1024 * 1024 * 1024) return 100;
        return 250;
    }

    public string Explain(int ms) => ms switch
    {
        <= 33 => "高壓力：畫面加快更新，方便立即判斷。",
        <= 100 => "需要注意：提高更新速度，但不追求每毫秒重畫。",
        <= 250 => "一般狀態：平衡即時性與耗電。",
        <= 500 => "背景狀態：降低不必要更新。",
        _ => "最小化／不可見：只保留低頻畫面更新，資料引擎仍獨立運作。"
    };
}
