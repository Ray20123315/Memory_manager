# Memory Manager / 記憶體管理器

Windows 11 記憶體監控、事故分析與安全調整工具。`v0.9.0-beta.29` 以「先觀測、可回復、不亂殺程式」為核心；不把記憶體最佳化包裝成一鍵清 RAM。

## beta.29 已驗證功能

### 記憶體觀測
- 1 ms 起的 Telemetry（要求取樣間隔），與 UI refresh 完全分離。
- RAM、Commit、Commit Headroom、Commit ETA、Page File 狀態。
- Process Working Set、Private Memory、真正 Process Commit Charge、Commit Δ。
- 持續成長型 Leak indicator，不把單次尖峰直接判成 leak。
- Adaptive Refresh：前景／背景／遊戲情境自動調整畫面更新負擔。

### 救援與事故分析
- Emergency Rescue 與 Safe Close Advisor；不自動亂殺系統程式。
- Flight Recorder V2：記憶體內約 250 ms frame、磁碟約 1 秒持久化，並限制 journal 大小。
- Windows Event Log Incident Timeline。
- Previous Crash Analyzer：結合 heartbeat、Event 2004、41/6008、BugCheck、WHEA 等證據，不把 Event 41 單獨當作根因。
- Reliability History 與 incident-only Support Bundle。

### 遊戲 / Per-App Memory Rules
- Game Memory Reserve V2 採 **可回復的 Windows Process Memory Priority**，不是硬鎖 RAM。
- Game Profile 可由常見遊戲安裝路徑自動偵測，也可手動加入目前前景程式。
- Per-App Memory Rule 預設總開關關閉；只有使用者建立規則並啟用後才可能套用。
- 前景、遊戲、反作弊、語音、輸入、安全與 Windows 核心程序有硬保護條件。
- 規則不再符合、程式變前景、停用規則或 Memory Manager 結束時，要求還原原本 Memory Priority。

### Update / OEM / 通知
- Notification Center 與 Windows 本機通知；背景更新是 GitHub Releases polling，不假裝成 true push。
- Built-in Update Center 支援 Beta / Stable channel、Release Notes、asset 大小與 GitHub 提供的 SHA-256 digest。
- 設定 Backup / Rollback；還原前會自動建立唯一命名的 safety backup。
- OEM Control Center V2：讀取 BIOS 廠牌／機型並偵測、開啟 MSI Center；沒有安全公開 API 時不直接寫 EC、風扇曲線、功耗或私有 register。
- Beginner Log 與「開啟 Log 資料夾」直接打開實際資料目錄。

### Installer V2 / Productization
- `MemoryManagerSetup.exe` 是真正 self-contained Windows 安裝器。
- 支援 Install / Repair / Uninstall，寫入 Start Menu 與 HKCU Installed Apps 資訊。
- CI 會在隔離目錄實際執行 quiet install → repair → uninstall，並用 SHA-256 驗證安裝與修復後 payload 與 build 產物一致。
- 預設最大化、深色／淺色介面、About / Credits / Links。

## 安裝

到 **GitHub Releases** 下載：

- `MemoryManagerSetup.exe`：建議使用，真正安裝／修復／解除安裝。
- `MemoryManager.exe`：免安裝版。
- `SHA256SUMS.txt`：檔案完整性驗證。

> `v0.9.0-beta.29` 目前仍是 prerelease。若 Release 頁尚未出現 beta.29，代表 final release workflow 尚未通過；不要使用開發中的 CI 產物冒充正式 prerelease。

## 安全原則

- 不宣稱「清空 RAM」會讓電腦神奇變快。
- Process Memory Priority 是 Windows Memory Manager 的修剪優先提示，不是保證保留 RAM。
- 不寫未公開 MSI EC/MSR。
- Event 41 / 6008 只代表異常關機／重新啟動序列，不單獨宣稱電源、RAM 或特定程式就是根因。
- 無正式受信任 Code Signing 憑證時，Release 會如實保持 unsigned；不假裝自簽能繞過 SmartScreen / Smart App Control。

## 開發與驗證

```powershell
dotnet build src/MemoryManager/MemoryManager.csproj -c Release
dotnet publish src/MemoryManager/MemoryManager.csproj -c Release -r win-x64 --self-contained true
```

GitHub Actions 的 Windows Gate 會執行 `MemoryManager.exe --self-test`、真正 installer build，以及 Installer V2 的 install / repair / uninstall audit。

## 網站

產品網站原始碼維護於 `Ray20123315/html`，公開網站由 GitHub Pages 部署。