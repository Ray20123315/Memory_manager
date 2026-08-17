# Security

- 不提交憑證私鑰、PFX 密碼、token 或 cookie。
- Release workflow 支援未來接上可信 Code Signing，但沒有憑證時會明確產出 unsigned build。
- 不以自簽憑證宣稱能繞過 SmartScreen / Smart App Control。
- 不使用未公開的 EC/MSR/OEM 寫入。
