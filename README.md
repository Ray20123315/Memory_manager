# Memory Manager / 記憶體管理器

Windows 11 記憶體監控與安全調整工具。beta.28 以「看得懂、可回復、不亂殺程式」為核心。

## beta.28 重點

- 1 ms 起的 Telemetry（資料取樣）與 UI 更新頻率分離。
- Dashboard、Processes、Notifications、Logs、Settings、About 六個主要頁面。
- Commit 壓力、Page File / Commit Headroom、Process Working Set / Private Memory。
- Commit Δ、疑似 Leak、Commit Exhaustion ETA、安全關閉建議。
- Flight Recorder 與 Incident Bundle 匯出。
- 內建 GitHub Release 更新檢查與桌面／App 內通知。
- 真正安裝程式 `MemoryManagerSetup.exe`：安裝到使用者程式目錄、建立捷徑、登錄解除安裝項目，支援 Repair / Uninstall。
- 預設最大化，深色／淺色切換。

> 1 ms 是「要求的取樣間隔」，不是 Windows 保證每一次 callback 都精準落在 1.000 ms。畫面更新與資料取樣分離，避免 UI 自己變成效能負擔。

## 安裝

到 **Releases** 下載：

- `MemoryManagerSetup.exe`：建議，一鍵真正安裝。
- `MemoryManager.exe`：免安裝版。
- `SHA256SUMS.txt`：檔案完整性驗證。

## 安全原則

本專案不會宣稱「清空 RAM」可以讓電腦神奇變快。對 Process 的操作以顯示、建議、可回復為優先；不寫未公開的 EC/MSR，也不假裝自簽憑證能繞過 Windows SmartScreen。

## 開發

```powershell
dotnet build src/MemoryManager/MemoryManager.csproj -c Release
dotnet publish src/MemoryManager/MemoryManager.csproj -c Release -r win-x64 --self-contained true
```

## 網站

產品網站原始碼維護於 `Ray20123315/html`。
