using System.Runtime.CompilerServices;

// ChartFileWriter.Save のような「公開 API ではなく不変条件として守りたい」
// メソッドを EditMode テストから検証するため、テストアセンブリに
// internal を公開する (Issue #225 の独立レビュー S-2)。
[assembly: InternalsVisibleTo("FlashBeat.Tests")]
