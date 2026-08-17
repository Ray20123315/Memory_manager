# Changelog

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
- 避免把背景更新檢查誤稱為 true push；目前採 GitHub Releases polling + 本機通知。
