## Win64 ビルド blocked: Unity ライセンス未設定

Win64 成果物 (ZIP / SHA-256) は生成されていません。リポジトリの
**Settings → Secrets and variables → Actions** に次の secret を登録してください。

| secret | 必須 | 内容 |
|---|---|---|
| `UNITY_EMAIL` | 必須 | Unity アカウントのメールアドレス。 |
| `UNITY_PASSWORD` | 必須 | Unity アカウントのパスワード。 |
| `UNITY_SERIAL` | 有償ライセンスのみ | `XX-XXXX-XXXX-XXXX-XXXX-XXXX` 形式。 |

- `UNITY_LICENSE` (`.ulf`) だけでは **hosted runner では使えません**
  (machine binding mismatch)。上のアカウント認証を使うこと。
- いずれの secret も値をログ・Issue・PR に出さないこと。
