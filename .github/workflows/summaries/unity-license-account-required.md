## Win64 ビルド blocked: `UNITY_LICENSE` だけでは hosted runner で認証できない

`UNITY_LICENSE` (`.ulf`) は **発行元マシンに紐づく** ため、GitHub Actions の
hosted runner (毎回別マシン) では必ず次のエラーで失敗します。

```
[Licensing::Client] Error: Code 400 while processing request (status: Machine bindings don't match)
No valid Unity Editor license found. Please activate your license.
```

**対応**: `UNITY_EMAIL` と `UNITY_PASSWORD` の secret を追加登録してください
(Unity アカウント認証はマシン非依存)。`UNITY_LICENSE` は削除して構いません。
