# Changelog

## v0.9.0-beta.29

### 記憶體觀測
- Process 頁改用 Windows `PROCESS_MEMORY_COUNTERS_EX.PrivateUsage` 顯示真正 Process Commit Charge。
- 新增 Commit Δ、持續成長 Leak 判斷、Commit ETA、Page File 白話狀態與 Adaptive Refresh。
- 1 ms Telemetry 保留，但 UI refresh 與資料取樣分離。

### 事故與可靠性
- 新增 Windows Event Log Incident Timeline、Previous Crash Analyzer、Reliability History。
- 新增跨程序 Session heartbeat 與 persistent Flight Recorder V2。
- Incident Bundle 改成只匯出事故附近的小時間窗。
- Event 41 / 6008 不再被誤當成單一根因；Resource Exhaustion 2004、BugCheck、WHEA 等依證據相關性顯示。

### Game Memory Reserve V2 / Per-App Rules
- 新增 Game Profile 自動／手動偵測。
- 新增使用 Windows Process Memory Priority 的可回復背景規則；不是硬鎖 RAM。
- 預設總開關關閉。
- 前景、遊戲、反作弊、語音、輸入、安全與 Windows 核心程序列入硬保護。
- 規則停用、程序不再符合或 App 結束時還原原本 Priority。

### Update / OEM / Backup
- Built-in Update Center 支援 Beta / Stable channel、Release Notes、assets、大小與 GitHub SHA-256 digest。
- 修正 WPF STA 下 async-over-sync 可能造成的更新檢查 deadlock。
- 新增設定 Backup / Rollback；還原前會建立 safety backup。
- 修正同一秒內 backup 檔名碰撞，改用毫秒 + unique suffix。
- OEM Control Center V2 讀取裝置資訊並安全偵測／開啟 MSI Center；不直接寫未公開 EC/MSR。

### Installer V2
- 安裝器改為共用可測試的 install / repair / uninstall service。
- 新增 quiet CLI 測試模式與隔離 `--target`。
- Windows CI 實際執行 install → SHA-256 read-back → repair → SHA-256 read-back → uninstall → residue audit。
- Log、事故紀錄與設定備份預設保留，避免解除安裝誤刪診斷資料。

### UI / Productization
- 新增遊戲 / 規則、更新 / OEM、事故頁面。
- 擴充 About / Credits / Links。
- 保留通知中心、Beginner Log、真正 Log 資料夾 opener、預設最大化與深／淺色切換。

### 已知限制
- beta.29 Release 若沒有正式 Code Signing PFX，會保持 unsigned；不宣稱能繞過 SmartScreen / Smart App Control。
- hosted Windows CI 無法替代實體 MSI Windows 11 機器上的最終 UI、MSI Center 與 SmartScreen 驗收。

## v0.9.0-beta.28

### 新功能
- 新增 Notifications 頁面與更新通知。
- 新增 beginner-friendly Logs 頁面。
- 新增 1 ms Telemetry 選項，並與 UI refresh 分離。
- 新增 Commit / Commit Headroom / ETA / Leak indicator。
- 新增 Flight Recorder / Incident Bundle。
- 新增 Safe Close Advisor、Page File Health、Emergency 狀態。
- 新增真正安裝器、Repair、Uninstall。
- 預設最大化。

### 修正
- 「開啟 Log」現在會開啟實際 Log 目錄。
- 避免把背景更新檢查誤稱為 true push；採 GitHub Releases polling + 本機通知。