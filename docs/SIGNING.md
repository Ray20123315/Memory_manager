# Code Signing

Release workflow 已預留 Authenticode signing：若 repository secrets 存在 `WINDOWS_CERTIFICATE_PFX` 與 `WINDOWS_CERTIFICATE_PASSWORD`，CI 會在發布前簽署並用 `signtool verify /pa /all` 驗證。

沒有可信憑證時，workflow 會發布 **unsigned** build。這是刻意設計：不使用自簽憑證假裝能避開 Windows SmartScreen / Smart App Control。
