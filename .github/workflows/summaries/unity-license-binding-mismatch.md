## Win64 ビルド blocked: Unity ライセンスの機械結合が一致しない

ソースのビルド問題では **ありません**。Unity のライセンス認証段階の失敗です。

`UNITY_LICENSE` (secret) に登録されている `.ulf` は、**その .ulf を発行した
マシンに紐づいています**。GitHub Actions の hosted runner は毎回別のマシンで
実行されるため、機械結合 (machine binding) が一致せず使えません。

```
[Licensing::Client] Error: Code 400 while processing request (status: Machine bindings don't match)
No valid Unity Editor license found. Please activate your license.
```

### 解決方法 (オーナー操作)

Settings → **Secrets and variables → Actions** に、**.ulf ではなく
Unity アカウント認証情報** を登録してください。

| secret | 内容 |
|---|---|
| `UNITY_EMAIL` | Unity アカウントのメール |
| `UNITY_PASSWORD` | Unity アカウントのパスワード |
| `UNITY_SERIAL` | 有償ライセンスのみ (`XX-XXXX-XXXX-XXXX-XXXX-XXXX`) |

既存 `UNITY_LICENSE` は **削除してよい** (残っていても、アカウント認証モードが
優先されるため、放置すると必ずこの失敗になる)。

> 参考: ビルド成果物が GitHub Actions を経由せず必要なら、
> 手元の Mac (Windows Build Support あり) でも同じ `BuildScript.BuildStandaloneWindows64`
> を実行できる: `script/win64-build-mac.sh`。
> ただし **Windows 実機での実行検証 (1 曲完走) は Mac では代替できない**。
