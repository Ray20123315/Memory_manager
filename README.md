# Memory Manager / 記憶體管理器

`v0.9.0-beta.30` 以使用者提供並驗證過的 **beta.27 Go + embedded WebView2 原始碼**重作。beta.27 的介面架構、動畫、三語、Theme / Accent、懸浮球、Game Memory Reserve、事故工具與原本操作邏輯是 beta.30 的 UI Canonical；先前 beta.29 的 WPF 重建介面不是 beta.30 的 UI 基準。

## beta.30 這次修正

- Welcome / 授權頁按下 **「不同意」會立即退出整個程式**：不等待 Theme / Accent 儲存、不進主畫面、不縮到 Tray；backend 進入完整退出流程並 `PostQuitMessage`。
- 新增通知頁面，但沿用原 WebView2 UI 視覺與導航架構。
- 新增 Log（簡單易懂版），並修正「開啟 Log」以開啟實際 Log 目錄。
- 更新檢查使用 GitHub Releases polling；支援 Beta / Stable 邏輯，不把 polling 誤稱為 true push。
- 主視窗預設最大化，同時保留原本全螢幕／縮放與 Theme / Accent 行為。
- 保留原版 Game Memory Reserve、Floating Ball、Flight Recorder、Incident / Crash 診斷與安裝／解除安裝流程。

## 原始碼

正式 beta.30 原始碼由本 repository 的 Windows Gate 所驗證。驗證流程使用 `_source_parts/` 重建 exact source archive，SHA-256 必須符合固定值後才進入測試；通過後會把 `beta30-source/` 與 `tests_beta30/` 展開回 `main`，方便直接瀏覽。

beta.30 source archive SHA-256：

`ffa22411e12572820c67457c478344f0219df9f84a1d1f2a3bfd63e6c91d2df9`

## Windows 驗證 Gate

`.github/workflows/beta30-go.yml` 在 Windows runner 上執行：

1. 重建並驗證 exact beta.30 source archive SHA-256。
2. Source / UI preservation regression gate。
3. `go test -count=1 ./...`，其中包含真正的 per-user install lifecycle 測試：暫時 LocalAppData、複製 EXE、寫 HKCU Installed Apps / Startup Run、核對內容，再解除安裝與清理。
4. `go vet -unsafeptr=false ./...`。
5. 連續兩次 deterministic Windows GUI build。
6. 恢復並驗證 beta.27 的 icon / manifest PE resource payload。
7. 重新計算並驗證 `SHA256SUMS.txt`。
8. 只有上述步驟全部成功後，帶 `[release-beta30]` 的 main commit 才能發布 prerelease assets。

## GitHub Release

`v0.9.0-beta.30` Release 預定包含：

- `MemoryManager_v0.9.0-beta.30.exe`
- `MemoryManager_v0.9.0-beta.30-source.tar.gz`
- `SHA256SUMS.txt`

Release binary 在沒有正式受信任 Code Signing credential 時會保持 **unsigned**；不宣稱能繞過 SmartScreen / Smart App Control。

## 安全／介面原則

- 不再以其他 UI framework 重畫 beta.27 的介面。
- 新功能必須嵌進既有 WebView2 HTML / CSS / JS 視覺與互動架構。
- 不同意授權時必須退出整個 App。
- 不把「清 RAM」包裝成保證提升效能的魔法按鈕。
- 不直接寫未公開 OEM EC / 私有 register。

## 網站

產品網站原始碼維護於 `Ray20123315/html`。beta.30 網站下載入口會在 beta.30 Release 驗證完成後再對齊，避免先連到不存在或未驗證的 asset。
